using System;
using System.Net.Http.Headers;
using Newtonsoft.Json.Linq;

namespace HttpApiClient.Models
{
    public class ApiResponse
	{
        public bool Success { get; set; }
        public string Resource { get; set; }
        public string Url { get; set; }
         public string OriginalUrl { get; set; }  // The Url before any redirects
        public string Method { get; set; }  // Http Method
        public string OriginalMethod { get; set; } // The Http Method before any redirects
        public int? StatusCode { get; set; }
        public string StatusText { get; set; }
        public bool Redirected { get; set; }
        public string ContentType { get; set; }
        public HttpResponseHeaders Headers { get; set; }
        public RetryInfo RetryInfo { get; set; }
        public JToken Data { get; set; }
        public string DataType { get; set; }  // Type of JToken Data: Object, Array, String, Integer, Boolean, Null
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