using LocalPhotoManager.Core.Models;

namespace LocalPhotoManager.Core.Services;

public interface IPhotoFileWatcher : IAsyncDisposable
{
    event EventHandler<PhotoFileChange>? FileChanged;

    void Start(string rootPath);
}
