using Microsoft.Data.Sqlite;
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Buddie.Services.ExceptionHandling;

namespace Buddie.Database
{
    public class DatabaseInitializer : IDatabaseInitializer
    {
        private readonly IDatabasePathProvider _pathProvider;
        private readonly DatabaseService _databaseService;

        public DatabaseInitializer(IDatabasePathProvider pathProvider, DatabaseService databaseService)
        {
            _pathProvider = pathProvider;
            _databaseService = databaseService;
        }

        public async Task InitializeAsync()
        {
            await ExceptionHandlingService.Database.ExecuteSafelyAsync(async () =>
            {
                using var connection = new SqliteConnection(_pathProvider.ConnectionString);
                await connection.OpenAsync();

                var createAppSettingsTable = @"
                    CREATE TABLE IF NOT EXISTS AppSettings (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        IsTopmost INTEGER NOT NULL DEFAULT 1,
                        ShowInTaskbar INTEGER NOT NULL DEFAULT 1,
                        EnableAnimation INTEGER NOT NULL DEFAULT 1,
                        IsDarkTheme INTEGER NOT NULL DEFAULT 0,
                        UpdatedAt TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP
                    )";

                var createApiConfigTable = @"
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
                    )";

                var createTtsConfigTable = @"
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
                        CreatedAt TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
                        UpdatedAt TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP
                    )";

                var createConversationsTable = @"
                    CREATE TABLE IF NOT EXISTS Conversations (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        Title TEXT NOT NULL,
                        CreatedAt TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
                        UpdatedAt TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
                        MessageCount INTEGER NOT NULL DEFAULT 0
                    )";

                var createMessagesTable = @"
                    CREATE TABLE IF NOT EXISTS Messages (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        ConversationId INTEGER NOT NULL,
                        Content TEXT NOT NULL,
                        IsUser INTEGER NOT NULL,
                        ReasoningContent TEXT,
                        CreatedAt TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
                        FOREIGN KEY (ConversationId) REFERENCES Conversations (Id) ON DELETE CASCADE
                    )";

                var createTtsAudioTable = @"
                    CREATE TABLE IF NOT EXISTS TtsAudio (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        TextHash TEXT NOT NULL UNIQUE,
                        AudioData BLOB NOT NULL,
                        TtsConfigJson TEXT NOT NULL,
                        CreatedAt TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
                        LastAccessedAt TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP
                    )";

                var createIndexes = @"
                    CREATE INDEX IF NOT EXISTS idx_messages_conversation_id ON Messages(ConversationId);
                    CREATE INDEX IF NOT EXISTS idx_conversations_updated_at ON Conversations(UpdatedAt DESC);
                    CREATE INDEX IF NOT EXISTS idx_messages_created_at ON Messages(CreatedAt);
                    CREATE INDEX IF NOT EXISTS idx_tts_audio_text_hash ON TtsAudio(TextHash);
                    CREATE INDEX IF NOT EXISTS idx_tts_audio_created_at ON TtsAudio(CreatedAt);
                ";

                using var command = connection.CreateCommand();
                command.CommandText = createAppSettingsTable; await command.ExecuteNonQueryAsync();
                command.CommandText = createApiConfigTable; await command.ExecuteNonQueryAsync();
                command.CommandText = createTtsConfigTable; await command.ExecuteNonQueryAsync();
                command.CommandText = createConversationsTable; await command.ExecuteNonQueryAsync();
                command.CommandText = createMessagesTable; await command.ExecuteNonQueryAsync();
                command.CommandText = createTtsAudioTable; await command.ExecuteNonQueryAsync();
                command.CommandText = createIndexes; await command.ExecuteNonQueryAsync();

                MigrateDatabaseSchema(connection);
                InitializeDefaultSettings(connection);

                Debug.WriteLine($"Database initialized at: {_pathProvider.DatabasePath}");
            }, "初始化数据库");
        }

        private static void MigrateDatabaseSchema(SqliteConnection connection)
        {
            ExceptionHandlingService.Database.ExecuteSafely(() =>
            {
                using var command = connection.CreateCommand();
                command.CommandText = "PRAGMA table_info(TtsConfigurations)";
                using var reader = command.ExecuteReader();
                bool hasIsActiveColumn = false;
                bool hasChannelTypeColumn = false;
                while (reader.Read())
                {
                    string columnName = reader.GetString(1);
                    if (columnName == "IsActive") hasIsActiveColumn = true;
                    else if (columnName == "ChannelType") hasChannelTypeColumn = true;
                }
                reader.Close();

                if (!hasIsActiveColumn)
                {
                    command.CommandText = "ALTER TABLE TtsConfigurations ADD COLUMN IsActive INTEGER NOT NULL DEFAULT 0";
                    command.ExecuteNonQuery();
                    Debug.WriteLine("Added IsActive column to TtsConfigurations table");
                }
                if (!hasChannelTypeColumn)
                {
                    command.CommandText = "ALTER TABLE TtsConfigurations ADD COLUMN ChannelType INTEGER DEFAULT 0";
                    command.ExecuteNonQuery();
                    Debug.WriteLine("Added ChannelType column to TtsConfigurations table");
                }

                command.CommandText = "PRAGMA table_info(Messages)";
                using var messagesReader = command.ExecuteReader();
                bool hasImageDataColumn = false;
                bool hasImageContentTypeColumn = false;
                while (messagesReader.Read())
                {
                    string columnName = messagesReader.GetString(1);
                    if (columnName == "ImageData") hasImageDataColumn = true;
                    else if (columnName == "ImageContentType") hasImageContentTypeColumn = true;
                }
                messagesReader.Close();

                if (!hasImageDataColumn)
                {
                    command.CommandText = "ALTER TABLE Messages ADD COLUMN ImageData BLOB";
                    command.ExecuteNonQuery();
                    Debug.WriteLine("Added ImageData column to Messages table");
                }
                if (!hasImageContentTypeColumn)
                {
                    command.CommandText = "ALTER TABLE Messages ADD COLUMN ImageContentType TEXT";
                    command.ExecuteNonQuery();
                    Debug.WriteLine("Added ImageContentType column to Messages table");
                }
            }, "数据库架构迁移");
        }

        private static void InitializeDefaultSettings(SqliteConnection connection)
        {
            using var checkCommand = connection.CreateCommand();
            checkCommand.CommandText = "SELECT COUNT(*) FROM AppSettings";
            var count = Convert.ToInt32(checkCommand.ExecuteScalar());
            Debug.WriteLine($"Found {count} app settings records in database");
            if (count == 0)
            {
                using var insertCommand = connection.CreateCommand();
                insertCommand.CommandText = @"
                    INSERT INTO AppSettings (IsTopmost, ShowInTaskbar, EnableAnimation, IsDarkTheme, UpdatedAt)
                    VALUES (1, 1, 1, 0, datetime('now'))";
                insertCommand.ExecuteNonQuery();
                Debug.WriteLine("Inserted default app settings into database");
            }
        }

        public void BackupDatabase(string backupPath)
        {
            ExceptionHandlingService.Database.ExecuteSafely(() =>
            {
                System.IO.File.Copy(_pathProvider.DatabasePath, backupPath, true);
                Debug.WriteLine($"Database backed up to: {backupPath}");
            }, "备份数据库");
        }

        public void RestoreDatabase(string backupPath)
        {
            ExceptionHandlingService.Database.ExecuteSafely(() =>
            {
                if (System.IO.File.Exists(backupPath))
                {
                    System.IO.File.Copy(backupPath, _pathProvider.DatabasePath, true);
                    Debug.WriteLine($"Database restored from: {backupPath}");
                }
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

