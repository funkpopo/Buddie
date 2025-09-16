using System;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Extensions.Logging;

namespace Buddie.Services
{
    public class ScreenService : IScreenService
    {
        private readonly ILogger<ScreenService> _logger;

        public ScreenService(ILogger<ScreenService> logger)
        {
            _logger = logger;
        }

        #region Win32 API
        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

        [DllImport("user32.dll")]
        private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

        private const uint MONITOR_DEFAULTTONEAREST = 2;

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct MONITORINFO
        {
            public int cbSize;
            public RECT rcMonitor;
            public RECT rcWork;
            public uint dwFlags;
        }
        #endregion

        public Rectangle GetCurrentWindowScreen(Window? window)
        {
            try
            {
                if (window != null)
                {
                    var byPosition = GetWindowScreenByPosition(window);
                    if (byPosition.Width > 0 && byPosition.Height > 0)
                    {
                        _logger.LogDebug("ScreenService: detected by position = {Bounds}", byPosition);
                        return byPosition;
                    }

                    var interop = new System.Windows.Interop.WindowInteropHelper(window);
                    var hwnd = interop.EnsureHandle();
                    if (hwnd != IntPtr.Zero)
                    {
                        var hMonitor = MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);
                        if (hMonitor != IntPtr.Zero)
                        {
                            var mi = new MONITORINFO { cbSize = Marshal.SizeOf(typeof(MONITORINFO)) };
                            if (GetMonitorInfo(hMonitor, ref mi))
                            {
                                var screenBounds = new Rectangle(
                                    mi.rcMonitor.Left,
                                    mi.rcMonitor.Top,
                                    mi.rcMonitor.Right - mi.rcMonitor.Left,
                                    mi.rcMonitor.Bottom - mi.rcMonitor.Top);
                                if (screenBounds.Width > 0 && screenBounds.Height > 0)
                                {
                                    _logger.LogDebug("ScreenService: detected by Win32 = {Bounds}", screenBounds);
                                    return screenBounds;
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ScreenService: error detecting window screen: {Message}", ex.Message);
            }

            _logger.LogInformation("ScreenService: fallback to primary screen");
            return GetPrimaryScreenBounds();
        }

        public Rectangle GetPrimaryScreenBounds()
        {
            var primary = System.Windows.Forms.Screen.PrimaryScreen;
            return primary?.Bounds ?? new Rectangle(0, 0, 1920, 1080);
        }

        public Task<byte[]?> CaptureScreenAsync(Rectangle screenBounds)
        {
            return Task.Run<byte[]?>(() =>
            {
                try
                {
                    using var bitmap = new Bitmap(screenBounds.Width, screenBounds.Height);
                    using var g = Graphics.FromImage(bitmap);
                    g.CopyFromScreen(screenBounds.X, screenBounds.Y, 0, 0, screenBounds.Size);
                    using var ms = new MemoryStream();
                    bitmap.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                    return ms.ToArray();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "ScreenService: capture failed: {Message}", ex.Message);
                    return null;
                }
            });
        }

        private Rectangle GetWindowScreenByPosition(Window window)
        {
            try
            {
                var left = window.Left;
                var top = window.Top;
                var w = window.ActualWidth;
                var h = window.ActualHeight;
                if (w <= 0 || h <= 0)
                {
                    w = 100; h = 100;
                }
                var cx = (int)(left + w / 2);
                var cy = (int)(top + h / 2);

                foreach (var screen in System.Windows.Forms.Screen.AllScreens)
                {
                    if (screen.Bounds.Contains(cx, cy))
                    {
                        return screen.Bounds;
                    }
                }

                var nearest = System.Windows.Forms.Screen.AllScreens
                    .OrderBy(s => Distance(cx, cy, s.Bounds))
                    .FirstOrDefault();
                if (nearest != null) return nearest.Bounds;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ScreenService: position detection failed");
            }
            return new Rectangle();
        }

        private static double Distance(int x, int y, Rectangle r)
        {
            var cx = r.X + r.Width / 2;
            var cy = r.Y + r.Height / 2;
            return Math.Sqrt(Math.Pow(x - cx, 2) + Math.Pow(y - cy, 2));
        }
    }
}

