using System;
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

        protected readonly ILogger<ProblemDetailsErrorParser<TClient>> _logger;

        public ProblemDetailsErrorParser(ILogger<ProblemDetailsErrorParser<TClient>> logger)
        {
            _logger = logger;
        }

        public bool ParseKnownErrors(ApiResponse apiResponse) {
            bool success = false;
            if (apiResponse != null) {
                _logger.LogDebug($"{this.GetType().ToString()} : ParseKnownErrors: Parsing Response Object for Known 'ProblemDetails'");
                try {
                    success = ParseKnownErrorsInternal(apiResponse);
                } catch (Exception exception) {
                    //log exception but don't throw one
                    _logger.LogError($"{DateTime.Now.ToString()} : ParseKnownErrors: Exception occurred while parsing Response Object for Known 'ProblemDetails'\nException: {exception.ToString()}");
                    _logger.LogError($"{DateTime.Now.ToString()} : ParseKnownErrors: Exception occurred while parsing apiResponse.Data: {apiResponse.Data?.ToString()}");
                    return false;
                }
            }
            return success;
        }

        protected virtual bool ParseKnownErrorsInternal(ApiResponse apiResponse) {
            bool success = false;
            // The error object maybe nested inside a root object
            // Try to find a root object which represents the error...

            // Look for an "errors" root json object eg. { "errors": [{ "title": "some title" }, ... ] }
            // If the object is an array, this will return the first element
            JObject errorObj = GetJObjectFromJToken(apiResponse.Data.SelectToken("errors"));
            if (errorObj == null) {
                // Otherwise look for an "error" root key eg. eg. { "error": { "title": "some title" } }
                errorObj = GetJObjectFromJToken(apiResponse.Data.SelectToken("error"));
            }
            if (errorObj == null) {
                // finally, just get the root as a JObject instead
                // It maybe the case that a root key of "error" was just a string rather then an object
                errorObj = GetJObjectFromJToken(apiResponse.Data);
            }
            if (errorObj != null) {
                // Now that we have an error object try to get the details from it...
                // Check for a common type of error object eg. { "error": "some error message" }
                string errorMessage = errorObj.SelectStringValue("error");
                if (!string.IsNullOrEmpty(errorMessage)) {
                    if (apiResponse.ErrorTitle != null) {
                        apiResponse.ErrorDetail = errorMessage;
                    } else {
                        apiResponse.ErrorTitle = errorMessage;
                    }
                    _logger.LogDebug($"{this.GetType().ToString()} : Error message has been found!");
                }


                // Now Check if the object is an RFC "Problem Details" object
                bool problemDetailsFound = false;
                // Try to get an error title
                string errorTitle = errorObj.SelectStringValue("title");
                if (errorTitle == null) errorTitle = errorObj.SelectToken("error")?.SelectStringValue("title");
                if (!string.IsNullOrEmpty(errorTitle)) {
                    apiResponse.ErrorTitle = errorTitle;
                    problemDetailsFound = true;
                }
                
                // Try to get an error type
                string errorType = errorObj.SelectStringValue("type");
                if (errorType == null) errorType = errorObj.SelectToken("error")?.SelectStringValue("type");
                if (!string.IsNullOrEmpty(errorType)) {
                    apiResponse.ErrorType = errorType;
                    problemDetailsFound = true;
                }
                // Try to get the error detail
                string errorDetail = errorObj.SelectStringValue("detail");
                if (errorDetail == null) errorDetail = errorObj.SelectToken("error")?.SelectStringValue("detail");
                if (!string.IsNullOrEmpty(errorDetail)) {
                    apiResponse.ErrorDetail = errorDetail;
                    problemDetailsFound = true;
                }
                // Try to get the error instance
                string errorInstance = errorObj.SelectStringValue("instance");
                if (errorInstance == null) errorInstance = errorObj.SelectToken("error")?.SelectStringValue("instance");
                if (!string.IsNullOrEmpty(errorInstance)) {
                    apiResponse.ErrorInstance = errorInstance;
                    problemDetailsFound = true;
                }
                if (problemDetailsFound) {
                    _logger.LogDebug($"{this.GetType().ToString()} : Known 'ProblemDetails' Errors have been found!");
                    success = true; // only treat as success if "Problem Details" object found
                }
            }
            return success;
        }

        // Checks if the JToken is an array, if so return the first item if it's a JObject
        protected JObject GetJObjectFromJToken(JToken jToken) {
            if (jToken == null) return null;
            JObject jObject = null;
            if (jToken.Type == JTokenType.Object) {
                jObject = (JObject)jToken;
            } else if (jToken.Type ==  JTokenType.Array) {
                var jArr = (JArray)jToken;
                if (jArr.Count > 0) {
                    var firstJToken = jArr.First();
                    if (firstJToken.Type == JTokenType.Object) {
                        jObject = (JObject)firstJToken;
                    }
                }
            }
            return jObject;
        }
    }
}