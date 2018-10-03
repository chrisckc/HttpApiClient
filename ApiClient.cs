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
        public CancellationTokenSource CancellationTokenSource { get; set; }
        public int RequestCounter { get; set; }
        public DateTimeOffset LastRequestTimeStamp { get; private set; }

        public ApiClient(HttpClient client, ApiClientOptions<TClient> options, ILogger<TClient> logger)
        {
            _client = client;
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

        public virtual async Task<ApiResponse> GetResource(string resource, TimeSpan? delay = null) {
            if (delay.HasValue) {
                _logger.LogDebug($"{DateTime.Now.ToString()} : Getting Resource {resource} after delay {delay.Value}...");
                await Task.Delay(delay.Value);
            } else {
                _logger.LogDebug($"{DateTime.Now.ToString()} : Getting Resource {resource} ...");
            }
            return await SendAsync(HttpMethod.Get, resource);
        }

        public virtual async Task<ApiResponse> PostResource(string resource, object obj, TimeSpan? delay = null) {
            if (delay.HasValue) {
                _logger.LogDebug($"{DateTime.Now.ToString()} : Posting Resource {resource} after delay {delay.Value}...");
                await Task.Delay(delay.Value);
            } else {
                _logger.LogDebug($"{DateTime.Now.ToString()} : Posting Resource {resource} ...");
            }
            return await SendAsync(HttpMethod.Post, resource, obj);                   
        }

        public virtual async Task<ApiResponse> PutResource(string resource, object obj, TimeSpan? delay = null) {
            if (delay.HasValue) {
                _logger.LogDebug($"{DateTime.Now.ToString()} : Putting Resource {resource} after delay {delay.Value}...");
                await Task.Delay(delay.Value);
            } else {
                _logger.LogDebug($"{DateTime.Now.ToString()} : Putting Resource {resource} ...");
            }
            return await SendAsync(HttpMethod.Put, resource, obj);                   
        }

        public virtual async Task<ApiResponse> SendAsync(HttpMethod method, string resource, object obj) {
            try {
                _logger.LogDebug($"{DateTime.Now.ToString()} : Serializing Object of Type: {obj?.GetType()?.ToString()}");
                StringContent stringContent = null;
                if (obj != null) {
                    var dataAsString = JsonConvert.SerializeObject(obj);
                    stringContent = new StringContent(dataAsString, System.Text.Encoding.UTF8, "application/json");
                }
                return await SendAsync(method, resource, stringContent);
            } catch (Exception exception) {
                _logger.LogError($"{DateTime.Now.ToString()} : Exception occurred during Serialization while attempting to Post resource: {resource}\nException: {exception.ToString()}");
                return null;
            }                      
        }

        public virtual async Task<ApiResponse> SendAsync(HttpMethod method, string resource, HttpContent content = null) {
            // Create the context here so we have access to it in the catch block
            Polly.Context context = new Polly.Context();
            //Create the Request
            HttpRequestMessage request = new HttpRequestMessage(method, resource);
            if (content != null) {
                request.Content = content;
            }
            // Set the PolicyExecutionContext so that it is available after execution of the request
            // https://github.com/App-vNext/Polly/issues/505
            request.SetPolicyExecutionContext(context);
            AugmentContext(context, resource, request);
            //CancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(2)); // Testing cancellation
            // Make the request
            RequestCounter++;
            LastRequestTimeStamp = DateTime.UtcNow;
            try {
                _logger.LogDebug($"{DateTime.Now.ToString()} : Sending request with Method: {method.ToString()} to Resource: {resource}");
                using(var response = await _client.SendAsync(request, CancellationTokenSource.Token)) {
                    TransferRetryInfo(response.RequestMessage, context);
                    return await _apiResponseBuilder.GetApiResponse(response, resource);
                } 
            } catch (Exception exception) {
                // Handles communication errors such as "Connection Refused" etc.
                // Network failures (System.Net.Http.HttpRequestException)
                // Timeouts (System.IO.IOException)
                AugmentException(exception, resource, request);
                TransferRetryInfo(exception, context);
                return _apiResponseBuilder.GetApiResponse(exception, request, resource);
            }                      
        }


        private void AugmentContext(Polly.Context context, string resource, HttpRequestMessage request) {
            context["Url"] = new Uri(_client.BaseAddress, resource).ToString();
            context["Resource"] = resource;
            context["HttpMethod"] = request.Method.ToString();
        }

        private void AugmentException(Exception exception, string resource, HttpRequestMessage request) {
            exception.Data["Url"] = new Uri(_client.BaseAddress, resource).ToString();
            exception.Data["Resource"] = resource;
            exception.Data["HttpMethod"] = request.Method.ToString();
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