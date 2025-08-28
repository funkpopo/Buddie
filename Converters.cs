using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace Buddie
{
    public class BooleanToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
                return boolValue ? Visibility.Visible : Visibility.Collapsed;
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is Visibility visibility)
                return visibility == Visibility.Visible;
            return false;
        }
    }

    public class InvertedBooleanToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
                return boolValue ? Visibility.Collapsed : Visibility.Visible;
            return Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is Visibility visibility)
                return visibility == Visibility.Collapsed;
            return true;
        }
    }

    public class BooleanToYesNoConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
                return boolValue ? "是" : "否";
            return "否";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string stringValue)
                return stringValue == "是";
            return false;
        }
    }

    public class BooleanToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
                return boolValue ? new SolidColorBrush(Colors.Green) : new SolidColorBrush(Colors.Gray);
            return new SolidColorBrush(Colors.Gray);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class ChannelTypeToNameConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is ChannelType channelType)
            {
                var channel = PresetChannels.GetPresetChannel(channelType);
                return channel.Name;
            }
            return "未知渠道";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class TtsChannelTypeToNameConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is TtsChannelType ttsChannelType)
            {
                var channel = TtsPresetChannels.GetPresetChannel(ttsChannelType);
                return channel.Name;
            }
            return "未知TTS渠道";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class ComboBoxItemToTtsChannelTypeConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is TtsChannelType ttsChannelType)
            {
                return ttsChannelType.ToString();
            }
            return value?.ToString() ?? string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string stringValue && Enum.TryParse<TtsChannelType>(stringValue, out var result))
            {
                return result;
            }
            return TtsChannelType.OpenAI; // 默认值
        }
    }

    public class ThemeToMessageBackgroundConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isDarkTheme && parameter is string messageType)
            {
                if (messageType == "user")
                {
                    // 用户消息背景在任何主题下都是蓝色
                    return new SolidColorBrush(Color.FromRgb(0, 132, 255));
                }
                else if (messageType == "ai")
                {
                    // AI消息背景根据主题变化
                    return isDarkTheme 
                        ? new SolidColorBrush(Color.FromRgb(58, 58, 60))
                        : new SolidColorBrush(Color.FromRgb(240, 240, 240));
                }
                else if (messageType == "reasoning")
                {
                    // 思维过程背景根据主题变化
                    return isDarkTheme
                        ? new SolidColorBrush(Color.FromRgb(45, 45, 48))
                        : new SolidColorBrush(Color.FromRgb(255, 253, 235));
                }
            }
            return new SolidColorBrush(Colors.White);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class ThemeToMessageForegroundConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isDarkTheme && parameter is string messageType)
            {
                if (messageType == "user")
                {
                    // 用户消息文字在任何主题下都是白色（因为背景是蓝色）
                    return Brushes.White;
                }
                else if (messageType == "ai" || messageType == "reasoning")
                {
                    // AI消息和思维过程文字根据主题变化
                    return isDarkTheme ? Brushes.White : Brushes.Black;
                }
            }
            return Brushes.Black;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}