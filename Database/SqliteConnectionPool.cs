using Microsoft.Data.Sqlite;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Buddie.Services.ExceptionHandling;

namespace Buddie.Database
{
    /// <summary>
    /// SQLite连接池，管理数据库连接的创建、复用和清理
    /// </summary>
    public sealed class SqliteConnectionPool : ISqliteConnectionPool, IDisposable
    {
        private readonly ConcurrentQueue<PooledConnection> _availableConnections;
        private readonly ConcurrentDictionary<string, PooledConnection> _usedConnections;
        private readonly SemaphoreSlim _connectionSemaphore;
        private readonly Timer _cleanupTimer;
        private readonly object _lockObject = new();

        // 连接池配置
        private readonly int _maxConnections;
        private readonly int _minConnections;
        private readonly TimeSpan _connectionTimeout;
        private readonly TimeSpan _cleanupInterval;

        private int _currentConnectionCount;
        private bool _disposed;

        private readonly IDatabasePathProvider _pathProvider;

        public SqliteConnectionPool(IDatabasePathProvider pathProvider)
        {
            _pathProvider = pathProvider;
            _maxConnections = 20; // 最大连接数
            _minConnections = 2;  // 最小连接数
            _connectionTimeout = TimeSpan.FromMinutes(5); // 连接超时时间
            _cleanupInterval = TimeSpan.FromMinutes(2);   // 清理间隔

            _availableConnections = new ConcurrentQueue<PooledConnection>();
            _usedConnections = new ConcurrentDictionary<string, PooledConnection>();
            _connectionSemaphore = new SemaphoreSlim(_maxConnections, _maxConnections);

            // 预创建最小连接数
            InitializeMinimumConnections();

            // 启动清理定时器
            _cleanupTimer = new Timer(CleanupExpiredConnections, null, _cleanupInterval, _cleanupInterval);
        }

        /// <summary>
        /// 获取数据库连接
        /// </summary>
        public async Task<IDisposableConnection> GetConnectionAsync(CancellationToken cancellationToken = default)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(SqliteConnectionPool));

            await _connectionSemaphore.WaitAsync(cancellationToken);

