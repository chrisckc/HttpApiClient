using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using HttpApiClient.Extensions;
using HttpApiClient.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions.Internal;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;
using Polly;

namespace HttpApiClient {
    // ApiClient must be subclassed as a specific type to use it
    public abstract class ApiClient<TClient> where TClient : class {
        protected HttpClient _client;
        protected readonly ILogger<TClient> _logger;
        private ApiResponseBuilder<TClient> _apiResponseBuilder;

        protected ApiClientOptions<TClient> _options;
        public CancellationTokenSource CancellationTokenSource { get; set; }
        public string BasicAuth { get; private set; }
        public string BearerToken { get; private set; }
        public string Cookie { get; private set; }
        public int PendingRequestCount { get; set; }
        public int RequestCount { get; set; }
        public DateTimeOffset LastRequestTimeStamp { get; private set; }

        public ApiClient(HttpClient client, ApiClientOptions<TClient> options, ILogger<TClient> logger) {
            _client = client;
            _options = options;
            _logger = logger;

            // Get class name for demo purposes, the class name is shown in the log output anyway
            var name = TypeNameHelper.GetTypeDisplayName(typeof(TClient));
            _logger.LogDebug($"{name} constructed");
            _logger.LogDebug($"{name} BaseAddress: {client.BaseAddress}");

            // Create the ApiResponseBuilder
            _apiResponseBuilder = new ApiResponseBuilder<TClient>(_logger); // new is glue is fine here
            _apiResponseBuilder.AlwaysPopulateResponseBody = options.AlwaysPopulateResponseBody.GetValueOrDefault();
            _apiResponseBuilder.PopulateResponseBodyOnParsingError = options.PopulateResponseBodyOnParsingError.GetValueOrDefault();
            _apiResponseBuilder.KnownErrorParsers = options.KnownErrorParsers;

            // Create a Cancellation token
            CancellationTokenSource = new CancellationTokenSource();
        }

        public void SetBasicAuth(string username, string password) {
            string basicAuth = string.Format("{0}:{1}", username, password);
            BasicAuth = basicAuth;
            string encodedAuth = Convert.ToBase64String(System.Text.ASCIIEncoding.ASCII.GetBytes(basicAuth));
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", encodedAuth);
        }

        public void clearBasicAuth() {
            _client.DefaultRequestHeaders.Remove("Authorization");
        }

        // Set the OAuth 2.0 bearer token
        public void SetBearerToken(string bearerToken) {
            BearerToken = bearerToken;
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
        }

        public void clearBearerToken() {
            _client.DefaultRequestHeaders.Remove("Authorization");
        }

        // Set the Cookie
        public void SetCookie(string cookie) {
            Cookie = cookie;
            _client.DefaultRequestHeaders.Add("Cookie", cookie);
        }

        public void clearCookie() {
            _client.DefaultRequestHeaders.Remove("Cookie");
        }

        public virtual async Task<ApiResponse> GetResource(string resourcePath, TimeSpan? delay = null) {
            if (delay.HasValue) {
                _logger.LogDebug($"{DateTime.Now.ToString()} : Getting Resource: {resourcePath} after delay: {delay.Value}...");
                await Task.Delay(delay.Value);
            } else {
                _logger.LogDebug($"{DateTime.Now.ToString()} : Getting Resource: {resourcePath} ...");
            }
            return await SendAsync(HttpMethod.Get, resourcePath);
        }

        public virtual async Task<ApiResponse> PostResource(string resourcePath, object obj, TimeSpan? delay = null) {
            if (delay.HasValue) {
                _logger.LogDebug($"{DateTime.Now.ToString()} : Posting Resource: {resourcePath} after delay: {delay.Value}...");
                await Task.Delay(delay.Value);
            } else {
                _logger.LogDebug($"{DateTime.Now.ToString()} : Posting Resource: {resourcePath} ...");
            }
            return await SendObjectAsync(HttpMethod.Post, resourcePath, obj);
        }

        public virtual async Task<ApiResponse> PutResource(string resourcePath, object obj, TimeSpan? delay = null) {
            if (delay.HasValue) {
                _logger.LogDebug($"{DateTime.Now.ToString()} : Putting Resource: {resourcePath} after delay: {delay.Value}...");
                await Task.Delay(delay.Value);
            } else {
                _logger.LogDebug($"{DateTime.Now.ToString()} : Putting Resource: {resourcePath} ...");
            }
            return await SendObjectAsync(HttpMethod.Put, resourcePath, obj);
        }

        public virtual async Task<ApiResponse> DeleteResource(string resourcePath, TimeSpan? delay = null) {
            if (delay.HasValue) {
                _logger.LogDebug($"{DateTime.Now.ToString()} : Deleting Resource: {resourcePath} after delay {delay.Value}...");
                await Task.Delay(delay.Value);
            } else {
                _logger.LogDebug($"{DateTime.Now.ToString()} : Deleting Resource: {resourcePath} ...");
            }
            return await SendAsync(HttpMethod.Delete, resourcePath);
        }

