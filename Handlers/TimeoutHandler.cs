using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using HttpApiClient.Extensions;
using HttpApiClient.Models;

namespace HttpApiClient.Handlers
{
   // Implements Timeouts, Polly Retry Detection and Redirect Detection
   internal class TimeoutHandler : DelegatingHandler
    {
        public TimeSpan DefaultTimeout { get; set; } = TimeSpan.FromSeconds(100); // HttpClient default timeout

        private readonly ILogger _logger;

        public TimeoutHandler(ILogger logger)
        {
            if (logger == null) throw new ArgumentNullException(nameof(logger));
            _logger = logger;
        }

        /*  This Handler must be configured as the PrimaryHttpMessageHandler so that it
            gets invoked during the Polly retry attempts and is able to detect the original url and method
            This handler swaps out OperationCanceledException when a request timeout occurs
            This Handler adds info about the request into the Request Properties and Exception Data
            This Handler has 3 purposes but can't be split out as the above functions rely on it being the primary handler.
        */
        protected async override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));

            using (var cts = GetCancellationTokenSource(request, cancellationToken))
            {
                try
                {
                    // If the Polly WaitAndRetryAsync handler has ran we will have the Retry Info
                    Polly.Context context = request.GetPolicyExecutionContext();
                    RetryInfo retryInfo = context.GetRetryInfo();
                    // The RequestUri and Method will always be the original ones as this code will not be hit
                    // if a redirection occurs as it is handles further down the chain.
                    if (retryInfo == null) {
                        AugmentRequest(request);
                    } else {
                        // It's possible to detect a redirect here during a retry as the new request uses the redirected url
                        string requestUrl = request?.RequestUri?.AbsoluteUri?.ToString();
                        string resourcePath = (string)request.GetResourcePath();
                        string originalRequestUrl = (string)request.GetOriginalRequestUrl();
                        bool isRedirected = false;
                        if (!String.IsNullOrEmpty(requestUrl) && requestUrl != originalRequestUrl) isRedirected = true;
                        string redirected = isRedirected ? " Redirected" : "";
                        string originalRequestMethod = (string)request.GetOriginalRequestMethod();
                        // Log the fact that this request is due to a retry
                        _logger.LogWarning($"{DateTime.Now.ToString()} SendAsync: Retrying a Failed{redirected} {originalRequestMethod} Request to Resource: {resourcePath} {redirected}Url: {requestUrl}");
                    }
                    var response = await base.SendAsync(request, cts?.Token ?? cancellationToken);
                    return response;
                }
                catch(OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
                {
                    // Also catches TaskCanceledException as it is a subclass of OperationCanceledException
                    // Throw a TimeoutException with useful message and pass along the InnerException
                    var tex =  new TimeoutException($"Request timed out, did not receive response within {(request.GetTimeout() ?? DefaultTimeout).TotalSeconds} seconds", ex.InnerException);
                    // Set the source of the exception
                    tex.Source = "TimeoutHandler";
                    AugmentException(tex, request);
                    throw tex;
                }
                catch(Exception ex)
                {
                    _logger.LogError($"{DateTime.Now.ToString()} SendAsync: Exception occurred in SendAsync() method, Request Url: {request?.RequestUri?.AbsoluteUri?.ToString()} Exception:\n{ex.ToString()}");
                    AugmentException(ex, request);
                    throw ex;
                }
            }
        }

        private void AugmentRequest(HttpRequestMessage request) {
            // As long as the request is not a retry,
            // the RequestUri and Method will always be the original ones as this code will not be hit
            // if a redirection occurs as it is handled further down the chain.
            request.SetOriginalRequestUrl(request?.RequestUri?.AbsoluteUri?.ToString());
            request.SetOriginalRequestMethod(request?.Method?.ToString());
        }

        private void AugmentException(Exception exception, HttpRequestMessage request) {
            // Store the current url and method in the exception, used to detect redirects
            exception.SetResourcePath(request?.GetResourcePath());
            exception.SetRequestUrl(request?.RequestUri?.AbsoluteUri?.ToString());
            exception.SetRequestMethod(request?.Method?.ToString());
            // Store the original url and method in the exception, used to detect redirects
            exception.SetOriginalRequestUrl(request?.GetOriginalRequestUrl());
            exception.SetOriginalRequestMethod(request?.GetOriginalRequestMethod());
        }

        private CancellationTokenSource GetCancellationTokenSource(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var timeout = request.GetTimeout() ?? DefaultTimeout;
            if (timeout == Timeout.InfiniteTimeSpan)
            {
                // No need to create a CTS if there's no timeout
                return null;
            }
            else
            {
                var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(timeout);
                return cts;
            }
        }
    }
}
