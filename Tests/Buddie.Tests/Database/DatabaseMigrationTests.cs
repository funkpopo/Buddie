using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Moq;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Buddie.Database;
using Buddie.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Buddie.Tests.Database
{
    public class DatabaseMigrationTests : IDisposable
    {
        private readonly Mock<ISqliteConnectionPool> _connectionPoolMock;
        private readonly DatabaseMigration _migration;
        private readonly SqliteConnection _connection;
        private readonly string _connectionString = "Data Source=:memory:";
        private readonly IServiceProvider _serviceProvider;

        public DatabaseMigrationTests()
        {
            // Setup service provider for this test FIRST, before any other initialization
            var serviceCollection = new ServiceCollection();
            serviceCollection.AddLogging();
            _serviceProvider = serviceCollection.BuildServiceProvider();

            // Set App.Services for this test BEFORE creating DatabaseMigration
            Buddie.App.Services = _serviceProvider;

            // Now create the mocks and migration instance
            _connectionPoolMock = new Mock<ISqliteConnectionPool>();
            _migration = new DatabaseMigration(_connectionPoolMock.Object);
            _connection = new SqliteConnection(_connectionString);
            _connection.Open();
            InitializeTestDatabase();
        }

        private void InitializeTestDatabase()
        {
            using var command = _connection.CreateCommand();
            command.CommandText = @"
                CREATE TABLE ApiConfigurations (
                    Id INTEGER PRIMARY KEY,
                    ApiKey TEXT
                );

                CREATE TABLE TtsConfigurations (
                    Id INTEGER PRIMARY KEY,
                    ApiKey TEXT
                );";
            command.ExecuteNonQuery();
        }

        [Fact]
        public async Task MigrateApiKeysToEncryptedFormatAsync_ShouldEncryptUnencryptedKeys_InApiConfigurations()
        {
            // Arrange
            var unencryptedKey = "test-api-key-12345";
            InsertApiConfiguration(1, unencryptedKey);

            var connectionWrapper = new Mock<IDisposableConnection>();
            connectionWrapper.SetupGet(c => c.Connection).Returns(_connection);
            _connectionPoolMock.Setup(p => p.GetConnectionAsync(default(CancellationToken))).ReturnsAsync(connectionWrapper.Object);

            // Act
            await _migration.MigrateApiKeysToEncryptedFormatAsync();

            // Assert
            var storedKey = GetApiKeyFromApiConfigurations(1);
            storedKey.Should().NotBe(unencryptedKey);
            ApiKeyProtection.IsProtected(storedKey).Should().BeTrue();
            ApiKeyProtection.Unprotect(storedKey).Should().Be(unencryptedKey);
        }

        [Fact]
        public async Task MigrateApiKeysToEncryptedFormatAsync_ShouldNotReencryptAlreadyEncryptedKeys_InApiConfigurations()
        {
            // Arrange
            var originalKey = "original-key-xyz";
            var encryptedKey = ApiKeyProtection.Protect(originalKey);
            InsertApiConfiguration(1, encryptedKey);

            var connectionWrapper = new Mock<IDisposableConnection>();
            connectionWrapper.SetupGet(c => c.Connection).Returns(_connection);
            _connectionPoolMock.Setup(p => p.GetConnectionAsync(default(CancellationToken))).ReturnsAsync(connectionWrapper.Object);

            // Act
            await _migration.MigrateApiKeysToEncryptedFormatAsync();

            // Assert
            var storedKey = GetApiKeyFromApiConfigurations(1);
            storedKey.Should().Be(encryptedKey);
        }

        [Fact]
        public async Task MigrateApiKeysToEncryptedFormatAsync_ShouldEncryptUnencryptedKeys_InTtsConfigurations()
        {
            // Arrange
            var unencryptedKey = "tts-api-key-98765";
            InsertTtsConfiguration(1, unencryptedKey);

            var connectionWrapper = new Mock<IDisposableConnection>();
            connectionWrapper.SetupGet(c => c.Connection).Returns(_connection);
            _connectionPoolMock.Setup(p => p.GetConnectionAsync(default(CancellationToken))).ReturnsAsync(connectionWrapper.Object);

            // Act
            await _migration.MigrateApiKeysToEncryptedFormatAsync();

            // Assert
            var storedKey = GetApiKeyFromTtsConfigurations(1);
            storedKey.Should().NotBe(unencryptedKey);
            ApiKeyProtection.IsProtected(storedKey).Should().BeTrue();
            ApiKeyProtection.Unprotect(storedKey).Should().Be(unencryptedKey);
        }

        [Fact]
        public async Task MigrateApiKeysToEncryptedFormatAsync_ShouldHandleMultipleKeys()
        {
            // Arrange
            var keys = new[] { "key1", "key2", "key3" };
            for (int i = 0; i < keys.Length; i++)
            {
                InsertApiConfiguration(i + 1, keys[i]);
                InsertTtsConfiguration(i + 1, $"tts-{keys[i]}");
            }

            var connectionWrapper = new Mock<IDisposableConnection>();
            connectionWrapper.SetupGet(c => c.Connection).Returns(_connection);
            _connectionPoolMock.Setup(p => p.GetConnectionAsync(default(CancellationToken))).ReturnsAsync(connectionWrapper.Object);

            // Act
            await _migration.MigrateApiKeysToEncryptedFormatAsync();

            // Assert
            for (int i = 0; i < keys.Length; i++)
            {
                var apiKey = GetApiKeyFromApiConfigurations(i + 1);
                ApiKeyProtection.IsProtected(apiKey).Should().BeTrue();
                ApiKeyProtection.Unprotect(apiKey).Should().Be(keys[i]);

                var ttsKey = GetApiKeyFromTtsConfigurations(i + 1);
                ApiKeyProtection.IsProtected(ttsKey).Should().BeTrue();
                ApiKeyProtection.Unprotect(ttsKey).Should().Be($"tts-{keys[i]}");
            }
        }

        [Fact]
        public async Task MigrateApiKeysToEncryptedFormatAsync_ShouldHandleEmptyTables()
        {
            // Arrange
            var connectionWrapper = new Mock<IDisposableConnection>();
            connectionWrapper.SetupGet(c => c.Connection).Returns(_connection);
            _connectionPoolMock.Setup(p => p.GetConnectionAsync(default(CancellationToken))).ReturnsAsync(connectionWrapper.Object);

            // Act & Assert
            await _migration.Invoking(m => m.MigrateApiKeysToEncryptedFormatAsync())
                .Should().NotThrowAsync();
        }

        [Fact]
        public async Task MigrateApiKeysToEncryptedFormatAsync_ShouldHandleMixedEncryptedAndUnencryptedKeys()
        {
            // Arrange
            var unencryptedKey = "unencrypted-key";
            var encryptedKey = ApiKeyProtection.Protect("already-encrypted");

            InsertApiConfiguration(1, unencryptedKey);
            InsertApiConfiguration(2, encryptedKey);

            var connectionWrapper = new Mock<IDisposableConnection>();
            connectionWrapper.SetupGet(c => c.Connection).Returns(_connection);
            _connectionPoolMock.Setup(p => p.GetConnectionAsync(default(CancellationToken))).ReturnsAsync(connectionWrapper.Object);

            // Act
            await _migration.MigrateApiKeysToEncryptedFormatAsync();

            // Assert
            var key1 = GetApiKeyFromApiConfigurations(1);
            ApiKeyProtection.IsProtected(key1).Should().BeTrue();
            ApiKeyProtection.Unprotect(key1).Should().Be(unencryptedKey);

            var key2 = GetApiKeyFromApiConfigurations(2);
            key2.Should().Be(encryptedKey);
        }

        [Fact]
        public async Task MigrateApiKeysToEncryptedFormatAsync_ShouldHandleDatabaseExceptions()
        {
            // Arrange
            var connectionWrapper = new Mock<IDisposableConnection>();
            connectionWrapper.SetupGet(c => c.Connection).Throws(new SqliteException("Database error", 1));
            _connectionPoolMock.Setup(p => p.GetConnectionAsync(default(CancellationToken))).ReturnsAsync(connectionWrapper.Object);

            // Act & Assert
            await _migration.Invoking(m => m.MigrateApiKeysToEncryptedFormatAsync())
                .Should().ThrowAsync<SqliteException>()
                .WithMessage("Database error");
        }

        [Fact]
        public async Task MigrateApiKeysToEncryptedFormatAsync_ShouldHandleNullKeys()
        {
            // Arrange
            // Since the database doesn't allow NULL values by default for ApiKey column,
            // we need to alter the table structure for this test
            using var alterCommand = _connection.CreateCommand();
            alterCommand.CommandText = @"
                DROP TABLE ApiConfigurations;
                CREATE TABLE ApiConfigurations (
                    Id INTEGER PRIMARY KEY,
                    ApiKey TEXT
                );";
            alterCommand.ExecuteNonQuery();

            InsertApiConfigurationWithNullableKey(1, null);
            InsertApiConfigurationWithNullableKey(2, "");
            InsertApiConfigurationWithNullableKey(3, "valid-key");

            var connectionWrapper = new Mock<IDisposableConnection>();
            connectionWrapper.SetupGet(c => c.Connection).Returns(_connection);
            _connectionPoolMock.Setup(p => p.GetConnectionAsync(default(CancellationToken))).ReturnsAsync(connectionWrapper.Object);

            // Act
            await _migration.MigrateApiKeysToEncryptedFormatAsync();

            // Assert
            // Only the valid key should be encrypted
            var key3 = GetApiKeyFromApiConfigurations(3);
            ApiKeyProtection.IsProtected(key3).Should().BeTrue();
            ApiKeyProtection.Unprotect(key3).Should().Be("valid-key");
        }

        [Fact]
        public async Task MigrateApiKeysToEncryptedFormatAsync_ShouldHandleVeryLongKeys()
        {
            // Arrange
            var longKey = new string('a', 1000); // 1000 character key
            InsertApiConfiguration(1, longKey);

            var connectionWrapper = new Mock<IDisposableConnection>();
            connectionWrapper.SetupGet(c => c.Connection).Returns(_connection);
            _connectionPoolMock.Setup(p => p.GetConnectionAsync(default(CancellationToken))).ReturnsAsync(connectionWrapper.Object);

            // Act
            await _migration.MigrateApiKeysToEncryptedFormatAsync();

            // Assert
            var storedKey = GetApiKeyFromApiConfigurations(1);
            ApiKeyProtection.IsProtected(storedKey).Should().BeTrue();
            ApiKeyProtection.Unprotect(storedKey).Should().Be(longKey);
        }

        [Fact]
        public async Task MigrateApiKeysToEncryptedFormatAsync_ShouldHandleSpecialCharactersInKeys()
        {
            // Arrange
            var specialCharsKey = "key!@#$%^&*()_+-=[]{}|;':\",./<>?`~";
            InsertApiConfiguration(1, specialCharsKey);

            var connectionWrapper = new Mock<IDisposableConnection>();
            connectionWrapper.SetupGet(c => c.Connection).Returns(_connection);
            _connectionPoolMock.Setup(p => p.GetConnectionAsync(default(CancellationToken))).ReturnsAsync(connectionWrapper.Object);

            // Act
            await _migration.MigrateApiKeysToEncryptedFormatAsync();

            // Assert
            var storedKey = GetApiKeyFromApiConfigurations(1);
            ApiKeyProtection.IsProtected(storedKey).Should().BeTrue();
            ApiKeyProtection.Unprotect(storedKey).Should().Be(specialCharsKey);
        }

        [Fact]
        public async Task MigrateApiKeysToEncryptedFormatAsync_ShouldHandleUnicodeCharactersInKeys()
        {
            // Arrange
            var unicodeKey = "key-中文-日本語-한국어-🚀";
            InsertApiConfiguration(1, unicodeKey);

            var connectionWrapper = new Mock<IDisposableConnection>();
            connectionWrapper.SetupGet(c => c.Connection).Returns(_connection);
            _connectionPoolMock.Setup(p => p.GetConnectionAsync(default(CancellationToken))).ReturnsAsync(connectionWrapper.Object);

            // Act
            await _migration.MigrateApiKeysToEncryptedFormatAsync();

            // Assert
            var storedKey = GetApiKeyFromApiConfigurations(1);
            ApiKeyProtection.IsProtected(storedKey).Should().BeTrue();
            ApiKeyProtection.Unprotect(storedKey).Should().Be(unicodeKey);
        }

        [Fact]
        public async Task MigrateApiKeysToEncryptedFormatAsync_ShouldProcessInBatches()
        {
            // Arrange
            var numberOfKeys = 100;
            for (int i = 1; i <= numberOfKeys; i++)
            {
                InsertApiConfiguration(i, $"api-key-{i}");
                InsertTtsConfiguration(i, $"tts-key-{i}");
            }

            var connectionWrapper = new Mock<IDisposableConnection>();
            connectionWrapper.SetupGet(c => c.Connection).Returns(_connection);
            _connectionPoolMock.Setup(p => p.GetConnectionAsync(default(CancellationToken))).ReturnsAsync(connectionWrapper.Object);

            // Act
            await _migration.MigrateApiKeysToEncryptedFormatAsync();

            // Assert
            for (int i = 1; i <= numberOfKeys; i++)
            {
                var apiKey = GetApiKeyFromApiConfigurations(i);
                ApiKeyProtection.IsProtected(apiKey).Should().BeTrue();
                ApiKeyProtection.Unprotect(apiKey).Should().Be($"api-key-{i}");

                var ttsKey = GetApiKeyFromTtsConfigurations(i);
                ApiKeyProtection.IsProtected(ttsKey).Should().BeTrue();
                ApiKeyProtection.Unprotect(ttsKey).Should().Be($"tts-key-{i}");
            }
        }

        private void InsertApiConfiguration(int id, string apiKey)
        {
            using var command = _connection.CreateCommand();
            command.CommandText = "INSERT INTO ApiConfigurations (Id, ApiKey) VALUES (@Id, @ApiKey)";
            command.Parameters.AddWithValue("@Id", id);
            command.Parameters.AddWithValue("@ApiKey", apiKey);
            command.ExecuteNonQuery();
        }

        private void InsertApiConfigurationWithNullableKey(int id, string? apiKey)
        {
            using var command = _connection.CreateCommand();
            command.CommandText = "INSERT INTO ApiConfigurations (Id, ApiKey) VALUES (@Id, @ApiKey)";
            command.Parameters.AddWithValue("@Id", id);
            command.Parameters.AddWithValue("@ApiKey", (object?)apiKey ?? DBNull.Value);
            command.ExecuteNonQuery();
        }

        private void InsertTtsConfiguration(int id, string apiKey)
        {
            using var command = _connection.CreateCommand();
            command.CommandText = "INSERT INTO TtsConfigurations (Id, ApiKey) VALUES (@Id, @ApiKey)";
            command.Parameters.AddWithValue("@Id", id);
            command.Parameters.AddWithValue("@ApiKey", apiKey);
            command.ExecuteNonQuery();
        }

        private string GetApiKeyFromApiConfigurations(int id)
        {
            using var command = _connection.CreateCommand();
            command.CommandText = "SELECT ApiKey FROM ApiConfigurations WHERE Id = @Id";
            command.Parameters.AddWithValue("@Id", id);
            return command.ExecuteScalar()?.ToString() ?? "";
        }

        private string GetApiKeyFromTtsConfigurations(int id)
        {
            using var command = _connection.CreateCommand();
            command.CommandText = "SELECT ApiKey FROM TtsConfigurations WHERE Id = @Id";
            command.Parameters.AddWithValue("@Id", id);
            return command.ExecuteScalar()?.ToString() ?? "";
        }

        public void Dispose()
        {
            _connection?.Dispose();
            (_serviceProvider as IDisposable)?.Dispose();
        }
    }

}