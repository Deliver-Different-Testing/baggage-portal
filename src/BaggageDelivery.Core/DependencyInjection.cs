using System.Net;
using Amazon.SecretsManager;
using BaggageDelivery.Core.Http;
using BaggageDelivery.Core.Interfaces;
using BaggageDelivery.Core.Models;
using BaggageDelivery.Core.MultiTenant;
using BaggageDelivery.Core.Notifications;
using BaggageDelivery.Core.Secrets;
using BaggageDelivery.Core.Security;
using BaggageDelivery.Core.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Mjml.Net;
using Polly;
using Polly.Extensions.Http;

namespace BaggageDelivery.Core;

public static class DependencyInjection
{
    extension(IServiceCollection services)
    {
        public void AddApplication()
        {
            services.AddSingleton(TimeProvider.System);
            services.AddSingleton<IEncryptionService, EncryptionService>();
            services.AddScoped<IPaxBookingService, PaxBookingService>();
            services.AddScoped<IPaxTrackingService, PaxTrackingService>();
            services.AddScoped<IDespatchJobReleaseService, DespatchJobReleaseService>();
            services.AddScoped<IBookingLinkDispatchService, BookingLinkDispatchService>();
            services.AddSingleton<IMjmlRenderer, MjmlRenderer>();
            services.AddScoped<INotificationRenderer, NotificationRenderer>();
            services.AddScoped<INotificationService, NotificationService>();
            services.AddScoped<ISmsSender, TwilioSmsSender>();
            services.AddScoped<IEmailSender, SendGridEmailSender>();
            services.AddScoped<ConfigTenantResolver>();
            services.AddScoped<ITenantResolver>(sp => new CachingTenantResolver(
                sp.GetRequiredService<ConfigTenantResolver>(),
                sp.GetRequiredService<IMemoryCache>()));
        }

        public void AddInfrastructure(IConfiguration configuration, bool isDevelopment = false)
        {
            services.AddDatabase(configuration);
            services.AddAwsServices(isDevelopment);
            services.AddNotifications(configuration);
            services.AddEncryption(configuration);
            services.AddBookingLinks(configuration);
            services.AddDespatchClient(configuration);
        }

        private void AddDatabase(IConfiguration configuration)
        {
            services.AddMemoryCache();

            var connectionString = configuration["SQLCredentials"]
                ?? configuration.GetConnectionString("Despatch")
                ?? throw new InvalidOperationException(
                    "Despatch DB connection string not configured (SQLCredentials env var or ConnectionStrings:Despatch).");

            services.AddDbContextPool<BaggageDeliveryContext>(opts =>
            {
                opts.UseSqlServer(connectionString)
                    .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
            });
        }

        private void AddAwsServices(bool isDevelopment)
        {
            if (isDevelopment)
            {
                services.AddSingleton<ISecretsService, InMemorySecretsService>();
            }
            else
            {
                services.AddAWSService<IAmazonSecretsManager>();
                services.AddScoped<ISecretsService, AwsSecretsService>();
            }
        }

        private void AddNotifications(IConfiguration configuration)
        {
            services.Configure<TwilioOptions>(configuration.GetSection(TwilioOptions.SectionName));
            services.PostConfigure<TwilioOptions>(opts =>
            {
                opts.AccountSid = Environment.GetEnvironmentVariable("TwilioAccountSid") ?? opts.AccountSid;
                opts.AuthToken = Environment.GetEnvironmentVariable("TwilioAuthToken") ?? opts.AuthToken;
                opts.FromNumber = Environment.GetEnvironmentVariable("TwilioFromNumber") ?? opts.FromNumber;
            });

            services.Configure<SendGridOptions>(configuration.GetSection(SendGridOptions.SectionName));
            services.PostConfigure<SendGridOptions>(opts =>
            {
                opts.ApiKey = Environment.GetEnvironmentVariable("SendGridApiKey") ?? opts.ApiKey;
            });
        }

        private void AddEncryption(IConfiguration configuration)
        {
            services.Configure<EncryptionOptions>(configuration.GetSection(EncryptionOptions.SectionName));
            services.PostConfigure<EncryptionOptions>(opts =>
            {
                opts.Key = Environment.GetEnvironmentVariable("BaggageDeliveryEncryptionKey") ?? opts.Key;
                opts.IV = Environment.GetEnvironmentVariable("BaggageDeliveryEncryptionIV") ?? opts.IV;
            });
            services.AddSingleton<IValidateOptions<EncryptionOptions>, EncryptionOptionsValidator>();
        }

        private void AddBookingLinks(IConfiguration configuration)
        {
            services.Configure<BookingLinkOptions>(configuration.GetSection(BookingLinkOptions.SectionName));
            services.PostConfigure<BookingLinkOptions>(opts =>
            {
                var publicUrl = Environment.GetEnvironmentVariable("BaggageDeliveryPublicBaseUrl");
                if (!string.IsNullOrEmpty(publicUrl))
                {
                    opts.PublicBaseUrl = publicUrl;
                }
            });
        }

        private void AddDespatchClient(IConfiguration configuration)
        {
            services.Configure<DespatchUrlsOptions>(configuration.GetSection(DespatchUrlsOptions.SectionName));
            services.PostConfigure<DespatchUrlsOptions>(opts =>
            {
                var apiUrl = Environment.GetEnvironmentVariable("WebAPIUrl")
                             ?? configuration["WebAPIUrl"];
                if (!string.IsNullOrEmpty(apiUrl))
                {
                    opts.ApiBaseUrl = new Uri(apiUrl.TrimEnd('/') + "/");
                }
            });

            var retryPolicy = HttpPolicyExtensions
                .HandleTransientHttpError()
                .WaitAndRetryAsync(3, attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt)));

            var circuitBreakerPolicy = HttpPolicyExtensions
                .HandleTransientHttpError()
                .CircuitBreakerAsync(5, TimeSpan.FromSeconds(30));

            services.AddHttpClient<IDespatchApiClient, DespatchApiClient>(client =>
                {
                    client.Timeout = TimeSpan.FromSeconds(30);
                })
                .AddPolicyHandler(retryPolicy)
                .AddPolicyHandler(circuitBreakerPolicy)
                .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
                {
                    AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
                });
        }
    }
}
