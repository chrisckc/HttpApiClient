﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using HttpApiClient.ErrorParsers;
using HttpApiClient.Extensions;
using HttpApiClient.Models;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace HttpApiClient
{
    internal class ApiResponseBuilder<TClient> where TClient : class
    {

        private readonly ILogger _logger;

        public List<IKnownErrorParser<TClient>> KnownErrorParsers { get; set; }

        public bool AlwaysPopulateResponseBody { get; set; }

        public bool PopulateResponseBodyOnParsingError { get; set; }

        public ApiResponseBuilder(ILogger logger)
        {
            _logger = logger;
            KnownErrorParsers = new List<IKnownErrorParser<TClient>>();
        }

        public async Task<ApiResponse> GetApiResponse(HttpResponseMessage response, string resource, Stopwatch requestTimer = null) {
            if (response != null) {
                // Build the ApiResponse object
                ApiResponse apiResponse = BuildApiResponse(response.IsSuccessStatusCode, resource, response, requestTimer);
                // Attempt to parse the response body, this may fail due to bad content
                try {
                    // If the content type header says json then try parsing into a JToken (JObject and JArray both inherit from JToken)
                    if (apiResponse.ContentType != null &&
                        (apiResponse.ContentType.ToLower().Contains("application/json") || apiResponse.ContentType.ToLower().Contains("application/problem+json"))) {
                        _logger.LogDebug($"{DateTime.Now.ToString()} : GetApiResponse: Content-Type header indicates JSON data, ApiResponse.ContentType: {apiResponse.ContentType}");
                        if (AlwaysPopulateResponseBody) apiResponse.Body = await response?.Content?.ReadAsStringAsync();
                        apiResponse.Data = await response.Content.ReadAsAsync<JToken>();
                        // Valid JSON value types: boolean (true/false) / null / object / array / number / string  ref: https://tools.ietf.org/html/rfc7159.html#section-3
                        apiResponse.DataType = apiResponse.Data?.Type.ToString();
                        //apiResponse.Data = JsonConvert.DeserializeObject<JToken>(await response?.Content?.ReadAsStringAsync());
                    } else {
                        if (apiResponse.ContentType == null) {
                            _logger.LogWarning($"{DateTime.Now.ToString()} : GetApiResponse: Content-Type header not found, treating response body as string content");
                        }
                        Stream stream = await response?.Content?.ReadAsStreamAsync();
                        apiResponse.BodyStream = new MemoryStream();
                        // The stream needs to be copied as it will be disposed when the HttpResponseMessage is disposed
                        await stream.CopyToAsync(apiResponse.BodyStream);
                        apiResponse.BodyStream.Seek(0, SeekOrigin.Begin); // The stream needs to be rewound back to the beginning
                        _logger.LogDebug($"{DateTime.Now.ToString()} : GetApiResponse: ApiResponse.BodyStream.Length: {apiResponse.BodyStream.Length} bytes");
                        if (AlwaysPopulateResponseBody) apiResponse.Body = await response?.Content?.ReadAsStringAsync();
                    }
                } catch(Exception ex) {
                    HandleResponseBodyParsingException(apiResponse, ex, response);
                }

                // If status code indicates non-success try and get more info from the response
                if (!response.IsSuccessStatusCode) {
                    // Get the value of the "WWW-Authenticate" header
                    var wwwAuthHeader = apiResponse.Headers.WwwAuthenticate.FirstOrDefault();
                    string authMessage = wwwAuthHeader?.ToString();
                    if (!string.IsNullOrEmpty(authMessage)) {
                        apiResponse.ErrorTitle = $"Authentication Failure: {authMessage} ApiResponse.StatusCode: {apiResponse.StatusCode} ({apiResponse.StatusText})";
                        apiResponse.ErrorType = "AuthenticationError";
                        _logger.LogDebug($"{DateTime.Now.ToString()} : GetApiResponse: Authentication Error message has been found!");
                    } else {
                        apiResponse.ErrorTitle = $"Failure StatusCode: {apiResponse.StatusCode} ({apiResponse.StatusText})";
                        apiResponse.ErrorType = "FailureStatusCode";
                    }
                    _logger.LogError($"{DateTime.Now.ToString()} : GetApiResponse: ApiResponse.ErrorTitle: {apiResponse.ErrorTitle}");
                    if (apiResponse.Data != null) {  // If we have an object (a JSON response)
                        _logger.LogError($"{DateTime.Now.ToString()} : GetApiResponse: StatusCode indicates Failure : ApiResponse.Data:\n{apiResponse.Data}");
                        // Try to extract some error details from the JSON response
                        ParseKnownErrors(apiResponse);
                    } else if (apiResponse.Body != null) {
                        _logger.LogError($"{DateTime.Now.ToString()} : GetApiResponse: StatusCode indicates Failure: ApiResponse.Body:\n{apiResponse.Body}");
                        // Just add a preview of the response body (could be some large html content)
                        apiResponse.ErrorDetail = apiResponse.Body.TruncateToLength(100);
                    } else {
                        _logger.LogError($"{DateTime.Now.ToString()} : GetApiResponse: StatusCode indicates Failure: Response Body is empty, no additional error information is available");
                    }
                }
                return apiResponse;
            } else {
                return BuildApiResponse(false, resource, null);
            }
        }

        // Builds the ApiResponse object, the code here needs to be a bullet proof as possible to avoid any exceptions
        private ApiResponse BuildApiResponse(bool IsSuccess, string resource, HttpResponseMessage response, Stopwatch requestTimer = null) {
            ApiResponse apiResponse = new ApiResponse(IsSuccess, resource);
            apiResponse.RequestDuration = requestTimer.ElapsedMilliseconds;
            apiResponse.Timestamp = DateTime.Now;
            _logger.LogDebug($"{DateTime.Now.ToString()} : BuildApiResponse: ApiResponse.Success: {apiResponse.Success} ApiResponse.RequestDuration: {apiResponse.RequestDuration}ms ApiResponse.Timestamp: {apiResponse.Timestamp}");
            if (response != null) {
                // Populate the ApiResponse object
                apiResponse.Url = response.RequestMessage?.RequestUri?.AbsoluteUri?.ToString();
                string originalUrl = (string)response.RequestMessage.GetOriginalRequestUrl();
                // Check if redirected
                if (apiResponse.Url != originalUrl) {
                    apiResponse.Redirected = true;
                     apiResponse.OriginalUrl = originalUrl;
                     apiResponse.OriginalMethod = (string)response.RequestMessage.GetOriginalRequestMethod();
                    _logger.LogWarning($"{DateTime.Now.ToString()} : BuildApiResponse: The Request was Redirected from: {apiResponse.OriginalUrl} \nto: {apiResponse.Url}");
                }
                _logger.LogDebug($"{DateTime.Now.ToString()} : BuildApiResponse: Response.Status: {(int)response.StatusCode} ({response.StatusCode.ToString()})");
                string contentType = response.Content?.Headers?.ContentType?.ToString();
                string contentLength = response.Content?.Headers?.ContentLength?.ToString();
                string contentEncoding = response.Content?.Headers?.ContentEncoding?.ToString();
                string transferEncoding = GetHeaderValue(response?.Headers, "Transfer-Encoding");
                //string contentType = GetHeaderValue(response.Headers, "Content-Type");
                _logger.LogDebug($"{DateTime.Now.ToString()} : BuildApiResponse: Response ContentType: {contentType}");
                _logger.LogDebug($"{DateTime.Now.ToString()} : BuildApiResponse: Response ContentEncoding: {contentEncoding}");
                _logger.LogDebug($"{DateTime.Now.ToString()} : BuildApiResponse: Response ContentLength: {contentLength}");
                _logger.LogDebug($"{DateTime.Now.ToString()} : BuildApiResponse: Response TransferEncoding: {transferEncoding}");
                _logger.LogDebug($"{DateTime.Now.ToString()} : BuildApiResponse: Response Headers:\n{response?.Headers?.ToString()}");
                apiResponse.StatusCode = (int)response.StatusCode;
                apiResponse.StatusText = response.StatusCode.ToString();
                apiResponse.Method = response.RequestMessage?.Method?.ToString();
                apiResponse.Headers = response.Headers;
                apiResponse.RetryAfter = GetServerWaitDuration(response.Headers);
                apiResponse.ContentType = contentType;
                apiResponse.ContentLength = contentLength;
                apiResponse.ContentEncoding = contentEncoding;
                apiResponse.TransferEncoding = transferEncoding;
                apiResponse.RetryInfo = response.RequestMessage.GetRetryInfo();
            }
            return apiResponse;
        }

        private async void HandleResponseBodyParsingException(ApiResponse apiResponse, Exception exception, HttpResponseMessage response) {
            string errorDetail = $"Error occurred while parsing the response body with content type: {apiResponse.ContentType} from resource: {apiResponse.Resource} \nError: {exception.Message}";
            _logger.LogError($"{DateTime.Now.ToString()} : HandleResponseBodyParsingException: {errorDetail}");
            apiResponse.Success = false;
            apiResponse.Exception = exception;
            apiResponse.ErrorTitle = $"Response Body Parsing Error";
            apiResponse.ErrorType = $"ResponseBodyParsingError";
            apiResponse.ErrorDetail = errorDetail;
            apiResponse.BodyParsingFailed = true;
            if (PopulateResponseBodyOnParsingError) {
                // Try just reading the content as a string regardless of the content type
                try {
                    apiResponse.Body = await response?.Content?.ReadAsStringAsync();
                    apiResponse.ErrorDetail = $"{apiResponse.ErrorDetail} \nThe response body was parsed a string instead. Refer to the Exception property for details of the error";
                } catch (Exception ex) {
                    _logger.LogError($"HandleResponseBodyParsingException: Exception occurred while attempting to parse response body as a string:\n{ex.ToString()}");
                    apiResponse.ErrorDetail = $"{apiResponse.ErrorDetail} \nThe response body could not even be parsed as a string. Refer to the Exception property for details of the error";
                }
            }
        }

        // Look for known error structures in the response JSON
        private void ParseKnownErrors(ApiResponse apiResponse) {
            foreach (IKnownErrorParser<TClient> parser in KnownErrorParsers) {
                // Stop on the first parser that found something, the one that returns true
                if (parser.ParseKnownErrors(apiResponse)) break;
            }
        }

        public ApiResponse GetApiResponseForException(Exception exception, HttpRequestMessage request, string resource, Stopwatch requestTimer = null) {
            ApiResponse apiResponse = new ApiResponse(false, resource);
            apiResponse.RequestDuration = requestTimer.ElapsedMilliseconds;
            apiResponse.Timestamp = DateTime.Now;
            _logger.LogError($"{DateTime.Now.ToString()} : GetApiResponseForException: ApiResponse.Success: {apiResponse.Success} ApiResponse.RequestDuration: {apiResponse.RequestDuration}ms ApiResponse.Timestamp: {apiResponse.Timestamp}");
            try {
                _logger.LogError($"{DateTime.Now.ToString()} : GetApiResponseForException: Exception occurred during HttpRequest, Exception: \n{exception.ToString()}");
                apiResponse.Method = request?.Method?.ToString();
                apiResponse.Exception = exception;
                if (exception is System.OperationCanceledException || exception is TaskCanceledException) {
                    apiResponse.ErrorTitle = "Request Cancelled";
                    apiResponse.ErrorDetail = $"The request was Cancelled while sending {apiResponse.Method} request to resource: \"{resource}\"";
                } else {
                    apiResponse.ErrorTitle = "Request Error";
                    apiResponse.ErrorDetail = $"Error occurred while sending {apiResponse.Method} request to resource: \"{resource}\"";
                }
                if (exception.InnerException != null) {
                    apiResponse.ErrorType = $"{exception.GetType().Name} | {exception.InnerException.GetType().Name}";
                    apiResponse.ErrorDetail = $"{apiResponse.ErrorDetail} \nException.Message: {exception.Message} \nInnerException.Message: {exception.InnerException.Message}";
                } else {
                    apiResponse.ErrorType = $"{exception.GetType().Name}";
                    apiResponse.ErrorDetail = $"{apiResponse.ErrorDetail} \nException.Message: {exception.Message}";
                }
                _logger.LogDebug($"{DateTime.Now.ToString()} : GetApiResponseForException: ApiResponse.ErrorDetail: {apiResponse.ErrorDetail}");
                apiResponse.RetryInfo = exception.GetRetryInfo();
                apiResponse.OriginalMethod = (string)request.GetOriginalRequestMethod();
                if (request?.RequestUri != null && request.RequestUri.IsAbsoluteUri) {
                    apiResponse.Url = request?.RequestUri?.AbsoluteUri?.ToString();
                } else {
                    apiResponse.Url = request?.RequestUri?.OriginalString;
                }
                apiResponse.OriginalUrl = (string)request.GetOriginalRequestUrl();
                if (apiResponse.OriginalUrl != null && apiResponse.Url != apiResponse.OriginalUrl) {
                    apiResponse.Redirected = true;
                    _logger.LogWarning($"{DateTime.Now.ToString()} : GetApiResponseForException: The Request was Redirected from: {apiResponse.OriginalUrl} \nto: {apiResponse.Url}");
                }
            } catch (Exception ex) {
                _logger.LogError($"{DateTime.Now.ToString()} : GetApiResponseForException: Exception occurred during GetApiResponseForException() method, Exception: \n{ex.ToString()}");
                if (apiResponse.ErrorDetail != null) {
                    apiResponse.ErrorDetail = $"{apiResponse.ErrorDetail} \nThe ApiResponse object could not be fully generated as an Exception occurred during GetApiResponseForException() method.";
                } else {
                    apiResponse.ErrorDetail = "The ApiResponse object could not be fully generated as an Exception occurred during GetApiResponseForException() method.";
                }
                _logger.LogDebug($"{DateTime.Now.ToString()} : GetApiResponseForException: ApiResponse.ErrorDetail: {apiResponse.ErrorDetail}");
            }
            return apiResponse;
        }

        private TimeSpan? GetServerWaitDuration(HttpResponseHeaders headers) {
            var retryAfter = headers?.RetryAfter;
            if (retryAfter == null) return null;

            return retryAfter.Date.HasValue ?
                retryAfter.Date.Value - DateTime.UtcNow :
                retryAfter.Delta.GetValueOrDefault(TimeSpan.Zero);
        }

        protected string GetHeaderValue(HttpResponseHeaders headers, string key) {
            string value = null;
            if (headers != null) {
                IEnumerable<string> values;  
                if (headers.TryGetValues(key, out values))
                {
                    value = values.First();
                }
            }
            return value;
        }

        private int? GetHeaderValueAsInt(HttpResponseHeaders headers, string key)
        {
            int? intValue = null;
            if (headers != null) {
                IEnumerable<string> values;  
                if (headers.TryGetValues(key, out values))
                {
                    string value = values.First();
                    int intVal;
                    bool success = Int32.TryParse(value, out intVal);
                    if (success) intValue = intVal;
                }
            }
            return intValue;
        }

    }
}