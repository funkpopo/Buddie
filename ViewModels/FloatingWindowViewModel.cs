using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Buddie;
using Buddie.Services;

namespace Buddie.ViewModels
{
    public partial class FloatingWindowViewModel : ObservableObject
    {
        // App settings and services
        public AppSettings AppSettings { get; }

        private readonly RealtimeInteractionService _realtimeService;

        // Cards
        [ObservableProperty]
        private ObservableCollection<CardData> cards = new();

        [ObservableProperty]
        private int currentCardIndex;

        public CardData? CurrentCard => (CurrentCardIndex >= 0 && CurrentCardIndex < Cards.Count)
            ? Cards[CurrentCardIndex]
            : null;

        // UI state flags
        [ObservableProperty]
        private bool isDialogVisible;

        [ObservableProperty]
        private bool isSettingsVisible;

        [ObservableProperty]
        private bool isRealtimeOpen;

        public FloatingWindowViewModel(AppSettings appSettings, RealtimeInteractionService realtimeService)
        {
            AppSettings = appSettings;
            _realtimeService = realtimeService;

            BuildCardsFromAppSettings();

            if (Cards.Count > 0)
            {
                CurrentCardIndex = 0;
            }
        }

        public void BuildCardsFromAppSettings()
        {
            Cards.Clear();

            foreach (var cfg in AppSettings.ApiConfigurations)
            {
                var front = new LinearGradientBrush(Colors.LightBlue, Colors.White, new System.Windows.Point(0, 0), new System.Windows.Point(1, 1));
                var back = new LinearGradientBrush(Colors.LightCoral, Colors.White, new System.Windows.Point(0, 0), new System.Windows.Point(1, 1));

                Cards.Add(new CardData
                {
                    FrontText = cfg.Name,
                    FrontSubText = cfg.ModelName,
                    BackText = "",
                    BackSubText = cfg.ChannelType.ToString(),
                    FrontBackground = front,
                    BackBackground = back,
                    ApiConfiguration = cfg
                });
            }

            if (Cards.Count == 0)
            {
                Cards.Add(new CardData
                {
                    FrontText = "示例配置",
                    FrontSubText = "请先添加API配置",
                    BackText = "无可用配置",
                    BackSubText = "点击设置按钮添加",
                    FrontBackground = new LinearGradientBrush(Colors.LightBlue, Colors.White, new System.Windows.Point(0, 0), new System.Windows.Point(1, 1)),
                    BackBackground = new LinearGradientBrush(Colors.LightCoral, Colors.White, new System.Windows.Point(0, 0), new System.Windows.Point(1, 1)),
                    ApiConfiguration = null
                });
            }
        }

        [RelayCommand]
        private void NextCard()
        {
            if (Cards.Count == 0) return;
            CurrentCardIndex = (CurrentCardIndex + 1) % Cards.Count;
            OnPropertyChanged(nameof(CurrentCard));
        }

        [RelayCommand]
        private void PreviousCard()
        {
            if (Cards.Count == 0) return;
            CurrentCardIndex = (CurrentCardIndex - 1 + Cards.Count) % Cards.Count;
            OnPropertyChanged(nameof(CurrentCard));
        }

        [RelayCommand]
        private void ToggleDialog()
        {
            IsDialogVisible = !IsDialogVisible;
        }

        [RelayCommand]
        private void ToggleSettings()
        {
            IsSettingsVisible = !IsSettingsVisible;
        }

        [RelayCommand]
        private async Task ToggleRealtimeAsync()
        {
            try
            {
                if (!IsRealtimeOpen)
                {
                    var activeConfig = AppSettings.RealtimeConfigurations.FirstOrDefault(c => c.IsActive);
                    if (activeConfig == null)
                    {
                        // No active config: surface via state; view can respond
                        // Keeping IsRealtimeOpen false
                        // Optionally, raise a message via NotificationRequested in future
                        return;
                    }
                    await _realtimeService.StartAsync(activeConfig);
                    IsRealtimeOpen = true;
                }
                else
                {
                    await _realtimeService.StopAsync();
                    IsRealtimeOpen = false;
                }
            }
            catch
            {
                IsRealtimeOpen = false;
                throw;
            }
        }
    }
}
