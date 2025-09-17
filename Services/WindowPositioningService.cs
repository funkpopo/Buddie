using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Interop;
using Microsoft.Extensions.Logging;

namespace Buddie.Services
{
    /// <summary>
    /// 窗口定位服务接口
    /// </summary>
    public interface IWindowPositioningService
    {
        /// <summary>
        /// 屏幕角位置枚举
        /// </summary>
        public enum ScreenCorner
        {
            TopLeft,
            TopRight,
            BottomLeft,
            BottomRight,
            Center
        }

        /// <summary>
        /// 获取所有可用的显示器信息
        /// </summary>
        IEnumerable<Screen> GetAllScreens();

        /// <summary>
        /// 获取窗口当前所在的显示器
        /// </summary>
        Screen GetWindowScreen(Window window);

        /// <summary>
        /// 将窗口对齐到指定显示器的指定角落
        /// </summary>
        void AlignWindowToScreen(Window window, Screen screen, ScreenCorner corner, int margin = 20);

        /// <summary>
        /// 将窗口对齐到当前显示器的指定角落
        /// </summary>
        void AlignWindowToCurrentScreen(Window window, ScreenCorner corner, int margin = 20);

        /// <summary>
        /// 智能定位窗口（避免遮挡任务栏）
        /// </summary>
        void SmartPositionWindow(Window window, Screen screen, double preferredX, double preferredY);

        /// <summary>
        /// 获取推荐的对话框位置（相对于父窗口）
        /// </summary>
        (double x, double y) GetDialogPosition(Window parentWindow, double dialogWidth, double dialogHeight, ScreenCorner preferredCorner = ScreenCorner.Center);

        /// <summary>
        /// 确保窗口在屏幕可见区域内
        /// </summary>
        void EnsureWindowInScreen(Window window);
    }

    /// <summary>
    /// 窗口定位服务实现
    /// </summary>
    public class WindowPositioningService : IWindowPositioningService
    {
        private readonly ILogger<WindowPositioningService> _logger;
        private readonly IScreenService _screenService;

        public WindowPositioningService(ILogger<WindowPositioningService> logger, IScreenService screenService)
        {
            _logger = logger;
            _screenService = screenService;
        }

        public IEnumerable<Screen> GetAllScreens()
        {
            return Screen.AllScreens;
        }

        public Screen GetWindowScreen(Window window)
        {
            try
            {
                var windowBounds = _screenService.GetCurrentWindowScreen(window);

                // 找到包含窗口中心点的显示器
                var centerX = windowBounds.X + windowBounds.Width / 2;
                var centerY = windowBounds.Y + windowBounds.Height / 2;

                foreach (var screen in Screen.AllScreens)
                {
                    if (screen.Bounds.Contains(centerX, centerY))
                    {
                        return screen;
                    }
                }

                // 如果找不到，返回最近的显示器
                return Screen.AllScreens
                    .OrderBy(s => CalculateDistance(centerX, centerY, s.Bounds))
                    .FirstOrDefault() ?? Screen.PrimaryScreen ?? Screen.AllScreens[0];
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取窗口所在显示器失败");
                return Screen.PrimaryScreen ?? Screen.AllScreens[0];
            }
        }

        public void AlignWindowToScreen(Window window, Screen screen, IWindowPositioningService.ScreenCorner corner, int margin = 20)
        {
            var workingArea = screen.WorkingArea; // 使用工作区域（排除任务栏）
            var windowWidth = window.Width > 0 ? window.Width : window.ActualWidth;
            var windowHeight = window.Height > 0 ? window.Height : window.ActualHeight;

            double left = 0;
            double top = 0;

            switch (corner)
            {
                case IWindowPositioningService.ScreenCorner.TopLeft:
                    left = workingArea.Left + margin;
                    top = workingArea.Top + margin;
                    break;

                case IWindowPositioningService.ScreenCorner.TopRight:
                    left = workingArea.Right - windowWidth - margin;
                    top = workingArea.Top + margin;
                    break;

                case IWindowPositioningService.ScreenCorner.BottomLeft:
                    left = workingArea.Left + margin;
                    top = workingArea.Bottom - windowHeight - margin;
                    break;

                case IWindowPositioningService.ScreenCorner.BottomRight:
                    left = workingArea.Right - windowWidth - margin;
                    top = workingArea.Bottom - windowHeight - margin;
                    break;

                case IWindowPositioningService.ScreenCorner.Center:
                    left = workingArea.Left + (workingArea.Width - windowWidth) / 2;
                    top = workingArea.Top + (workingArea.Height - windowHeight) / 2;
                    break;
            }

            window.Left = left;
            window.Top = top;

            _logger.LogDebug("窗口已对齐到 {Corner} 位置: ({Left}, {Top})", corner, left, top);
        }

        public void AlignWindowToCurrentScreen(Window window, IWindowPositioningService.ScreenCorner corner, int margin = 20)
        {
            var currentScreen = GetWindowScreen(window);
            AlignWindowToScreen(window, currentScreen, corner, margin);
        }

