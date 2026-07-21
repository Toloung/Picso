using LocalPhotoManager.Core.Models;

namespace LocalPhotoManager.Core.Services;

public interface IPhotoScanner
{
    IAsyncEnumerable<DiscoveredPhoto> ScanAsync(ScanRequest request, CancellationToken cancellationToken = default);
}
