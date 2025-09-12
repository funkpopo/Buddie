using Microsoft.Data.Sqlite;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Buddie.Services.ExceptionHandling;

namespace Buddie.Database
{
    public class DatabaseService : IDisposable
    {
        private readonly ISqliteConnectionPool _connectionPool;
        private bool _disposed;

        public DatabaseService(ISqliteConnectionPool connectionPool)
        {
            _connectionPool = connectionPool;
        }
        private static DateTime ParseDateTime(string dateTimeStr)
        {
            if (string.IsNullOrEmpty(dateTimeStr) || dateTimeStr == "0")
            {
                return DateTime.UtcNow;
            }

            if (DateTime.TryParse(dateTimeStr, out DateTime result))
            {
                return result;
            }

            return DateTime.UtcNow;
        }

        #region App Settings

        public async Task<DbAppSettings?> GetAppSettingsAsync()
        {
            using var connectionWrapper = await _connectionPool.GetConnectionAsync();
            var connection = connectionWrapper.Connection;

            using var command = connection.CreateCommand();
            command.CommandText = "SELECT * FROM AppSettings LIMIT 1";

            using var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return new DbAppSettings
                {
                    Id = reader.GetInt32(0),
                    IsTopmost = reader.GetBoolean(1),
                    ShowInTaskbar = reader.GetBoolean(2),
                    EnableAnimation = reader.GetBoolean(3),
                    IsDarkTheme = reader.GetBoolean(4),
                    UpdatedAt = ParseDateTime(reader.GetString(5))
                };
            }

            return null;
        }

        public async Task SaveAppSettingsAsync(DbAppSettings settings)
        {
            settings.UpdatedAt = DateTime.UtcNow;

            using var connectionWrapper = await _connectionPool.GetConnectionAsync();
            var connection = connectionWrapper.Connection;

            using var command = connection.CreateCommand();
            if (settings.Id == 0)
            {
                // Insert new settings
                command.CommandText = @"
                    INSERT INTO AppSettings (IsTopmost, ShowInTaskbar, EnableAnimation, IsDarkTheme, UpdatedAt)
                    VALUES (@IsTopmost, @ShowInTaskbar, @EnableAnimation, @IsDarkTheme, @UpdatedAt)";
            }
            else
            {
                // Update existing settings
                command.CommandText = @"
                    UPDATE AppSettings 
                    SET IsTopmost = @IsTopmost, ShowInTaskbar = @ShowInTaskbar, 
                        EnableAnimation = @EnableAnimation, IsDarkTheme = @IsDarkTheme, 
                        UpdatedAt = @UpdatedAt 
                    WHERE Id = @Id";
                command.Parameters.AddWithValue("@Id", settings.Id);
            }

            command.Parameters.AddWithValue("@IsTopmost", settings.IsTopmost);
            command.Parameters.AddWithValue("@ShowInTaskbar", settings.ShowInTaskbar);
            command.Parameters.AddWithValue("@EnableAnimation", settings.EnableAnimation);
            command.Parameters.AddWithValue("@IsDarkTheme", settings.IsDarkTheme);
            command.Parameters.AddWithValue("@UpdatedAt", settings.UpdatedAt.ToString("yyyy-MM-dd HH:mm:ss"));

            await command.ExecuteNonQueryAsync();
        }

        #endregion

        #region API Configurations

        public async Task<List<DbApiConfiguration>> GetApiConfigurationsAsync()
        {
            var configurations = new List<DbApiConfiguration>();

            using var connectionWrapper = await _connectionPool.GetConnectionAsync();
            var connection = connectionWrapper.Connection;

            using var command = connection.CreateCommand();
            command.CommandText = "SELECT * FROM ApiConfigurations ORDER BY CreatedAt";

            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                configurations.Add(new DbApiConfiguration
                {
                    Id = reader.GetInt32(0),
                    Name = reader.GetString(1),
                    ApiUrl = reader.GetString(2),
                    ApiKey = reader.GetString(3),
                    ModelName = reader.GetString(4),
                    IsStreamingEnabled = reader.GetBoolean(5),
                    IsMultimodalEnabled = reader.GetBoolean(6),
                    ChannelType = reader.GetInt32(7),
                    SupportsThinking = reader.GetBoolean(8),
                    CreatedAt = ParseDateTime(reader.GetString(9)),
                    UpdatedAt = ParseDateTime(reader.GetString(10))
                });
            }

