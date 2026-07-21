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
    }

    public async Task UpsertPhotoAsync(string directoryPath, DiscoveredPhoto photo, CancellationToken cancellationToken = default)
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
                INSERT INTO photos(directory_id, path, file_name, extension, file_size, created_time, modified_time, indexed_at, updated_at)
                VALUES ((SELECT id FROM directories WHERE path = $directoryPath), $path, $fileName, $extension, $fileSize, $createdAt, $modifiedAt, $now, $now)
                ON CONFLICT(path) DO UPDATE SET
                    file_name = excluded.file_name,
                    extension = excluded.extension,
                    file_size = excluded.file_size,
                    created_time = excluded.created_time,
                    modified_time = excluded.modified_time,
                    indexed_at = excluded.indexed_at,
                    updated_at = excluded.updated_at,
                    is_missing = 0;
                """;
            photoCommand.Parameters.AddWithValue("$directoryPath", directoryPath);
            photoCommand.Parameters.AddWithValue("$path", photo.Path);
            photoCommand.Parameters.AddWithValue("$fileName", photo.FileName);
            photoCommand.Parameters.AddWithValue("$extension", photo.Extension);
            photoCommand.Parameters.AddWithValue("$fileSize", photo.FileSize);
            photoCommand.Parameters.AddWithValue("$createdAt", photo.CreatedAtUtc.ToString("O"));
            photoCommand.Parameters.AddWithValue("$modifiedAt", photo.ModifiedAtUtc.ToString("O"));
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
}
