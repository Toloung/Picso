using System.Security.Cryptography;
using System.Text;
using LocalPhotoManager.Core.Models;
using LocalPhotoManager.Core.Services;

namespace LocalPhotoManager.Imaging;

public sealed class ThumbnailPathFactory(AppDataPaths paths)
{
    public string GetThumbnailPath(DiscoveredPhoto photo, int size)
    {
        if (size is not (256 or 1024 or 2048))
        {
            throw new ArgumentOutOfRangeException(nameof(size), "Supported thumbnail sizes are 256, 1024, and 2048 pixels.");
        }

        var input = $"{photo.Path}|{photo.FileSize}|{photo.ModifiedAtUtc:O}|v1";
        var key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(input))).ToLowerInvariant();
        var directory = Path.Combine(paths.ThumbnailsRoot, size.ToString(System.Globalization.CultureInfo.InvariantCulture));
        Directory.CreateDirectory(directory);
        return Path.Combine(directory, $"{key}.jpg");
    }
}
