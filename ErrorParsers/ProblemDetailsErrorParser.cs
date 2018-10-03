using HttpApiClient.Models;
using Microsoft.Extensions.Logging;

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
                _logger.LogDebug($"{this.GetType().ToString()} : Parsing Response Object for Known Errors");
                // Also Check if the members of the Problem Details object are under an "error" root object
                // Try to get an error title
                string errorTitle = apiResponse.Data?.SelectToken("title");
                if (errorTitle == null) errorTitle = apiResponse.Data?.SelectToken("error")?.SelectToken("title")?.Value;
                if (!string.IsNullOrEmpty(errorTitle)) {
                    apiResponse.ErrorTitle = errorTitle;
                }
                // Try to get an error type
                string errorType = apiResponse.Data?.SelectToken("type");
                if (errorType == null) errorType = apiResponse.Data?.SelectToken("error")?.SelectToken("type")?.Value;
                if (!string.IsNullOrEmpty(errorType)) {
                    apiResponse.ErrorType = errorType;
                }
                // Try to get the error detail
                string errorDetail = apiResponse.Data?.SelectToken("detail");
                if (errorDetail == null) errorDetail = apiResponse.Data?.SelectToken("error")?.SelectToken("detail")?.Value;
                if (!string.IsNullOrEmpty(errorDetail)) {
                    apiResponse.ErrorDetail = errorDetail;
                }
                // Try to get the error instance
                string errorInstance = apiResponse.Data?.SelectToken("instance");
                if (errorInstance == null) errorInstance = apiResponse.Data?.SelectToken("error")?.SelectToken("instance")?.Value;
                if (!string.IsNullOrEmpty(errorInstance)) {
                    apiResponse.ErrorInstance = errorInstance;
                }
                if (!string.IsNullOrEmpty(errorTitle) || !string.IsNullOrEmpty(errorDetail)) {
                    _logger.LogDebug($"{this.GetType().ToString()} : Known Errors have been found!");
                    apiResponse.ErrorType = "ErrorReturnedByServer";
                    success = true;
                }
                    
            }
            return success;
        }
    }
}