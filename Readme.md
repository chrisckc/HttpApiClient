# HttpApiClient

This .Net Core 2.1 Library provides a wrapper around a 'Typed' HttpClient generated using HttpClientFactory and extended with the Polly library to provide resilience and transient-fault-handling. It is designed as a generic HTTP API Client for third party JSON API's.

This library makes easy and clean work of using multiple HttpClients by allowing sub-classing and customising of ApiClients configured to point to different API's. It's main purpose is to encapsulate much of the plumbing code required to get Polly setup for each client and fix any issues with HttpClient.

The ApiClient class acts as a wrapper around HttpClient, encapsulating the Polly configuration and providing some useful but limited configuration options. Logging has been added to show when retries are taking place.

Configuration is provided by the Options framework using the IConfigureOptions interface.
The Configuration object ApiClientOptions can be sub-classed to add additional configuration options which are made available to the sub-classed ApiClient in it's constructor. The .Net Core source code was useful in figuring out how to create the required ServiceCollection extension methods.

Response data is returned as part of an ApiResponse object, as either a JObject or a string depending on the Content-Type header received. A Library such as AutoMapper could then be used to hydrate your own business objects from the JObject if required.

If the Content-Type header received is not "application/json" or "application/problem+json", the response is read as a string. This feature has been added because it is often the case that some 3rd party JSON API's can return unexpected and inconsistent content types when handling errors.

Other useful info about the response is returned as part of the ApiResponse object including information about any request retries that occurred while getting the response.


## Using the Library

ApiClient must be sub-classed to be used and is intended to be extended to suit the particular use case.

DemoApiClient and DemoApiClientOptions are sub-classed from ApiClient and ApiClientOptions.

As in the standard HttpClient, configuration options can be provided from appsettings.json files
```
// Register the options for the DemoApiClient populated from the app config
services.Configure<DemoApiClientOptions>(configuration.GetSection("DemoApi"));
```

As in the standard HttpClient, configuration options can be provided in the configuration lambda passed to the Action delegate parameter of the ServiceCollection extension method.
```
// Configures the ApiClient and provides additional option values
services.AddApiClient<DemoApiClient, DemoApiClientOptions>(options => {
        options.BaseUrl = new Uri(configuration["Api:BaseUrl"]);
        options.UserAgent = "ApiClient";
        options.RequestTimeout = 60;
        options.RetryCount = 3;
        options.RetryWaitDuration = 2;
        options.UseExponentialRetryWaitDuration = true;
});
```

Sub-classing ApiClientOptions is optional, just use `ApiClientOptions<TClient>` ...

```
// Register the options for the SampleApiClient populated from the app config
services.Configure<ApiClientOptions<SampleApiClient>>(configuration.GetSection("SampleApi"));

// Configures the ApiClient using an ApiClientOptions object of same type as the client
services.AddApiClient<SampleApiClient>(options => {
        options.BaseUrl = new Uri(configuration["Api:BaseUrl"]);
        options.UserAgent = "ApiClient";
        options.RequestTimeout = 60;
        options.RetryCount = 5;
        options.RetryWaitDuration = 10;
});
```

Note: Options provided in the configuration lambda override options from config files.

Refer to the sample project for usage examples:
https://github.com/chrisckc/HttpApiClientDemo.git

## Features

ServiceCollection extension methods provided for clean configuration and addition of ApiClients into the DI container.

Provides separate instances of configuration options for each ApiClient sub-class. Customised configuration options can be provided via an ApiClientOptions sub-class.

Ability to specify the Polly retry count, wait duration and which Http StatusCodes and Methods to retry on with sensible defaults built-in.

Optional exponential retry-wait duration provided by UseExponentialRetryWaitDuration in the style of typical Optimistic concurrency exponential back-off routines.

Supports request cancellation

IKnownErrorParser interface is provided for supplying a class to be used to extract error information from responses.
```
services.AddSingleton<IKnownErrorParser<DemoApiClient>, DemoErrorParser>();
```

There is a built in KnownErrorParser for parsing an RFC Problem Details object as described here:
https://tools.ietf.org/html/rfc7807

Multiple KnownErrorParsers can be added, running KnownErrorParsers against the response object stops when one of the KnownErrorParsers finds something and returns true. The built-in ProblemDetailsErrorParser runs last. Refer to the sample project for examples.

## Advantages

Encapsulates all of the Polly configuration for multiple HttpClients resulting in a much cleaner ConfigureServices method in app Startup.

