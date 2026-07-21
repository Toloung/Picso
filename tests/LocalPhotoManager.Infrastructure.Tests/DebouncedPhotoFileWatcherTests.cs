using LocalPhotoManager.Core.Models;
using LocalPhotoManager.Infrastructure;

namespace LocalPhotoManager.Infrastructure.Tests;

public sealed class DebouncedPhotoFileWatcherTests : IAsyncDisposable
{
    private readonly string temporaryDirectory = Path.Combine(Path.GetTempPath(), "LocalPhotoManagerTests", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task StartPublishesCreatedImageEvent()
    {
        Directory.CreateDirectory(temporaryDirectory);
        await using var watcher = new DebouncedPhotoFileWatcher(TimeSpan.FromMilliseconds(25));
        var changeSource = new TaskCompletionSource<PhotoFileChange>(TaskCreationOptions.RunContinuationsAsynchronously);
        watcher.FileChanged += (_, change) =>
        {
            if (change.Kind is PhotoFileChangeKind.Created or PhotoFileChangeKind.Changed)
            {
                changeSource.TrySetResult(change);
            }
        };
        watcher.Start(temporaryDirectory);

        var imagePath = Path.Combine(temporaryDirectory, "new-image.jpg");
        await File.WriteAllTextAsync(imagePath, "test");
        var change = await changeSource.Task.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(Path.GetFullPath(imagePath).ToUpperInvariant(), change.Path);
    }

    public ValueTask DisposeAsync()
    {
        if (Directory.Exists(temporaryDirectory))
        {
            Directory.Delete(temporaryDirectory, recursive: true);
        }

        return ValueTask.CompletedTask;
    }
}
