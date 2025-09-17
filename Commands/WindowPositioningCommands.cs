using System.Windows.Input;
using Buddie.Services;
using ICommand = System.Windows.Input.ICommand;

namespace Buddie.Commands
{
    /// <summary>
    /// 窗口定位相关命令
    /// </summary>
    public static class WindowPositioningCommands
    {
        /// <summary>
        /// 对齐到屏幕左上角的路由命令
        /// </summary>
        public static readonly RoutedUICommand AlignTopLeft = new RoutedUICommand(
            "Align Top Left",
            "AlignTopLeft",
            typeof(WindowPositioningCommands),
            new InputGestureCollection { new KeyGesture(Key.D7, ModifierKeys.Control) });

        /// <summary>
        /// 对齐到屏幕右上角的路由命令
        /// </summary>
        public static readonly RoutedUICommand AlignTopRight = new RoutedUICommand(
            "Align Top Right",
            "AlignTopRight",
            typeof(WindowPositioningCommands),
            new InputGestureCollection { new KeyGesture(Key.D9, ModifierKeys.Control) });

        /// <summary>
        /// 对齐到屏幕左下角的路由命令
        /// </summary>
        public static readonly RoutedUICommand AlignBottomLeft = new RoutedUICommand(
            "Align Bottom Left",
            "AlignBottomLeft",
            typeof(WindowPositioningCommands),
            new InputGestureCollection { new KeyGesture(Key.D1, ModifierKeys.Control) });

        /// <summary>
        /// 对齐到屏幕右下角的路由命令
        /// </summary>
        public static readonly RoutedUICommand AlignBottomRight = new RoutedUICommand(
            "Align Bottom Right",
            "AlignBottomRight",
            typeof(WindowPositioningCommands),
            new InputGestureCollection { new KeyGesture(Key.D3, ModifierKeys.Control) });

        /// <summary>
        /// 对齐到屏幕中央的路由命令
        /// </summary>
        public static readonly RoutedUICommand AlignCenter = new RoutedUICommand(
            "Align Center",
            "AlignCenter",
            typeof(WindowPositioningCommands),
            new InputGestureCollection { new KeyGesture(Key.D5, ModifierKeys.Control) });

        /// <summary>
        /// 切换到下一个显示器的路由命令
        /// </summary>
        public static readonly RoutedUICommand NextScreen = new RoutedUICommand(
            "Next Screen",
            "NextScreen",
            typeof(WindowPositioningCommands),
            new InputGestureCollection { new KeyGesture(Key.M, ModifierKeys.Control | ModifierKeys.Shift) });
    }
}