It fixes the issue where it is not possible to differentiate between a request timeout and a request being cancelled as both appear as an OperationCanceledException.

HttpClient throws TaskCanceledException on timeout (looks like its only going to be fixed in v3.0)
https://github.com/dotnet/corefx/issues/20296

The TaskCanceledException on timeout solution used here is based on this blog post:
https://www.thomaslevesque.com/2018/02/25/better-timeout-handling-with-httpclient/

## References

A How-To issue that I logged: ".Net Core: Access Information about retry attempts from HttpResponseMessage returned from HttpClient"
https://github.com/App-vNext/Polly/issues/505

Resulting in updated Docs: https://github.com/App-vNext/Polly/wiki/Polly-and-HttpClientFactory

[already possible] Respect Retry-After HTTP header:
https://github.com/App-vNext/Polly/issues/414

Using Execution Context in Polly:
http://www.thepollyproject.org/2017/05/04/putting-the-context-into-polly/

How to use HttpClientHandler with IHttpClientFactory:
https://github.com/aspnet/HttpClientFactory/issues/71

https://docs.microsoft.com/en-us/aspnet/core/fundamentals/configuration/options?view=aspnetcore-2.1

Source code references:
https://github.com/aspnet/Options/tree/master/src/Microsoft.Extensions.Options

https://github.com/aspnet/HttpClientFactory/blob/master/src/Microsoft.Extensions.Http/DependencyInjection/HttpClientFactoryServiceCollectionExtensions.cs

https://github.com/aspnet/HttpClientFactory/blob/master/src/Microsoft.Extensions.Http/DependencyInjection/HttpClientBuilderExtensions.cs

HttpClient throws TaskCanceledException on timeout (looks like its only going to be fixed in v3.0)
https://github.com/dotnet/corefx/issues/20296

https://www.thomaslevesque.com/2018/02/25/better-timeout-handling-with-httpclient/

http://thedatafarm.com/dotnet/twitter-education-re-aspnet-core-scope/


## Useful info

List of Http StatusCodes in the .NET Core HttpStatusCode enum

https://github.com/dotnet/corefx/issues/4382
```
public enum HttpStatusCode
    {
        // Informational 1xx
        Continue = 100,
        SwitchingProtocols = 101,
+       Processing = 102,
+       EarlyHints = 103,

        // Successful 2xx
        OK = 200,
        Created = 201,
        Accepted = 202,
        NonAuthoritativeInformation = 203,
        NoContent = 204,
        ResetContent = 205,
        PartialContent = 206,
+       MultiStatus = 207,
+       AlreadyReported = 208,
        
+       IMUsed = 226,

        // Redirection 3xx
        MultipleChoices = 300,
        Ambiguous = 300,
        MovedPermanently = 301,
        Moved = 301,
        Found = 302,
        Redirect = 302,
        SeeOther = 303,
        RedirectMethod = 303,
        NotModified = 304,
        UseProxy = 305,
        Unused = 306,
        TemporaryRedirect = 307,
        RedirectKeepVerb = 307,
+       PermanentRedirect = 308,

        // Client Error 4xx
        BadRequest = 400,
        Unauthorized = 401,
        PaymentRequired = 402,
        Forbidden = 403,
        NotFound = 404,
        MethodNotAllowed = 405,
        NotAcceptable = 406,
        ProxyAuthenticationRequired = 407,
        RequestTimeout = 408,
        Conflict = 409,
        Gone = 410,
        LengthRequired = 411,
        PreconditionFailed = 412,
        RequestEntityTooLarge = 413,
        RequestUriTooLong = 414,
        UnsupportedMediaType = 415,
        RequestedRangeNotSatisfiable = 416,
        ExpectationFailed = 417,
+       // Removed status code: ImATeapot = 418,

+       MisdirectedRequest = 421,
+       UnprocessableEntity = 422,
+       Locked = 423,
+       FailedDependency = 424,

        UpgradeRequired = 426,
        
+       PreconditionRequired = 428,
+       TooManyRequests = 429,
        
+       RequestHeaderFieldsTooLarge = 431,
        
+       UnavailableForLegalReasons = 451,

        // Server Error 5xx
        InternalServerError = 500,
        NotImplemented = 501,
        BadGateway = 502,
        ServiceUnavailable = 503,
        GatewayTimeout = 504,
        HttpVersionNotSupported = 505,
+       VariantAlsoNegotiates = 506,
+       InsufficientStorage = 507,
+       LoopDetected = 508,
        
+       NotExtended = 510,
+       NetworkAuthenticationRequired = 511,
    }
```