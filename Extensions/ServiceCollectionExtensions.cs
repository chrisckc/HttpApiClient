using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using HttpApiClient.ErrorParsers;

namespace HttpApiClient.Extensions
{
    public static class ServiceCollectionExtensions
    {        
        public static IHttpClientBuilder AddApiClient<TClient>(this IServiceCollection services, Action<ApiClientOptions<TClient>> configureClient = null, IKnownErrorParser<TClient> errorParser = null)
            where TClient : ApiClient<TClient>
        {
            return AddApiClient<TClient, ApiClientOptions<TClient>>(services, configureClient);
        }

        public static IHttpClientBuilder AddApiClient<TClient, TOptions>(this IServiceCollection services, Action<TOptions> configureClient = null, IKnownErrorParser<TClient> errorParser = null)
            where TClient : ApiClient<TClient>
            where TOptions : ApiClientOptions<TClient>, new()
        {
            if (services == null) throw new ArgumentNullException(nameof(services));
            //if (configureClient == null) throw new ArgumentNullException(nameof(configureClient));

            // I Preferred to just call Invoke on configureClient rather than add another service just to retrieve it later!
            //services.Configure<TOptions>(configureClient);

            // Add the options class to the DI container for use later
            //services.AddSingleton<TOptions>(); // Previous code, removed to avoid duplication in the DI container
            
            var scopeFactory = services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();
            using (var scope = scopeFactory.CreateScope())
            {
                // Get the Service Provider
                var provider = scope.ServiceProvider;
                // Get a logger for the type of ApiClient we are creating
                var logger = provider.GetRequiredService<ILogger<TClient>>();

                // Get a customised ApiClientOptions class from DI container if available
                //TOptions options = provider.GetRequiredService<TOptions>(); // Previous code
                
                // new up the options then add the configured instance to the DI container later
                TOptions options = new TOptions();

                // If we have a configuration registered in DI (usually from config files) use it to configure the options
                var configureOptions = provider.GetService<IConfigureOptions<TOptions>>();
                if (configureOptions != null) {
                    configureOptions.Configure(options);
                }

                // The configuration actions passed in to this method override the configuration registered in DI
                configureClient?.Invoke(options);
                
                if (errorParser != null) {
                    // Use the specified error parser first if provided
                    options.KnownErrorParsers = new List<IKnownErrorParser<TClient>>();
                    options.KnownErrorParsers.Add(errorParser);
                } else {
                    // Populate any error parsers from the Service collection
                    IEnumerable<IKnownErrorParser<TClient>> errorParsers = provider.GetServices<IKnownErrorParser<TClient>>();
                    options.KnownErrorParsers = errorParsers.ToList();
                }
                
                // Add the Problem Details error parser after the supplied ones
                // This will be the last one in the list, the default fallback error parser
                var problemDetailsErrorParserLogger = provider.GetRequiredService<ILogger<ProblemDetailsErrorParser<TClient>>>();
                options.KnownErrorParsers.Add(new ProblemDetailsErrorParser<TClient>(problemDetailsErrorParserLogger));
                
                // Create the builder and configure
                var builder = new ApiClientBuilder<TClient>(services, options, logger);
                
                // Save the configured options into the service collection for later use by the ApiClient
                // Due to this ServiceScope, this needs to be done even if we pulled the options from DI in the first place
                services.AddSingleton<TOptions>(options);
                
                // Finally configure the client
                return builder.ConfigureApiClient();
            }
        }
    }
}
