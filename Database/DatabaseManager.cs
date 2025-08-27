using Microsoft.Data.Sqlite;
using System;
using System.IO;
using System.Reflection;
using System.Diagnostics;
using System.Linq;

namespace Buddie.Database
{
    public static class DatabaseManager
    {
        private static string? _connectionString;
        private static string? _databasePath;

        public static string ConnectionString
        {
            get
            {
                if (string.IsNullOrEmpty(_connectionString))
                {
                    InitializeDatabasePath();
                }
                return _connectionString!;
            }
        }

        public static string DatabasePath
        {
            get
            {
                if (string.IsNullOrEmpty(_databasePath))
                {
                    InitializeDatabasePath();
                }
                return _databasePath!;
            }
        }

        private static void InitializeDatabasePath()
        {
            string dbDirectory;
            
            // 判断是否为开发环境
            if (IsDebugMode())
            {
                // 开发环境：使用项目根目录的 data 文件夹
                var projectRoot = GetProjectRoot();
                dbDirectory = Path.Combine(projectRoot, "data");
            }
            else
            {
                // 生产环境：使用 exe 同级目录的 data 文件夹
                var exeDirectory = AppDomain.CurrentDomain.BaseDirectory;
                dbDirectory = Path.Combine(exeDirectory, "data");
            }

            // 确保目录存在
            Directory.CreateDirectory(dbDirectory);

            _databasePath = Path.Combine(dbDirectory, "buddie.db");
            _connectionString = $"Data Source={_databasePath}";
        }

        private static bool IsDebugMode()
        {
            #if DEBUG
                return true;
            #else
                return false;
            #endif
        }

        private static string GetProjectRoot()
        {
            var assembly = Assembly.GetExecutingAssembly();
            var location = assembly.Location;
            var directory = new DirectoryInfo(Path.GetDirectoryName(location)!);

            // 向上查找，直到找到包含 .csproj 文件的目录
            while (directory != null && !directory.GetFiles("*.csproj").Any())
            {
                directory = directory.Parent;
            }

            return directory?.FullName ?? AppDomain.CurrentDomain.BaseDirectory;
        }

        public static void InitializeDatabase()
        {
            try
            {
                using var connection = new SqliteConnection(ConnectionString);
                connection.Open();

                // 创建应用设置表
                var createAppSettingsTable = @"
                    CREATE TABLE IF NOT EXISTS AppSettings (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        IsTopmost INTEGER NOT NULL DEFAULT 1,
                        ShowInTaskbar INTEGER NOT NULL DEFAULT 1,
                        EnableAnimation INTEGER NOT NULL DEFAULT 1,
                        IsDarkTheme INTEGER NOT NULL DEFAULT 0,
                        UpdatedAt TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP
                    )";
                
                // 创建API配置表
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

                // 创建TTS配置表
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

                // 创建对话表
                var createConversationsTable = @"
                    CREATE TABLE IF NOT EXISTS Conversations (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        Title TEXT NOT NULL,
                        CreatedAt TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
                        UpdatedAt TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
                        MessageCount INTEGER NOT NULL DEFAULT 0
                    )";

                // 创建消息表
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

                // 创建TTS音频缓存表
                var createTtsAudioTable = @"
                    CREATE TABLE IF NOT EXISTS TtsAudio (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        TextHash TEXT NOT NULL UNIQUE,
                        AudioData BLOB NOT NULL,
                        TtsConfigJson TEXT NOT NULL,
                        CreatedAt TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
                        LastAccessedAt TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP
                    )";

                // 创建索引
                var createIndexes = @"
                    CREATE INDEX IF NOT EXISTS idx_messages_conversation_id ON Messages(ConversationId);
                    CREATE INDEX IF NOT EXISTS idx_conversations_updated_at ON Conversations(UpdatedAt DESC);
                    CREATE INDEX IF NOT EXISTS idx_messages_created_at ON Messages(CreatedAt);
                    CREATE INDEX IF NOT EXISTS idx_tts_audio_text_hash ON TtsAudio(TextHash);
                    CREATE INDEX IF NOT EXISTS idx_tts_audio_created_at ON TtsAudio(CreatedAt);
                ";

                using var command = connection.CreateCommand();
                
                // 执行所有建表语句
                command.CommandText = createAppSettingsTable;
                command.ExecuteNonQuery();

                command.CommandText = createApiConfigTable;
                command.ExecuteNonQuery();

                command.CommandText = createTtsConfigTable;
                command.ExecuteNonQuery();

                command.CommandText = createConversationsTable;
                command.ExecuteNonQuery();

                command.CommandText = createMessagesTable;
                command.ExecuteNonQuery();

                command.CommandText = createTtsAudioTable;
                command.ExecuteNonQuery();

                command.CommandText = createIndexes;
                command.ExecuteNonQuery();

                // 执行数据库迁移
                MigrateDatabaseSchema(connection);

                // 初始化默认应用设置
                InitializeDefaultSettings(connection);

                Debug.WriteLine($"Database initialized at: {DatabasePath}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Database initialization failed: {ex.Message}");
                throw;
            }
        }

