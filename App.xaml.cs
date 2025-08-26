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
                System.Diagnostics.Debug.WriteLine("Application starting up...");
                
                // Initialize database
                System.Diagnostics.Debug.WriteLine("Initializing database...");
                DatabaseManager.InitializeDatabase();
                System.Diagnostics.Debug.WriteLine("Database initialization completed");

                // Create and show main window
                var floatingWindow = new FloatingWindow();
                System.Diagnostics.Debug.WriteLine("FloatingWindow created");
                
                // Load settings from database
                System.Diagnostics.Debug.WriteLine("Loading settings from database...");
                await floatingWindow.LoadSettingsFromDatabaseAsync();
                System.Diagnostics.Debug.WriteLine("Settings loading completed");
                
                floatingWindow.Show();
                System.Diagnostics.Debug.WriteLine("FloatingWindow shown");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Application startup failed: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
                MessageBox.Show($"应用程序启动失败: {ex.Message}", "启动错误", MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown();
            }

            base.OnStartup(e);
        }
    }
}