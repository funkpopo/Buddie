using System;
using System.Windows;
using Buddie.Database;

namespace Buddie
{
    public partial class App : Application
    {
        protected override async void OnStartup(StartupEventArgs e)
        {
            try
            {
                // Initialize database
                DatabaseManager.InitializeDatabase();

                // Create and show main window
                var floatingWindow = new FloatingWindow();
                
                // Load settings from database
                await floatingWindow.LoadSettingsFromDatabaseAsync();
                
                floatingWindow.Show();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"应用程序启动失败: {ex.Message}", "启动错误", MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown();
            }

            base.OnStartup(e);
        }
    }
}