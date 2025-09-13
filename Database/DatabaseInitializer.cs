using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Buddie.Services.ExceptionHandling;

namespace Buddie.Database
{
    public class DatabaseInitializer : IDatabaseInitializer
    {
        private readonly IDatabasePathProvider _pathProvider;
        private readonly DatabaseService _databaseService;
        private readonly ISqliteConnectionPool _connectionPool;
        private const int CommandTimeoutSeconds = 30; // 默认命令超时时间

        public DatabaseInitializer(IDatabasePathProvider pathProvider, DatabaseService databaseService, ISqliteConnectionPool connectionPool)
        {
            _pathProvider = pathProvider;
            _databaseService = databaseService;
            _connectionPool = connectionPool;
        }

        public async Task InitializeAsync()
        {
            await ExceptionHandlingService.Database.ExecuteSafelyAsync(async () =>
            {
                using var connection = new SqliteConnection(_pathProvider.ConnectionString);
                await connection.OpenAsync();

                // 创建迁移跟踪表（用于幂等性校验）
                await CreateMigrationTrackingTableAsync(connection);

                // 执行数据库初始化
                await CreateTablesAsync(connection);
                await CreateIndexesAsync(connection);
                await MigrateDatabaseSchemaAsync(connection);
                await InitializeDefaultSettingsAsync(connection);

                // 执行 API Key 加密迁移
                var migration = new DatabaseMigration(_connectionPool);
                await migration.MigrateApiKeysToEncryptedFormatAsync();

                Debug.WriteLine($"Database initialized at: {_pathProvider.DatabasePath}");
            }, "初始化数据库");
        }

        private async Task CreateMigrationTrackingTableAsync(SqliteConnection connection)
        {
            const string sql = @"
                CREATE TABLE IF NOT EXISTS MigrationHistory (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    MigrationName TEXT NOT NULL UNIQUE,
                    AppliedAt TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
                    Checksum TEXT
                )";

            using var command = CreateCommand(connection, sql);
            await command.ExecuteNonQueryAsync();
        }

        private async Task<bool> IsMigrationAppliedAsync(SqliteConnection connection, string migrationName)
        {
            const string sql = "SELECT COUNT(*) FROM MigrationHistory WHERE MigrationName = @migrationName";
            using var command = CreateCommand(connection, sql);
            command.Parameters.AddWithValue("@migrationName", migrationName);
            var count = Convert.ToInt32(await command.ExecuteScalarAsync());
            return count > 0;
        }

        private async Task RecordMigrationAsync(SqliteConnection connection, string migrationName, string? checksum = null)
        {
            const string sql = @"
                INSERT OR IGNORE INTO MigrationHistory (MigrationName, Checksum, AppliedAt)
                VALUES (@migrationName, @checksum, datetime('now'))";
            
            using var command = CreateCommand(connection, sql);
            command.Parameters.AddWithValue("@migrationName", migrationName);
            command.Parameters.AddWithValue("@checksum", checksum ?? string.Empty);
            await command.ExecuteNonQueryAsync();
        }

