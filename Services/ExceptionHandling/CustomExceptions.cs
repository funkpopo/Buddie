using System;

namespace Buddie.Services.ExceptionHandling
{
    /// <summary>
    /// 数据库操作异常
    /// </summary>
    public class DatabaseException : Exception
    {
        public DatabaseException(string message) : base(message) { }
        public DatabaseException(string message, Exception innerException) : base(message, innerException) { }
    }

    /// <summary>
    /// API配置异常
    /// </summary>
    public class ApiConfigurationException : Exception
    {
        public ApiConfigurationException(string message) : base(message) { }
        public ApiConfigurationException(string message, Exception innerException) : base(message, innerException) { }
    }

    /// <summary>
    /// 音频处理异常
    /// </summary>
    public class AudioProcessingException : Exception
    {
        public AudioProcessingException(string message) : base(message) { }
        public AudioProcessingException(string message, Exception innerException) : base(message, innerException) { }
    }
}