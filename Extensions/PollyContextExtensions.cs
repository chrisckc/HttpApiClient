using System;
using HttpApiClient.Models;

namespace HttpApiClient.Extensions
{
    internal static class PollyContextExtensions
    {
        public static RetryInfo GetRetryInfo(this Polly.Context context) {
            if (context == null) throw new ArgumentNullException(nameof(context));
            object retryInfo = null;
            context.TryGetValue(PropertyKeys.RetryInfoKey, out retryInfo);
            if (retryInfo != null && retryInfo is RetryInfo) {
                return (RetryInfo)retryInfo;
            }
            return null; 
        }

        public static void SetRetryInfo(this Polly.Context context, RetryInfo retryInfo) {
            if (context == null) throw new ArgumentNullException(nameof(context));
            context[PropertyKeys.RetryInfoKey] = retryInfo;
        }
    }
}
