using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using HttpApiClient.ErrorParsers;

namespace HttpApiClient
{
    public interface IApiClientOptions<TClient> where TClient : class
    {
        Uri BaseUrl { get; set; }
        string BasicAuthUsername { get; set; }
        string BasicAuthPassword { get; set; }
        string BearerToken { get; set; }
        string UserAgent { get; set; }
        double? RequestTimeout { get; set; }
        int RetryCount { get; set; }
        double? RetryWaitDuration { get; set; }
        int? RetryJitterDuration { get; set; }
        double? DefaultTooManyRequestsRetryDuration { get; set; }
        bool? UseExponentialRetryWaitDuration { get; set; }
        bool? AlwaysPopulateResponseBody { get; set; }
        List<HttpStatusCode> HttpStatusCodesToRetry { get; set; }
        List<HttpMethod> HttpMethodsToRetry { get; set; }
        List<IKnownErrorParser<TClient>> KnownErrorParsers { get; set; }
    }
}