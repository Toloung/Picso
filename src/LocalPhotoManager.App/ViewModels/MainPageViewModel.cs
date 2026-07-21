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
    private readonly ThumbnailPathFactory thumbnailPathFactory;
    private readonly ILogger<MainPageViewModel> logger;
    private CancellationTokenSource? scanCancellation;

    public MainPageViewModel(
        IPhotoScanner scanner,
        PhotoLibraryDatabase database,
        ThumbnailPathFactory thumbnailPathFactory,
        ILogger<MainPageViewModel> logger)
    {
        this.scanner = scanner;
        this.database = database;
        this.thumbnailPathFactory = thumbnailPathFactory;
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
                await database.UpsertPhotoAsync(directoryPath, photo, scanCancellation.Token);
                _ = thumbnailPathFactory.GetThumbnailPath(photo, 256);
                Photos.Add(PhotoListItem.From(photo));
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

    public void Dispose()
    {
        scanCancellation?.Cancel();
        scanCancellation?.Dispose();
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Error, Message = "The scan of {FolderPath} failed.")]
    private static partial void LogScanFailed(ILogger logger, Exception exception, string folderPath);
}

public sealed record PhotoListItem(string FileName, string Path, string SizeLabel)
{
    public static PhotoListItem From(DiscoveredPhoto photo) => new(
        photo.FileName,
        photo.Path,
        $"{photo.FileSize / 1024d / 1024d:0.0} MB");
}
