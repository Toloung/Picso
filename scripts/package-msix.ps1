param(
    [string]$Configuration = "Release",
    [string]$Platform = "x64",
    [string]$Publisher = "CN=Toloung",
    [string]$DotnetRoot = "D:\CodexTools\dotnet-10.0.302",
    [string]$NugetPackages = "D:\CodexTools\nuget-packages"
)

$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$projectPath = Join-Path $repoRoot "src\LocalPhotoManager.App\LocalPhotoManager.App.csproj"
$packageRoot = Join-Path $repoRoot "artifacts\packages"
$runtimeIdentifier = "win-$Platform"
$signtool = Join-Path $NugetPackages "microsoft.windows.sdk.buildtools\10.0.26100.7705\bin\10.0.26100.0\$Platform\signtool.exe"

if (!(Test-Path $signtool)) {
    throw "signtool.exe was not found at $signtool. Restore the solution first so Microsoft.Windows.SDK.BuildTools is available."
}

$env:DOTNET_ROOT = $DotnetRoot
$env:DOTNET_CLI_HOME = "D:\CodexTools\dotnet-cli"
$env:NUGET_PACKAGES = $NugetPackages
$env:NUGET_HTTP_CACHE_PATH = "D:\CodexTools\nuget-http-cache"
$env:DOTNET_CLI_TELEMETRY_OPTOUT = "1"

& (Join-Path $DotnetRoot "dotnet.exe") publish $projectPath `
    -c $Configuration `
    -p:Platform=$Platform `
    -r $runtimeIdentifier `
    -p:GenerateAppxPackageOnBuild=true `
    -p:AppxBundle=Never `
    -p:AppxPackageSigningEnabled=false `
    -p:PublishTrimmed=false `
    -p:AppxPackageDir="$packageRoot\"

$packagePath = Get-ChildItem -Path $packageRoot -Recurse -Filter "*.msix" |
    Sort-Object LastWriteTime -Descending |
    Select-Object -First 1

if ($null -eq $packagePath) {
    throw "No MSIX package was generated under $packageRoot."
}

$cert = Get-ChildItem Cert:\CurrentUser\My |
    Where-Object { $_.Subject -eq $Publisher -and $_.HasPrivateKey } |
    Sort-Object NotAfter -Descending |
    Select-Object -First 1

if ($null -eq $cert) {
    $cert = New-SelfSignedCertificate `
        -Type CodeSigningCert `
        -Subject $Publisher `
        -CertStoreLocation "Cert:\CurrentUser\My" `
        -KeyAlgorithm RSA `
        -KeyLength 2048 `
        -HashAlgorithm SHA256 `
        -NotAfter (Get-Date).AddYears(3)
}

$certificatePath = [System.IO.Path]::ChangeExtension($packagePath.FullName, ".cer")
Export-Certificate -Cert $cert -FilePath $certificatePath | Out-Null

& $signtool sign /fd SHA256 /sha1 $cert.Thumbprint $packagePath.FullName

$archivePath = Join-Path (Split-Path -Parent $packageRoot) "LocalPhotoManager_1.0.0.0_${Platform}_Test.zip"
if (Test-Path $archivePath) {
    Remove-Item -LiteralPath $archivePath
}

Compress-Archive -Path (Join-Path $packagePath.DirectoryName "*") -DestinationPath $archivePath

Write-Host "Package: $($packagePath.FullName)"
Write-Host "Certificate: $certificatePath"
Write-Host "Archive: $archivePath"
