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

        await database.UpsertPhotoAsync("C:\\Photos", photo);
        await database.UpsertPhotoAsync("C:\\Photos", photo with { FileSize = 64 });

        Assert.Equal(1, await database.GetPhotoCountAsync());
    }

    public void Dispose()
    {
        if (Directory.Exists(temporaryDirectory))
        {
            Directory.Delete(temporaryDirectory, recursive: true);
        }
    }
}
