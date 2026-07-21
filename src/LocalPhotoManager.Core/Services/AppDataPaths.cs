namespace LocalPhotoManager.Core.Services;

public sealed class AppDataPaths
{
    public AppDataPaths(string applicationName)
    {
        Root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), applicationName);
    }

    public string Root { get; }
    public string DatabasePath => Path.Combine(Root, "library.db");
    public string ThumbnailsRoot => Path.Combine(Root, "thumbnails");
    public string CachePath => Path.Combine(Root, "cache");
    public string ModelsPath => Path.Combine(Root, "models");
    public string LogsPath => Path.Combine(Root, "logs");
    public string BackupsPath => Path.Combine(Root, "backups");
    public string SettingsPath => Path.Combine(Root, "settings.json");

    public void EnsureCreated()
    {
        Directory.CreateDirectory(Root);
        Directory.CreateDirectory(ThumbnailsRoot);
        Directory.CreateDirectory(CachePath);
        Directory.CreateDirectory(ModelsPath);
        Directory.CreateDirectory(LogsPath);
        Directory.CreateDirectory(BackupsPath);
    }
}
