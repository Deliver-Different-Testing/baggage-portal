using BaggageDelivery.Core.Models;
using BaggageDelivery.Core.Notifications;
using Mjml.Net;
using Xunit;

namespace BaggageDelivery.UnitTests.Notifications;

public class NotificationRendererTests
{
    private static readonly NotificationRenderer Renderer = new(new MjmlRenderer());

    private static Task<RenderedNotification> Render(
        string channel = NotificationChannel.Email,
        string passengerName = "Jane Pax",
        string airlineLabel = "Air New Zealand",
        string? fileReference = "AKLNZ12345") =>
        Renderer.RenderBookingLinkAsync(
            new BookingNotificationContext(
                channel, passengerName, airlineLabel, fileReference, "https://bags.example.com/c/TOKEN"),
            TestContext.Current.CancellationToken);

    // ---- Email: the reference the portal shows -------------------------------

    [Fact]
    public async Task Email_quotes_the_file_reference_alongside_the_airline()
    {
        var rendered = await Render(fileReference: "AKLNZ12345");

        Assert.Contains("File Reference: AKLNZ12345", rendered.Body);
        Assert.Contains("Air New Zealand", rendered.Body);
    }

    [Fact]
    public async Task Email_never_calls_it_a_booking_reference()
    {
        var rendered = await Render();

        Assert.DoesNotContain("Booking Reference", rendered.Body);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Email_without_a_file_reference_shows_the_airline_alone(string? fileReference)
    {
        // The portal hides the tag rather than showing the internal id behind the
        // link, so the email that sends the passenger there does the same.
        var rendered = await Render(fileReference: fileReference);

        Assert.DoesNotContain("File Reference", rendered.Body);
        Assert.DoesNotContain("·", rendered.Body);
        Assert.Contains("Air New Zealand", rendered.Body);
    }

    // ---- Email: naming and attribution ---------------------------------------

    [Fact]
    public async Task Email_subject_names_the_airline()
    {
        var rendered = await Render(airlineLabel: "Air New Zealand");

        Assert.Equal("Your Air New Zealand baggage is ready for delivery", rendered.Subject);
    }

    [Fact]
    public async Task Email_carries_the_powered_by_attribution_in_the_footer()
    {
        var rendered = await Render();

        Assert.Contains("Powered by Deliver DFRNT", rendered.Body);
        // The attribution is the sign-off, not the headline: it sits after the
        // call to action.
        Assert.True(
            rendered.Body.IndexOf("Powered by Deliver DFRNT", StringComparison.Ordinal) >
            rendered.Body.IndexOf("Confirm delivery details", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Email_greets_the_passenger_and_links_to_the_confirm_page()
    {
        var rendered = await Render(passengerName: "Jane Pax");

        Assert.Contains("Hi Jane Pax,", rendered.Body);
        Assert.Contains("https://bags.example.com/c/TOKEN", rendered.Body);
    }

    [Fact]
    public async Task Email_escapes_markup_significant_characters_in_supplied_values()
    {
        // tucClient.UcclName is free text — an ampersand in it must not break the
        // MJML parse or land raw in the html.
        var rendered = await Render(airlineLabel: "Loganair & Co", passengerName: "Jane & John");

        Assert.Contains("Loganair &amp; Co", rendered.Body);
        Assert.Contains("Jane &amp; John", rendered.Body);
    }

    // ---- SMS -----------------------------------------------------------------

    [Fact]
    public async Task Sms_names_the_airline_and_carries_the_link()
    {
        var rendered = await Render(channel: NotificationChannel.Sms);

        Assert.Equal(
            "Hi Jane, your Air New Zealand baggage is ready for delivery. " +
            "Confirm your address and time slot: https://bags.example.com/c/TOKEN",
            rendered.Body);
        Assert.Equal("", rendered.Subject);
    }

    [Fact]
    public async Task Unknown_channel_throws()
    {
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => Render(channel: "carrier-pigeon"));
    }

    // ---- Booking confirmed ---------------------------------------------------

    private static Task<RenderedNotification> RenderConfirmed(
        string channel = NotificationChannel.Email,
        string passengerName = "Jane Pax",
        string airlineLabel = "Air New Zealand",
        string? fileReference = "AKLNZ12345",
        string? trackingUrl = "https://tracking.example.com/#/TOKEN") =>
        Renderer.RenderBookingConfirmedAsync(
            new BookingConfirmedNotificationContext(
                channel, passengerName, airlineLabel, fileReference,
                "Tomorrow, Thu 11 Jun", "2:00 PM – 4:00 PM", trackingUrl),
            TestContext.Current.CancellationToken);

    [Fact]
    public async Task Confirmed_sms_states_the_slot_and_the_tracking_link()
    {
        var rendered = await RenderConfirmed(NotificationChannel.Sms);

        Assert.Equal("", rendered.Subject);
        Assert.Contains("Jane", rendered.Body);
        Assert.Contains("Tomorrow, Thu 11 Jun", rendered.Body);
        Assert.Contains("2:00 PM", rendered.Body);
        Assert.Contains("https://tracking.example.com/#/TOKEN", rendered.Body);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task Confirmed_sms_omits_the_tracking_sentence_when_there_is_no_link(string? trackingUrl)
    {
        var rendered = await RenderConfirmed(NotificationChannel.Sms, trackingUrl: trackingUrl);

        Assert.DoesNotContain("Track", rendered.Body);
        Assert.Contains("Tomorrow, Thu 11 Jun", rendered.Body);
    }

    [Fact]
    public async Task Confirmed_email_links_the_tracking_url_and_names_the_airline()
    {
        var rendered = await RenderConfirmed();

        Assert.Contains("Your Air New Zealand baggage delivery is booked", rendered.Subject);
        Assert.Contains("File Reference: AKLNZ12345", rendered.Body);
        Assert.Contains("https://tracking.example.com/#/TOKEN", rendered.Body);
        Assert.Contains("Tomorrow, Thu 11 Jun", rendered.Body);
    }

    [Fact]
    public async Task Confirmed_email_without_a_tracking_url_has_no_tracking_button()
    {
        var rendered = await RenderConfirmed(trackingUrl: null);

        Assert.DoesNotContain("Track your delivery", rendered.Body);
        Assert.Contains("Tomorrow, Thu 11 Jun", rendered.Body);
    }
}
