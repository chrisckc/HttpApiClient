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

        public static string GetResourcePath(this Exception exception)
        {
            if (exception == null) throw new ArgumentNullException(nameof(exception));
            return (string)exception.Data[PropertyKeys.ResourcePath];
        }

        public static void SetResourcePath(this Exception exception, string resourcePath)
        {
            if (exception == null) throw new ArgumentNullException(nameof(exception));
            exception.Data[PropertyKeys.ResourcePath] = resourcePath;
        }

        public static string GetRequestUrl(this Exception exception)
        {
            if (exception == null) throw new ArgumentNullException(nameof(exception));
            return (string)exception.Data[PropertyKeys.RequestUrl];
        }

        public static void SetRequestUrl(this Exception exception, string url)
        {
            if (exception == null) throw new ArgumentNullException(nameof(exception));
            exception.Data[PropertyKeys.RequestUrl] = url;
        }

        public static string GetOriginalRequestUrl(this Exception exception)
        {
            if (exception == null) throw new ArgumentNullException(nameof(exception));
            return (string)exception.Data[PropertyKeys.OriginalRequestUrl];
        }

        public static void SetOriginalRequestUrl(this Exception exception, string url)
        {
            if (exception == null) throw new ArgumentNullException(nameof(exception));
            exception.Data[PropertyKeys.OriginalRequestUrl] = url;
        }

        public static string GetRequestMethod(this Exception exception)
        {
            if (exception == null) throw new ArgumentNullException(nameof(exception));
            return (string)exception.Data[PropertyKeys.RequestMethod];
        }

        public static void SetRequestMethod(this Exception exception, string method)
        {
            if (exception == null) throw new ArgumentNullException(nameof(exception));
            exception.Data[PropertyKeys.RequestMethod] = method;
        }

        public static string GetOriginalRequestMethod(this Exception exception)
        {
            if (exception == null) throw new ArgumentNullException(nameof(exception));
            return (string)exception.Data[PropertyKeys.OriginalRequestMethod];
        }

        public static void SetOriginalRequestMethod(this Exception exception, string method)
        {
            if (exception == null) throw new ArgumentNullException(nameof(exception));
            exception.Data[PropertyKeys.OriginalRequestMethod] = method;
        }

        
    }
}
