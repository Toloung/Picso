# Changelog

## Unreleased

- Created the WinUI 3 solution and layered projects.
- Added local SQLite schema initialization and photo upsert support.
- Added cancellable local file scanning and path policy tests.
- Added a local-library UI shell with folder selection and scan status.
- Added Windows-native thumbnail generation and a debounced FileSystemWatcher service.
- Added content validation, EXIF-oriented metadata extraction, database migration v2, and startup index restoration.
- Connected file watcher events to local index updates for created, changed, deleted, and renamed photos.
- Added folder summaries, timeline month summaries, and UI navigation for browsing indexed photos.
- Added a repeatable MSIX packaging script and produced a signed x64 test package.
- Fixed the MSIX test certificate generation so sideload certificates include Basic Constraints.
- Display generated thumbnails in the photo list and keep navigation stable while scanning.
- Added click-to-preview with in-app image preview and photo details.
- Reworked the main layout with a top menu, left library/sidebar, large preview canvas, bottom filmstrip, navigation arrows, and a toggleable info pane.
- Store indexed photos under their actual parent folders so subfolder browsing works after rescans.
- Removed redundant large sidebar function buttons so the sidebar focuses on folders and timeline browsing.
- Added expandable folder-tree navigation and recursive parent-folder photo filtering.
- Added photo-viewer mouse and keyboard interactions: selected filmstrip state, position counter, previous/next keys, open original, reveal in Explorer, and info-pane shortcuts.
