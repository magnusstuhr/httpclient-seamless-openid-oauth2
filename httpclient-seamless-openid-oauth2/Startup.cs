using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using httpclient_seamless_openid_oauth2.Clients;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
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

            services.AddEndpointsApiExplorer();

            var openIdConfig = _configuration.GetSection("Demo");
            var authority = $"{openIdConfig["Authority"]}";
            var tokenEndpoint = $"{authority}{openIdConfig["TokenEndpointPath"]}";

            services.AddSwaggerGen(swaggerGenOptions =>
            {
                swaggerGenOptions.AddSecurityDefinition("oauth2", new OpenApiSecurityScheme
                {
                    Type = SecuritySchemeType.OAuth2,
                    Flows = new OpenApiOAuthFlows
                    {
                        ClientCredentials = new OpenApiOAuthFlow
                        {
                            TokenUrl = new Uri(tokenEndpoint),
                            Scopes = new Dictionary<string, string>
                            {
                                { "api", "Access to all API endpoints." }
                            },
                        },
                    }
                });
                swaggerGenOptions.AddSecurityRequirement(new OpenApiSecurityRequirement()
                {
                    {
                        new OpenApiSecurityScheme
                        {
                            Reference = new OpenApiReference
                            {
                                Type = ReferenceType.SecurityScheme,
                                Id = "oauth2"
                            },
                            Scheme = "oauth2",
                            Name = "oauth2",
                            In = ParameterLocation.Header
                        },
                        new List<string>()
                    }
                });
            });

            const string openIdConnectAuthenticationScheme = OpenIdConnectDefaults.AuthenticationScheme;


            services.AddAuthentication(options =>
                {
                    options.DefaultChallengeScheme = openIdConnectAuthenticationScheme;
                })
                .AddOpenIdConnect(openIdConnectAuthenticationScheme, options =>
                {
                    options.Authority = authority;
                    var openIdConnectConfiguration = new OpenIdConnectConfiguration
                    {
                        TokenEndpoint = tokenEndpoint
                    };

                    options.Configuration = openIdConnectConfiguration;

                    options.ClientId = openIdConfig["ClientId"];
                    options.ClientSecret = openIdConfig["ClientSecret"];
                })
                .AddJwtBearer(options =>
                {
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ClockSkew = TimeSpan.Zero,
                        ValidateAudience = false,
                        ValidateIssuer = true,
                        ValidateLifetime = true,
                    };
                    var authorizationConfig = _configuration.GetSection("Authorization");

                    options.Authority = authorizationConfig["Authority"];
                    options.Audience = authorizationConfig["Audience"];
                    options.IncludeErrorDetails = true;
                });

            services.AddAuthorization(options =>
            {
                options.AddPolicy("authenticatedUser",
                    builder =>
                    {
                        builder.AuthenticationSchemes = new List<string>
                        {
                            JwtBearerDefaults.AuthenticationScheme
                        };
                        builder.RequireAuthenticatedUser();
                    });
            });

            services.AddClientAccessTokenManagement(options => { options.DefaultClient.Scope = openIdConfig["Scope"]; })
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

            app.UseSwagger();
            app.UseSwaggerUI();
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