        private static void MigrateDatabaseSchema(SqliteConnection connection)
        {
            try
            {
                // 检查TtsConfigurations表是否有IsActive列和ChannelType列
                using var command = connection.CreateCommand();
                command.CommandText = "PRAGMA table_info(TtsConfigurations)";
                using var reader = command.ExecuteReader();
                
                bool hasIsActiveColumn = false;
                bool hasChannelTypeColumn = false;
                while (reader.Read())
                {
                    string columnName = reader.GetString(1);
                    if (columnName == "IsActive")
                    {
                        hasIsActiveColumn = true;
                    }
                    else if (columnName == "ChannelType")
                    {
                        hasChannelTypeColumn = true;
                    }
                }
                reader.Close();
                
                // 如果没有IsActive列，则添加它
                if (!hasIsActiveColumn)
                {
                    command.CommandText = "ALTER TABLE TtsConfigurations ADD COLUMN IsActive INTEGER NOT NULL DEFAULT 0";
                    command.ExecuteNonQuery();
                    Debug.WriteLine("Added IsActive column to TtsConfigurations table");
                }
                
                // 如果没有ChannelType列，则添加它
                if (!hasChannelTypeColumn)
                {
                    command.CommandText = "ALTER TABLE TtsConfigurations ADD COLUMN ChannelType INTEGER DEFAULT 0"; // 0对应TtsChannelType.OpenAI
                    command.ExecuteNonQuery();
                    Debug.WriteLine("Added ChannelType column to TtsConfigurations table");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Database migration failed: {ex.Message}");
                // 不抛出异常，允许应用程序继续运行
            }
        }

        private static void InitializeDefaultSettings(SqliteConnection connection)
        {
            // 检查是否已有应用设置
            using var checkCommand = connection.CreateCommand();
            checkCommand.CommandText = "SELECT COUNT(*) FROM AppSettings";
            var count = Convert.ToInt32(checkCommand.ExecuteScalar());
            
            Debug.WriteLine($"Found {count} app settings records in database");

            if (count == 0)
            {
                // 插入默认应用设置
                using var insertCommand = connection.CreateCommand();
                insertCommand.CommandText = @"
                    INSERT INTO AppSettings (IsTopmost, ShowInTaskbar, EnableAnimation, IsDarkTheme, UpdatedAt)
                    VALUES (1, 1, 1, 0, datetime('now'))";
                insertCommand.ExecuteNonQuery();
                Debug.WriteLine("Inserted default app settings into database");
            }
            else
            {
                Debug.WriteLine("App settings already exist, skipping default initialization");
            }
        }

        public static void BackupDatabase(string backupPath)
        {
            try
            {
                File.Copy(DatabasePath, backupPath, true);
                Debug.WriteLine($"Database backed up to: {backupPath}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Database backup failed: {ex.Message}");
                throw;
            }
        }

        public static void RestoreDatabase(string backupPath)
        {
            try
            {
                if (File.Exists(backupPath))
                {
                    File.Copy(backupPath, DatabasePath, true);
                    Debug.WriteLine($"Database restored from: {backupPath}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Database restore failed: {ex.Message}");
                throw;
            }
        }

        public static void CleanupTtsAudioCache()
        {
            try
            {
                using var connection = new SqliteConnection(ConnectionString);
                connection.Open();

                using var command = connection.CreateCommand();
                command.CommandText = "DELETE FROM TtsAudio";
                var rowsAffected = command.ExecuteNonQuery();
                
                Debug.WriteLine($"Cleaned up {rowsAffected} TTS audio cache entries");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"TTS audio cache cleanup failed: {ex.Message}");
            }
        }
    }
}