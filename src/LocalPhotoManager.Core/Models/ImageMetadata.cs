namespace LocalPhotoManager.Core.Models;

public sealed record ImageMetadata(
    string MimeType,
    uint Width,
    uint Height,
    int? Orientation,
    DateTimeOffset? TakenAtUtc,
    string? CameraMake,
    string? CameraModel);
