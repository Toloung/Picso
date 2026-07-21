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
    private readonly WindowsThumbnailGenerator thumbnailGenerator;
    private readonly ILogger<MainPageViewModel> logger;
    private CancellationTokenSource? scanCancellation;

    public MainPageViewModel(
        IPhotoScanner scanner,
        PhotoLibraryDatabase database,
        WindowsThumbnailGenerator thumbnailGenerator,
        ILogger<MainPageViewModel> logger)
    {
        this.scanner = scanner;
        this.database = database;
        this.thumbnailGenerator = thumbnailGenerator;
        this.logger = logger;
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
        scanCancellation?.Cancel();
        scanCancellation?.Dispose();
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
