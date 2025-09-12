using System.Threading.Tasks;

namespace Buddie.Database
{
    public interface IDatabaseInitializer
    {
        Task InitializeAsync();
        void BackupDatabase(string backupPath);
        void RestoreDatabase(string backupPath);
        Task CleanupTtsAudioCacheAsync(int daysToKeep = 7, int maxCount = 1000, long maxSizeMB = 500);
    }
}

