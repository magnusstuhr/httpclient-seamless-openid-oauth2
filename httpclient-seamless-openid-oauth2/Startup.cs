using System;
using System.Net;
using System.Net.Http;
using httpclient_seamless_openid_oauth2.Clients;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Polly;
using Polly.Extensions.Http;
using Polly.Retry;

namespace httpclient_seamless_openid_oauth2
{
    public class Startup
    {
        private readonly IConfiguration _configuration;

        public Startup(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddControllers();

            const string openIdConnectAuthenticationScheme = OpenIdConnectDefaults.AuthenticationScheme;

            var openIdConfig = _configuration.GetSection("Demo");

            services.AddAuthentication(options =>
                {
                    options.DefaultChallengeScheme = openIdConnectAuthenticationScheme;
                })
                .AddOpenIdConnect(openIdConnectAuthenticationScheme, options =>
                {
                    var authority = $"{openIdConfig["Authority"]}";
                    options.Authority = authority;

                    var openIdConnectConfiguration = new OpenIdConnectConfiguration
                    {
                        TokenEndpoint = $"{authority}{openIdConfig["TokenEndpointPath"]}"
                    };

                    options.Configuration = openIdConnectConfiguration;

                    options.ClientId = openIdConfig["ClientId"];
                    options.ClientSecret = openIdConfig["ClientSecret"];
                });

            services.AddClientAccessTokenManagement(options =>
                {
                    options.DefaultClient.Scope = openIdConfig["Scope"];
                })
                .ConfigureBackchannelHttpClient()
                .AddTransientHttpErrorPolicy(CreateRetryPolicy);

            services.AddHttpClient<IDuendeClient, DuendeClient>(client =>
                {
                    client.BaseAddress = new Uri("https://demo.duendesoftware.com/api/");
                })
                .AddClientAccessTokenHandler()
                .AddPolicyHandler(GetDefaultRetryPolicy())
                .AddPolicyHandler(GetDefaultCircuitBreakerPolicy());
        }

        public void Configure(IApplicationBuilder app)
        {
            app.UseHttpsRedirection();
            app.UseRouting();
            app.UseAuthorization();
            app.UseEndpoints(endpoints => { endpoints.MapControllers(); });
        }

        private static IAsyncPolicy<HttpResponseMessage> GetDefaultRetryPolicy()
        {
            return CreateRetryPolicy(BuildDefaultHttpErrorHandling());
        }

        private static IAsyncPolicy<HttpResponseMessage> GetDefaultCircuitBreakerPolicy()
        {
            return BuildDefaultHttpErrorHandling()
                .CircuitBreakerAsync(handledEventsAllowedBeforeBreaking: 3, TimeSpan.FromSeconds(30));
        }

        private static PolicyBuilder<HttpResponseMessage> BuildDefaultHttpErrorHandling()
        {
            return HttpPolicyExtensions
                .HandleTransientHttpError()
                .OrResult(msg => msg.StatusCode == HttpStatusCode.TooManyRequests);
        }

        private static AsyncRetryPolicy<HttpResponseMessage> CreateRetryPolicy(
            PolicyBuilder<HttpResponseMessage> policy)
        {
            return policy.WaitAndRetryAsync(retryCount: 3,
                CalculateSecondsToWaitForRetry);
        }

        private static TimeSpan CalculateSecondsToWaitForRetry(int retryAttempt)
        {
            return TimeSpan.FromSeconds(Math.Pow(2, retryAttempt));
        }
    }
}