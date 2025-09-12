using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Buddie.ViewModels
{
    public partial class SettingsViewModel : ObservableObject
    {
        public AppSettings AppSettings { get; }

        public SettingsViewModel(AppSettings appSettings)
        {
            AppSettings = appSettings;
        }

        // Forwarded properties for binding
        public bool IsTopmost
        {
            get => AppSettings.IsTopmost;
            set
            {
                if (AppSettings.IsTopmost != value)
                {
                    AppSettings.IsTopmost = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool ShowInTaskbar
        {
            get => AppSettings.ShowInTaskbar;
            set
            {
                if (AppSettings.ShowInTaskbar != value)
                {
                    AppSettings.ShowInTaskbar = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool IsDarkTheme
        {
            get => AppSettings.IsDarkTheme;
            set
            {
                if (AppSettings.IsDarkTheme != value)
                {
                    AppSettings.IsDarkTheme = value;
                    OnPropertyChanged();
                }
            }
        }

        public event System.EventHandler? CloseRequested;
        public event System.EventHandler? ResetRequested;

        [RelayCommand]
        private void Close()
        {
            CloseRequested?.Invoke(this, System.EventArgs.Empty);
        }

        [RelayCommand]
        private void Reset()
        {
            ResetRequested?.Invoke(this, System.EventArgs.Empty);
        }
    }
}

