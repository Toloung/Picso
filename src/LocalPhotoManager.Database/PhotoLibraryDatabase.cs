using LocalPhotoManager.Core.Models;
using Microsoft.Data.Sqlite;

namespace LocalPhotoManager.Database;

public sealed class PhotoLibraryDatabase
{
    private readonly string connectionString;

    public PhotoLibraryDatabase(string databasePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);
        Directory.CreateDirectory(Path.GetDirectoryName(databasePath)!);
        connectionString = new SqliteConnectionStringBuilder { DataSource = databasePath, ForeignKeys = true, Pooling = false }.ToString();
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS schema_migrations (version INTEGER PRIMARY KEY);
            CREATE TABLE IF NOT EXISTS directories (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                path TEXT NOT NULL UNIQUE,
                last_scan_time TEXT,
                created_at TEXT NOT NULL,
                updated_at TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS photos (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                directory_id INTEGER NOT NULL,
                path TEXT NOT NULL UNIQUE,
                file_name TEXT NOT NULL,
                extension TEXT NOT NULL,
                file_size INTEGER NOT NULL,
                created_time TEXT NOT NULL,
                modified_time TEXT NOT NULL,
                indexed_at TEXT NOT NULL,
                updated_at TEXT NOT NULL,
                is_missing INTEGER NOT NULL DEFAULT 0,
                FOREIGN KEY(directory_id) REFERENCES directories(id)
            );
            CREATE INDEX IF NOT EXISTS idx_photos_directory_id ON photos(directory_id);
            CREATE INDEX IF NOT EXISTS idx_photos_modified_time ON photos(modified_time);
            CREATE INDEX IF NOT EXISTS idx_photos_file_name ON photos(file_name);
            INSERT OR IGNORE INTO schema_migrations(version) VALUES (1);
            """;
        await command.ExecuteNonQueryAsync(cancellationToken);
        await ApplyMetadataMigrationAsync(connection, cancellationToken);
    }

    public async Task UpsertPhotoAsync(string directoryPath, DiscoveredPhoto photo, ImageMetadata metadata, CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var transaction = connection.BeginTransaction();
        var now = DateTimeOffset.UtcNow.ToString("O");

        await using (var directoryCommand = connection.CreateCommand())
        {
            directoryCommand.Transaction = transaction;
            directoryCommand.CommandText = """
                INSERT INTO directories(path, last_scan_time, created_at, updated_at)
                VALUES ($path, $now, $now, $now)
                ON CONFLICT(path) DO UPDATE SET last_scan_time = excluded.last_scan_time, updated_at = excluded.updated_at;
                """;
            directoryCommand.Parameters.AddWithValue("$path", directoryPath);
            directoryCommand.Parameters.AddWithValue("$now", now);
            await directoryCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        await using (var photoCommand = connection.CreateCommand())
        {
            photoCommand.Transaction = transaction;
            photoCommand.CommandText = """
                INSERT INTO photos(directory_id, path, file_name, extension, mime_type, file_size, created_time, modified_time, taken_time, width, height, orientation, camera_make, camera_model, indexed_at, updated_at)
                VALUES ((SELECT id FROM directories WHERE path = $directoryPath), $path, $fileName, $extension, $mimeType, $fileSize, $createdAt, $modifiedAt, $takenAt, $width, $height, $orientation, $cameraMake, $cameraModel, $now, $now)
                ON CONFLICT(path) DO UPDATE SET
                    file_name = excluded.file_name,
                    extension = excluded.extension,
                    mime_type = excluded.mime_type,
                    file_size = excluded.file_size,
                    created_time = excluded.created_time,
                    modified_time = excluded.modified_time,
                    taken_time = excluded.taken_time,
                    width = excluded.width,
                    height = excluded.height,
                    orientation = excluded.orientation,
                    camera_make = excluded.camera_make,
                    camera_model = excluded.camera_model,
                    indexed_at = excluded.indexed_at,
                    updated_at = excluded.updated_at,
                    is_missing = 0;
                """;
            photoCommand.Parameters.AddWithValue("$directoryPath", directoryPath);
            photoCommand.Parameters.AddWithValue("$path", photo.Path);
            photoCommand.Parameters.AddWithValue("$fileName", photo.FileName);
            photoCommand.Parameters.AddWithValue("$extension", photo.Extension);
            photoCommand.Parameters.AddWithValue("$mimeType", metadata.MimeType);
            photoCommand.Parameters.AddWithValue("$fileSize", photo.FileSize);
            photoCommand.Parameters.AddWithValue("$createdAt", photo.CreatedAtUtc.ToString("O"));
            photoCommand.Parameters.AddWithValue("$modifiedAt", photo.ModifiedAtUtc.ToString("O"));
            photoCommand.Parameters.AddWithValue("$takenAt", metadata.TakenAtUtc?.ToString("O") ?? (object)DBNull.Value);
            photoCommand.Parameters.AddWithValue("$width", metadata.Width);
            photoCommand.Parameters.AddWithValue("$height", metadata.Height);
            photoCommand.Parameters.AddWithValue("$orientation", metadata.Orientation ?? (object)DBNull.Value);
            photoCommand.Parameters.AddWithValue("$cameraMake", metadata.CameraMake ?? (object)DBNull.Value);
            photoCommand.Parameters.AddWithValue("$cameraModel", metadata.CameraModel ?? (object)DBNull.Value);
            photoCommand.Parameters.AddWithValue("$now", now);
            await photoCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<int> GetPhotoCountAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM photos WHERE is_missing = 0;";
        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken), System.Globalization.CultureInfo.InvariantCulture);
    }

    public async Task<bool> MarkPhotoMissingAsync(string path, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE photos
            SET is_missing = 1, updated_at = $now
            WHERE path = $path;
            """;
        command.Parameters.AddWithValue("$path", path);
        command.Parameters.AddWithValue("$now", DateTimeOffset.UtcNow.ToString("O"));
        return await command.ExecuteNonQueryAsync(cancellationToken) > 0;
    }

