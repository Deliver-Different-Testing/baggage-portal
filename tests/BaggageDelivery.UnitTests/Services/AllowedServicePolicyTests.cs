using BaggageDelivery.Core.Http;
using BaggageDelivery.Core.Services;
using Xunit;

namespace BaggageDelivery.UnitTests.Services;

public class AllowedServicePolicyTests
{
    private static readonly AllowedServiceOptions Options = new();

    private static CandidateService Service(
        int jobTypeId = 3,
        int? scheduleId = null,
        string name = "Standard",
        string? systemName = "STD",
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
    public void The_standard_speed_is_offered_by_name() =>
        Assert.True(AllowedServicePolicy.IsAllowed(Service(), Options, Flags()));

    [Fact]
    public void The_name_match_ignores_case() =>
        Assert.True(AllowedServicePolicy.IsAllowed(
            Service(name: "STANDARD"), Options, Flags()));

    [Fact]
    public void Agent_standard_is_denied_now_only_the_standard_speed_is_enabled() =>
        Assert.False(AllowedServicePolicy.IsAllowed(
            Service(jobTypeId: 56, name: "Agent Standard", systemName: null), Options, Flags()));

    [Theory]
    [InlineData(94)]
    [InlineData(95)]
    [InlineData(96)]
    [InlineData(110)]
    [InlineData(120)]
    [InlineData(121)]
    public void Afternoon_home_and_home_delivery_speeds_are_denied_now_only_standard_is_enabled(
        int jobTypeId) =>
        Assert.False(AllowedServicePolicy.IsAllowed(
            Service(jobTypeId: jobTypeId, name: "Afternoon Home", systemName: null), Options, Flags()));

    [Fact]
    public void Economy_run_is_denied_now_only_the_standard_speed_is_enabled() =>
        Assert.False(AllowedServicePolicy.IsAllowed(
            Service(jobTypeId: 37, name: "Economy Run", systemName: "ER"), Options, Flags()));

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
    public void A_denied_job_type_is_not_rescued_by_being_named_standard() =>
        Assert.False(AllowedServicePolicy.IsAllowed(
            Service(jobTypeId: 10, name: "Standard", systemName: null), Options, Flags()));

    [Fact]
    public void A_scheduled_standard_service_is_offered_on_its_composite_id() =>
        Assert.True(AllowedServicePolicy.IsAllowed(
            Service(jobTypeId: 3003, scheduleId: 3, name: "Standard", systemName: "STD"),
            Options, Flags()));

    [Fact]
    public void A_scheduled_non_standard_service_is_denied() =>
        Assert.False(AllowedServicePolicy.IsAllowed(
            Service(jobTypeId: 4037, scheduleId: 4, name: "Chch PM run", systemName: "ER"),
            Options, Flags()));

    [Fact]
    public void A_scheduled_service_over_a_denied_speed_is_still_denied()
    {
        Assert.False(AllowedServicePolicy.IsAllowed(
            Service(jobTypeId: 4010, scheduleId: 4, name: "Direct run", systemName: null), Options, Flags()));
    }

    [Fact]
    public void A_scheduled_standard_service_is_denied_when_scheduling_is_off()
    {
        var options = new AllowedServiceOptions { AllowScheduledServices = false };

        Assert.False(AllowedServicePolicy.IsAllowed(
            Service(jobTypeId: 3003, scheduleId: 3, name: "Standard", systemName: "STD"),
            options, Flags()));
    }

    [Fact]
    public void Filter_keeps_only_the_standard_speed_and_preserves_order()
    {
        CandidateService[] candidates =
        [
            Service(jobTypeId: 8, name: "Two Hour", systemName: null),
            Service(jobTypeId: 3, name: "Standard", systemName: "STD"),
            Service(jobTypeId: 10, name: "Direct Drive Sameday", systemName: null),
            Service(jobTypeId: 56, name: "Standard", systemName: null)
        ];

        var kept = AllowedServicePolicy.Filter(candidates, Options, Flags());

        Assert.Equal([3, 56], kept.Select(s => s.JobTypeId));
    }

    private static CandidateService Coloured(string availability, string? colour) =>
        new(3, null, "Standard", "STD", null, availability, null, 180, colour);

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
    public void A_row_with_no_colour_is_offered_so_nationwide_standard_survives()
    {
        var nationwide = new CandidateService(
            56, null, "Standard", null, null, AvailabilityVerdict.Available, null, 4320, null);

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
