using LocalPhotoManager.Core.Models;
using LocalPhotoManager.Infrastructure;

namespace LocalPhotoManager.Infrastructure.Tests;

public sealed class FileSystemPhotoScannerTests : IDisposable
{
    private readonly string temporaryDirectory = Path.Combine(Path.GetTempPath(), "LocalPhotoManagerTests", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task ScanAsyncOnlyYieldsSupportedFilesAndSkipsExcludedDirectories()
    {
        Directory.CreateDirectory(Path.Combine(temporaryDirectory, "photos"));
        Directory.CreateDirectory(Path.Combine(temporaryDirectory, "node_modules"));
        await File.WriteAllTextAsync(Path.Combine(temporaryDirectory, "photos", "keep.jpg"), "test");
        await File.WriteAllTextAsync(Path.Combine(temporaryDirectory, "photos", "skip.txt"), "test");
        await File.WriteAllTextAsync(Path.Combine(temporaryDirectory, "node_modules", "ignored.png"), "test");

        var scanner = new FileSystemPhotoScanner();
        var photos = new List<DiscoveredPhoto>();
        await foreach (var photo in scanner.ScanAsync(new ScanRequest(temporaryDirectory)))
        {
            photos.Add(photo);
        }

        var discoveredPhoto = Assert.Single(photos);
        Assert.Equal("keep.jpg", discoveredPhoto.FileName);
    }

    public void Dispose()
    {
        if (Directory.Exists(temporaryDirectory))
        {
            Directory.Delete(temporaryDirectory, recursive: true);
        }
    }
}
