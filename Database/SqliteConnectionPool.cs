using Microsoft.Data.Sqlite;
using System;
using System.Threading;
using System.Threading.Tasks;
using Buddie.Services.ExceptionHandling;

namespace Buddie.Database
{
    /// <summary>
    /// 简化的SQLite连接池，针对SQLite的文件锁特性进行优化
    /// SQLite最佳实践：使用单连接或短连接，避免过多并发连接
    /// </summary>
    public sealed class SqliteConnectionPool : ISqliteConnectionPool, IDisposable
    {
        private readonly IDatabasePathProvider _pathProvider;
        private readonly SemaphoreSlim _connectionSemaphore;
        private SqliteConnection? _sharedConnection;
        private readonly object _lockObject = new();
        private bool _disposed;
        
        // SQLite建议的配置
        private const int BusyTimeoutMs = 30000; // 30秒的忙等待超时
        private const int MaxConcurrentOperations = 1; // SQLite写操作是串行的，限制为1个并发

        public SqliteConnectionPool(IDatabasePathProvider pathProvider)
        {
            _pathProvider = pathProvider ?? throw new ArgumentNullException(nameof(pathProvider));
            _connectionSemaphore = new SemaphoreSlim(MaxConcurrentOperations, MaxConcurrentOperations);
        }

        /// <summary>
        /// 获取数据库连接
        /// 使用短连接模式，每次操作都创建新连接
        /// </summary>
        public async Task<IDisposableConnection> GetConnectionAsync(CancellationToken cancellationToken = default)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(SqliteConnectionPool));

            // 等待获取连接权限（串行化写操作）
            await _connectionSemaphore.WaitAsync(cancellationToken);

            try
            {
                // 创建新的短连接
                var connection = new SqliteConnection(_pathProvider.ConnectionString);
                
                // 设置SQLite推荐的参数
                await connection.OpenAsync(cancellationToken);
                
                // 设置忙等待超时，避免SQLITE_BUSY错误
                using (var cmd = connection.CreateCommand())
                {
                    cmd.CommandText = $"PRAGMA busy_timeout = {BusyTimeoutMs};";
                    await cmd.ExecuteNonQueryAsync(cancellationToken);
                    
                    // 启用WAL模式以提升并发性能
                    cmd.CommandText = "PRAGMA journal_mode = WAL;";
                    await cmd.ExecuteNonQueryAsync(cancellationToken);
                    
                    // 设置同步模式为NORMAL以平衡性能和安全性
                    cmd.CommandText = "PRAGMA synchronous = NORMAL;";
                    await cmd.ExecuteNonQueryAsync(cancellationToken);
                }
                
                var connectionId = Guid.NewGuid().ToString();
                return new DisposableConnection(connection, connectionId, this);
            }
            catch
            {
                _connectionSemaphore.Release();
                throw;
            }
        }

        /// <summary>
        /// 获取共享的只读连接（用于频繁的读操作）
        /// </summary>
        public Task<SqliteConnection> GetSharedReadConnectionAsync()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(SqliteConnectionPool));

            if (_sharedConnection == null || _sharedConnection.State != System.Data.ConnectionState.Open)
            {
                lock (_lockObject)
                {
                    if (_sharedConnection == null || _sharedConnection.State != System.Data.ConnectionState.Open)
                    {
                        _sharedConnection?.Dispose();
                        _sharedConnection = new SqliteConnection(_pathProvider.ConnectionString);
                        _sharedConnection.Open();
                        
                        // 配置只读连接
                        using (var cmd = _sharedConnection.CreateCommand())
                        {
                            cmd.CommandText = "PRAGMA query_only = true;";
                            cmd.ExecuteNonQuery();
                            
                            cmd.CommandText = $"PRAGMA busy_timeout = {BusyTimeoutMs};";
                            cmd.ExecuteNonQuery();
                        }
                    }
                }
            }

            return Task.FromResult(_sharedConnection);
        }

        /// <summary>
        /// 释放连接
        /// </summary>
        internal void ReleaseConnection(string connectionId, SqliteConnection connection)
        {
            try
            {
                // 短连接模式：直接关闭并释放连接
                connection?.Close();
                connection?.Dispose();
            }
            finally
            {
                _connectionSemaphore.Release();
            }
        }

        /// <summary>
        /// 获取连接池统计信息
        /// </summary>
        public (int Available, int InUse, int Total, int MaxConnections) GetStatistics()
        {
            var inUse = MaxConcurrentOperations - _connectionSemaphore.CurrentCount;
            return (
                Available: _connectionSemaphore.CurrentCount,
                InUse: inUse,
                Total: inUse + (_sharedConnection != null ? 1 : 0),
                MaxConnections: MaxConcurrentOperations
            );
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;

            _connectionSemaphore?.Dispose();
            
            lock (_lockObject)
            {
                _sharedConnection?.Close();
                _sharedConnection?.Dispose();
                _sharedConnection = null;
            }

            System.Diagnostics.Debug.WriteLine("SQLite connection pool disposed");
        }
    }

    /// <summary>
    /// 可释放的连接实现
    /// </summary>
    internal class DisposableConnection : IDisposableConnection
    {
        public SqliteConnection Connection { get; }
        private readonly string _connectionId;
        private readonly SqliteConnectionPool _pool;
        private bool _disposed;

        public DisposableConnection(SqliteConnection connection, string connectionId, SqliteConnectionPool pool)
        {
            Connection = connection ?? throw new ArgumentNullException(nameof(connection));
            _connectionId = connectionId ?? throw new ArgumentNullException(nameof(connectionId));
            _pool = pool ?? throw new ArgumentNullException(nameof(pool));
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            _pool.ReleaseConnection(_connectionId, Connection);
        }
    }
}