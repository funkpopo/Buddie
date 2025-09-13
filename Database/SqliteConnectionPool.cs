using Microsoft.Data.Sqlite;
using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Buddie.Database
{
    /// <summary>
    /// 简化的SQLite连接管理器，遵循SQLite最佳实践
    /// 使用短连接模式，每个操作创建新连接，操作完成后立即释放
    /// 通过信号量序列化写操作，避免并发冲突
    /// </summary>
    public sealed class SqliteConnectionPool : ISqliteConnectionPool, IDisposable
    {
        private readonly IDatabasePathProvider _pathProvider;
        private readonly SemaphoreSlim _writeSemaphore;
        private bool _disposed;
        private readonly Microsoft.Extensions.Logging.ILogger _logger;
        
        // SQLite推荐配置
        private const int BusyTimeoutMs = 30000; // 30秒忙等待超时
        private const int MaxWriteOperations = 1; // 序列化写操作

        public SqliteConnectionPool(IDatabasePathProvider pathProvider, Microsoft.Extensions.Logging.ILoggerFactory? loggerFactory = null)
        {
            _pathProvider = pathProvider ?? throw new ArgumentNullException(nameof(pathProvider));
            _writeSemaphore = new SemaphoreSlim(MaxWriteOperations, MaxWriteOperations);
            _logger = (loggerFactory ?? Buddie.App.Services?.GetService(typeof(Microsoft.Extensions.Logging.ILoggerFactory)) as Microsoft.Extensions.Logging.ILoggerFactory)?.CreateLogger(typeof(SqliteConnectionPool).FullName!)
                      ?? Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance;
        }

        /// <summary>
        /// 获取数据库连接（短连接模式）
        /// 每次创建新连接，使用完后立即释放
        /// </summary>
        public async Task<IDisposableConnection> GetConnectionAsync(CancellationToken cancellationToken = default)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(SqliteConnectionPool));

            // 序列化写操作
            await _writeSemaphore.WaitAsync(cancellationToken);

            try
            {
                // 创建并配置新连接
                var connection = await CreateAndConfigureConnectionAsync(cancellationToken);
                return new DisposableConnection(connection, this);
            }
            catch
            {
                _writeSemaphore.Release();
                throw;
            }
        }

        /// <summary>
        /// 创建并配置SQLite连接
        /// </summary>
        private async Task<SqliteConnection> CreateAndConfigureConnectionAsync(CancellationToken cancellationToken)
        {
            var connection = new SqliteConnection(_pathProvider.ConnectionString);
            
            try
            {
                await connection.OpenAsync(cancellationToken);
                
                // 应用SQLite优化配置
                using (var cmd = connection.CreateCommand())
                {
                    // 设置忙等待超时
                    cmd.CommandText = $"PRAGMA busy_timeout = {BusyTimeoutMs};";
                    await cmd.ExecuteNonQueryAsync(cancellationToken);
                    
                    // 启用WAL模式（写前日志）提升并发读性能
                    cmd.CommandText = "PRAGMA journal_mode = WAL;";
                    await cmd.ExecuteNonQueryAsync(cancellationToken);
                    
                    // 同步模式设为NORMAL，平衡性能和数据安全
                    cmd.CommandText = "PRAGMA synchronous = NORMAL;";
                    await cmd.ExecuteNonQueryAsync(cancellationToken);
                    
                    // 设置缓存大小（2MB）
                    cmd.CommandText = "PRAGMA cache_size = -2000;";
                    await cmd.ExecuteNonQueryAsync(cancellationToken);
                    
                    // 启用外键约束
                    cmd.CommandText = "PRAGMA foreign_keys = ON;";
                    await cmd.ExecuteNonQueryAsync(cancellationToken);
                }
                
                return connection;
            }
            catch
            {
                connection?.Dispose();
                throw;
            }
        }

        /// <summary>
        /// 释放连接并解除写锁
        /// </summary>
        internal void ReleaseConnection(SqliteConnection connection)
        {
            try
            {
                connection?.Close();
                connection?.Dispose();
            }
            finally
            {
                _writeSemaphore.Release();
            }
        }

        /// <summary>
        /// 获取统计信息
        /// </summary>
        public (int Available, int InUse, int Total, int MaxConnections) GetStatistics()
        {
            var inUse = MaxWriteOperations - _writeSemaphore.CurrentCount;
            return (
                Available: _writeSemaphore.CurrentCount,
                InUse: inUse,
                Total: inUse,
                MaxConnections: MaxWriteOperations
            );
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            _writeSemaphore?.Dispose();
            
            _logger.LogInformation("SQLite connection manager disposed");
        }
    }

    /// <summary>
    /// 可释放的连接包装器
    /// </summary>
    internal sealed class DisposableConnection : IDisposableConnection
    {
        public SqliteConnection Connection { get; }
        private readonly SqliteConnectionPool _pool;
        private bool _disposed;

        public DisposableConnection(SqliteConnection connection, SqliteConnectionPool pool)
        {
            Connection = connection ?? throw new ArgumentNullException(nameof(connection));
            _pool = pool ?? throw new ArgumentNullException(nameof(pool));
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            _pool.ReleaseConnection(Connection);
        }
    }
}
