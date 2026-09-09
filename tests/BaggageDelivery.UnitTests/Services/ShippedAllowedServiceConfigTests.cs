using BaggageDelivery.Core.Http;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace BaggageDelivery.UnitTests.Services;

public class ShippedAllowedServiceConfigTests
{
    private static IConfigurationRoot ShippedConfiguration() =>
        new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional: false)
            .Build();

    [Fact]
    public void Shipped_appsettings_starts_the_host()
    {
        var configuration = ShippedConfiguration();

        var options = new AllowedServiceOptions();
        configuration.GetSection(AllowedServiceOptions.SectionName).Bind(options);

        if (bool.TryParse(configuration["AddressGuardRailsEnabled"], out var enabled))
        {
            options.Enabled = enabled;
        }

        var notify = configuration["UnserviceableAddressNotifyEmail"];
        if (!string.IsNullOrEmpty(notify))
        {
            options.UnserviceableAddressNotifyEmail = notify;
        }

        var result = new AllowedServiceOptionsValidator().Validate(null, options);

        Assert.True(result.Succeeded, result.FailureMessage);
    }
}
