using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Retry;
using HttpApiClient.Extensions;
using HttpApiClient.Handlers;
using HttpApiClient.Models;

namespace HttpApiClient
{
    internal class ApiClientBuilder<TClient> where TClient : class
    {
        private readonly ILogger _logger;
        private readonly ApiClientOptions<TClient> _options;
        private IServiceCollection _services { get; }

        public ApiClientBuilder(IServiceCollection services, ApiClientOptions<TClient> options, ILogger logger)
        {
            if (services == null) throw new ArgumentNullException(nameof(services));
            if (options == null) throw new ArgumentNullException(nameof(options));
            _services = services;
            _options = options;
            _logger = logger;
            SetDefaults();
        }

        private void SetDefaults() {
            // This should be done in the ApiClientOptions constructor
            // However it's easier to check the correct operation of Configuration middleware done this way
            if (string.IsNullOrEmpty(_options.UserAgent)) {
                _options.UserAgent = "HttpClient";
            }
            if (!_options.RequestTimeout.HasValue) {
                _options.RequestTimeout = 60; // use 60 seconds rather than the HttpClient default of 100 seconds
            }
            if (!_options.AllowAutoRedirect.HasValue) {
                _options.AllowAutoRedirect = true; // same default as the HttpClientHandler
            }
            if (!_options.RetryWaitDuration.HasValue) {
                _options.RetryWaitDuration = 4;  // 4 seconds
            }
            if (!_options.RetryJitterDuration.HasValue) {
                _options.RetryJitterDuration = 100; // 100 milliseconds
            }
            if (!_options.DefaultTooManyRequestsRetryDuration.HasValue) {
                _options.DefaultTooManyRequestsRetryDuration = 60;  // 60 seconds
            }
            if (!_options.UseExponentialRetryWaitDuration.HasValue) {
                _options.UseExponentialRetryWaitDuration = false;
            }
            if (!_options.AlwaysPopulateResponseBody.HasValue) {
                _options.AlwaysPopulateResponseBody = false;
            }
            // Set a sensible list of defaults for status codes to retry on
            if (_options.HttpStatusCodesToRetry ==  null) {
                _options.HttpStatusCodesToRetry = new List<HttpStatusCode>() {
                    HttpStatusCode.RequestTimeout, // 408
                    (HttpStatusCode)429, // 429  // TooManyRequests not available in .NET Standard
                    HttpStatusCode.InternalServerError, // 500
                    HttpStatusCode.BadGateway, // 502
                    HttpStatusCode.ServiceUnavailable, // 503
                    HttpStatusCode.GatewayTimeout // 504
                };
            }
            // Set a sensible list of defaults for http methods to retry on
            // These are the only safe defaults unless familiar with the remote API behaviour
            if (_options.HttpMethodsToRetry ==  null) {
                _options.HttpMethodsToRetry = new List<HttpMethod>() {
                    HttpMethod.Get,
                    HttpMethod.Delete,
                    HttpMethod.Options,
                    HttpMethod.Head,
                    HttpMethod.Trace
                };
            }
        }

        public IHttpClientBuilder ConfigureApiClient() {
            // Create the custom HttpClientHandler
            var handler = new HttpClientHandler() {
                UseCookies = false, // allows Cookie to be set via DefaultRequestHeaders
                AllowAutoRedirect = _options.AllowAutoRedirect.Value,
                AutomaticDecompression = DecompressionMethods.Deflate | DecompressionMethods.GZip
            };
            if (_options.IgnoreServerCertificateErrors) {
                handler.ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => { return true; };
            }

            // Add the HttpClient
            return _services.AddHttpClient<TClient>(client => {
                client.BaseAddress = _options.BaseUrl;
                client.DefaultRequestHeaders.Add("Accept", "application/json");
                client.DefaultRequestHeaders.Add("User-Agent", _options.UserAgent);
                if (!string.IsNullOrEmpty(_options.BasicAuthUsername)) {
                    string basicAuth = string.Format("{0}:{1}", _options.BasicAuthUsername, _options.BasicAuthPassword);
                    string encodedAuth = Convert.ToBase64String(System.Text.ASCIIEncoding.ASCII.GetBytes(basicAuth));
                    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", encodedAuth);
                }
                if (!string.IsNullOrEmpty(_options.BearerToken)) {
                    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _options.BearerToken);
                }
                if (!string.IsNullOrEmpty(_options.Cookie)) {
                    client.DefaultRequestHeaders.Add("Cookie", _options.Cookie);
                }
                client.Timeout = Timeout.InfiniteTimeSpan; // Required when using the TimeoutHandler     
            })
            // Add the Timeout Handler so we can determine when a request actually times out
            // AutomaticDecompression, on .NET Core 2.0, the default is now DecompressionMethods.None;
            // Note about cookies: https://github.com/Microsoft/dotnet/issues/395
            .ConfigurePrimaryHttpMessageHandler(() => new TimeoutHandler(_logger) {
                DefaultTimeout = TimeSpan.FromSeconds(_options.RequestTimeout.Value),
                InnerHandler = handler
            })
            // Configure Polly
            //.AddPolicyHandler(waitAndRetryPolicy)
            //.AddPolicyHandler(request => request.Method == HttpMethod.Post ? GetWaitAndRetryPolicy() : GetNoOpPolicy());
            .AddPolicyHandler((httpRequestMessage) =>
            {
                if (_options.HttpMethodsToRetry.Contains(httpRequestMessage.Method)) {
                    return GetWaitAndRetryPolicy();
                }
                return GetNoOpPolicy();
            });
        }