            return configurations;
        }

        public async Task<int> SaveApiConfigurationAsync(DbApiConfiguration config)
        {
            config.UpdatedAt = DateTime.UtcNow;

            using var connectionWrapper = await _connectionPool.GetConnectionAsync();
            var connection = connectionWrapper.Connection;

            using var command = connection.CreateCommand();
            if (config.Id == 0)
            {
                // Insert new configuration
                config.CreatedAt = DateTime.UtcNow;
                command.CommandText = @"
                    INSERT INTO ApiConfigurations (Name, ApiUrl, ApiKey, ModelName, IsStreamingEnabled, 
                                                 IsMultimodalEnabled, ChannelType, SupportsThinking, CreatedAt, UpdatedAt)
                    VALUES (@Name, @ApiUrl, @ApiKey, @ModelName, @IsStreamingEnabled, 
                           @IsMultimodalEnabled, @ChannelType, @SupportsThinking, @CreatedAt, @UpdatedAt);
                    SELECT last_insert_rowid();";
            }
            else
            {
                // Update existing configuration
                command.CommandText = @"
                    UPDATE ApiConfigurations 
                    SET Name = @Name, ApiUrl = @ApiUrl, ApiKey = @ApiKey, ModelName = @ModelName,
                        IsStreamingEnabled = @IsStreamingEnabled, IsMultimodalEnabled = @IsMultimodalEnabled,
                        ChannelType = @ChannelType, SupportsThinking = @SupportsThinking, UpdatedAt = @UpdatedAt
                    WHERE Id = @Id;
                    SELECT @Id;";
                command.Parameters.AddWithValue("@Id", config.Id);
            }

            command.Parameters.AddWithValue("@Name", config.Name);
            command.Parameters.AddWithValue("@ApiUrl", config.ApiUrl);
            command.Parameters.AddWithValue("@ApiKey", config.ApiKey);
            command.Parameters.AddWithValue("@ModelName", config.ModelName);
            command.Parameters.AddWithValue("@IsStreamingEnabled", config.IsStreamingEnabled);
            command.Parameters.AddWithValue("@IsMultimodalEnabled", config.IsMultimodalEnabled);
            command.Parameters.AddWithValue("@ChannelType", config.ChannelType);
            command.Parameters.AddWithValue("@SupportsThinking", config.SupportsThinking);
            command.Parameters.AddWithValue("@CreatedAt", config.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss"));
            command.Parameters.AddWithValue("@UpdatedAt", config.UpdatedAt.ToString("yyyy-MM-dd HH:mm:ss"));

            var result = await command.ExecuteScalarAsync();
            var id = Convert.ToInt32(result);
            config.Id = id;
            return id;
        }

        public async Task DeleteApiConfigurationAsync(int id)
        {
            using var connectionWrapper = await _connectionPool.GetConnectionAsync();
            var connection = connectionWrapper.Connection;

            using var command = connection.CreateCommand();
            command.CommandText = "DELETE FROM ApiConfigurations WHERE Id = @Id";
            command.Parameters.AddWithValue("@Id", id);

            await command.ExecuteNonQueryAsync();
        }

        #endregion

        #region TTS Configurations

        public async Task<List<DbTtsConfiguration>> GetTtsConfigurationsAsync()
        {
            var configurations = new List<DbTtsConfiguration>();

            using var connectionWrapper = await _connectionPool.GetConnectionAsync();
            var connection = connectionWrapper.Connection;

            using var command = connection.CreateCommand();
            command.CommandText = "SELECT * FROM TtsConfigurations ORDER BY CreatedAt";

            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var createdAtStr = reader.GetString(8); // Fixed: CreatedAt is at position 8, not 9
                var updatedAtStr = reader.GetString(9); // Fixed: UpdatedAt is at position 9, not 10
                
                // 处理可能的无效日期格式
                DateTime createdAt = DateTime.UtcNow;
                DateTime updatedAt = DateTime.UtcNow;
                
                if (!string.IsNullOrEmpty(createdAtStr) && createdAtStr != "0")
                {
                    if (!DateTime.TryParse(createdAtStr, out createdAt))
                    {
                        createdAt = DateTime.UtcNow;
                    }
                }
                
                if (!string.IsNullOrEmpty(updatedAtStr) && updatedAtStr != "0")
                {
                    if (!DateTime.TryParse(updatedAtStr, out updatedAt))
                    {
                        updatedAt = DateTime.UtcNow;
                    }
                }
                
                var config = new DbTtsConfiguration
                {
                    Id = reader.GetInt32(0),
                    Name = reader.GetString(1),
                    ApiUrl = reader.GetString(2),
                    ApiKey = reader.GetString(3),
                    Model = reader.GetString(4),
                    Voice = reader.GetString(5),
                    Speed = reader.GetDouble(6),
                    IsStreamingEnabled = reader.GetBoolean(7),
                    IsActive = reader.GetBoolean(10), // Fixed: IsActive is at position 10, not 8
                    ChannelType = reader.IsDBNull(11) ? null : reader.GetInt32(11), // Also read ChannelType at position 11
                    CreatedAt = createdAt,
                    UpdatedAt = updatedAt
                };
                configurations.Add(config);
            }

            return configurations;
        }

        public async Task<int> SaveTtsConfigurationAsync(DbTtsConfiguration config)
        {
            config.UpdatedAt = DateTime.UtcNow;

            using var connectionWrapper = await _connectionPool.GetConnectionAsync();
            var connection = connectionWrapper.Connection;

            using var command = connection.CreateCommand();
            if (config.Id == 0)
            {
                // Insert new configuration
                config.CreatedAt = DateTime.UtcNow;
                command.CommandText = @"
                    INSERT INTO TtsConfigurations (Name, ApiUrl, ApiKey, Model, Voice, Speed, IsStreamingEnabled, IsActive, ChannelType, CreatedAt, UpdatedAt)
                    VALUES (@Name, @ApiUrl, @ApiKey, @Model, @Voice, @Speed, @IsStreamingEnabled, @IsActive, @ChannelType, @CreatedAt, @UpdatedAt);
                    SELECT last_insert_rowid();";
            }
            else
            {
                // Update existing configuration
                command.CommandText = @"
                    UPDATE TtsConfigurations 
                    SET Name = @Name, ApiUrl = @ApiUrl, ApiKey = @ApiKey, Model = @Model,
                        Voice = @Voice, Speed = @Speed, IsStreamingEnabled = @IsStreamingEnabled, IsActive = @IsActive, ChannelType = @ChannelType, UpdatedAt = @UpdatedAt
                    WHERE Id = @Id;
                    SELECT @Id;";
                command.Parameters.AddWithValue("@Id", config.Id);
            }

            command.Parameters.AddWithValue("@Name", config.Name);
            command.Parameters.AddWithValue("@ApiUrl", config.ApiUrl);
            command.Parameters.AddWithValue("@ApiKey", config.ApiKey);
            command.Parameters.AddWithValue("@Model", config.Model);
            command.Parameters.AddWithValue("@Voice", config.Voice);
            command.Parameters.AddWithValue("@Speed", config.Speed);
            command.Parameters.AddWithValue("@IsStreamingEnabled", config.IsStreamingEnabled);
            command.Parameters.AddWithValue("@IsActive", config.IsActive);
            command.Parameters.AddWithValue("@ChannelType", config.ChannelType ?? 0); // Default to OpenAI if null
            command.Parameters.AddWithValue("@CreatedAt", config.CreatedAt == default ? DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss") : config.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss"));
            command.Parameters.AddWithValue("@UpdatedAt", config.UpdatedAt == default ? DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss") : config.UpdatedAt.ToString("yyyy-MM-dd HH:mm:ss"));

            var result = await command.ExecuteScalarAsync();
            var id = Convert.ToInt32(result);
            config.Id = id;
            return id;
        }

        public async Task DeleteTtsConfigurationAsync(int id)
        {
            using var connectionWrapper = await _connectionPool.GetConnectionAsync();
            var connection = connectionWrapper.Connection;

            using var command = connection.CreateCommand();
            command.CommandText = "DELETE FROM TtsConfigurations WHERE Id = @Id";
            command.Parameters.AddWithValue("@Id", id);

            await command.ExecuteNonQueryAsync();
        }

        #endregion

        #region Conversations

        public async Task<List<DbConversation>> GetConversationsAsync()
        {
            var conversations = new List<DbConversation>();

            using var connectionWrapper = await _connectionPool.GetConnectionAsync();
            var connection = connectionWrapper.Connection;

            using var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT c.*, COUNT(m.Id) as MessageCount 
                FROM Conversations c 
                LEFT JOIN Messages m ON c.Id = m.ConversationId 
                GROUP BY c.Id 
                ORDER BY c.UpdatedAt DESC";

            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                conversations.Add(new DbConversation
                {
                    Id = reader.GetInt32(0),
                    Title = reader.GetString(1),
                    CreatedAt = ParseDateTime(reader.GetString(2)),
                    UpdatedAt = ParseDateTime(reader.GetString(3)),
                    MessageCount = reader.GetInt32(4)
                });
            }

            return conversations;
        }

        public async Task<int> SaveConversationAsync(DbConversation conversation)
        {
            conversation.UpdatedAt = DateTime.UtcNow;

            using var connectionWrapper = await _connectionPool.GetConnectionAsync();
            var connection = connectionWrapper.Connection;

            using var command = connection.CreateCommand();
            if (conversation.Id == 0)
            {
                // Insert new conversation
                conversation.CreatedAt = DateTime.UtcNow;
                command.CommandText = @"
                    INSERT INTO Conversations (Title, CreatedAt, UpdatedAt)
                    VALUES (@Title, @CreatedAt, @UpdatedAt);
                    SELECT last_insert_rowid();";
            }
            else
            {
                // Update existing conversation
                command.CommandText = @"
                    UPDATE Conversations 
                    SET Title = @Title, UpdatedAt = @UpdatedAt
                    WHERE Id = @Id;
                    SELECT @Id;";
                command.Parameters.AddWithValue("@Id", conversation.Id);
            }

            command.Parameters.AddWithValue("@Title", conversation.Title);
            command.Parameters.AddWithValue("@CreatedAt", conversation.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss"));
            command.Parameters.AddWithValue("@UpdatedAt", conversation.UpdatedAt.ToString("yyyy-MM-dd HH:mm:ss"));

            var result = await command.ExecuteScalarAsync();
            var id = Convert.ToInt32(result);
            conversation.Id = id;
            return id;
        }

        public async Task DeleteConversationAsync(int id)
        {
            await ExceptionHandlingService.Database.ExecuteSafelyAsync(async () =>
            {
                using var connectionWrapper = await _connectionPool.GetConnectionAsync();
                var connection = connectionWrapper.Connection;

                using var transaction = connection.BeginTransaction();
                
                try
                {
                    // Delete messages first (due to foreign key constraint)
                    using var deleteMessagesCommand = connection.CreateCommand();
                    deleteMessagesCommand.Transaction = transaction;
                    deleteMessagesCommand.CommandText = "DELETE FROM Messages WHERE ConversationId = @Id";
                    deleteMessagesCommand.Parameters.AddWithValue("@Id", id);
                    await deleteMessagesCommand.ExecuteNonQueryAsync();

                    // Delete conversation
                    using var deleteConversationCommand = connection.CreateCommand();
                    deleteConversationCommand.Transaction = transaction;
                    deleteConversationCommand.CommandText = "DELETE FROM Conversations WHERE Id = @Id";
                    deleteConversationCommand.Parameters.AddWithValue("@Id", id);
                    await deleteConversationCommand.ExecuteNonQueryAsync();

                    transaction.Commit();
                }
                catch
                {
                    transaction.Rollback();
                    throw;
                }
            }, "删除对话");
        }

        #endregion

        #region Messages

        public async Task<List<DbMessage>> GetMessagesAsync(int conversationId)
        {
            var messages = new List<DbMessage>();

            using var connectionWrapper = await _connectionPool.GetConnectionAsync();
            var connection = connectionWrapper.Connection;

            using var command = connection.CreateCommand();
            command.CommandText = "SELECT Id, ConversationId, Content, IsUser, ReasoningContent, CreatedAt, ImageData, ImageContentType FROM Messages WHERE ConversationId = @ConversationId ORDER BY CreatedAt";
            command.Parameters.AddWithValue("@ConversationId", conversationId);

            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                messages.Add(new DbMessage
                {
                    Id = reader.GetInt32(0),
                    ConversationId = reader.GetInt32(1),
                    Content = reader.GetString(2),
                    IsUser = reader.GetBoolean(3),
                    ReasoningContent = reader.IsDBNull(4) ? null : reader.GetString(4),
                    CreatedAt = ParseDateTime(reader.GetString(5)),
                    ImageData = reader.IsDBNull(6) ? null : (byte[])reader["ImageData"],
                    ImageContentType = reader.IsDBNull(7) ? null : reader.GetString(7)
                });
            }

            return messages;
        }

        public async Task<int> SaveMessageAsync(DbMessage message)
        {
            return await ExceptionHandlingService.Database.ExecuteSafelyAsync(async () =>
            {
                message.CreatedAt = DateTime.UtcNow;

                using var connectionWrapper = await _connectionPool.GetConnectionAsync();
                var connection = connectionWrapper.Connection;

                using var transaction = connection.BeginTransaction();
                try
                {
                    // Insert message
                    using var insertCommand = connection.CreateCommand();
                    insertCommand.Transaction = transaction;
                    insertCommand.CommandText = @"
                        INSERT INTO Messages (ConversationId, Content, IsUser, ReasoningContent, CreatedAt, ImageData, ImageContentType)
                        VALUES (@ConversationId, @Content, @IsUser, @ReasoningContent, @CreatedAt, @ImageData, @ImageContentType);
                        SELECT last_insert_rowid();";

                    insertCommand.Parameters.AddWithValue("@ConversationId", message.ConversationId);
                    insertCommand.Parameters.AddWithValue("@Content", message.Content);
                    insertCommand.Parameters.AddWithValue("@IsUser", message.IsUser);
                    insertCommand.Parameters.AddWithValue("@ReasoningContent", (object?)message.ReasoningContent ?? DBNull.Value);
                    insertCommand.Parameters.AddWithValue("@CreatedAt", message.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss"));
                    insertCommand.Parameters.AddWithValue("@ImageData", (object?)message.ImageData ?? DBNull.Value);
                    insertCommand.Parameters.AddWithValue("@ImageContentType", (object?)message.ImageContentType ?? DBNull.Value);

                    var result = await insertCommand.ExecuteScalarAsync();
                    var messageId = Convert.ToInt32(result);
                    message.Id = messageId;

                    // Update conversation UpdatedAt timestamp
                    using var updateCommand = connection.CreateCommand();
                    updateCommand.Transaction = transaction;
                    updateCommand.CommandText = @"
                        UPDATE Conversations 
                        SET UpdatedAt = @UpdatedAt 
                        WHERE Id = @ConversationId";
                    updateCommand.Parameters.AddWithValue("@UpdatedAt", message.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss"));
                    updateCommand.Parameters.AddWithValue("@ConversationId", message.ConversationId);
                    await updateCommand.ExecuteNonQueryAsync();

                    transaction.Commit();
                    return messageId;
                }
                catch
                {
                    transaction.Rollback();
                    throw;
                }
            }, 0, "保存消息");
        }

        public async Task DeleteMessageAsync(int id)
        {
            using var connectionWrapper = await _connectionPool.GetConnectionAsync();
            var connection = connectionWrapper.Connection;

            using var command = connection.CreateCommand();
            command.CommandText = "DELETE FROM Messages WHERE Id = @Id";
            command.Parameters.AddWithValue("@Id", id);

            await command.ExecuteNonQueryAsync();
        }

        #endregion

        #region TTS Audio Cache

        public async Task<DbTtsAudio?> GetTtsAudioAsync(string textHash)
        {
            using var connectionWrapper = await _connectionPool.GetConnectionAsync();
            var connection = connectionWrapper.Connection;

            using var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT * FROM TtsAudio 
                WHERE TextHash = @TextHash;
                
                UPDATE TtsAudio 
                SET LastAccessedAt = datetime('now') 
                WHERE TextHash = @TextHash;";
            command.Parameters.AddWithValue("@TextHash", textHash);

            using var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return new DbTtsAudio
                {
                    Id = reader.GetInt32(0),
                    TextHash = reader.GetString(1),
                    AudioData = (byte[])reader["AudioData"],
                    TtsConfigJson = reader.GetString(3),
                    CreatedAt = ParseDateTime(reader.GetString(4)),
                    LastAccessedAt = ParseDateTime(reader.GetString(5))
                };
            }

            return null;
        }

        public async Task SaveTtsAudioAsync(string textHash, byte[] audioData, string ttsConfigJson)
        {
            using var connectionWrapper = await _connectionPool.GetConnectionAsync();
            var connection = connectionWrapper.Connection;

            using var command = connection.CreateCommand();
            command.CommandText = @"
                INSERT OR REPLACE INTO TtsAudio (TextHash, AudioData, TtsConfigJson, CreatedAt, LastAccessedAt)
                VALUES (@TextHash, @AudioData, @TtsConfigJson, datetime('now'), datetime('now'))";
            
            command.Parameters.AddWithValue("@TextHash", textHash);
            command.Parameters.AddWithValue("@AudioData", audioData);
            command.Parameters.AddWithValue("@TtsConfigJson", ttsConfigJson);

            await command.ExecuteNonQueryAsync();
        }

        public async Task CleanupOldTtsAudioAsync(int daysToKeep = 7)
        {
            using var connectionWrapper = await _connectionPool.GetConnectionAsync();
            var connection = connectionWrapper.Connection;

            using var command = connection.CreateCommand();
            command.CommandText = @"
                DELETE FROM TtsAudio 
                WHERE LastAccessedAt < datetime('now', '-' || @DaysToKeep || ' days')";
            command.Parameters.AddWithValue("@DaysToKeep", daysToKeep);

            var rowsAffected = await command.ExecuteNonQueryAsync();
            System.Diagnostics.Debug.WriteLine($"Cleaned up {rowsAffected} old TTS audio entries");
        }

        /// <summary>
        /// 获取TTS缓存统计信息
        /// </summary>
        public async Task<(int count, long totalSizeBytes)> GetTtsCacheStatsAsync()
        {
            using var connectionWrapper = await _connectionPool.GetConnectionAsync();
            var connection = connectionWrapper.Connection;

            using var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT 
                    COUNT(*) as Count,
                    COALESCE(SUM(LENGTH(AudioData)), 0) as TotalSize
                FROM TtsAudio";

            using var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return (reader.GetInt32(0), reader.GetInt64(1));
            }

            return (0, 0);
        }

        /// <summary>
        /// 基于LRU策略清理TTS缓存，保留最近访问的条目
        /// </summary>
        public async Task CleanupTtsCacheByLruAsync(int maxCount, long maxSizeBytes)
        {
            using var connectionWrapper = await _connectionPool.GetConnectionAsync();
            var connection = connectionWrapper.Connection;

            // 检查当前缓存状态
            var (currentCount, currentSize) = await GetTtsCacheStatsAsync();
            System.Diagnostics.Debug.WriteLine($"TTS Cache - Current: {currentCount} items, {currentSize / (1024 * 1024.0):F2} MB");

            if (currentCount <= maxCount && currentSize <= maxSizeBytes)
            {
                System.Diagnostics.Debug.WriteLine("TTS Cache - No cleanup needed");
                return;
            }

            // 计算需要删除的条目数
            int itemsToDelete = 0;
            if (currentCount > maxCount)
            {
                itemsToDelete = Math.Max(itemsToDelete, currentCount - maxCount);
            }

            // 如果超过大小限制，删除更多条目
            if (currentSize > maxSizeBytes)
            {
                // 保守估计，删除25%额外的条目以留出缓冲空间
                var estimatedItemsNeededForSize = (int)((currentSize - maxSizeBytes) * currentCount / (double)currentSize * 1.25);
                itemsToDelete = Math.Max(itemsToDelete, estimatedItemsNeededForSize);
            }

            if (itemsToDelete <= 0) return;

            // 执行LRU清理：删除最久未访问的条目
            using var deleteCommand = connection.CreateCommand();
            deleteCommand.CommandText = @"
                DELETE FROM TtsAudio 
                WHERE Id IN (
                    SELECT Id FROM TtsAudio 
                    ORDER BY LastAccessedAt ASC 
                    LIMIT @ItemsToDelete
                )";
            deleteCommand.Parameters.AddWithValue("@ItemsToDelete", itemsToDelete);

            var deletedCount = await deleteCommand.ExecuteNonQueryAsync();
            
            // 获取清理后的统计信息
            var (newCount, newSize) = await GetTtsCacheStatsAsync();
            System.Diagnostics.Debug.WriteLine($"TTS Cache - Deleted {deletedCount} items, New stats: {newCount} items, {newSize / (1024 * 1024.0):F2} MB");
        }

        /// <summary>
        /// 综合清理TTS缓存：同时应用时间、数量和大小限制
        /// </summary>
        public async Task CleanupTtsCacheAsync(int daysToKeep = 7, int maxCount = 1000, long maxSizeMB = 500)
        {
            await ExceptionHandlingService.Database.ExecuteSafelyAsync(async () =>
            {
                // 首先清理过期条目
                await CleanupOldTtsAudioAsync(daysToKeep);

                // 然后应用LRU策略清理数量和大小
                var maxSizeBytes = maxSizeMB * 1024 * 1024;
                await CleanupTtsCacheByLruAsync(maxCount, maxSizeBytes);

                System.Diagnostics.Debug.WriteLine("TTS Cache cleanup completed successfully");
            }, "TTS缓存清理");
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    // Note: We don't dispose the connection pool here because it's a singleton
                    // The connection pool will be disposed when the application shuts down
                }
                _disposed = true;
            }
        }

        #endregion
    }
}
