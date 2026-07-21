namespace LocalPhotoManager.Core.Services;

public static class PathPolicy
{
    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".tif", ".tiff", ".webp",
    };

    private static readonly HashSet<string> ExcludedDirectoryNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "$Recycle.Bin", "System Volume Information", "node_modules", ".git", ".vs", "bin", "obj", "cache", "temp",
    };

    public static bool IsSupportedImage(string path) => SupportedExtensions.Contains(Path.GetExtension(path));

    public static bool IsExcludedDirectory(string path)
    {
        var name = Path.GetFileName(Path.TrimEndingDirectorySeparator(path));
        return ExcludedDirectoryNames.Contains(name);
    }

    public static string NormalizePath(string path) => Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar).ToUpperInvariant();
}