        // Send a string with optional media type
        public virtual async Task<ApiResponse> SendStringAsync(HttpMethod method, string resourcePath, string str, string mediaType = "text/plain") {
            _logger.LogDebug("{DateTime.Now.ToString()} : SendStringAsync: Sending string as StringContent");
            StringContent stringContent = null; // StringContent: Provides HTTP content based on a string.
            if (str != null) {
                _logger.LogDebug($"{DateTime.Now.ToString()} : SendStringAsync: Creating StringContent with Length: {ASCIIEncoding.UTF8.GetByteCount(str)} bytes");
                stringContent = new StringContent(str, System.Text.Encoding.UTF8, mediaType);
            }
            return await SendAsync(method, resourcePath, stringContent);
        }

        // Send a string with optional media type
        public virtual async Task<ApiResponse> SendStringAsStreamAsync(HttpMethod method, string resourcePath, string str, string mediaType = "text/plain") {
            _logger.LogDebug("{DateTime.Now.ToString()} : SendStringAsStreamAsync: Sending string as StreamContent");
            StreamContent streamContent = null;
            if (str != null) {
                streamContent = CreateStringStreamContent(str, mediaType);
            }
            return await SendAsync(method, resourcePath, streamContent);
        }

        // Send an object as JSON
        public virtual async Task<ApiResponse> SendObjectAsync(HttpMethod method, string resourcePath, object obj) {
            StringContent stringContent = null;
            if (obj != null) {
                string str = obj as string;
                if (str != null) {
                    _logger.LogDebug("{DateTime.Now.ToString()} : SendObjectAsync: Sending string as StringContent");
                    stringContent = new StringContent(str, System.Text.Encoding.UTF8, "text/plain");
                } else {
                    _logger.LogDebug($"{DateTime.Now.ToString()} : SendObjectAsync: Sending object as JSON Serialized StringContent");
                    var settings = GetJsonSerializerSettings();
                    try {
                        _logger.LogDebug($"{DateTime.Now.ToString()} : SendObjectAsync: Serializing Object of Type: \"{obj?.GetType()?.Name?.ToString()}\" to JSON");
                        string jsonString = JsonConvert.SerializeObject(obj, _options.SerializeUseFormattingIndented ? Formatting.Indented : Formatting.None, settings);
                        _logger.LogDebug($"{DateTime.Now.ToString()} : SendObjectAsync: Creating StringContent with Length: {ASCIIEncoding.UTF8.GetByteCount(jsonString)} bytes");
                        stringContent = new StringContent(jsonString, System.Text.Encoding.UTF8, "application/json");
                    } catch (Exception exception) {
                        //log exception but don't throw one
                        _logger.LogError($"{DateTime.Now.ToString()} : SendObjectAsync: Exception occurred during Serialization while attempting to {method?.ToString()} resource: {resourcePath}\nException: {exception.ToString()}");
                        return null;
                    }
                }
            }
            return await SendAsync(method, resourcePath, stringContent);
        }

        public virtual async Task<ApiResponse> SendObjectAsStreamAsync(HttpMethod method, string resourcePath, object obj) {
            StreamContent streamContent = null;
            if (obj != null) {
                string str = obj as string;
                if (str != null) {
                    _logger.LogDebug("{DateTime.Now.ToString()} : SendObjectAsStreamAsync: Sending string as StreamContent");
                    streamContent = CreateStringStreamContent(str, "text/plain");
                } else {
                    _logger.LogDebug($"{DateTime.Now.ToString()} : SendObjectAsStreamAsync: Sending object as JSON Serialized StreamContent");
                    var settings = GetJsonSerializerSettings();
                    try {
                        _logger.LogDebug($"{DateTime.Now.ToString()} : SendObjectAsStreamAsync: Serializing Object of Type: \"{obj?.GetType()?.Name?.ToString()}\" to JSON");
                        streamContent = CreateJsonStreamContent(obj, settings);
                    } catch (Exception exception) {
                        //log exception but don't throw one
                        _logger.LogError($"{DateTime.Now.ToString()} : SendObjectAsStreamAsync: Exception occurred during Serialization while attempting to {method?.ToString()} resource: {resourcePath}\nException: {exception.ToString()}");
                        return null;
                    }
                }
            }
            return await SendAsync(method, resourcePath, streamContent);
        }

        private JsonSerializerSettings GetJsonSerializerSettings() {
            var settings = new JsonSerializerSettings {
                ContractResolver = new DefaultContractResolver(),
                ReferenceLoopHandling = Newtonsoft.Json.ReferenceLoopHandling.Ignore, // Avoids cyclic references
                NullValueHandling = NullValueHandling.Ignore
            };
            if (_options.SerializeNullValues) {
                settings.NullValueHandling = NullValueHandling.Include;
            }
            if (_options.SerializeEnumsAsStrings) {
                settings.Converters = new JsonConverter[] { new StringEnumConverter() };
            }
            if (_options.SerializePropertiesAsCamelCase) {
                // these options are the same as the CamelCasePropertyNamesContractResolver
                // https://github.com/JamesNK/Newtonsoft.Json/blob/master/Src/Newtonsoft.Json/Serialization/CamelCasePropertyNamesContractResolver.cs
                settings.ContractResolver = new DefaultContractResolver {
                    NamingStrategy = new CamelCaseNamingStrategy() {
                        ProcessDictionaryKeys = true,
                        OverrideSpecifiedNames = true
                    }
                };
            }
            return settings;
        }