        private IAsyncPolicy<HttpResponseMessage> GetNoOpPolicy() {
            return Policy.NoOpAsync().AsAsyncPolicy<HttpResponseMessage>();
        }

        private RetryPolicy<HttpResponseMessage> GetWaitAndRetryPolicy() {
            //int jittererMaxDelay = 100; // Maximum delay to add in Milliseconds
            Random jitterer = new Random();
            // Handle exceptions and a set of http status codes in one policy definition
            return Policy.Handle<TimeoutException>()
                .Or<HttpRequestException>()
                .OrResult<HttpResponseMessage>(r => _options.HttpStatusCodesToRetry.Contains(r.StatusCode))
                .WaitAndRetryAsync(
                    retryCount: _options.RetryCount,
                    sleepDurationProvider: (retryAttempt, result, context) => {
                        // result.Result will be null if the cause of the retry was an exception
                        if (result.Result != null && result.Result.StatusCode == (HttpStatusCode)429) {
                            // If there is no Retry-After Header, the timespan will be zero
                            TimeSpan retryAfter = GetServerWaitDuration(result.Result?.Headers);
                            // Use either the server specified wait duration or the DefaultTooManyRequestsRetryDuration.
                            var waitDuration = Math.Max(TimeSpan.FromSeconds(_options.DefaultTooManyRequestsRetryDuration.Value).TotalMilliseconds, retryAfter.TotalMilliseconds);
                            return TimeSpan.FromMilliseconds(waitDuration) + TimeSpan.FromMilliseconds(jitterer.Next(0, _options.RetryJitterDuration.Value));
                        } else {
                            if (_options.UseExponentialRetryWaitDuration.Value) {
                                // Otherwise just use an exponential wait based on the RetryWaitDuration and retryAttempt
                                // RetryWaitDuration to the power of retryAttempt and jitter duration to the power of 3
                                int jitterMaxDelay = _options.RetryJitterDuration.Value * retryAttempt * retryAttempt * retryAttempt;
                                return TimeSpan.FromSeconds(Math.Pow(_options.RetryWaitDuration.Value, retryAttempt)) +
                                TimeSpan.FromMilliseconds(jitterer.Next(0, jitterMaxDelay));
                            } else {
                                return TimeSpan.FromSeconds(_options.RetryWaitDuration.Value) + TimeSpan.FromMilliseconds(jitterer.Next(0, _options.RetryJitterDuration.Value));
                            }
                        }
                    },
                    onRetryAsync : LogRetry()
                );

        }

        private Func<DelegateResult<HttpResponseMessage>, TimeSpan, int, Context, Task> LogRetry() {
            return async(result, timeSpan, retryAttempt, context) => {
                // Some nice logging
                var retryInfo = GetRetryInfo(result, timeSpan, retryAttempt);
                _logger.LogWarning(retryInfo.message);
                string contentType = null;
                string responseBody = null;
                if (result.Result != null) {
                    contentType = result.Result.Content?.Headers?.ContentType?.ToString();
                    responseBody = await result.Result.Content?.ReadAsStringAsync();
                    _logger.LogDebug($"ContentType: {contentType}\nResponseBody:\n{responseBody}");
                }
                // Store information about the retry attempts in the PolicyExecutionContext
                BuildRetryInfo(result, timeSpan, retryAttempt, context, retryInfo.reason, retryInfo.message, contentType, responseBody);
            };
        }

