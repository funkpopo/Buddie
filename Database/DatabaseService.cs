using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Buddie.Database
{
    public class DatabaseService
    {
        #region App Settings

        public async Task<DbAppSettings?> GetAppSettingsAsync()
        {
            using var connection = new SqliteConnection(DatabaseManager.ConnectionString);
            await connection.OpenAsync();

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
                    UpdatedAt = DateTime.Parse(reader.GetString(5))
                };
            }

            return null;
        }

        public async Task SaveAppSettingsAsync(DbAppSettings settings)
        {
            settings.UpdatedAt = DateTime.UtcNow;

            using var connection = new SqliteConnection(DatabaseManager.ConnectionString);
            await connection.OpenAsync();

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

            using var connection = new SqliteConnection(DatabaseManager.ConnectionString);
            await connection.OpenAsync();

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
                    CreatedAt = DateTime.Parse(reader.GetString(9)),
                    UpdatedAt = DateTime.Parse(reader.GetString(10))
                });
            }

            return configurations;
        }

        public async Task<int> SaveApiConfigurationAsync(DbApiConfiguration config)
        {
            config.UpdatedAt = DateTime.UtcNow;

            using var connection = new SqliteConnection(DatabaseManager.ConnectionString);
            await connection.OpenAsync();

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
            using var connection = new SqliteConnection(DatabaseManager.ConnectionString);
            await connection.OpenAsync();

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

            using var connection = new SqliteConnection(DatabaseManager.ConnectionString);
            await connection.OpenAsync();

            using var command = connection.CreateCommand();
            command.CommandText = "SELECT * FROM TtsConfigurations ORDER BY CreatedAt";

            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                configurations.Add(new DbTtsConfiguration
                {
                    Id = reader.GetInt32(0),
                    Name = reader.GetString(1),
                    ApiUrl = reader.GetString(2),
                    ApiKey = reader.GetString(3),
                    Model = reader.GetString(4),
                    Voice = reader.GetString(5),
                    Speed = reader.GetDouble(6),
                    IsStreamingEnabled = reader.GetBoolean(7),
                    CreatedAt = DateTime.Parse(reader.GetString(8)),
                    UpdatedAt = DateTime.Parse(reader.GetString(9))
                });
            }

            return configurations;
        }

        public async Task<int> SaveTtsConfigurationAsync(DbTtsConfiguration config)
        {
            config.UpdatedAt = DateTime.UtcNow;

            using var connection = new SqliteConnection(DatabaseManager.ConnectionString);
            await connection.OpenAsync();

            using var command = connection.CreateCommand();
            if (config.Id == 0)
            {
                // Insert new configuration
                config.CreatedAt = DateTime.UtcNow;
                command.CommandText = @"
                    INSERT INTO TtsConfigurations (Name, ApiUrl, ApiKey, Model, Voice, Speed, IsStreamingEnabled, CreatedAt, UpdatedAt)
                    VALUES (@Name, @ApiUrl, @ApiKey, @Model, @Voice, @Speed, @IsStreamingEnabled, @CreatedAt, @UpdatedAt);
                    SELECT last_insert_rowid();";
            }
            else
            {
                // Update existing configuration
                command.CommandText = @"
                    UPDATE TtsConfigurations 
                    SET Name = @Name, ApiUrl = @ApiUrl, ApiKey = @ApiKey, Model = @Model,
                        Voice = @Voice, Speed = @Speed, IsStreamingEnabled = @IsStreamingEnabled, UpdatedAt = @UpdatedAt
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
            command.Parameters.AddWithValue("@CreatedAt", config.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss"));
            command.Parameters.AddWithValue("@UpdatedAt", config.UpdatedAt.ToString("yyyy-MM-dd HH:mm:ss"));

            var result = await command.ExecuteScalarAsync();
            var id = Convert.ToInt32(result);
            config.Id = id;
            return id;
        }

        public async Task DeleteTtsConfigurationAsync(int id)
        {
            using var connection = new SqliteConnection(DatabaseManager.ConnectionString);
            await connection.OpenAsync();

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

            using var connection = new SqliteConnection(DatabaseManager.ConnectionString);
            await connection.OpenAsync();

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
                    CreatedAt = DateTime.Parse(reader.GetString(2)),
                    UpdatedAt = DateTime.Parse(reader.GetString(3)),
                    MessageCount = reader.GetInt32(4)
                });
            }

            return conversations;
        }

        public async Task<int> SaveConversationAsync(DbConversation conversation)
        {
            conversation.UpdatedAt = DateTime.UtcNow;

            using var connection = new SqliteConnection(DatabaseManager.ConnectionString);
            await connection.OpenAsync();

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
            using var connection = new SqliteConnection(DatabaseManager.ConnectionString);
            await connection.OpenAsync();

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
        }

        #endregion

        #region Messages

        public async Task<List<DbMessage>> GetMessagesAsync(int conversationId)
        {
            var messages = new List<DbMessage>();

            using var connection = new SqliteConnection(DatabaseManager.ConnectionString);
            await connection.OpenAsync();

            using var command = connection.CreateCommand();
            command.CommandText = "SELECT * FROM Messages WHERE ConversationId = @ConversationId ORDER BY CreatedAt";
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
                    CreatedAt = DateTime.Parse(reader.GetString(5))
                });
            }

            return messages;
        }

        public async Task<int> SaveMessageAsync(DbMessage message)
        {
            message.CreatedAt = DateTime.UtcNow;

            using var connection = new SqliteConnection(DatabaseManager.ConnectionString);
            await connection.OpenAsync();

            using var transaction = connection.BeginTransaction();
            try
            {
                // Insert message
                using var insertCommand = connection.CreateCommand();
                insertCommand.Transaction = transaction;
                insertCommand.CommandText = @"
                    INSERT INTO Messages (ConversationId, Content, IsUser, ReasoningContent, CreatedAt)
                    VALUES (@ConversationId, @Content, @IsUser, @ReasoningContent, @CreatedAt);
                    SELECT last_insert_rowid();";

                insertCommand.Parameters.AddWithValue("@ConversationId", message.ConversationId);
                insertCommand.Parameters.AddWithValue("@Content", message.Content);
                insertCommand.Parameters.AddWithValue("@IsUser", message.IsUser);
                insertCommand.Parameters.AddWithValue("@ReasoningContent", (object?)message.ReasoningContent ?? DBNull.Value);
                insertCommand.Parameters.AddWithValue("@CreatedAt", message.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss"));

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
        }

        public async Task DeleteMessageAsync(int id)
        {
            using var connection = new SqliteConnection(DatabaseManager.ConnectionString);
            await connection.OpenAsync();

            using var command = connection.CreateCommand();
            command.CommandText = "DELETE FROM Messages WHERE Id = @Id";
            command.Parameters.AddWithValue("@Id", id);

            await command.ExecuteNonQueryAsync();
        }

        #endregion
    }
}