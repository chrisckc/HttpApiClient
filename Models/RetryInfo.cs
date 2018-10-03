using System;
using System.Collections.Generic;

namespace HttpApiClient.Models
{
    public class RetryInfo
	{
        public int RetryCount { get; set; }
        public List<RetryAttempt> RetryAttempts { get; set; }
    }

    public class RetryAttempt
	{
        public int RetryAttemptNumber { get; set; }
        public TimeSpan RetryDelay { get; set; }
        public string RetryMessage { get; set; }
        public RequestFailure RequestFailure { get; set; }
    }

    public class RequestFailure
	{
        public string Reason { get; set; }
        public int? StatusCode { get; set; }
        public string ContentType { get; set; }
        public string ResponseBody { get; set; }
        public RequestException RequestException { get; set; }
    }

    public class RequestException
	{
        public string Message { get; set; }
        public string Type { get; set; }
        public string Source { get; set; }
    }
}