        // Get info about the retry event
        // Returns a Named Tuple https://docs.microsoft.com/en-us/dotnet/csharp/tuples
        private (string message, string reason) GetRetryInfo(DelegateResult<HttpResponseMessage> result, TimeSpan timeSpan, int retryAttempt) {
            string reason = null;
            string resourcePath = null;
            string requestUrl = null;
            string originalRequestUrl = null;
            string originalRequestMethod = null;
            if (result.Exception != null) {
                reason = $"Exception: {result.Exception.Message}";
                resourcePath = result.Exception.GetResourcePath();
                requestUrl = result.Exception.GetRequestUrl();
                originalRequestUrl = result.Exception.GetOriginalRequestUrl();
                originalRequestMethod = result.Exception.GetOriginalRequestMethod();
            } else if (result.Result != null) { //result.Result is a HttpResponseMessage
                reason = $"StatusCode: {(int)result.Result.StatusCode} ({result.Result.StatusCode.ToString()})";
                resourcePath = result.Result.RequestMessage.GetResourcePath();
                requestUrl = result.Result?.RequestMessage?.RequestUri?.AbsoluteUri?.ToString();
                originalRequestUrl = result.Result.RequestMessage.GetOriginalRequestUrl();
                originalRequestMethod = result.Result.RequestMessage.GetOriginalRequestMethod();
            }
            // Detect if we were redirected
            bool isRedirected = false;
            if (!String.IsNullOrEmpty(requestUrl) && requestUrl != originalRequestUrl) isRedirected = true;
            string redirected = isRedirected ? "Redirected" : "";
            string message = $"{DateTime.Now.ToString()} : Failed {redirected} {originalRequestMethod} Request to Resource: {resourcePath} {redirected}Url: {requestUrl} failed with {reason}. \nWaiting {timeSpan} ({timeSpan.TotalSeconds} seconds) before retrying...";
            if (retryAttempt > 1) message = $"{message}. Retry attempts: {retryAttempt - 1}";
            return (message, reason);
        }

        private void BuildRetryInfo(DelegateResult<HttpResponseMessage> result, TimeSpan timeSpan, int retryAttempt, Context context,
                                                 string retryReason, string retryMessage, string contentType, string responseBody) {
            RetryInfo retryInfo = context.GetRetryInfo();
            if (retryInfo == null) {
                retryInfo = new RetryInfo() { RetryAttempts = new List<RetryAttempt>() };
                context.SetRetryInfo(retryInfo);
            }
            UpdateRetryInfo(retryInfo, result, timeSpan, retryAttempt, retryReason, retryMessage, contentType, responseBody);                         
        }

        private void UpdateRetryInfo(RetryInfo retryInfo, DelegateResult<HttpResponseMessage> result, TimeSpan timeSpan, int retryAttempt,
                                                 string retryReason, string retryMessage, string contentType, string responseBody) {
            retryInfo.RetryCount = retryAttempt;
            RetryAttempt retry = new RetryAttempt {
                RetryAttemptNumber = retryAttempt,
                RetryDelay = timeSpan,
                RetryMessage = retryMessage,
                RequestFailure = new RequestFailure {
                    Reason = retryReason,
                    StatusCode = (int?) result?.Result?.StatusCode,
                    ContentType = contentType,
                    ResponseBody = responseBody
                }
            };
            if (result.Exception != null) {
                retry.RequestFailure.RequestException = new RequestException {
                    Message = result.Exception.Message,
                    Type = result.Exception.GetType().ToString(),
                    Source = result.Exception.Source
                };
            }
            retryInfo.RetryAttempts.Add(retry);
        }

        // Some of the WaitAndRetryAsync overloads want an Action parameter rather than a Func
        // private static Action<DelegateResult<HttpResponseMessage>, TimeSpan, int, Context> LogRetry() {
        //     return (result, timeSpan, retryAttempt, context) =>
        //         {
        //             .........
        //         };
        // }

        private TimeSpan GetServerWaitDuration(HttpResponseHeaders headers) {
            var retryAfter = headers?.RetryAfter;
            if (retryAfter == null) return TimeSpan.Zero;

            return retryAfter.Date.HasValue ?
                retryAfter.Date.Value - DateTime.UtcNow :
                retryAfter.Delta.GetValueOrDefault(TimeSpan.Zero);
        }

    }
}