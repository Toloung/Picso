using System.Collections.Concurrent;
using LocalPhotoManager.Core.Models;
using LocalPhotoManager.Core.Services;

namespace LocalPhotoManager.Infrastructure;

public sealed class DebouncedPhotoFileWatcher : IPhotoFileWatcher
{
    private readonly ConcurrentDictionary<string, CancellationTokenSource> pending = new(StringComparer.OrdinalIgnoreCase);
    private readonly TimeSpan debounceDelay;
    private FileSystemWatcher? watcher;
    private string? rootPath;
    private bool disposed;

    public DebouncedPhotoFileWatcher(TimeSpan? debounceDelay = null)
    {
        this.debounceDelay = debounceDelay ?? TimeSpan.FromMilliseconds(600);
    }

    public event EventHandler<PhotoFileChange>? FileChanged;

    public void Start(string rootPath)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);
        if (!Directory.Exists(rootPath))
        {
            throw new DirectoryNotFoundException($"The watched directory does not exist: {rootPath}");
        }

        watcher?.Dispose();
        this.rootPath = PathPolicy.NormalizePath(rootPath);
        watcher = new FileSystemWatcher(rootPath)
        {
            IncludeSubdirectories = true,
            Filter = "*",
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.CreationTime,
            InternalBufferSize = 64 * 1024,
            EnableRaisingEvents = true,
        };
        watcher.Created += (_, eventArgs) => Schedule(eventArgs.FullPath, PhotoFileChangeKind.Created);
        watcher.Changed += (_, eventArgs) => Schedule(eventArgs.FullPath, PhotoFileChangeKind.Changed);
        watcher.Deleted += (_, eventArgs) => Schedule(eventArgs.FullPath, PhotoFileChangeKind.Deleted);
        watcher.Renamed += (_, eventArgs) => ScheduleRename(eventArgs.OldFullPath, eventArgs.FullPath);
        watcher.Error += (_, _) => Raise(new PhotoFileChange(PhotoFileChangeKind.RescanRequired, this.rootPath));
    }

    private void ScheduleRename(string oldPath, string newPath)
    {
        CancelPending(oldPath);
        Schedule(newPath, PhotoFileChangeKind.Renamed, oldPath);
    }

    private void Schedule(string path, PhotoFileChangeKind kind, string? previousPath = null)
    {
        if (!PathPolicy.IsSupportedImage(path))
        {
            return;
        }

        var normalizedPath = PathPolicy.NormalizePath(path);
        CancelPending(normalizedPath);
        var cancellation = new CancellationTokenSource();
        pending[normalizedPath] = cancellation;
        _ = DispatchAfterDebounceAsync(normalizedPath, kind, previousPath, cancellation);
    }

    private async Task DispatchAfterDebounceAsync(string path, PhotoFileChangeKind kind, string? previousPath, CancellationTokenSource cancellation)
    {
        try
        {
            await Task.Delay(debounceDelay, cancellation.Token);
            if (kind is not PhotoFileChangeKind.Deleted && !await IsFileStableAsync(path, cancellation.Token))
            {
                return;
            }

            Raise(new PhotoFileChange(kind, path, previousPath is null ? null : PathPolicy.NormalizePath(previousPath)));
        }
        catch (OperationCanceledException)
        {
            // A newer event for this file superseded the pending work.
        }
        finally
        {
            pending.TryRemove(new KeyValuePair<string, CancellationTokenSource>(path, cancellation));
            cancellation.Dispose();
        }
    }

    private static async Task<bool> IsFileStableAsync(string path, CancellationToken cancellationToken)
    {
        try
        {
            var first = new FileInfo(path);
            if (!first.Exists)
            {
                return false;
            }

            var firstLength = first.Length;
            var firstWrite = first.LastWriteTimeUtc;
            await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken);
            var second = new FileInfo(path);
            return second.Exists && second.Length == firstLength && second.LastWriteTimeUtc == firstWrite;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    private void CancelPending(string path)
    {
        if (pending.TryRemove(PathPolicy.NormalizePath(path), out var existing))
        {
            existing.Cancel();
        }
    }

    private void Raise(PhotoFileChange change) => FileChanged?.Invoke(this, change);

    public ValueTask DisposeAsync()
    {
        if (disposed)
        {
            return ValueTask.CompletedTask;
        }

        disposed = true;
        watcher?.Dispose();
        foreach (var cancellation in pending.Values)
        {
            cancellation.Cancel();
        }

        pending.Clear();
        return ValueTask.CompletedTask;
    }
}
