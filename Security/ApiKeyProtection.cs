using System;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Buddie.Security
{
    public static class ApiKeyProtection
    {
        private static ILogger Logger => ((Buddie.App.Services?.GetService(typeof(ILoggerFactory)) as ILoggerFactory)?.CreateLogger(typeof(ApiKeyProtection).FullName!)) ?? NullLogger.Instance;
        private static readonly byte[] _additionalEntropy = 
            { 0x42, 0x75, 0x64, 0x64, 0x69, 0x65, 0x41, 0x70, 0x69, 0x4B, 0x65, 0x79 }; // "BuddieApiKey"

        /// <summary>
        /// 使用 Windows DPAPI 加密 API Key
        /// </summary>
        public static string Protect(string? apiKey)
        {
            if (string.IsNullOrEmpty(apiKey))
                return string.Empty;

            try
            {
                byte[] plainBytes = Encoding.UTF8.GetBytes(apiKey);
                byte[] protectedBytes = ProtectedData.Protect(
                    plainBytes, 
                    _additionalEntropy, 
                    DataProtectionScope.CurrentUser);
                
                return Convert.ToBase64String(protectedBytes);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to protect API key: {Message}", ex.Message);
                // 如果加密失败，返回原始值（向后兼容）
                return apiKey;
            }
        }

        /// <summary>
        /// 使用 Windows DPAPI 解密 API Key
        /// </summary>
        public static string Unprotect(string? protectedApiKey)
        {
            if (string.IsNullOrEmpty(protectedApiKey))
                return string.Empty;

            try
            {
                // 如果不是 Base64 格式，可能是未加密的旧数据
                if (!IsBase64String(protectedApiKey))
                    return protectedApiKey;

                byte[] protectedBytes = Convert.FromBase64String(protectedApiKey);
                byte[] plainBytes = ProtectedData.Unprotect(
                    protectedBytes, 
                    _additionalEntropy, 
                    DataProtectionScope.CurrentUser);
                
                return Encoding.UTF8.GetString(plainBytes);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to unprotect API key: {Message}", ex.Message);
                // 如果解密失败，可能是未加密的旧数据，返回原始值
                return protectedApiKey;
            }
        }

        /// <summary>
        /// 掩码 API Key 用于日志和显示
        /// </summary>
        public static string Mask(string? apiKey)
        {
            if (string.IsNullOrEmpty(apiKey))
                return string.Empty;

            // 对于短 key，显示前2个字符
            if (apiKey.Length <= 8)
                return apiKey.Length > 2 
                    ? $"{apiKey.Substring(0, 2)}{"*".PadRight(apiKey.Length - 2, '*')}" 
                    : new string('*', apiKey.Length);

            // 对于长 key，显示前4个和后4个字符
            return $"{apiKey.Substring(0, 4)}{"*".PadRight(8, '*')}{apiKey.Substring(apiKey.Length - 4)}";
        }

        /// <summary>
        /// 用于导出时的深度掩码（只显示长度信息）
        /// </summary>
        public static string DeepMask(string? apiKey)
        {
            if (string.IsNullOrEmpty(apiKey))
                return string.Empty;

            return $"[MASKED:{apiKey.Length} chars]";
        }

        /// <summary>
        /// 检查是否为有效的 Base64 字符串
        /// </summary>
        private static bool IsBase64String(string s)
        {
            if (string.IsNullOrEmpty(s))
                return false;

            try
            {
                Convert.FromBase64String(s);
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 判断字符串是否已加密
        /// </summary>
        public static bool IsProtected(string? value)
        {
            if (string.IsNullOrEmpty(value))
                return false;

            // 检查是否为 Base64 格式且能够解密
            if (!IsBase64String(value))
                return false;

            try
            {
                byte[] protectedBytes = Convert.FromBase64String(value);
                // 尝试解密，如果成功则说明已加密
                ProtectedData.Unprotect(protectedBytes, _additionalEntropy, DataProtectionScope.CurrentUser);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
