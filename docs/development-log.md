# Development log

## 2026-07-21

- Installed the project-local .NET 10.0.302 SDK in `D:\CodexTools\dotnet-10.0.302`.
- Kept NuGet packages and CLI state under `D:\CodexTools`.
- Created the source repository at `E:\Picso\LocalPhotoManager` with remote `Toloung/Picso`.
- Built the x64 Debug solution with zero warnings and zero errors.
- Ran 13 automated tests successfully.
- Started the Debug WinUI application successfully.
- Added content validation, metadata persistence, and startup recovery for indexed photos.
- Connected debounced file-system changes to SQLite index updates and the visible photo list.
- Added SQLite-backed folder and timeline browsing queries and connected them to the WinUI navigation surface.
- Produced a signed x64 MSIX test package and zipped install folder under `E:\Picso\LocalPhotoManager\artifacts`.
- Fixed the MSIX test certificate Basic Constraints issue and regenerated the signed x64 package.
- Replaced photo-list placeholders with generated thumbnails and blocked navigation changes during active scans.
- Added in-app click preview with file name, folder, path, image dimensions, MIME type, and size.
- Reworked the WinUI shell into a photo-browser layout with top menu, left function/folder sidebar, central preview, bottom filmstrip, previous/next controls, and a toggleable information pane.
- Changed indexing to store each photo against its actual parent folder and added a regression test for moving existing rows from a scan root to a subfolder.
