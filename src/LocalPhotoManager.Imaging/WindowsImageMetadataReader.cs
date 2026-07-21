using System.Globalization;
using LocalPhotoManager.Core.Models;
using Windows.Graphics.Imaging;
using Windows.Storage;

namespace LocalPhotoManager.Imaging;

public static class WindowsImageMetadataReader
{
    private static readonly string[] RequestedProperties =
    [
        "System.Photo.DateTaken",
        "System.Photo.Orientation",
        "System.Photo.CameraManufacturer",
        "System.Photo.CameraModel",
    ];

    public static async Task<ImageMetadata?> TryReadAsync(DiscoveredPhoto photo, CancellationToken cancellationToken = default)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var sourceFile = await StorageFile.GetFileFromPathAsync(photo.Path);
            using var sourceStream = await sourceFile.OpenReadAsync();
            var decoder = await BitmapDecoder.CreateAsync(sourceStream);
            var properties = await decoder.BitmapProperties.GetPropertiesAsync(RequestedProperties);

            return new ImageMetadata(
                sourceFile.ContentType,
                decoder.PixelWidth,
                decoder.PixelHeight,
                ReadInteger(properties, "System.Photo.Orientation"),
                ReadDateTimeOffset(properties, "System.Photo.DateTaken"),
                ReadText(properties, "System.Photo.CameraManufacturer"),
                ReadText(properties, "System.Photo.CameraModel"));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (exception is ArgumentException or FileNotFoundException or IOException or UnauthorizedAccessException or System.Runtime.InteropServices.COMException)
        {
            return null;
        }
    }

    private static int? ReadInteger(BitmapPropertySet properties, string propertyName)
    {
        var typedValue = FindValue(properties, propertyName);
        return typedValue?.Value is not null
            ? Convert.ToInt32(typedValue.Value, CultureInfo.InvariantCulture)
            : null;
    }

    private static DateTimeOffset? ReadDateTimeOffset(BitmapPropertySet properties, string propertyName)
    {
        var typedValue = FindValue(properties, propertyName);
        if (typedValue?.Value is null)
        {
            return null;
        }

        return typedValue.Value switch
        {
            DateTimeOffset dateTimeOffset => dateTimeOffset,
            DateTime dateTime => new DateTimeOffset(dateTime),
            _ => null,
        };
    }

    private static string? ReadText(BitmapPropertySet properties, string propertyName)
    {
        var typedValue = FindValue(properties, propertyName);
        return typedValue?.Value is not null
            ? Convert.ToString(typedValue.Value, CultureInfo.InvariantCulture)
            : null;
    }

    private static BitmapTypedValue? FindValue(BitmapPropertySet properties, string propertyName)
    {
        foreach (var current in properties)
        {
            if (StringComparer.Ordinal.Equals(current.Key, propertyName))
            {
                return current.Value;
            }
        }

        return null;
    }
}
