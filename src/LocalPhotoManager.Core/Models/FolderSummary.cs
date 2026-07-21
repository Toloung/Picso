namespace LocalPhotoManager.Core.Models;

public sealed record FolderSummary(string DirectoryPath, int PhotoCount, DateTimeOffset? LatestModifiedAtUtc);
