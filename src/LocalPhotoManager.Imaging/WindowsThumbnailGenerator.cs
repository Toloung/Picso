using LocalPhotoManager.Core.Models;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Streams;

namespace LocalPhotoManager.Imaging;

public sealed class WindowsThumbnailGenerator(ThumbnailPathFactory pathFactory)
{
    public async Task<string> GenerateAsync(DiscoveredPhoto photo, int size, CancellationToken cancellationToken = default)
    {
        var outputPath = pathFactory.GetThumbnailPath(photo, size);
        if (File.Exists(outputPath))
        {
            return outputPath;
        }

        cancellationToken.ThrowIfCancellationRequested();
        var sourceFile = await StorageFile.GetFileFromPathAsync(photo.Path);
        using var sourceStream = await sourceFile.OpenReadAsync();
        var decoder = await BitmapDecoder.CreateAsync(sourceStream);
        var scale = Math.Min((double)size / decoder.PixelWidth, (double)size / decoder.PixelHeight);
        var transform = new BitmapTransform
        {
            ScaledWidth = (uint)Math.Max(1, Math.Round(decoder.PixelWidth * Math.Min(1, scale))),
            ScaledHeight = (uint)Math.Max(1, Math.Round(decoder.PixelHeight * Math.Min(1, scale))),
        };
        var pixels = await decoder.GetPixelDataAsync(
            BitmapPixelFormat.Bgra8,
            BitmapAlphaMode.Ignore,
            transform,
            ExifOrientationMode.RespectExifOrientation,
            ColorManagementMode.DoNotColorManage);

        var outputDirectory = await StorageFolder.GetFolderFromPathAsync(Path.GetDirectoryName(outputPath)!);
        var outputFile = await outputDirectory.CreateFileAsync(Path.GetFileName(outputPath), CreationCollisionOption.ReplaceExisting);
        using var outputStream = await outputFile.OpenAsync(FileAccessMode.ReadWrite);
        var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.JpegEncoderId, outputStream);
        encoder.SetPixelData(
            BitmapPixelFormat.Bgra8,
            BitmapAlphaMode.Ignore,
            transform.ScaledWidth,
            transform.ScaledHeight,
            96,
            96,
            pixels.DetachPixelData());
        await encoder.FlushAsync();
        return outputPath;
    }
}