        public void SerializeJsonIntoStream(object obj, Stream stream, JsonSerializerSettings settings = null) {
            using(var sw = new StreamWriter(stream, new UTF8Encoding(false), 1024, true))
            using(var jtw = new JsonTextWriter(sw) { Formatting = _options.SerializeUseFormattingIndented ? Formatting.Indented : Formatting.None }) {
                var js = JsonSerializer.CreateDefault(settings);
                js.Serialize(jtw, obj);
                jtw.Flush();
            }
        }

        public StreamContent CreateJsonStreamContent(object obj, JsonSerializerSettings settings = null) {
            StreamContent httpContent = null;
            if (obj != null) {
                var ms = new MemoryStream();
                SerializeJsonIntoStream(obj, ms, settings);
                ms.Seek(0, SeekOrigin.Begin);
                _logger.LogDebug($"{DateTime.Now.ToString()} : CreateJsonStreamContent: Stream Length: {ms.Length} bytes");
                httpContent = new StreamContent(ms);
                httpContent.Headers.ContentType = new MediaTypeHeaderValue("application/json");
            }
            return httpContent;
        }

        public StreamContent CreateStringStreamContent(string str, string mediaType = "text/plain") {
            StreamContent httpContent = null;
            if (str != null) {
                byte[] byteArray = Encoding.UTF8.GetBytes(str);
                var ms = new MemoryStream(byteArray);
                ms.Seek(0, SeekOrigin.Begin);
                _logger.LogDebug($"{DateTime.Now.ToString()} : CreateStringStreamContent: Stream Length: {ms.Length} bytes");
                httpContent = new StreamContent(ms);
                httpContent.Headers.ContentType = new MediaTypeHeaderValue(mediaType);
            }
            return httpContent;
        }

        // Send HttpContent, used by all above methods, accept any of the HttpContent sub-classes
        // FormUrlEncodedContent: A container for name/value tuples encoded using application/x-www-form-urlencoded MIME type.
        // MultipartContent: Provides a collection of HttpContent objects that get serialized using the multipart/* content type specification.
        // MultipartFormDataContent: Provides a container for content encoded using multipart/form-data MIME type. (Use this format if you are uploading a file to a server)
        // StreamContent: Provides HTTP content based on a stream.
        // StringContent: Provides HTTP content based on a string.
        // ObjectContent: Contains a value as well as an associated MediaTypeFormatter that will be used to serialize the value when writing this content.
        // ObjectContent comes from the System.Net.Http.Formatting assembly provided by package Microsoft.AspNet.WebApi.Client
        // HttpContent: A base class representing an HTTP entity body and content headers.
        public virtual async Task<ApiResponse> SendAsync(HttpMethod method, string resourcePath, HttpContent content = null) {
            // Create the context here so we have access to it in the catch block
            Polly.Context context = new Polly.Context();
            //Create the Request
            HttpRequestMessage request = new HttpRequestMessage(method, resourcePath);
            if (content != null) {
                request.Content = content;
            } else {
                // content is normally provided for post and put methods
                if (method == HttpMethod.Post || method == HttpMethod.Put) _logger.LogDebug($"{DateTime.Now.ToString()} : SendAsync: The HttpContent is null for POST or PUT request!");
            }
            // Set the PolicyExecutionContext so that it is available after execution of the request
            // https://github.com/App-vNext/Polly/issues/505
            request.SetPolicyExecutionContext(context);
            request.SetResourcePath(resourcePath);
            // Make the request
            RequestCount++;
            PendingRequestCount++;
            LastRequestTimeStamp = DateTime.UtcNow;
            var requestTimer = new Stopwatch();
            try {
                if (content ==  null) {
                    _logger.LogDebug($"{DateTime.Now.ToString()} : SendAsync: Sending request with Method: {method?.ToString()} HttpContent is null, RequestUri: {request.RequestUri}");
                } else {
                    _logger.LogDebug($"{DateTime.Now.ToString()} : SendAsync: Sending request with Method: {method?.ToString()} HttpContent Type: \"{content?.GetType()?.Name?.ToString()}\" RequestUri: {request.RequestUri}");
                }
                requestTimer.Start();
                using(var response = await _client.SendAsync(request, CancellationTokenSource.Token)) {
                    requestTimer.Stop();
                    TransferRetryInfo(response.RequestMessage, context);
                    return await _apiResponseBuilder.GetApiResponse(response, resourcePath, requestTimer);
                }
            } catch (Exception exception) {
                // Handles communication errors such as "Connection Refused" etc.
                // Network failures (System.Net.Http.HttpRequestException)
                // Timeouts (System.IO.IOException)
                requestTimer.Stop();
                TransferRetryInfo(exception, context);
                return _apiResponseBuilder.GetApiResponse(exception, request, resourcePath, requestTimer);
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