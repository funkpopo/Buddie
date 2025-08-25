using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Buddie
{
    public class OpenApiConfiguration : INotifyPropertyChanged
    {
        private string _name = "";
        private string _apiUrl = "";
        private string _apiKey = "";
        private string _modelName = "";
        private bool _isStreamingEnabled = true;
        private bool _isMultimodalEnabled = false;

        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value);
        }

        public string ApiUrl
        {
            get => _apiUrl;
            set => SetProperty(ref _apiUrl, value);
        }

        public string ApiKey
        {
            get => _apiKey;
            set => SetProperty(ref _apiKey, value);
        }

        public string ModelName
        {
            get => _modelName;
            set => SetProperty(ref _modelName, value);
        }

        public bool IsStreamingEnabled
        {
            get => _isStreamingEnabled;
            set => SetProperty(ref _isStreamingEnabled, value);
        }

        public bool IsMultimodalEnabled
        {
            get => _isMultimodalEnabled;
            set => SetProperty(ref _isMultimodalEnabled, value);
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetProperty<T>(ref T backingStore, T value, [CallerMemberName] string? propertyName = null)
        {
            if (Equals(backingStore, value))
                return false;

            backingStore = value;
            OnPropertyChanged(propertyName);
            return true;
        }
    }

    public class AppSettings : INotifyPropertyChanged
    {
        private bool _isTopmost = true;
        private bool _showInTaskbar = true;
        private bool _enableAnimation = true;
        private ObservableCollection<OpenApiConfiguration> _apiConfigurations = new ObservableCollection<OpenApiConfiguration>();

        public bool IsTopmost
        {
            get => _isTopmost;
            set => SetProperty(ref _isTopmost, value);
        }

        public bool ShowInTaskbar
        {
            get => _showInTaskbar;
            set => SetProperty(ref _showInTaskbar, value);
        }

        public bool EnableAnimation
        {
            get => _enableAnimation;
            set => SetProperty(ref _enableAnimation, value);
        }

        public ObservableCollection<OpenApiConfiguration> ApiConfigurations
        {
            get => _apiConfigurations;
            set => SetProperty(ref _apiConfigurations, value);
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetProperty<T>(ref T backingStore, T value, [CallerMemberName] string? propertyName = null)
        {
            if (Equals(backingStore, value))
                return false;

            backingStore = value;
            OnPropertyChanged(propertyName);
            return true;
        }
    }
}