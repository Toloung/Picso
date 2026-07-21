using LocalPhotoManager.Core.Models;
using LocalPhotoManager.Database;

namespace LocalPhotoManager.Database.Tests;

public sealed class PhotoLibraryDatabaseTests : IDisposable
{
    private readonly string temporaryDirectory = Path.Combine(Path.GetTempPath(), "LocalPhotoManagerTests", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task UpsertPhotoAsyncPersistsOnePhotoAndUpdatesItInPlace()
    {
        var database = new PhotoLibraryDatabase(Path.Combine(temporaryDirectory, "library.db"));
        await database.InitializeAsync();
        var photo = new DiscoveredPhoto("C:\\Photos\\sample.jpg", "sample.jpg", ".jpg", 42, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);

        var metadata = new ImageMetadata("image/jpeg", 1920, 1080, 1, DateTimeOffset.UtcNow, "Camera", "Model");
        await database.UpsertPhotoAsync("C:\\Photos", photo, metadata);
        await database.UpsertPhotoAsync("C:\\Photos", photo with { FileSize = 64 }, metadata);

        Assert.Equal(1, await database.GetPhotoCountAsync());
        var storedPhoto = Assert.Single(await database.GetRecentPhotosAsync());
        Assert.Equal((uint)1920, storedPhoto.Metadata.Width);
        Assert.Equal("Camera", storedPhoto.Metadata.CameraMake);
    }

    [Fact]
    public async Task MarkPhotoMissingAsyncHidesPhotoFromRecentResults()
    {
        var database = new PhotoLibraryDatabase(Path.Combine(temporaryDirectory, "library.db"));
        await database.InitializeAsync();
        var photo = new DiscoveredPhoto("C:\\Photos\\missing.jpg", "missing.jpg", ".jpg", 42, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
        var metadata = new ImageMetadata("image/jpeg", 1920, 1080, null, null, null, null);

        await database.UpsertPhotoAsync("C:\\Photos", photo, metadata);
        var marked = await database.MarkPhotoMissingAsync(photo.Path);

        Assert.True(marked);
        Assert.Equal(0, await database.GetPhotoCountAsync());
        Assert.Empty(await database.GetRecentPhotosAsync());
    }

    public void Dispose()
    {
        if (Directory.Exists(temporaryDirectory))
        {
            Directory.Delete(temporaryDirectory, recursive: true);
        }
    }
}
