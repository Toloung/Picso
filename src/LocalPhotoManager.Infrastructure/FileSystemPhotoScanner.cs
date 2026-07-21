using System.Runtime.CompilerServices;
using LocalPhotoManager.Core.Models;
using LocalPhotoManager.Core.Services;

namespace LocalPhotoManager.Infrastructure;

public sealed class FileSystemPhotoScanner : IPhotoScanner
{
    public async IAsyncEnumerable<DiscoveredPhoto> ScanAsync(
        ScanRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request.RootPath);

        foreach (var filePath in EnumerateFilesSafely(request.RootPath, request.IncludeSubdirectories))
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!PathPolicy.IsSupportedImage(filePath))
            {
                continue;
            }

            FileInfo info;
            try
            {
                info = new FileInfo(filePath);
                if (!info.Exists)
                {
                    continue;
                }
            }
            catch (UnauthorizedAccessException)
            {
                continue;
            }
            catch (IOException)
            {
                continue;
            }

            yield return new DiscoveredPhoto(
                PathPolicy.NormalizePath(info.FullName),
                info.Name,
                info.Extension.ToLowerInvariant(),
                info.Length,
                info.CreationTimeUtc,
                info.LastWriteTimeUtc);

            await Task.Yield();
        }
    }

    private static IEnumerable<string> EnumerateFilesSafely(string rootPath, bool includeSubdirectories)
    {
        var pending = new Stack<string>();
        pending.Push(rootPath);

        while (pending.Count > 0)
        {
            var currentDirectory = pending.Pop();
            if (PathPolicy.IsExcludedDirectory(currentDirectory))
            {
                continue;
            }

            IEnumerable<string> files;
            try
            {
                files = Directory.EnumerateFiles(currentDirectory, "*", SearchOption.TopDirectoryOnly);
            }
            catch (UnauthorizedAccessException)
            {
                continue;
            }
            catch (DirectoryNotFoundException)
            {
                continue;
            }
            catch (IOException)
            {
                continue;
            }

            foreach (var file in files)
            {
                yield return file;
            }

            if (!includeSubdirectories)
            {
                continue;
            }

            IEnumerable<string> directories;
            try
            {
                directories = Directory.EnumerateDirectories(currentDirectory, "*", SearchOption.TopDirectoryOnly);
            }
            catch (UnauthorizedAccessException)
            {
                continue;
            }
            catch (DirectoryNotFoundException)
            {
                continue;
            }
            catch (IOException)
            {
                continue;
            }

            foreach (var directory in directories)
            {
                try
                {
                    var attributes = File.GetAttributes(directory);
                    if ((attributes & FileAttributes.ReparsePoint) == 0)
                    {
                        pending.Push(directory);
                    }
                }
                catch (IOException)
                {
                    // The directory changed while the scan was running.
                }
                catch (UnauthorizedAccessException)
                {
                    // Skip inaccessible directories without failing the scan.
                }
            }
        }
    }
}
