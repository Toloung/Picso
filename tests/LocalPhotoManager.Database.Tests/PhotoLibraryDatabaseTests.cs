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

    [Fact]
    public async Task FolderAndTimelineQueriesReturnIndexedGroups()
    {
        var database = new PhotoLibraryDatabase(Path.Combine(temporaryDirectory, "library.db"));
        await database.InitializeAsync();
        var firstTaken = new DateTimeOffset(2026, 7, 20, 10, 0, 0, TimeSpan.Zero);
        var secondTaken = new DateTimeOffset(2026, 6, 15, 10, 0, 0, TimeSpan.Zero);
        var firstPhoto = new DiscoveredPhoto("C:\\Photos\\A\\first.jpg", "first.jpg", ".jpg", 42, firstTaken, firstTaken);
        var secondPhoto = new DiscoveredPhoto("C:\\Photos\\B\\second.jpg", "second.jpg", ".jpg", 64, secondTaken, secondTaken);

        await database.UpsertPhotoAsync("C:\\Photos\\A", firstPhoto, new ImageMetadata("image/jpeg", 1920, 1080, null, firstTaken, null, null));
        await database.UpsertPhotoAsync("C:\\Photos\\B", secondPhoto, new ImageMetadata("image/jpeg", 1080, 720, null, secondTaken, null, null));

        var folders = await database.GetFolderSummariesAsync();
        var timeline = await database.GetTimelineGroupsAsync();
        var folderPhotos = await database.GetPhotosByDirectoryAsync("C:\\Photos\\A");
        var julyPhotos = await database.GetPhotosByMonthAsync(2026, 7);

        Assert.Equal(2, folders.Count);
        Assert.Equal("C:\\Photos\\A", folders[0].DirectoryPath);
        Assert.Equal("first.jpg", Assert.Single(folderPhotos).Photo.FileName);
        Assert.Equal(2026, timeline[0].Year);
        Assert.Equal(7, timeline[0].Month);
        Assert.Equal("first.jpg", Assert.Single(julyPhotos).Photo.FileName);
    }

    [Fact]
    public async Task UpsertPhotoAsyncMovesPhotoBetweenDirectories()
    {
        var database = new PhotoLibraryDatabase(Path.Combine(temporaryDirectory, "library.db"));
        await database.InitializeAsync();
        var photo = new DiscoveredPhoto("C:\\Photos\\A\\moved.jpg", "moved.jpg", ".jpg", 42, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
        var metadata = new ImageMetadata("image/jpeg", 1920, 1080, null, null, null, null);

        await database.UpsertPhotoAsync("C:\\Photos", photo, metadata);
        await database.UpsertPhotoAsync("C:\\Photos\\A", photo, metadata);

        Assert.Empty(await database.GetPhotosByDirectoryAsync("C:\\Photos"));
        Assert.Equal("moved.jpg", Assert.Single(await database.GetPhotosByDirectoryAsync("C:\\Photos\\A")).Photo.FileName);
    }

    public void Dispose()
    {
        if (Directory.Exists(temporaryDirectory))
        {
            Directory.Delete(temporaryDirectory, recursive: true);
        }
    }
}
