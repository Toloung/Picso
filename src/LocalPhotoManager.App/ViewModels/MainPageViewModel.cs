using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LocalPhotoManager.Core.Models;
using LocalPhotoManager.Core.Services;
using LocalPhotoManager.Database;
using LocalPhotoManager.Imaging;
using Microsoft.Extensions.Logging;
using Windows.Storage.Pickers;

namespace LocalPhotoManager.App.ViewModels;

public sealed partial class MainPageViewModel : ObservableObject, IDisposable
{
    private readonly IPhotoScanner scanner;
    private readonly PhotoLibraryDatabase database;
    private readonly IPhotoFileWatcher watcher;
    private readonly WindowsThumbnailGenerator thumbnailGenerator;
    private readonly ILogger<MainPageViewModel> logger;
    private CancellationTokenSource? scanCancellation;
    private string? watchedDirectoryPath;
    private bool disposed;

    public MainPageViewModel(
        IPhotoScanner scanner,
        PhotoLibraryDatabase database,
        IPhotoFileWatcher watcher,
        WindowsThumbnailGenerator thumbnailGenerator,
        ILogger<MainPageViewModel> logger)
    {
        this.scanner = scanner;
        this.database = database;
        this.watcher = watcher;
        this.thumbnailGenerator = thumbnailGenerator;
        this.logger = logger;
        this.watcher.FileChanged += OnFileChanged;
    }

    public ObservableCollection<PhotoListItem> Photos { get; } = [];

    [ObservableProperty]
    public partial string StatusMessage { get; set; } = "选择一个文件夹以在本机创建图片索引。";

    [ObservableProperty]
    public partial bool IsScanning { get; set; }

    [RelayCommand]
    private async Task ScanFolderAsync()
    {
        if (IsScanning)
        {
            return;
        }

        var picker = new FolderPicker();
        picker.FileTypeFilter.Add("*");
        WinRT.Interop.InitializeWithWindow.Initialize(picker, App.WindowHandle);
        var folder = await picker.PickSingleFolderAsync();
        if (folder is null)
        {
            return;
        }

        scanCancellation = new CancellationTokenSource();
        IsScanning = true;
        Photos.Clear();
        var count = 0;
        try
        {
            await database.InitializeAsync(scanCancellation.Token);
            var directoryPath = PathPolicy.NormalizePath(folder.Path);
            await foreach (var photo in scanner.ScanAsync(new ScanRequest(folder.Path), scanCancellation.Token))
            {
                var metadata = await WindowsImageMetadataReader.TryReadAsync(photo, scanCancellation.Token);
                if (metadata is null)
                {
                    LogInvalidImageSkipped(logger, photo.Path);
                    continue;
                }

                await database.UpsertPhotoAsync(directoryPath, photo, metadata, scanCancellation.Token);
                await TryGenerateThumbnailAsync(photo, scanCancellation.Token);
                Photos.Add(PhotoListItem.From(photo, metadata));
                count++;
                StatusMessage = $"正在索引 {count:N0} 张图片…";
            }

            StatusMessage = $"已在本机索引 {count:N0} 张图片。";
            StartWatching(directoryPath);
        }
        catch (OperationCanceledException)
        {
            StatusMessage = $"已取消扫描；已索引 {count:N0} 张图片。";
        }
        catch (Exception exception)
        {
            LogScanFailed(logger, exception, folder.Path);
            StatusMessage = "扫描未完成。详细信息已写入本地日志。";
        }
        finally
        {
            IsScanning = false;
            scanCancellation?.Dispose();
            scanCancellation = null;
        }
    }

    [RelayCommand]
    private void CancelScan() => scanCancellation?.Cancel();

    private void StartWatching(string directoryPath)
    {
        try
        {
            watchedDirectoryPath = directoryPath;
            watcher.Start(directoryPath);
        }
        catch (Exception exception)
        {
            LogWatcherStartFailed(logger, exception, directoryPath);
            StatusMessage = "已完成索引，但实时监听未启动。详细信息已写入本地日志。";
        }
    }

    private void OnFileChanged(object? sender, PhotoFileChange change)
    {
        if (disposed)
        {
            return;
        }

        var dispatcher = App.DispatcherQueue;
        if (dispatcher is not null && dispatcher.TryEnqueue(() => _ = HandleFileChangeAsync(change)))
        {
            return;
        }

        _ = HandleFileChangeAsync(change);
    }

    private async Task HandleFileChangeAsync(PhotoFileChange change)
    {
        if (disposed || IsScanning)
        {
            return;
        }

        try
        {
            switch (change.Kind)
            {
                case PhotoFileChangeKind.Created:
                case PhotoFileChangeKind.Changed:
                    await IndexPhotoPathAsync(change.Path);
                    break;
                case PhotoFileChangeKind.Deleted:
                    await MarkPhotoMissingAsync(change.Path);
                    break;
                case PhotoFileChangeKind.Renamed:
                    if (change.PreviousPath is not null)
                    {
                        await MarkPhotoMissingAsync(change.PreviousPath);
                    }

                    await IndexPhotoPathAsync(change.Path);
                    break;
                case PhotoFileChangeKind.RescanRequired:
                    StatusMessage = "检测到大量文件变化，请重新扫描当前文件夹以校准本地索引。";
                    break;
            }
        }
        catch (Exception exception)
        {
            LogWatcherChangeFailed(logger, exception, change.Path);
            StatusMessage = "实时索引更新未完成。详细信息已写入本地日志。";
        }
    }

