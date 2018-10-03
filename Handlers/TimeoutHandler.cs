using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using HttpApiClient.Extensions;
using HttpApiClient.Models;

namespace HttpApiClient.Handlers
{
    internal class TimeoutHandler : DelegatingHandler
    {
        public TimeSpan DefaultTimeout { get; set; } = TimeSpan.FromSeconds(100);

        private readonly ILogger _logger;

        public TimeoutHandler(ILogger logger)
        {
            _logger = logger;
        }
        
        /*  This Handler must be configured as the PrimaryHttpMessageHandler to be able to
            be invoked during the Polly retry attempts and also swapout the
            OperationCanceledException when a request timeout occurs
            This Handler has a dual purpose for the above reason
        */
        protected async override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            using (var cts = GetCancellationTokenSource(request, cancellationToken))
            {
                try
                {
                    // If the Polly WaitAndRetryAsync handler has ran we will have the Retry Info
                    Polly.Context context = request.GetPolicyExecutionContext();
                    RetryInfo retryInfo = context.GetRetryInfo();
                    if (retryInfo != null)
                    {
                        // Log the fact that this request is due to a retry
                        _logger.LogWarning($"{DateTime.Now.ToString()} : Retrying {request.Method.ToString()} Request to Resource: {context["Resource"]}");
                    }
                    var response = await base.SendAsync(request, cts?.Token ?? cancellationToken);
                    return response;
                }
                catch(OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
                {
                    // Throw a TimeoutException with useful message and pass along the InnerException
                    var tex =  new TimeoutException($"Request timed out, did not receive response within {(request.GetTimeout() ?? DefaultTimeout).TotalSeconds} seconds", ex.InnerException);
                    // Set the source of the exception
                    tex.Source = "TimeoutHandler";
                    throw tex;
                } 
                catch(Exception ex)
                {
                    throw ex;
                }
            }
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