    public async Task<IReadOnlyList<IndexedPhoto>> GetRecentPhotosAsync(int limit = 500, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(limit, 1);
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT path, file_name, extension, file_size, created_time, modified_time, mime_type, width, height, orientation, taken_time, camera_make, camera_model
            FROM photos
            WHERE is_missing = 0
            ORDER BY COALESCE(taken_time, modified_time) DESC
            LIMIT $limit;
            """;
        command.Parameters.AddWithValue("$limit", limit);
        var photos = new List<IndexedPhoto>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var photo = new DiscoveredPhoto(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetInt64(3),
                DateTimeOffset.Parse(reader.GetString(4), System.Globalization.CultureInfo.InvariantCulture),
                DateTimeOffset.Parse(reader.GetString(5), System.Globalization.CultureInfo.InvariantCulture));
            var metadata = new ImageMetadata(
                reader.IsDBNull(6) ? "application/octet-stream" : reader.GetString(6),
                reader.IsDBNull(7) ? 0 : Convert.ToUInt32(reader.GetInt64(7), System.Globalization.CultureInfo.InvariantCulture),
                reader.IsDBNull(8) ? 0 : Convert.ToUInt32(reader.GetInt64(8), System.Globalization.CultureInfo.InvariantCulture),
                reader.IsDBNull(9) ? null : reader.GetInt32(9),
                reader.IsDBNull(10) ? null : DateTimeOffset.Parse(reader.GetString(10), System.Globalization.CultureInfo.InvariantCulture),
                reader.IsDBNull(11) ? null : reader.GetString(11),
                reader.IsDBNull(12) ? null : reader.GetString(12));
            photos.Add(new IndexedPhoto(photo, metadata));
        }

        return photos;
    }

    private static async Task ApplyMetadataMigrationAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        await using var checkCommand = connection.CreateCommand();
        checkCommand.CommandText = "SELECT EXISTS(SELECT 1 FROM schema_migrations WHERE version = 2);";
        if (Convert.ToInt32(await checkCommand.ExecuteScalarAsync(cancellationToken), System.Globalization.CultureInfo.InvariantCulture) == 1)
        {
            return;
        }

        await using var migrationCommand = connection.CreateCommand();
        migrationCommand.CommandText = """
            ALTER TABLE photos ADD COLUMN mime_type TEXT;
            ALTER TABLE photos ADD COLUMN taken_time TEXT;
            ALTER TABLE photos ADD COLUMN width INTEGER;
            ALTER TABLE photos ADD COLUMN height INTEGER;
            ALTER TABLE photos ADD COLUMN orientation INTEGER;
            ALTER TABLE photos ADD COLUMN camera_make TEXT;
            ALTER TABLE photos ADD COLUMN camera_model TEXT;
            INSERT INTO schema_migrations(version) VALUES (2);
            """;
        await migrationCommand.ExecuteNonQueryAsync(cancellationToken);
    }
}
