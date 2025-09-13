using System;

namespace Buddie.Services
{
    /// <summary>
    /// 提供窗口点击穿透功能的服务接口
    /// </summary>
    public interface IClickThroughService
    {
        /// <summary>
        /// 获取当前点击穿透状态
        /// </summary>
        bool IsClickThrough { get; }
        
        /// <summary>
        /// 启用或禁用点击穿透
        /// </summary>
        /// <param name="enable">是否启用点击穿透</param>
        void SetClickThrough(bool enable);
        
        /// <summary>
        /// 设置窗口句柄
        /// </summary>
        /// <param name="hwnd">窗口句柄</param>
        void SetWindowHandle(IntPtr hwnd);
    }
}