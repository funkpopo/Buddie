using System;
using System.Windows.Input;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Buddie.ViewModels
{
    public partial class CardControlViewModel : ObservableObject
    {
        [ObservableProperty]
        private string _frontText = string.Empty;

        [ObservableProperty]
        private string _frontSubText = string.Empty;

        [ObservableProperty]
        private string _backText = string.Empty;

        [ObservableProperty]
        private string _backSubText = string.Empty;

        [ObservableProperty]
        private Brush? _frontBackground;

        [ObservableProperty]
        private Brush? _backBackground;

        [ObservableProperty]
        private bool _isFlipped;

        [ObservableProperty]
        private bool _isDialogOpen;

        [ObservableProperty]
        private bool _isBuddieOpen;

        [ObservableProperty]
        private bool _isSettingsOpen;

        [ObservableProperty]
        private int _cardIndex;

        [ObservableProperty]
        private int _totalCards;

        [ObservableProperty]
        private OpenApiConfiguration? _apiConfiguration;

        // Commands passed from parent
        public ICommand? DialogCommand { get; set; }
        public ICommand? BuddieCommand { get; set; }
        public ICommand? SettingsCommand { get; set; }
        public ICommand? PreviousCardCommand { get; set; }
        public ICommand? NextCardCommand { get; set; }

        public CardControlViewModel()
        {
        }

        public CardControlViewModel(CardData cardData, int index, int total)
        {
            UpdateFromCardData(cardData, index, total);
        }

        public void UpdateFromCardData(CardData cardData, int index, int total)
        {
            FrontText = cardData.FrontText;
            FrontSubText = cardData.FrontSubText;
            BackText = cardData.BackText;
            BackSubText = cardData.BackSubText;
            FrontBackground = cardData.FrontBackground;
            BackBackground = cardData.BackBackground;
            ApiConfiguration = cardData.ApiConfiguration;
            CardIndex = index;
            TotalCards = total;
        }

        [RelayCommand]
        private void FlipCard()
        {
            IsFlipped = !IsFlipped;
        }

        [RelayCommand]
        private void ResetFlip()
        {
            IsFlipped = false;
        }

        public void SetOpenStates(bool dialogOpen, bool buddieOpen, bool settingsOpen)
        {
            IsDialogOpen = dialogOpen;
            IsBuddieOpen = buddieOpen;
            IsSettingsOpen = settingsOpen;
        }
    }
}