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

            // Build initial cards and keep in sync with settings changes
            BuildCardsFromAppSettings();
            if (AppSettings.ApiConfigurations is System.Collections.Specialized.INotifyCollectionChanged incc)
            {
                incc.CollectionChanged += (s, e) => BuildCardsFromAppSettings();
            }

            if (Cards.Count > 0)
            {
                CurrentCardIndex = 0;
            }
        }

        public void BuildCardsFromAppSettings()
        {
            Cards.Clear();
            var colorMgr = new CardColorManager();

            if (AppSettings.ApiConfigurations.Count > 0)
            {
                var pairs = colorMgr.GetMultipleColorPairs(AppSettings.ApiConfigurations.Count);
                for (int i = 0; i < AppSettings.ApiConfigurations.Count; i++)
                {
                    var cfg = AppSettings.ApiConfigurations[i];
                    var (frontColor, backColor) = pairs[i];

                    Cards.Add(new CardData
                    {
                        FrontText = cfg.Name,
                        FrontSubText = cfg.ModelName,
                        BackText = "API配置",
                        BackSubText = cfg.ChannelType.ToString(),
                        FrontBackground = colorMgr.CreateGradientBrush(frontColor),
                        BackBackground = colorMgr.CreateGradientBrush(backColor),
                        ApiConfiguration = cfg
                    });
                }
            }

            if (Cards.Count == 0)
            {
                var (frontColor, backColor) = colorMgr.GetColorPair();
                Cards.Add(new CardData
                {
                    FrontText = "欢迎使用",
                    FrontSubText = "点击设置添加AI配置",
                    BackText = "配置提示",
                    BackSubText = "在设置中添加OpenAI API配置后即可开始对话",
                    FrontBackground = colorMgr.CreateGradientBrush(frontColor),
                    BackBackground = colorMgr.CreateGradientBrush(backColor),
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
