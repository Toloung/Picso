namespace LocalPhotoManager.Core.Models;

public sealed record IndexedPhoto(DiscoveredPhoto Photo, ImageMetadata Metadata);
