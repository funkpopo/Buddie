namespace Buddie.Database
{
    public interface IDatabasePathProvider
    {
        string ConnectionString { get; }
        string DatabasePath { get; }
    }
}

