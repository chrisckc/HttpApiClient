using System;
using System.Net.Http.Headers;

namespace HttpApiClient.Models
{
    public class ApiResponse
	{
        public bool Success { get; set; }
        public string Resource { get; set; }
        public string Url { get; set; }
        public string Method { get; set; }
        public int? StatusCode { get; set; }
        public string StatusText { get; set; }
        public string ContentType { get; set; }
        public HttpResponseHeaders Headers { get; set; }
        public RetryInfo RetryInfo { get; set; }
        public dynamic Data { get; set; }
        public string Body { get; set; }
        public Exception Exception { get; set; }
        public string ErrorTitle { get; set; }
        public string ErrorType { get; set; }
        public string ErrorDetail { get; set; }
        public string ErrorInstance { get; set; }
        public TimeSpan? RetryAfter { get; set; }
        public DateTimeOffset Timestamp { get; set; }

        public ApiResponse(bool success, string resource)
        {
            Success = success;
            Resource  = resource;
        }
    }
}