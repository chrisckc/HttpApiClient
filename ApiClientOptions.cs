using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using HttpApiClient.ErrorParsers;

namespace HttpApiClient
{
    public class ApiClientOptions<TClient> where TClient : class
    {
        public Uri BaseUrl { get; set; }
        public string BasicAuthUsername { get; set; } // Basic Authentication Scheme username
        public string BasicAuthPassword { get; set; } // Basic Authentication Scheme username
        public string BearerToken { get; set; } // Bearer Access Token
        public string UserAgent { get; set; } // The UserAgent string sent in the request
        public double? RequestTimeout { get; set; } // Request timeout in Seconds
        public int RetryCount { get; set; } // Setting this to zero disables retries
        public double? RetryWaitDuration { get; set; } // How long to wait on retries in Seconds (only first retry if UseExponentialRetryWaitDuration enabled)
        public int? RetryJitterDuration { get; set; } // Milliseconds
        public double? DefaultTooManyRequestsRetryDuration { get; set; } // Seconds
        public bool? UseExponentialRetryWaitDuration { get; set; } // Use Optimistic concurrency control (OCC) style increasing back-off (wait) and retries
        public bool? AlwaysPopulateResponseBody { get; set; } // Always put the response body into the ApiResponse.Body, even when content-type is application/json
        public List<HttpStatusCode> HttpStatusCodesToRetry { get; set; } // List of Http Status Codes to Retry on
        public List<HttpMethod> HttpMethodsToRetry { get; set; } // List of Http Methods to enable Retries for
        public List<IKnownErrorParser<TClient>> KnownErrorParsers { get; set; } // KnownErrorParsers to use when parsing errors returned from the Api
    }
}