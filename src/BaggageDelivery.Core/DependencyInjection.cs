using BaggageDelivery.Core.AddressLookup;
using BaggageDelivery.Core.Http;
using BaggageDelivery.Core.Interfaces;
using BaggageDelivery.Core.Models;
using BaggageDelivery.Core.Notifications;
using BaggageDelivery.Core.Security;
using BaggageDelivery.Core.Services;
using Microsoft.EntityFrameworkCore;
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
            services.AddScoped<IDespatchCalendar, DespatchCalendar>();
            services.AddScoped<IPaxBookingService, PaxBookingService>();
            services.AddSingleton<IMjmlRenderer, MjmlRenderer>();
            services.AddScoped<INotificationRenderer, NotificationRenderer>();
            services.AddScoped<INotificationService, TucManualMessageSender>();
            services.AddScoped<IJobTrackingLinkService, JobTrackingLinkService>();
            services.AddScoped<ISuburbResolver, SuburbResolver>();
            services.AddScoped<IAvailableServicesQuery, AvailableServicesQuery>();
            services.AddScoped<IServiceAvailabilityService, ServiceAvailabilityService>();
        }

        public void AddInfrastructure(IConfiguration configuration, bool isDevelopment = false)
        {
            _ = isDevelopment;
            services.AddDatabase(configuration);
            services.AddEncryption(configuration);
            services.AddBookingLinks(configuration);
            services.AddDespatchOptions(configuration);
            services.AddAllowedServiceOptions(configuration);
            services.AddTrackingPageUrls(configuration);
            services.AddAddressLookup(configuration);
        }

        private void AddDatabase(IConfiguration configuration)
        {
            services.AddMemoryCache();

            var connectionString = configuration.GetConnectionString("DefaultConnection")
                                   ?? throw new InvalidOperationException(
                                       "Despatch DB connection string not configured (ConnectionStrings:DefaultConnection / ConnectionStrings__DefaultConnection env var).");

            services.AddDbContextPool<BaggageDeliveryContext>(opts =>
            {
                opts.UseSqlServer(connectionString)
                    .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
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

        private void AddDespatchOptions(IConfiguration configuration)
        {
            services.Configure<DespatchOptions>(configuration.GetSection(DespatchOptions.SectionName));
            services.PostConfigure<DespatchOptions>(opts =>
            {
                var timeZone = Environment.GetEnvironmentVariable("TimeZone")
                               ?? configuration["TimeZone"];
                if (!string.IsNullOrEmpty(timeZone))
                {
                    opts.TimeZone = timeZone;
                }

                var supportPhone = Environment.GetEnvironmentVariable("SupportPhone")
                                   ?? configuration["SupportPhone"];
                if (!string.IsNullOrEmpty(supportPhone))
                {
                    opts.SupportPhone = supportPhone;
                }

                var replyTo = Environment.GetEnvironmentVariable("NotificationReplyToEmail")
                              ?? configuration["NotificationReplyToEmail"];
                if (!string.IsNullOrEmpty(replyTo))
                {
                    opts.NotificationReplyToEmail = replyTo;
                }
            });
        }

        private void AddAllowedServiceOptions(IConfiguration configuration)
        {
            services.Configure<AllowedServiceOptions>(
                configuration.GetSection(AllowedServiceOptions.SectionName));
            services.PostConfigure<AllowedServiceOptions>(opts =>
            {
                var enabled = Environment.GetEnvironmentVariable("AddressGuardRailsEnabled")
                              ?? configuration["AddressGuardRailsEnabled"];
                if (bool.TryParse(enabled, out var flag))
                {
                    opts.Enabled = flag;
                }

                var notify = Environment.GetEnvironmentVariable("UnserviceableAddressNotifyEmail")
                             ?? configuration["UnserviceableAddressNotifyEmail"];
                if (!string.IsNullOrEmpty(notify))
                {
                    opts.UnserviceableAddressNotifyEmail = notify;
                }
            });
            services.AddSingleton<IValidateOptions<AllowedServiceOptions>,
                AllowedServiceOptionsValidator>();
            services.AddOptions<AllowedServiceOptions>().ValidateOnStart();
        }

        private void AddTrackingPageUrls(IConfiguration configuration)
        {
            services.Configure<TrackingPageUrlsOptions>(configuration.GetSection(TrackingPageUrlsOptions.SectionName));
            services.PostConfigure<TrackingPageUrlsOptions>(opts =>
            {
                var trackingUrl = Environment.GetEnvironmentVariable("TrackingPageUrl")
                                  ?? configuration["TrackingPageUrl"];
                if (!string.IsNullOrEmpty(trackingUrl))
                {
                    opts.BaseUrl = new Uri(trackingUrl.TrimEnd('/') + "/");
                }
            });
        }

        private void AddAddressLookup(IConfiguration configuration)
        {
            services.Configure<HereMapsOptions>(configuration.GetSection(HereMapsOptions.SectionName));
            services.AddScoped<IAddressLookupService, AddressLookupService>();

            var retryPolicy = HttpPolicyExtensions
                .HandleTransientHttpError()
                .WaitAndRetryAsync(3, attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt)));

            services.AddHttpClient("HereMaps")
                .AddPolicyHandler(retryPolicy)
                .ConfigureHttpClient(c => c.Timeout = TimeSpan.FromSeconds(10));
        }
    }
}