    private async Task IndexPhotoPathAsync(string path)
    {
        if (watchedDirectoryPath is null)
        {
            return;
        }

        var photo = TryCreateDiscoveredPhoto(path);
        if (photo is null)
        {
            await MarkPhotoMissingAsync(path);
            return;
        }

        var metadata = await WindowsImageMetadataReader.TryReadAsync(photo);
        if (metadata is null)
        {
            LogInvalidImageSkipped(logger, photo.Path);
            return;
        }

        await database.UpsertPhotoAsync(watchedDirectoryPath, photo, metadata);
        await TryGenerateThumbnailAsync(photo, CancellationToken.None);
        AddOrReplacePhoto(PhotoListItem.From(photo, metadata));
        StatusMessage = $"已更新本地索引：{photo.FileName}";
    }

    private async Task MarkPhotoMissingAsync(string path)
    {
        await database.MarkPhotoMissingAsync(PathPolicy.NormalizePath(path));
        RemovePhoto(path);
        StatusMessage = "已从本地索引中标记移除一张图片。";
    }

    private static DiscoveredPhoto? TryCreateDiscoveredPhoto(string path)
    {
        try
        {
            var fileInfo = new FileInfo(path);
            if (!fileInfo.Exists)
            {
                return null;
            }

            return new DiscoveredPhoto(
                PathPolicy.NormalizePath(fileInfo.FullName),
                fileInfo.Name,
                fileInfo.Extension.ToLowerInvariant(),
                fileInfo.Length,
                fileInfo.CreationTimeUtc,
                fileInfo.LastWriteTimeUtc);
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    private void AddOrReplacePhoto(PhotoListItem photo)
    {
        RemovePhoto(photo.Path);
        Photos.Insert(0, photo);
    }

    private bool RemovePhoto(string path)
    {
        var normalizedPath = PathPolicy.NormalizePath(path);
        for (var index = 0; index < Photos.Count; index++)
        {
            if (StringComparer.OrdinalIgnoreCase.Equals(Photos[index].Path, normalizedPath))
            {
                Photos.RemoveAt(index);
                return true;
            }
        }

        return false;
    }

    public async Task LoadAsync()
    {
        try
        {
            await database.InitializeAsync();
            var indexedPhotos = await database.GetRecentPhotosAsync();
            Photos.Clear();
            foreach (var indexedPhoto in indexedPhotos)
            {
                Photos.Add(PhotoListItem.From(indexedPhoto));
            }

            StatusMessage = indexedPhotos.Count == 0
                ? "选择一个文件夹以在本机创建图片索引。"
                : $"已加载本地索引中的 {indexedPhotos.Count:N0} 张图片。";
        }
        catch (Exception exception)
        {
            LogLibraryLoadFailed(logger, exception);
            StatusMessage = "无法加载本地索引。详细信息已写入本地日志。";
        }
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        watcher.FileChanged -= OnFileChanged;
        scanCancellation?.Cancel();
        scanCancellation?.Dispose();
        watcher.DisposeAsync().AsTask().GetAwaiter().GetResult();
    }

    private async Task TryGenerateThumbnailAsync(DiscoveredPhoto photo, CancellationToken cancellationToken)
    {
        try
        {
            await thumbnailGenerator.GenerateAsync(photo, 256, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            LogThumbnailFailed(logger, exception, photo.Path);
        }
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Error, Message = "The scan of {FolderPath} failed.")]
    private static partial void LogScanFailed(ILogger logger, Exception exception, string folderPath);

    [LoggerMessage(EventId = 2, Level = LogLevel.Warning, Message = "Thumbnail generation failed for {PhotoPath}.")]
    private static partial void LogThumbnailFailed(ILogger logger, Exception exception, string photoPath);

    [LoggerMessage(EventId = 3, Level = LogLevel.Warning, Message = "Skipped an unsupported or corrupted image file: {PhotoPath}.")]
    private static partial void LogInvalidImageSkipped(ILogger logger, string photoPath);

    [LoggerMessage(EventId = 4, Level = LogLevel.Error, Message = "Loading the local photo index failed.")]
    private static partial void LogLibraryLoadFailed(ILogger logger, Exception exception);

    [LoggerMessage(EventId = 5, Level = LogLevel.Warning, Message = "Starting the file watcher for {FolderPath} failed.")]
    private static partial void LogWatcherStartFailed(ILogger logger, Exception exception, string folderPath);

    [LoggerMessage(EventId = 6, Level = LogLevel.Warning, Message = "Applying a watched file change failed for {PhotoPath}.")]
    private static partial void LogWatcherChangeFailed(ILogger logger, Exception exception, string photoPath);
}

public sealed record PhotoListItem(string FileName, string Path, string SizeLabel, string DetailsLabel)
{
    public static PhotoListItem From(DiscoveredPhoto photo, ImageMetadata metadata) => new(
        photo.FileName,
        photo.Path,
        $"{photo.FileSize / 1024d / 1024d:0.0} MB",
        $"{metadata.Width} × {metadata.Height} · {metadata.MimeType}");

    public static PhotoListItem From(IndexedPhoto indexedPhoto) => From(indexedPhoto.Photo, indexedPhoto.Metadata);
}
