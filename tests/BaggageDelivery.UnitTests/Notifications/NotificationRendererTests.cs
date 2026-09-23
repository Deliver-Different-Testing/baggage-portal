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
        string airlineName = "Air New Zealand",
        string? fileReference = "AKLNZ12345") =>
        Renderer.RenderBookingLinkAsync(
            new BookingNotificationContext(
                channel, passengerName, airlineName, fileReference, "https://bags.example.com/c/TOKEN"),
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
        var rendered = await Render(airlineName: "Air NZ");

        Assert.Equal(
            "Air NZ: your baggage is here - confirm delivery to receive it (Ref AKLNZ12345)",
            rendered.Subject);
    }

    [Fact]
    public async Task Email_subject_drops_the_reference_when_there_is_none()
    {
        var rendered = await Render(airlineName: "Air NZ", fileReference: "  ");

        Assert.Equal(
            "Air NZ: your baggage is here - confirm delivery to receive it",
            rendered.Subject);
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
        var rendered = await Render(airlineName: "Loganair & Co", passengerName: "Jane & John");

        Assert.Contains("Loganair &amp; Co", rendered.Body);
        Assert.Contains("Jane &amp; John", rendered.Body);
    }

    // ---- SMS -----------------------------------------------------------------

    [Fact]
    public async Task Sms_names_the_airline_and_carries_the_link()
    {
        var rendered = await Render(channel: NotificationChannel.Sms);

        Assert.Equal(
            "Hi Jane, your Air New Zealand baggage is here. " +
            "Confirm delivery to receive it (Ref AKLNZ12345) https://bags.example.com/c/TOKEN",
            rendered.Body);
        Assert.Equal("", rendered.Subject);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Sms_without_a_file_reference_still_carries_the_link(string? fileReference)
    {
        var rendered = await Render(channel: NotificationChannel.Sms, fileReference: fileReference);

        Assert.Equal(
            "Hi Jane, your Air New Zealand baggage is here. " +
            "Confirm delivery to receive it https://bags.example.com/c/TOKEN",
            rendered.Body);
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
        string airlineName = "Air New Zealand",
        string? fileReference = "AKLNZ12345",
        string jobNumber = "URG-179252",
        string? trackingUrl = "https://tracking.example.com/#/TOKEN") =>
        Renderer.RenderBookingConfirmedAsync(
            new BookingConfirmedNotificationContext(
                channel, passengerName, airlineName, fileReference, jobNumber,
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

    [Fact]
    public async Task Confirmed_email_says_the_baggage_is_booked_in_for_delivery()
    {
        var rendered = await RenderConfirmed();

        Assert.Contains("booked in for delivery", rendered.Body);
    }

    [Fact]
    public async Task Confirmed_email_quotes_the_job_number_as_the_tracking_number()
    {
        var rendered = await RenderConfirmed(jobNumber: "URG-179252");

        Assert.Contains("Urgent Couriers will deliver your baggage - Tracking Number", rendered.Body);
        Assert.Contains("URG-179252", rendered.Body);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Confirmed_email_omits_the_tracking_number_line_without_a_job_number(string jobNumber)
    {
        var rendered = await RenderConfirmed(jobNumber: jobNumber);

        Assert.DoesNotContain("Tracking Number", rendered.Body);
        Assert.Contains("Tomorrow, Thu 11 Jun", rendered.Body);
    }

    [Fact]
    public async Task Confirmed_email_lists_what_happens_next()
    {
        var rendered = await RenderConfirmed();

        Assert.Contains("What happens next", rendered.Body);
        Assert.Contains(
            "We collect your bag and deliver it to your address within the Delivery Window.",
            rendered.Body);
        Assert.Contains(
            "We will text/email you when your bag is collected from the airport with a tracking link.",
            rendered.Body);
        Assert.Contains(
            "You can track the driver from the airport to your address.",
            rendered.Body);
    }

    [Fact]
    public async Task Confirmed_email_invites_a_reply_when_something_changes()
    {
        var rendered = await RenderConfirmed();

        Assert.Contains("If anything changes please reply to this email with your update.", rendered.Body);
    }

    [Fact]
    public async Task Confirmed_email_escapes_markup_significant_characters_in_supplied_values()
    {
        var rendered = await RenderConfirmed(
            airlineName: "Loganair & Co", passengerName: "Jane & John", jobNumber: "URG&179252");

        Assert.Contains("Loganair &amp; Co", rendered.Body);
        Assert.Contains("Jane &amp; John", rendered.Body);
        Assert.Contains("URG&amp;179252", rendered.Body);
    }
}
