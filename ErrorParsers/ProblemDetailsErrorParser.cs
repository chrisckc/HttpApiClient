using System.Linq;
using HttpApiClient.Extensions;
using HttpApiClient.Models;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace HttpApiClient.ErrorParsers
{
    // Parses errors from a Problem Details response
    // https://tools.ietf.org/html/rfc7807
    public class ProblemDetailsErrorParser<TClient> : IKnownErrorParser<TClient> where TClient : class
    {

        private readonly ILogger<TClient> _logger;

        public ProblemDetailsErrorParser(ILogger<TClient> logger)
        {
            _logger = logger;
        }

        public bool ParseKnownErrors(ApiResponse apiResponse) {
            bool success = false;
            if (apiResponse != null) {
                _logger.LogDebug($"{this.GetType().ToString()} : Parsing Response Object for Known 'ProblemDetails' Errors");
                // Try to find the error root object
                JObject errorObj = GetErrorObject(apiResponse.Data.SelectToken("errors"));
                if (errorObj == null) {
                    errorObj = GetErrorObject(apiResponse.Data.SelectToken("error"));
                }
                if (errorObj == null) {
                    errorObj = GetErrorObject(apiResponse.Data);
                }
                if (errorObj != null) {
                    // Also Check if the members of the Problem Details object are under an "error" root object
                    // Try to get an error title
                    string errorTitle = errorObj.SelectStringValue("title");
                    if (errorTitle == null) errorTitle = errorObj.SelectToken("error")?.SelectStringValue("title");
                    if (!string.IsNullOrEmpty(errorTitle)) {
                        apiResponse.ErrorTitle = errorTitle;
                    }
                    // Try to get an error type
                    string errorType = errorObj.SelectStringValue("type");
                    if (errorType == null) errorType = errorObj.SelectToken("error")?.SelectStringValue("type");
                    if (!string.IsNullOrEmpty(errorType)) {
                        apiResponse.ErrorType = errorType;
                    }
                    // Try to get the error detail
                    string errorDetail = errorObj.SelectStringValue("detail");
                    if (errorDetail == null) errorDetail = errorObj.SelectToken("error")?.SelectStringValue("detail");
                    if (!string.IsNullOrEmpty(errorDetail)) {
                        apiResponse.ErrorDetail = errorDetail;
                    }
                    // Try to get the error instance
                    string errorInstance = errorObj.SelectStringValue("instance");
                    if (errorInstance == null) errorInstance = errorObj.SelectToken("error")?.SelectStringValue("instance");
                    if (!string.IsNullOrEmpty(errorInstance)) {
                        apiResponse.ErrorInstance = errorInstance;
                    }
                    if (!string.IsNullOrEmpty(errorTitle) || !string.IsNullOrEmpty(errorDetail)) {
                        _logger.LogDebug($"{this.GetType().ToString()} : Known 'ProblemDetails' Errors have been found!");
                        success = true;
                    }
                }
            }
            return success;
        }

        private JObject GetErrorObject(JToken error) {
            if (error == null) return null;
            JObject errorObj = null;
            if (error.Type == JTokenType.Object) {
                errorObj = (JObject)error;
            } else if (error.Type ==  JTokenType.Array) {
                var errorArr = (JArray)error;
                if (errorArr.Count > 0) {
                    var firstError = errorArr.First();
                    if (firstError.Type == JTokenType.Object) {
                        errorObj = (JObject)firstError;
                    }
                }
            }
            return errorObj;
        }
    }
}