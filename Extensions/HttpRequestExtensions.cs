using System;
using System.Net.Http;
using HttpApiClient.Models;

namespace HttpApiClient.Extensions
{
    internal static class HttpRequestExtensions
    {
      
        public static TimeSpan? GetTimeout(this HttpRequestMessage request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (request.Properties.TryGetValue(PropertyKeys.TimeoutKey, out var value) && value is TimeSpan timeout) {
                return timeout;
            }
            return null;
        }

        public static void SetTimeout(this HttpRequestMessage request, TimeSpan? timeout)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            request.Properties[PropertyKeys.TimeoutKey] = timeout;
        }

        public static Polly.Context GetPolicyExecutionContext(this HttpRequestMessage request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (request.Properties.TryGetValue(PropertyKeys.PolicyExecutionContextKey, out var value) && value is Polly.Context context) {
                return context;
            }
            return null;
        }

        public static RetryInfo GetRetryInfo(this HttpRequestMessage request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (request.Properties.TryGetValue(PropertyKeys.RetryInfoKey, out var value) && value is RetryInfo retryInfo) {
                return retryInfo;
            }
            return null;
        }

        public static void SetRetryInfo(this HttpRequestMessage request, RetryInfo retryInfo)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            request.Properties[PropertyKeys.RetryInfoKey] = retryInfo;
        }

        public static string GetResourcePath(this HttpRequestMessage request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (request.Properties.TryGetValue(PropertyKeys.ResourcePath, out var value) && value is string resourcePath) {
                return resourcePath;
            }
            return null;
        }

        public static void SetResourcePath(this HttpRequestMessage request, string resourcePath)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            request.Properties[PropertyKeys.ResourcePath] = resourcePath;
        }

        public static string GetOriginalRequestUrl(this HttpRequestMessage request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (request.Properties.TryGetValue(PropertyKeys.OriginalRequestUrl, out var value) && value is string originalRequestUrl) {
                return originalRequestUrl;
            }
            return null;
        }

        public static void SetOriginalRequestUrl(this HttpRequestMessage request, string originalRequestUrl)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            request.Properties[PropertyKeys.OriginalRequestUrl] = originalRequestUrl;
        }

        public static string GetOriginalRequestMethod(this HttpRequestMessage request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (request.Properties.TryGetValue(PropertyKeys.OriginalRequestMethod, out var value) && value is string originalRequestMethod) {
                return originalRequestMethod;
            }
            return null;
        }

        public static void SetOriginalRequestMethod(this HttpRequestMessage request, string originalRequestMethod)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            request.Properties[PropertyKeys.OriginalRequestMethod] = originalRequestMethod;
        }
    }
}
