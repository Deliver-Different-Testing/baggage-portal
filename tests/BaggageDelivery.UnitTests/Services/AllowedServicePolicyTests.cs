using BaggageDelivery.Core.Http;
using BaggageDelivery.Core.Services;
using Xunit;

namespace BaggageDelivery.UnitTests.Services;

public class AllowedServicePolicyTests
{
    private static readonly AllowedServiceOptions Options = new();

    private static CandidateService Service(
        int jobTypeId = 37,
        int? scheduleId = null,
        string name = "Economy Run",
        string? systemName = "ER",
        string availability = AvailabilityVerdict.Available) =>
        new(jobTypeId, scheduleId, name, systemName, null, availability, null, 180);

    private static ClientServiceFlags Flags(bool economyActive = true, bool economyRuns = true) =>
        new(economyActive, economyRuns);

    [Theory]
    [InlineData(AvailabilityVerdict.Possible)]
    [InlineData(AvailabilityVerdict.Unlikely)]
    [InlineData(AvailabilityVerdict.Unavailable)]
    public void Amber_and_unavailable_verdicts_are_never_offered(string availability)
    {
        Assert.False(AllowedServicePolicy.IsAllowed(
            Service(availability: availability), Options, Flags()));
    }

    [Fact]
    public void An_available_economy_run_is_offered_when_the_client_runs_economy_runs() =>
        Assert.True(AllowedServicePolicy.IsAllowed(Service(), Options, Flags()));

    [Fact]
    public void Economy_run_is_suppressed_when_the_client_does_not_use_runs() =>
        Assert.False(AllowedServicePolicy.IsAllowed(
            Service(systemName: "ER"), Options, Flags(economyRuns: false)));

    [Fact]
    public void Plain_economy_is_suppressed_when_the_client_uses_runs() =>
        Assert.False(AllowedServicePolicy.IsAllowed(
            Service(jobTypeId: 36, name: "Economy", systemName: "EC"), Options, Flags(economyRuns: true)));

    [Fact]
    public void Plain_economy_is_offered_when_the_client_does_not_use_runs() =>
        Assert.True(AllowedServicePolicy.IsAllowed(
            Service(jobTypeId: 36, name: "Economy", systemName: "EC"), Options, Flags(economyRuns: false)));

    [Fact]
    public void Economy_is_suppressed_entirely_when_the_client_has_no_economy() =>
        Assert.False(AllowedServicePolicy.IsAllowed(
            Service(systemName: "ER"), Options, Flags(economyActive: false)));

    [Fact]
    public void Agent_standard_is_offered_because_it_is_road_not_a_flight() =>
        Assert.True(AllowedServicePolicy.IsAllowed(
            Service(jobTypeId: 56, name: "Agent Standard", systemName: null), Options, Flags()));

    [Theory]
    [InlineData(94)]
    [InlineData(95)]
    [InlineData(96)]
    [InlineData(110)]
    [InlineData(120)]
    [InlineData(121)]
    public void Afternoon_home_and_home_delivery_speeds_are_offered(int jobTypeId) =>
        Assert.True(AllowedServicePolicy.IsAllowed(
            Service(jobTypeId: jobTypeId, name: "Afternoon Home", systemName: null), Options, Flags()));

    [Fact]
    public void Direct_drive_sameday_is_denied() =>
        Assert.False(AllowedServicePolicy.IsAllowed(
            Service(jobTypeId: 10, name: "Direct Drive Sameday", systemName: null), Options, Flags()));

    [Fact]
    public void A_row_relabelled_DIRECT_is_denied_whatever_its_speed() =>
        Assert.False(AllowedServicePolicy.IsAllowed(
            Service(jobTypeId: 37, name: "DIRECT", systemName: "ER"), Options, Flags()));

    [Theory]
    [InlineData("UT", "Urgent Tonight")]
    [InlineData("MR", "Medical Run")]
    public void Urgent_tonight_and_medical_run_are_denied(string systemName, string name)
    {
        Assert.False(AllowedServicePolicy.IsAllowed(
            Service(jobTypeId: 35, name: name, systemName: systemName), Options, Flags()));
    }

    [Fact]
    public void An_unrecognised_speed_is_denied_by_default()
    {
        Assert.False(AllowedServicePolicy.IsAllowed(
            Service(jobTypeId: 8, name: "Two Hour", systemName: null), Options, Flags()));
    }

    [Fact]
    public void A_scheduled_service_is_offered_on_its_composite_id()
    {
        Assert.True(AllowedServicePolicy.IsAllowed(
            Service(jobTypeId: 4037, scheduleId: 4, name: "Chch PM run", systemName: "ER"), Options, Flags()));
    }

