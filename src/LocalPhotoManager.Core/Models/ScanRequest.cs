namespace LocalPhotoManager.Core.Models;

public sealed record ScanRequest(string RootPath, bool IncludeSubdirectories = true);
