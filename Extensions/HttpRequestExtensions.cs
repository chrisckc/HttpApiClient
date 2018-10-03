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
    }
}
