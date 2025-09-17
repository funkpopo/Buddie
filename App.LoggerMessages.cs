using Microsoft.Extensions.Logging;

namespace Buddie
{
    public partial class App
    {
        private static partial class Log
        {
            [LoggerMessage(
                EventId = 1,
                Level = LogLevel.Information,
                Message = "Host started successfully")]
            public static partial void HostStarted(ILogger logger);

            [LoggerMessage(
                EventId = 2,
                Level = LogLevel.Debug,
                Message = "Getting IErrorNotifier...")]
            public static partial void GettingErrorNotifier(ILogger logger);

            [LoggerMessage(
                EventId = 3,
                Level = LogLevel.Debug,
                Message = "Got IErrorNotifier successfully")]
            public static partial void GotErrorNotifier(ILogger logger);

            [LoggerMessage(
                EventId = 4,
                Level = LogLevel.Debug,
                Message = "Getting ILoggerFactory...")]
            public static partial void GettingLoggerFactory(ILogger logger);

            [LoggerMessage(
                EventId = 5,
                Level = LogLevel.Debug,
                Message = "Got ILoggerFactory successfully")]
            public static partial void GotLoggerFactory(ILogger logger);

            [LoggerMessage(
                EventId = 6,
                Level = LogLevel.Information,
                Message = "Configuring ExceptionHandlingService...")]
            public static partial void ConfiguringExceptionHandlingService(ILogger logger);

            [LoggerMessage(
                EventId = 7,
                Level = LogLevel.Information,
                Message = "ExceptionHandlingService configured successfully")]
            public static partial void ExceptionHandlingServiceConfigured(ILogger logger);

            [LoggerMessage(
                EventId = 8,
                Level = LogLevel.Information,
                Message = "Setting up global exception handlers...")]
            public static partial void SettingUpExceptionHandlers(ILogger logger);

            [LoggerMessage(
                EventId = 9,
                Level = LogLevel.Information,
                Message = "Global exception handlers set up successfully")]
            public static partial void ExceptionHandlersSetUp(ILogger logger);

            [LoggerMessage(
                EventId = 10,
                Level = LogLevel.Debug,
                Message = "Getting IDatabaseInitializer...")]
            public static partial void GettingDatabaseInitializer(ILogger logger);

            [LoggerMessage(
                EventId = 11,
                Level = LogLevel.Debug,
                Message = "Got IDatabaseInitializer successfully")]
            public static partial void GotDatabaseInitializer(ILogger logger);

            [LoggerMessage(
                EventId = 12,
                Level = LogLevel.Information,
                Message = "Initializing database...")]
            public static partial void InitializingDatabase(ILogger logger);

            [LoggerMessage(
                EventId = 13,
                Level = LogLevel.Information,
                Message = "Database initialized successfully")]
            public static partial void DatabaseInitialized(ILogger logger);

            [LoggerMessage(
                EventId = 14,
                Level = LogLevel.Information,
                Message = "Creating FloatingWindow...")]
            public static partial void CreatingFloatingWindow(ILogger logger);

            [LoggerMessage(
                EventId = 15,
                Level = LogLevel.Information,
                Message = "FloatingWindow created successfully")]
            public static partial void FloatingWindowCreated(ILogger logger);

            [LoggerMessage(
                EventId = 16,
                Level = LogLevel.Information,
                Message = "Loading settings from database...")]
            public static partial void LoadingSettings(ILogger logger);

            [LoggerMessage(
                EventId = 17,
                Level = LogLevel.Information,
                Message = "Settings loaded successfully")]
            public static partial void SettingsLoaded(ILogger logger);

            [LoggerMessage(
                EventId = 18,
                Level = LogLevel.Information,
                Message = "Showing FloatingWindow...")]
            public static partial void ShowingFloatingWindow(ILogger logger);

            [LoggerMessage(
                EventId = 19,
                Level = LogLevel.Information,
                Message = "FloatingWindow shown successfully")]
            public static partial void FloatingWindowShown(ILogger logger);
        }
    }
}