    [Fact]
    public void A_scheduled_service_over_a_denied_speed_is_still_denied()
    {
        Assert.False(AllowedServicePolicy.IsAllowed(
            Service(jobTypeId: 4010, scheduleId: 4, name: "Direct run", systemName: null), Options, Flags()));
    }

    [Fact]
    public void A_scheduled_service_ignores_the_economy_client_flags()
    {
        Assert.True(AllowedServicePolicy.IsAllowed(
            Service(jobTypeId: 4037, scheduleId: 4, systemName: "ER"), Options, Flags(economyRuns: false)));
    }

    [Fact]
    public void Removing_a_system_name_from_config_stops_it_being_offered()
    {
        var options = new AllowedServiceOptions { AllowedSystemNames = ["EC"] };

        Assert.False(AllowedServicePolicy.IsAllowed(Service(systemName: "ER"), options, Flags()));
    }

    [Fact]
    public void Filter_keeps_only_the_allowed_services_and_preserves_order()
    {
        CandidateService[] candidates =
        [
            Service(jobTypeId: 8, name: "Two Hour", systemName: null),
            Service(jobTypeId: 37, name: "Economy Run", systemName: "ER"),
            Service(jobTypeId: 10, name: "Direct Drive Sameday", systemName: null),
            Service(jobTypeId: 56, name: "Agent Standard", systemName: null)
        ];

        var kept = AllowedServicePolicy.Filter(candidates, Options, Flags());

        Assert.Equal(["Economy Run", "Agent Standard"], kept.Select(s => s.Name));
    }

    [Fact]
    public void The_standard_speed_is_offered_by_name()
    {
        Assert.True(AllowedServicePolicy.IsAllowed(
            Service(jobTypeId: 3, name: "Standard", systemName: "STD"), Options, Flags()));
    }

    [Fact]
    public void The_name_match_ignores_case()
    {
        Assert.True(AllowedServicePolicy.IsAllowed(
            Service(jobTypeId: 3, name: "STANDARD", systemName: null), Options, Flags()));
    }

    [Fact]
    public void A_name_not_on_the_list_is_still_denied()
    {
        Assert.False(AllowedServicePolicy.IsAllowed(
            Service(jobTypeId: 3, name: "Two Hour", systemName: null), Options, Flags()));
    }

    [Fact]
    public void A_denied_speed_is_not_rescued_by_its_name()
    {
        var options = new AllowedServiceOptions { AllowedNames = ["Direct Drive Sameday"] };

        Assert.False(AllowedServicePolicy.IsAllowed(
            Service(jobTypeId: 10, name: "Direct Drive Sameday", systemName: null),
            options, Flags()));
    }

    [Fact]
    public void Removing_a_name_from_config_stops_it_being_offered()
    {
        var options = new AllowedServiceOptions { AllowedNames = [] };

        Assert.False(AllowedServicePolicy.IsAllowed(
            Service(jobTypeId: 3, name: "Standard", systemName: "STD"), options, Flags()));
    }

    private static CandidateService Coloured(string availability, string? colour) =>
        new(37, null, "Economy Run", "ER", null, availability, null, 180, colour);

    [Fact]
    public void A_green_available_row_is_offered()
    {
        Assert.True(AllowedServicePolicy.IsAllowed(
            Coloured(AvailabilityVerdict.Available, "#00FF00"), Options, Flags()));
    }

    [Theory]
    [InlineData("orange")]
    [InlineData("yellow")]
    public void An_amber_row_the_proc_rewrote_to_Available_is_refused(string colour)
    {
        Assert.False(AllowedServicePolicy.IsAllowed(
            Coloured(AvailabilityVerdict.Available, colour), Options, Flags()));
    }

    [Fact]
    public void A_row_with_no_colour_is_offered_so_nationwide_agent_standard_survives()
    {
        var nationwide = new CandidateService(
            56, null, "Agent Standard", null, null, AvailabilityVerdict.Available, null, 4320, null);

        Assert.True(AllowedServicePolicy.IsAllowed(nationwide, Options, Flags()));
    }

    [Fact]
    public void A_red_row_is_refused_even_if_it_claims_to_be_available()
    {
        Assert.False(AllowedServicePolicy.IsAllowed(
            Coloured(AvailabilityVerdict.Available, "#FF0000"), Options, Flags()));
    }

    [Fact]
    public void Colour_matching_ignores_case_and_padding()
    {
        Assert.True(AllowedServicePolicy.IsAllowed(
            Coloured(AvailabilityVerdict.Available, " #00ff00 "), Options, Flags()));
    }
}
