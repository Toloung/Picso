# Architecture

`LocalPhotoManager` is a local-only Windows desktop application. Original image files remain in place and are accessed read-only.

## Layers

- `LocalPhotoManager.App`: WinUI 3 user interface, host configuration and MVVM view models.
- `LocalPhotoManager.Core`: domain records, scan contracts and path/data-directory policy.
- `LocalPhotoManager.Infrastructure`: file-system scanner and future watcher integration.
- `LocalPhotoManager.Database`: SQLite schema migration and parameterized persistence.
- `LocalPhotoManager.Imaging`: thumbnail-cache identity and future decode/render services.

## Data flow

`FolderPicker` → `IPhotoScanner` → `PhotoLibraryDatabase` → `ThumbnailPathFactory` → UI list.

The application data root is `%LOCALAPPDATA%\LocalPhotoManager`. It contains the database, thumbnails, logs and future cache/model/backup files. No service, account, upload or telemetry component is part of this architecture.
