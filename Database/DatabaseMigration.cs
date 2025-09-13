using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Buddie.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Buddie.Database
{
    public class DatabaseMigration
    {
        private readonly ISqliteConnectionPool _connectionPool;
        private readonly ILogger _logger;

        public DatabaseMigration(ISqliteConnectionPool connectionPool)
        {
            _connectionPool = connectionPool;
            var loggerFactory = Buddie.App.Services?.GetService(typeof(ILoggerFactory)) as ILoggerFactory;
            _logger = (loggerFactory?.CreateLogger(typeof(DatabaseMigration).FullName!)) ?? NullLogger.Instance;
        }

        /// <summary>
        /// 迁移数据库中的未加密 API Key 到加密格式
        /// </summary>
        public async Task MigrateApiKeysToEncryptedFormatAsync()
        {
            try
            {
                _logger.LogInformation("Starting API key encryption migration...");

                using var connectionWrapper = await _connectionPool.GetConnectionAsync();
                var connection = connectionWrapper.Connection;

                // 迁移 ApiConfigurations 表中的 API Keys
                await MigrateApiConfigurationsAsync(connection);

                // 迁移 TtsConfigurations 表中的 API Keys
                await MigrateTtsConfigurationsAsync(connection);

                _logger.LogInformation("API key encryption migration completed successfully.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during API key migration: {Message}", ex.Message);
                throw;
            }
        }

        private async Task MigrateApiConfigurationsAsync(SqliteConnection connection)
        {
            // 获取所有配置
            using var selectCommand = connection.CreateCommand();
            selectCommand.CommandText = "SELECT Id, ApiKey FROM ApiConfigurations";

            var updates = new List<(int id, string encryptedKey)>();

            using (var reader = await selectCommand.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    var id = reader.GetInt32(0);
                    var apiKey = reader.GetString(1);

                    // 检查是否已加密
                    if (!ApiKeyProtection.IsProtected(apiKey))
                    {
                        // 加密未加密的 key
                        var encryptedKey = ApiKeyProtection.Protect(apiKey);
                        updates.Add((id, encryptedKey));
                        _logger.LogDebug("Encrypting API key for ApiConfiguration ID: {Id}", id);
                    }
                }
            }

            // 批量更新
            foreach (var (id, encryptedKey) in updates)
            {
                using var updateCommand = connection.CreateCommand();
                updateCommand.CommandText = "UPDATE ApiConfigurations SET ApiKey = @ApiKey WHERE Id = @Id";
                updateCommand.Parameters.AddWithValue("@ApiKey", encryptedKey);
                updateCommand.Parameters.AddWithValue("@Id", id);
                await updateCommand.ExecuteNonQueryAsync();
            }

            if (updates.Count > 0)
            {
                _logger.LogInformation("Migrated {Count} API keys in ApiConfigurations table.", updates.Count);
            }
        }

        private async Task MigrateTtsConfigurationsAsync(SqliteConnection connection)
        {
            // 获取所有配置
            using var selectCommand = connection.CreateCommand();
            selectCommand.CommandText = "SELECT Id, ApiKey FROM TtsConfigurations";

            var updates = new List<(int id, string encryptedKey)>();

            using (var reader = await selectCommand.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    var id = reader.GetInt32(0);
                    var apiKey = reader.GetString(1);

                    // 检查是否已加密
                    if (!ApiKeyProtection.IsProtected(apiKey))
                    {
                        // 加密未加密的 key
                        var encryptedKey = ApiKeyProtection.Protect(apiKey);
                        updates.Add((id, encryptedKey));
                        _logger.LogDebug("Encrypting API key for TtsConfiguration ID: {Id}", id);
                    }
                }
            }

            // 批量更新
            foreach (var (id, encryptedKey) in updates)
            {
                using var updateCommand = connection.CreateCommand();
                updateCommand.CommandText = "UPDATE TtsConfigurations SET ApiKey = @ApiKey WHERE Id = @Id";
                updateCommand.Parameters.AddWithValue("@ApiKey", encryptedKey);
                updateCommand.Parameters.AddWithValue("@Id", id);
                await updateCommand.ExecuteNonQueryAsync();
            }

            if (updates.Count > 0)
            {
                _logger.LogInformation("Migrated {Count} API keys in TtsConfigurations table.", updates.Count);
            }
        }
    }
}
