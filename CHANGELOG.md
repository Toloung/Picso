# Changelog

## Unreleased

- Created the WinUI 3 solution and layered projects.
- Added local SQLite schema initialization and photo upsert support.
- Added cancellable local file scanning and path policy tests.
- Added a local-library UI shell with folder selection and scan status.
- Added Windows-native thumbnail generation and a debounced FileSystemWatcher service.
- Added content validation, EXIF-oriented metadata extraction, database migration v2, and startup index restoration.
- Connected file watcher events to local index updates for created, changed, deleted, and renamed photos.
