using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions.Internal;
using Newtonsoft.Json;
using Polly;
using HttpApiClient.Extensions;
using HttpApiClient.Models;

namespace HttpApiClient
{
    // ApiClient must be subclassed as a specific type to use it
    public abstract class ApiClient<TClient> where TClient : class
    {
        protected HttpClient _client;
        protected readonly ILogger<TClient> _logger;
        private ApiResponseBuilder<TClient> _apiResponseBuilder;

        protected ApiClientOptions<TClient> _options;
        public CancellationTokenSource CancellationTokenSource { get; set; }
        public int PendingRequestCount { get; set; }
        public int RequestCount { get; set; }
        public DateTimeOffset LastRequestTimeStamp { get; private set; }

        public ApiClient(HttpClient client, ApiClientOptions<TClient> options, ILogger<TClient> logger)
        {
            _client = client;
            _options = options;
            _logger = logger;

            // Get class name for demo purposes, the class name is shown in the log output anyway
            var name = TypeNameHelper.GetTypeDisplayName(typeof(TClient));
            _logger.LogDebug($"{name} constructed");
            _logger.LogDebug($"{name} BaseAddress: {client.BaseAddress}");

            // Create the ApiResponseBuilder
            _apiResponseBuilder = new ApiResponseBuilder<TClient>(_logger);  // new is glue is fine here
            _apiResponseBuilder.KnownErrorParsers = options.KnownErrorParsers;

            // Create a Cancellation token
            CancellationTokenSource = new CancellationTokenSource();
        }

        // Set the OAuth 2.0 bearer token
        public void SetBearerToken(string bearerToken)
        {
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
        }

        public void clearBearerToken(string bearerToken)
        {
            _client.DefaultRequestHeaders.Remove("Authorization");
        }

        // Set the Cookie
        public void SetCookie(string cookie)
        {
            _client.DefaultRequestHeaders.Add("Cookie", cookie);
        }

        public void clearCookie(string bearerToken)
        {
            _client.DefaultRequestHeaders.Remove("Cookie");
        }

        public virtual async Task<ApiResponse> GetResource(string resourcePath, TimeSpan? delay = null) {
            if (delay.HasValue) {
                _logger.LogDebug($"{DateTime.Now.ToString()} : Getting Resource {resourcePath} after delay {delay.Value}...");
                await Task.Delay(delay.Value);
            } else {
                _logger.LogDebug($"{DateTime.Now.ToString()} : Getting Resource {resourcePath} ...");
            }
            return await SendAsync(HttpMethod.Get, resourcePath);
        }

        public virtual async Task<ApiResponse> PostResource(string resourcePath, object obj, TimeSpan? delay = null) {
            if (delay.HasValue) {
                _logger.LogDebug($"{DateTime.Now.ToString()} : Posting Resource {resourcePath} after delay {delay.Value}...");
                await Task.Delay(delay.Value);
            } else {
                _logger.LogDebug($"{DateTime.Now.ToString()} : Posting Resource {resourcePath} ...");
            }
            return await SendAsync(HttpMethod.Post, resourcePath, obj);                   
        }

        public virtual async Task<ApiResponse> PutResource(string resourcePath, object obj, TimeSpan? delay = null) {
            if (delay.HasValue) {
                _logger.LogDebug($"{DateTime.Now.ToString()} : Putting Resource {resourcePath} after delay {delay.Value}...");
                await Task.Delay(delay.Value);
            } else {
                _logger.LogDebug($"{DateTime.Now.ToString()} : Putting Resource {resourcePath} ...");
            }
            return await SendAsync(HttpMethod.Put, resourcePath, obj);                   
        }

        public virtual async Task<ApiResponse> DeleteResource(string resourcePath, TimeSpan? delay = null) {
            if (delay.HasValue) {
                _logger.LogDebug($"{DateTime.Now.ToString()} : Deleting Resource {resourcePath} after delay {delay.Value}...");
                await Task.Delay(delay.Value);
            } else {
                _logger.LogDebug($"{DateTime.Now.ToString()} : Deleting Resource {resourcePath} ...");
            }
            return await SendAsync(HttpMethod.Delete, resourcePath);                   
        }

        public virtual async Task<ApiResponse> SendAsync(HttpMethod method, string resourcePath, object obj) {
            try {
                StringContent stringContent = null;
                if (obj != null) {
                    _logger.LogDebug($"{DateTime.Now.ToString()} : Serializing Object of Type: {obj?.GetType()?.ToString()}");
                    var dataAsString = JsonConvert.SerializeObject(obj);
                    stringContent = new StringContent(dataAsString, System.Text.Encoding.UTF8, "application/json");
                }
                return await SendAsync(method, resourcePath, stringContent);
            } catch (Exception exception) {
                _logger.LogError($"{DateTime.Now.ToString()} : Exception occurred during Serialization while attempting to Post resource: {resourcePath}\nException: {exception.ToString()}");
                return null;
            }                      
        }

        public virtual async Task<ApiResponse> SendAsync(HttpMethod method, string resourcePath, HttpContent content = null) {
            // Create the context here so we have access to it in the catch block
            Polly.Context context = new Polly.Context();
            //Create the Request
            HttpRequestMessage request = new HttpRequestMessage(method, resourcePath);
            if (content != null) {
                request.Content = content;
            }
            // Set the PolicyExecutionContext so that it is available after execution of the request
            // https://github.com/App-vNext/Polly/issues/505
            request.SetPolicyExecutionContext(context);
            request.SetResourcePath(resourcePath);
            // Make the request
            RequestCount++;
            PendingRequestCount++;
            LastRequestTimeStamp = DateTime.UtcNow;
            try {
                _logger.LogDebug($"{DateTime.Now.ToString()} : Sending request with Method: {method.ToString()} to Resource: {resourcePath}");
                using(var response = await _client.SendAsync(request, CancellationTokenSource.Token)) {
                    TransferRetryInfo(response.RequestMessage, context);
                    return await _apiResponseBuilder.GetApiResponse(response, resourcePath);
                } 
            } catch (Exception exception) {
                // Handles communication errors such as "Connection Refused" etc.
                // Network failures (System.Net.Http.HttpRequestException)
                // Timeouts (System.IO.IOException)
                TransferRetryInfo(exception, context);
                return _apiResponseBuilder.GetApiResponse(exception, request, resourcePath);
            } finally {
                PendingRequestCount--;
            }                    
        }

        // Transfers the RetryInfo from the PolicyExecutionContext into the Request Properties
        private void TransferRetryInfo(HttpRequestMessage request, Polly.Context context) {
            RetryInfo retryInfo = context.GetRetryInfo();
            if (retryInfo != null) {
                request.SetRetryInfo(retryInfo);
            }
        }

        // Transfers the RetryInfo from the PolicyExecutionContext into the Exception Data
        private void TransferRetryInfo(Exception exception, Polly.Context context) {
            RetryInfo retryInfo = context.GetRetryInfo();
            if (retryInfo != null) {
                exception.SetRetryInfo(retryInfo);
            }
        }
    }
}