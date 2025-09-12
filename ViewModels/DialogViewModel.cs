using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Buddie.ViewModels
{
    public partial class DialogViewModel : ObservableObject
    {
        private readonly AppSettings _appSettings;

        public DialogViewModel(AppSettings appSettings)
        {
            _appSettings = appSettings;
            _appSettings.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(AppSettings.IsDarkTheme))
                {
                    OnPropertyChanged(nameof(IsDarkTheme));
                }
            };
        }

        public bool IsDarkTheme => _appSettings.IsDarkTheme;

        public event EventHandler<string>? SendRequested;
        public event EventHandler? ScreenshotRequested;
        public event EventHandler? ImageUploadRequested;
        public event EventHandler? ToggleSidebarRequested;
        public event EventHandler? CloseRequested;
        public event EventHandler? NewConversationRequested;
        public event EventHandler<string>? CopyRequested;
        public event EventHandler<string>? PlayTtsRequested;
        public event EventHandler<int>? DeleteConversationRequested;
        public event EventHandler? RemoveScreenshotRequested;
        public event EventHandler? OpenScreenshotRequested;

        [RelayCommand]
        private void SendMessage(string message)
        {
            SendRequested?.Invoke(this, message ?? string.Empty);
        }

        [RelayCommand]
        private void Screenshot()
        {
            ScreenshotRequested?.Invoke(this, EventArgs.Empty);
        }

        [RelayCommand]
        private void ImageUpload()
        {
            ImageUploadRequested?.Invoke(this, EventArgs.Empty);
        }

        [RelayCommand]
        private void ToggleSidebar()
        {
            ToggleSidebarRequested?.Invoke(this, EventArgs.Empty);
        }

        [RelayCommand]
        private void Close()
        {
            CloseRequested?.Invoke(this, EventArgs.Empty);
        }

        [RelayCommand]
        private void NewConversation()
        {
            NewConversationRequested?.Invoke(this, EventArgs.Empty);
        }

        [RelayCommand]
        private void CopyMessage(string? text)
        {
            if (text is null) text = string.Empty;
            CopyRequested?.Invoke(this, text);
        }

        [RelayCommand]
        private void PlayTts(string? text)
        {
            if (text is null) text = string.Empty;
            PlayTtsRequested?.Invoke(this, text);
        }

        [RelayCommand]
        private void DeleteConversation(int conversationId)
        {
            DeleteConversationRequested?.Invoke(this, conversationId);
        }

        [RelayCommand]
        private void RemoveScreenshot()
        {
            RemoveScreenshotRequested?.Invoke(this, EventArgs.Empty);
        }

        [RelayCommand]
        private void OpenScreenshot()
        {
            OpenScreenshotRequested?.Invoke(this, EventArgs.Empty);
        }
    }
}
