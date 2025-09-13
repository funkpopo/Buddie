using System;
using System.Runtime.InteropServices;

namespace Buddie.Services
{
    /// <summary>
    /// 窗口点击穿透服务的实现
    /// </summary>
    public class ClickThroughService : IClickThroughService
    {
        #region Windows API
        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hwnd, int index);
        
        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hwnd, int index, int newStyle);
        
        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_TRANSPARENT = 0x00000020;
        private const int WS_EX_LAYERED = 0x00080000;
        #endregion

        private IntPtr _hwnd;
        private bool _isClickThrough;

        public bool IsClickThrough => _isClickThrough;

        public void SetWindowHandle(IntPtr hwnd)
        {
            _hwnd = hwnd;
        }

        public void SetClickThrough(bool enable)
        {
            if (_hwnd == IntPtr.Zero)
                return;

            var extendedStyle = GetWindowLong(_hwnd, GWL_EXSTYLE);
            
            if (enable)
            {
                SetWindowLong(_hwnd, GWL_EXSTYLE, extendedStyle | WS_EX_TRANSPARENT | WS_EX_LAYERED);
            }
            else
            {
                SetWindowLong(_hwnd, GWL_EXSTYLE, extendedStyle & ~WS_EX_TRANSPARENT);
            }
            
            _isClickThrough = enable;
        }
    }
}