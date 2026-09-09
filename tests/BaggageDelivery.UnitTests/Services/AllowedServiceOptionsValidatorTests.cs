using BaggageDelivery.Core.Http;
using Xunit;

namespace BaggageDelivery.UnitTests.Services;

public class AllowedServiceOptionsValidatorTests
{
    private static readonly AllowedServiceOptionsValidator Validator = new();

    [Fact]
    public void Disabled_guard_rails_need_no_airline_address()
    {
        var result = Validator.Validate(null, new AllowedServiceOptions { Enabled = false });

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Enabled_guard_rails_require_an_airline_address()
    {
        var result = Validator.Validate(null, new AllowedServiceOptions
        {
            Enabled = true,
            UnserviceableAddressNotifyEmail = string.Empty
        });

        Assert.True(result.Failed);
        Assert.Contains("UnserviceableAddressNotifyEmail", result.FailureMessage);
    }

    [Fact]
    public void Whitespace_is_not_an_airline_address()
    {
        var result = Validator.Validate(null, new AllowedServiceOptions
        {
            Enabled = true,
            UnserviceableAddressNotifyEmail = "   "
        });

        Assert.True(result.Failed);
    }

    [Fact]
    public void Enabled_guard_rails_with_an_airline_address_pass()
    {
        var result = Validator.Validate(null, new AllowedServiceOptions
        {
            Enabled = true,
            UnserviceableAddressNotifyEmail = "ops@airline.test"
        });

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Enabled_guard_rails_need_something_on_the_allow_list()
    {
        var result = Validator.Validate(null, new AllowedServiceOptions
        {
            Enabled = true,
            UnserviceableAddressNotifyEmail = "ops@airline.test",
            AllowedSystemNames = [],
            AllowedJobTypeIds = [],
            AllowedNames = [],
            AllowScheduledServices = false
        });

        Assert.True(result.Failed);
        Assert.Contains("allow", result.FailureMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Scheduled_services_alone_are_a_valid_allow_list()
    {
        var result = Validator.Validate(null, new AllowedServiceOptions
        {
            Enabled = true,
            UnserviceableAddressNotifyEmail = "ops@airline.test",
            AllowedSystemNames = [],
            AllowedJobTypeIds = [],
            AllowedNames = [],
            AllowScheduledServices = true
        });

        Assert.True(result.Succeeded);
    }
}
