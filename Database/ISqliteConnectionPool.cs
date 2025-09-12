using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;

namespace Buddie.Database
{
    public interface IDisposableConnection
    {
        SqliteConnection Connection { get; }
    }

    public interface ISqliteConnectionPool
    {
        Task<IDisposableConnection> GetConnectionAsync(CancellationToken cancellationToken = default);
        (int Available, int InUse, int Total, int MaxConnections) GetStatistics();
    }
}

