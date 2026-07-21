namespace LocalPhotoManager.Core.Models;

public sealed record DiscoveredPhoto(
    string Path,
    string FileName,
    string Extension,
    long FileSize,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset ModifiedAtUtc);
