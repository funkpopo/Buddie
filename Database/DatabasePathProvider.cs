using System;
using System.IO;
using System.Linq;
using System.Reflection;

namespace Buddie.Database
{
    public class DatabasePathProvider : IDatabasePathProvider
    {
        private string? _connectionString;
        private string? _databasePath;

        public string ConnectionString
        {
            get
            {
                EnsureInitialized();
                return _connectionString!;
            }
        }

        public string DatabasePath
        {
            get
            {
                EnsureInitialized();
                return _databasePath!;
            }
        }

        private void EnsureInitialized()
        {
            if (!string.IsNullOrEmpty(_connectionString) && !string.IsNullOrEmpty(_databasePath))
                return;

            string dbDirectory;
            if (IsDebugMode())
            {
                var projectRoot = GetProjectRoot();
                dbDirectory = Path.Combine(projectRoot, "data");
            }
            else
            {
                var exeDirectory = AppDomain.CurrentDomain.BaseDirectory;
                dbDirectory = Path.Combine(exeDirectory, "data");
            }

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
            while (directory != null && !directory.GetFiles("*.csproj").Any())
            {
                directory = directory.Parent;
            }
            return directory?.FullName ?? AppDomain.CurrentDomain.BaseDirectory;
        }
    }
}

