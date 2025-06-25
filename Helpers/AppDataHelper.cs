using System;
using System.IO;

namespace Lyxie_desktop.Helpers
{
    public static class AppDataHelper
    {
        private static readonly string AppDataRootPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Lyxie");

        // 获取并创建应用程序的根数据目录
        public static string GetAppDataRootPath()
        {
            if (!Directory.Exists(AppDataRootPath))
            {
                Directory.CreateDirectory(AppDataRootPath);
            }
            return AppDataRootPath;
        }

        // 获取并创建临时文件目录
        public static string GetTempPath()
        {
            var tempPath = Path.Combine(GetAppDataRootPath(), "temp");
            if (!Directory.Exists(tempPath))
            {
                Directory.CreateDirectory(tempPath);
            }
            return tempPath;
        }

        // 获取设置文件的完整路径
        public static string GetSettingsFilePath()
        {
            return Path.Combine(GetAppDataRootPath(), "settings.json");
        }
    }
} 