        private async Task CreateTablesAsync(SqliteConnection connection)
        {
            var tableMigrations = new Dictionary<string, string>
            {
                ["CreateAppSettingsTable"] = @"
                    CREATE TABLE IF NOT EXISTS AppSettings (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        IsTopmost INTEGER NOT NULL DEFAULT 1,
                        ShowInTaskbar INTEGER NOT NULL DEFAULT 1,
                        EnableAnimation INTEGER NOT NULL DEFAULT 1,
                        IsDarkTheme INTEGER NOT NULL DEFAULT 0,
                        UpdatedAt TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP
                    )",

                ["CreateApiConfigurationsTable"] = @"
                    CREATE TABLE IF NOT EXISTS ApiConfigurations (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        Name TEXT NOT NULL,
                        ApiUrl TEXT NOT NULL,
                        ApiKey TEXT NOT NULL,
                        ModelName TEXT NOT NULL,
                        IsStreamingEnabled INTEGER NOT NULL DEFAULT 1,
                        IsMultimodalEnabled INTEGER NOT NULL DEFAULT 0,
                        ChannelType INTEGER NOT NULL DEFAULT 0,
                        SupportsThinking INTEGER NOT NULL DEFAULT 0,
                        CreatedAt TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
                        UpdatedAt TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP
                    )",

                ["CreateTtsConfigurationsTable"] = @"
                    CREATE TABLE IF NOT EXISTS TtsConfigurations (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        Name TEXT NOT NULL,
                        ApiUrl TEXT NOT NULL DEFAULT 'http://localhost:5050/v1/audio/speech',
                        ApiKey TEXT NOT NULL,
                        Model TEXT NOT NULL DEFAULT 'tts-1',
                        Voice TEXT NOT NULL DEFAULT 'alloy',
                        Speed REAL NOT NULL DEFAULT 1.0,
                        IsStreamingEnabled INTEGER NOT NULL DEFAULT 0,
                        IsActive INTEGER NOT NULL DEFAULT 0,
                        ChannelType INTEGER DEFAULT 0,
                        CreatedAt TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
                        UpdatedAt TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP
                    )",

                ["CreateConversationsTable"] = @"
                    CREATE TABLE IF NOT EXISTS Conversations (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        Title TEXT NOT NULL,
                        CreatedAt TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
                        UpdatedAt TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
                        MessageCount INTEGER NOT NULL DEFAULT 0
                    )",

                ["CreateMessagesTable"] = @"
                    CREATE TABLE IF NOT EXISTS Messages (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        ConversationId INTEGER NOT NULL,
                        Content TEXT NOT NULL,
                        IsUser INTEGER NOT NULL,
                        ReasoningContent TEXT,
                        ImageData BLOB,
                        ImageContentType TEXT,
                        CreatedAt TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
                        FOREIGN KEY (ConversationId) REFERENCES Conversations (Id) ON DELETE CASCADE
                    )",

                ["CreateTtsAudioTable"] = @"
                    CREATE TABLE IF NOT EXISTS TtsAudio (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        TextHash TEXT NOT NULL UNIQUE,
                        AudioData BLOB NOT NULL,
                        TtsConfigJson TEXT NOT NULL,
                        CreatedAt TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
                        LastAccessedAt TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP
                    )"
            };

            foreach (var migration in tableMigrations)
            {
                if (!await IsMigrationAppliedAsync(connection, migration.Key))
                {
                    using var command = CreateCommand(connection, migration.Value);
                    await command.ExecuteNonQueryAsync();
                    await RecordMigrationAsync(connection, migration.Key);
                    Debug.WriteLine($"Applied migration: {migration.Key}");
                }
            }
        }

        private async Task CreateIndexesAsync(SqliteConnection connection)
        {
            var indexMigrations = new Dictionary<string, string>
            {
                ["CreateMessagesConversationIdIndex"] = "CREATE INDEX IF NOT EXISTS idx_messages_conversation_id ON Messages(ConversationId)",
                ["CreateConversationsUpdatedAtIndex"] = "CREATE INDEX IF NOT EXISTS idx_conversations_updated_at ON Conversations(UpdatedAt DESC)",
                ["CreateMessagesCreatedAtIndex"] = "CREATE INDEX IF NOT EXISTS idx_messages_created_at ON Messages(CreatedAt)",
                ["CreateTtsAudioTextHashIndex"] = "CREATE INDEX IF NOT EXISTS idx_tts_audio_text_hash ON TtsAudio(TextHash)",
                ["CreateTtsAudioCreatedAtIndex"] = "CREATE INDEX IF NOT EXISTS idx_tts_audio_created_at ON TtsAudio(CreatedAt)"
            };

            foreach (var migration in indexMigrations)
            {
                if (!await IsMigrationAppliedAsync(connection, migration.Key))
                {
                    using var command = CreateCommand(connection, migration.Value);
                    await command.ExecuteNonQueryAsync();
                    await RecordMigrationAsync(connection, migration.Key);
                    Debug.WriteLine($"Applied index migration: {migration.Key}");
                }
            }
        }

        private async Task MigrateDatabaseSchemaAsync(SqliteConnection connection)
        {
            await ExceptionHandlingService.Database.ExecuteSafelyAsync(async () =>
            {
                // TtsConfigurations表的列迁移
                var ttsColumnMigrations = new Dictionary<string, string>
                {
                    ["AddTtsIsActiveColumn"] = "ALTER TABLE TtsConfigurations ADD COLUMN IsActive INTEGER NOT NULL DEFAULT 0",
                    ["AddTtsChannelTypeColumn"] = "ALTER TABLE TtsConfigurations ADD COLUMN ChannelType INTEGER DEFAULT 0"
                };

                await ApplyColumnMigrationsAsync(connection, "TtsConfigurations", ttsColumnMigrations);

                // Messages表的列迁移
                var messagesColumnMigrations = new Dictionary<string, string>
                {
                    ["AddMessagesImageDataColumn"] = "ALTER TABLE Messages ADD COLUMN ImageData BLOB",
                    ["AddMessagesImageContentTypeColumn"] = "ALTER TABLE Messages ADD COLUMN ImageContentType TEXT"
                };

                await ApplyColumnMigrationsAsync(connection, "Messages", messagesColumnMigrations);
            }, "数据库架构迁移");
        }