            try
            {
                // 尝试从可用连接池中获取连接
                if (_availableConnections.TryDequeue(out var pooledConnection))
                {
                    if (IsConnectionValid(pooledConnection))
                    {
                        pooledConnection.LastUsed = DateTime.UtcNow;
                        var connectionId = Guid.NewGuid().ToString();
                        _usedConnections.TryAdd(connectionId, pooledConnection);
                        return new DisposableConnection(pooledConnection.Connection, connectionId, this);
                    }
                    else
                    {
                        // 连接无效，释放并创建新连接
                        DisposeConnection(pooledConnection);
                    }
                }

                // 创建新连接
                var newConnection = await CreateNewConnectionAsync();
                var newConnectionId = Guid.NewGuid().ToString();
                _usedConnections.TryAdd(newConnectionId, newConnection);
                return new DisposableConnection(newConnection.Connection, newConnectionId, this);
            }
            catch
            {
                _connectionSemaphore.Release();
                throw;
            }
        }

        /// <summary>
        /// 归还连接到池中
        /// </summary>
        internal void ReturnConnection(string connectionId)
        {
            if (_disposed || !_usedConnections.TryRemove(connectionId, out var pooledConnection))
            {
                _connectionSemaphore.Release();
                return;
            }

            if (IsConnectionValid(pooledConnection) && _availableConnections.Count < _maxConnections / 2)
            {
                // 将连接返回到可用连接池
                pooledConnection.LastUsed = DateTime.UtcNow;
                _availableConnections.Enqueue(pooledConnection);
            }
            else
            {
                // 连接池已满或连接无效，直接释放
                DisposeConnection(pooledConnection);
            }

            _connectionSemaphore.Release();
        }

        /// <summary>
        /// 初始化最小连接数
        /// </summary>
        private void InitializeMinimumConnections()
        {
            lock (_lockObject)
            {
                for (int i = 0; i < _minConnections; i++)
                {
                    ExceptionHandlingService.ExecuteSafely(() =>
                    {
                        var connection = CreateNewConnectionAsync().GetAwaiter().GetResult();
                        _availableConnections.Enqueue(connection);
                    }, ExceptionHandlingService.HandlingStrategy.LogOnly, context: new ExceptionHandlingService.ExceptionContext
                    {
                        Component = "SqliteConnectionPool",
                        Operation = "初始化最小连接数"
                    });
                }
            }
        }

        /// <summary>
        /// 创建新的数据库连接
        /// </summary>
        private async Task<PooledConnection> CreateNewConnectionAsync()
        {
            var connection = new SqliteConnection(_pathProvider.ConnectionString);
            await connection.OpenAsync();
            
            Interlocked.Increment(ref _currentConnectionCount);
            
            return new PooledConnection
            {
                Connection = connection,
                Created = DateTime.UtcNow,
                LastUsed = DateTime.UtcNow
            };
        }

        /// <summary>
        /// 检查连接是否有效
        /// </summary>
        private bool IsConnectionValid(PooledConnection pooledConnection)
        {
            if (pooledConnection?.Connection == null)
                return false;

            return ExceptionHandlingService.ExecuteSafely(() =>
            {
                // 检查连接是否超时
                if (DateTime.UtcNow - pooledConnection.LastUsed > _connectionTimeout)
                    return false;

                // 检查连接状态
                return pooledConnection.Connection.State == System.Data.ConnectionState.Open;
            },
            ExceptionHandlingService.HandlingStrategy.LogOnly,
            false,
            new ExceptionHandlingService.ExceptionContext
            {
                Component = "SqliteConnectionPool",
                Operation = "检查连接有效性"
            });
        }

        /// <summary>
        /// 释放连接资源
        /// </summary>
        private void DisposeConnection(PooledConnection pooledConnection)
        {
            ExceptionHandlingService.ExecuteSafely(() =>
            {
                pooledConnection?.Connection?.Dispose();
                Interlocked.Decrement(ref _currentConnectionCount);
            }, ExceptionHandlingService.HandlingStrategy.LogOnly, context: new ExceptionHandlingService.ExceptionContext
            {
                Component = "SqliteConnectionPool",
                Operation = "释放连接资源"
            });
        }

        /// <summary>
        /// 清理过期连接
        /// </summary>
        private void CleanupExpiredConnections(object? state)
        {
            if (_disposed)
                return;

            var expiredConnections = new List<PooledConnection>();

            // 收集过期的可用连接
            var remainingConnections = new List<PooledConnection>();
            while (_availableConnections.TryDequeue(out var connection))
            {
                if (IsConnectionValid(connection))
                {
                    remainingConnections.Add(connection);
                }
                else
                {
                    expiredConnections.Add(connection);
                }
            }

            // 将有效连接重新加入队列
            foreach (var connection in remainingConnections)
            {
                _availableConnections.Enqueue(connection);
            }

            // 释放过期连接
            foreach (var connection in expiredConnections)
            {
                DisposeConnection(connection);
            }

            // 确保最小连接数
            var availableCount = _availableConnections.Count;
            if (availableCount < _minConnections)
            {
                var connectionsToCreate = _minConnections - availableCount;
                for (int i = 0; i < connectionsToCreate && _currentConnectionCount < _maxConnections; i++)
                {
                    ExceptionHandlingService.ExecuteSafely(() =>
                    {
                        var newConnection = CreateNewConnectionAsync().GetAwaiter().GetResult();
                        _availableConnections.Enqueue(newConnection);
                    }, ExceptionHandlingService.HandlingStrategy.LogOnly, context: new ExceptionHandlingService.ExceptionContext
                    {
                        Component = "SqliteConnectionPool",
                        Operation = "清理期间创建连接"
                    });
                }
            }

            System.Diagnostics.Debug.WriteLine($"Connection pool cleanup: {expiredConnections.Count} expired, {_availableConnections.Count} available, {_usedConnections.Count} in use, {_currentConnectionCount} total");
        }

        /// <summary>
        /// 获取连接池统计信息
        /// </summary>
        public (int Available, int InUse, int Total, int MaxConnections) GetStatistics()
        {
            return (
                Available: _availableConnections.Count,
                InUse: _usedConnections.Count,
                Total: _currentConnectionCount,
                MaxConnections: _maxConnections
            );
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;

            _cleanupTimer?.Dispose();
            _connectionSemaphore?.Dispose();

            // 清理所有可用连接
            while (_availableConnections.TryDequeue(out var connection))
            {
                DisposeConnection(connection);
            }

            // 清理所有使用中的连接
            foreach (var kvp in _usedConnections)
            {
                DisposeConnection(kvp.Value);
            }
            _usedConnections.Clear();

            System.Diagnostics.Debug.WriteLine("SQLite connection pool disposed");
        }

        /// <summary>
        /// 池中的连接包装器
        /// </summary>
        private class PooledConnection
        {
            public SqliteConnection Connection { get; set; } = null!;
            public DateTime Created { get; set; }
            public DateTime LastUsed { get; set; }
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
            _pool.ReturnConnection(_connectionId);
        }
    }
}
