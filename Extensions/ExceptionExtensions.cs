using System;
using HttpApiClient.Models;

namespace HttpApiClient.Extensions
{
    internal static class ExceptionExtensions
    {

        public static RetryInfo GetRetryInfo(this Exception exception)
        {
            if (exception == null) throw new ArgumentNullException(nameof(exception));
            return (RetryInfo)exception.Data[PropertyKeys.RetryInfoKey];
        }

        public static void SetRetryInfo(this Exception exception, RetryInfo retryInfo)
        {
            if (exception == null) throw new ArgumentNullException(nameof(exception));
            exception.Data[PropertyKeys.RetryInfoKey] = retryInfo;
        }
    }
}