        private async Task ApplyColumnMigrationsAsync(SqliteConnection connection, string tableName, Dictionary<string, string> migrations)
        {
            // 获取表的列信息
            var existingColumns = new HashSet<string>();
            using (var command = CreateCommand(connection, $"PRAGMA table_info({tableName})"))
            using (var reader = await command.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    existingColumns.Add(reader.GetString(1)); // 列名在第二列
                }
            }

            // 应用需要的迁移
            foreach (var migration in migrations)
            {
                var columnName = ExtractColumnNameFromAlterStatement(migration.Value);
                
                // 检查列是否已存在（幂等性）
                if (!existingColumns.Contains(columnName) && !await IsMigrationAppliedAsync(connection, migration.Key))
                {
                    using var command = CreateCommand(connection, migration.Value);
                    await command.ExecuteNonQueryAsync();
                    await RecordMigrationAsync(connection, migration.Key);
                    Debug.WriteLine($"Applied column migration: {migration.Key}");
                }
            }
        }

        private string ExtractColumnNameFromAlterStatement(string alterStatement)
        {
            // 从 ALTER TABLE ... ADD COLUMN ColumnName ... 提取列名
            var parts = alterStatement.Split(new[] { "COLUMN", " " }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < parts.Length; i++)
            {
                if (parts[i].Equals("COLUMN", StringComparison.OrdinalIgnoreCase) && i + 1 < parts.Length)
                {
                    return parts[i + 1].Trim();
                }
            }
            return string.Empty;
        }

        private async Task InitializeDefaultSettingsAsync(SqliteConnection connection)
        {
            const string checkSql = "SELECT COUNT(*) FROM AppSettings";
            using var checkCommand = CreateCommand(connection, checkSql);
            var count = Convert.ToInt32(await checkCommand.ExecuteScalarAsync());
            
            Debug.WriteLine($"Found {count} app settings records in database");
            
            if (count == 0)
            {
                const string insertSql = @"
                    INSERT INTO AppSettings (IsTopmost, ShowInTaskbar, EnableAnimation, IsDarkTheme, UpdatedAt)
                    VALUES (@isTopmost, @showInTaskbar, @enableAnimation, @isDarkTheme, datetime('now'))";
                
                using var insertCommand = CreateCommand(connection, insertSql);
                insertCommand.Parameters.AddWithValue("@isTopmost", 1);
                insertCommand.Parameters.AddWithValue("@showInTaskbar", 1);
                insertCommand.Parameters.AddWithValue("@enableAnimation", 1);
                insertCommand.Parameters.AddWithValue("@isDarkTheme", 0);
                
                await insertCommand.ExecuteNonQueryAsync();
                Debug.WriteLine("Inserted default app settings into database");
            }
        }

        private SqliteCommand CreateCommand(SqliteConnection connection, string commandText)
        {
            var command = connection.CreateCommand();
            command.CommandText = commandText;
            command.CommandTimeout = CommandTimeoutSeconds;
            return command;
        }

        public void BackupDatabase(string backupPath)
        {
            ExceptionHandlingService.Database.ExecuteSafely(() =>
            {
                var directory = System.IO.Path.GetDirectoryName(backupPath);
                if (!string.IsNullOrEmpty(directory) && !System.IO.Directory.Exists(directory))
                {
                    System.IO.Directory.CreateDirectory(directory);
                }
                
                System.IO.File.Copy(_pathProvider.DatabasePath, backupPath, true);
                Debug.WriteLine($"Database backed up to: {backupPath}");
            }, "备份数据库");
        }

        public void RestoreDatabase(string backupPath)
        {
            ExceptionHandlingService.Database.ExecuteSafely(() =>
            {
                if (!System.IO.File.Exists(backupPath))
                {
                    throw new System.IO.FileNotFoundException($"Backup file not found: {backupPath}");
                }
                
                System.IO.File.Copy(backupPath, _pathProvider.DatabasePath, true);
                Debug.WriteLine($"Database restored from: {backupPath}");
            }, "恢复数据库");
        }

        public async Task CleanupTtsAudioCacheAsync(int daysToKeep = 7, int maxCount = 1000, long maxSizeMB = 500)
        {
            await ExceptionHandlingService.Database.ExecuteSafelyAsync(async () =>
            {
                await _databaseService.CleanupTtsCacheAsync(daysToKeep, maxCount, maxSizeMB);
            }, "TTS缓存清理");
        }
    }
}

