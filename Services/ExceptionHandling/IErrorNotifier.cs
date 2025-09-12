using System;

namespace Buddie.Services.ExceptionHandling
{
    public interface IErrorNotifier
    {
        void NotifyError(string message, Exception? exception = null, ExceptionHandlingService.ExceptionContext? context = null);
    }
}

