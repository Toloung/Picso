namespace LocalPhotoManager.Core.Models;

public enum PhotoFileChangeKind
{
    Created,
    Changed,
    Deleted,
    Renamed,
    RescanRequired,
}

public sealed record PhotoFileChange(PhotoFileChangeKind Kind, string Path, string? PreviousPath = null);