        public void SmartPositionWindow(Window window, Screen screen, double preferredX, double preferredY)
        {
            var workingArea = screen.WorkingArea;
            var windowWidth = window.Width > 0 ? window.Width : window.ActualWidth;
            var windowHeight = window.Height > 0 ? window.Height : window.ActualHeight;

            // 确保窗口不超出工作区域
            var left = Math.Max(workingArea.Left, Math.Min(preferredX, workingArea.Right - windowWidth));
            var top = Math.Max(workingArea.Top, Math.Min(preferredY, workingArea.Bottom - windowHeight));

            // 检查是否会遮挡任务栏
            if (screen.Bounds.Bottom != workingArea.Bottom) // 任务栏在底部
            {
                top = Math.Min(top, workingArea.Bottom - windowHeight - 10);
            }
            if (screen.Bounds.Top != workingArea.Top) // 任务栏在顶部
            {
                top = Math.Max(top, workingArea.Top + 10);
            }
            if (screen.Bounds.Left != workingArea.Left) // 任务栏在左侧
            {
                left = Math.Max(left, workingArea.Left + 10);
            }
            if (screen.Bounds.Right != workingArea.Right) // 任务栏在右侧
            {
                left = Math.Min(left, workingArea.Right - windowWidth - 10);
            }

            window.Left = left;
            window.Top = top;

            _logger.LogDebug("智能定位窗口到: ({Left}, {Top})", left, top);
        }

        public (double x, double y) GetDialogPosition(Window parentWindow, double dialogWidth, double dialogHeight, IWindowPositioningService.ScreenCorner preferredCorner = IWindowPositioningService.ScreenCorner.Center)
        {
            var screen = GetWindowScreen(parentWindow);
            var workingArea = screen.WorkingArea;

            double x = 0;
            double y = 0;

            if (preferredCorner == IWindowPositioningService.ScreenCorner.Center)
            {
                // 相对于父窗口居中
                x = parentWindow.Left + (parentWindow.ActualWidth - dialogWidth) / 2;
                y = parentWindow.Top + (parentWindow.ActualHeight - dialogHeight) / 2;
            }
            else
            {
                // 相对于父窗口的指定角落
                switch (preferredCorner)
                {
                    case IWindowPositioningService.ScreenCorner.TopLeft:
                        x = parentWindow.Left + 20;
                        y = parentWindow.Top + 20;
                        break;

                    case IWindowPositioningService.ScreenCorner.TopRight:
                        x = parentWindow.Left + parentWindow.ActualWidth - dialogWidth - 20;
                        y = parentWindow.Top + 20;
                        break;

                    case IWindowPositioningService.ScreenCorner.BottomLeft:
                        x = parentWindow.Left + 20;
                        y = parentWindow.Top + parentWindow.ActualHeight - dialogHeight - 20;
                        break;

                    case IWindowPositioningService.ScreenCorner.BottomRight:
                        x = parentWindow.Left + parentWindow.ActualWidth - dialogWidth - 20;
                        y = parentWindow.Top + parentWindow.ActualHeight - dialogHeight - 20;
                        break;
                }
            }

            // 确保对话框在工作区域内
            x = Math.Max(workingArea.Left, Math.Min(x, workingArea.Right - dialogWidth));
            y = Math.Max(workingArea.Top, Math.Min(y, workingArea.Bottom - dialogHeight));

            return (x, y);
        }

        public void EnsureWindowInScreen(Window window)
        {
            var screen = GetWindowScreen(window);
            var workingArea = screen.WorkingArea;
            var windowWidth = window.Width > 0 ? window.Width : window.ActualWidth;
            var windowHeight = window.Height > 0 ? window.Height : window.ActualHeight;

            var adjusted = false;

            // 检查并调整左边界
            if (window.Left < workingArea.Left)
            {
                window.Left = workingArea.Left;
                adjusted = true;
            }
            // 检查并调整右边界
            else if (window.Left + windowWidth > workingArea.Right)
            {
                window.Left = workingArea.Right - windowWidth;
                adjusted = true;
            }

            // 检查并调整上边界
            if (window.Top < workingArea.Top)
            {
                window.Top = workingArea.Top;
                adjusted = true;
            }
            // 检查并调整下边界
            else if (window.Top + windowHeight > workingArea.Bottom)
            {
                window.Top = workingArea.Bottom - windowHeight;
                adjusted = true;
            }

            if (adjusted)
            {
                _logger.LogDebug("窗口位置已调整到可见区域: ({Left}, {Top})", window.Left, window.Top);
            }
        }

        private double CalculateDistance(int x, int y, Rectangle bounds)
        {
            var centerX = bounds.X + bounds.Width / 2;
            var centerY = bounds.Y + bounds.Height / 2;
            return Math.Sqrt(Math.Pow(x - centerX, 2) + Math.Pow(y - centerY, 2));
        }
    }
}