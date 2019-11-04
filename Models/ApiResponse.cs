using System;
using System.IO;
using System.Net.Http.Headers;
using System.Text;
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
        public JToken Data { get; set; } // JToken parsed from the response body
        public string DataType { get; set; }  // Type of JToken Data: Object, Array, String, Integer, Boolean, Null
        public string Body { get; set; } // The raw response body as a string
        public Stream BodyStream { get; set; } // The raw response body as a Stream
        public Exception Exception { get; set; }
        public string ErrorTitle { get; set; }
        public string ErrorType { get; set; }
        public string ErrorDetail { get; set; }
        public string ErrorInstance { get; set; }
        public bool BodyParsingFailed { get; set; } // If an error occurred while parsing the response body
        public TimeSpan? RetryAfter { get; set; }
        public DateTimeOffset Timestamp { get; set; }

        public ApiResponse(bool success, string resource)
        {
            Success = success;
            Resource  = resource;
        }

        public string GetErrorText() {
            StringBuilder sb = new StringBuilder();
            if (!string.IsNullOrEmpty(ErrorTitle)) {
                sb.AppendLine($"ErrorTitle: \"{ErrorTitle}\" ");
            }
            if (!string.IsNullOrEmpty(ErrorType)) {
                sb.AppendLine($"ErrorType: \"{ErrorType}\" ");
            }
            if (!string.IsNullOrEmpty(ErrorDetail)) {
                sb.AppendLine($"ErrorDetail: \"{ErrorDetail}\" ");
            }
            if (!string.IsNullOrEmpty(ErrorInstance)) {
                sb.AppendLine($"ErrorInstance: \"{ErrorInstance}\" ");
            }
            if (sb.Length > 0) {
                return $"{sb.ToString()} {Environment.NewLine} Error occurred while sending \"{Method}\" request to resource: {Resource}";
            }
            return null;
        }
    }
}