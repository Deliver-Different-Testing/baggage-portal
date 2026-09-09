using BaggageDelivery.Core.Http;
using BaggageDelivery.Core.Interfaces;
using BaggageDelivery.Core.Models;
using BaggageDelivery.Core.Notifications;
using BaggageDelivery.UnitTests.Helpers;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;

namespace BaggageDelivery.UnitTests.Notifications;

public class AddressUnserviceableNotificationTests
{
    private static AddressUnserviceableNotificationContext Context() =>
        new(
            Channel: NotificationChannel.Email,
            AirlineName: "Air NZ",
            JobNumber: "URG-99123",
            FileReference: "AKLNZ12345",
            PassengerName: "Jane Pax",
            PassengerPhone: "+64211234567",
            PassengerEmail: "jane@example.com",
            CurrentAddress: "12 Old Road, Ponsonby, Auckland, 1011, NZ",
            RequestedAddress: "5 Remote Track, Haast, West Coast, 7886, NZ");

    [Fact]
    public async Task Renderer_names_the_job_the_passenger_and_both_addresses()
    {
        var renderer = new NotificationRenderer(new Mjml.Net.MjmlRenderer());

        var rendered = await renderer.RenderAddressUnserviceableAsync(
            Context(), TestContext.Current.CancellationToken);

        Assert.Contains("URG-99123", rendered.Subject);
        Assert.Contains("URG-99123", rendered.Body);
        Assert.Contains("AKLNZ12345", rendered.Body);
        Assert.Contains("Jane Pax", rendered.Body);
        Assert.Contains("+64211234567", rendered.Body);
        Assert.Contains("jane@example.com", rendered.Body);
        Assert.Contains("5 Remote Track, Haast, West Coast, 7886, NZ", rendered.Body);
        Assert.Contains("12 Old Road, Ponsonby, Auckland, 1011, NZ", rendered.Body);
    }

    [Fact]
    public async Task Renderer_asks_the_airline_to_contact_the_passenger()
    {
        var renderer = new NotificationRenderer(new Mjml.Net.MjmlRenderer());

        var rendered = await renderer.RenderAddressUnserviceableAsync(
            Context(), TestContext.Current.CancellationToken);

        Assert.Contains("contact", rendered.Body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Renderer_copes_with_a_passenger_who_left_contact_details_blank()
    {
        var renderer = new NotificationRenderer(new Mjml.Net.MjmlRenderer());

        var rendered = await renderer.RenderAddressUnserviceableAsync(
            Context() with { PassengerPhone = null, PassengerEmail = null },
            TestContext.Current.CancellationToken);

        Assert.Contains("Jane Pax", rendered.Body);
    }

    [Fact]
    public async Task Sender_enqueues_an_email_row_against_the_job()
    {
        await using var db = InMemoryDb.NewContext();
        var renderer = Substitute.For<INotificationRenderer>();
        renderer.RenderAddressUnserviceableAsync(
                Arg.Any<AddressUnserviceableNotificationContext>(), Arg.Any<CancellationToken>())
            .Returns(new RenderedNotification("Address check needed - URG-99123", "body text"));
        var time = new FakeTimeProvider(new DateTime(2026, 9, 9, 3, 0, 0, DateTimeKind.Utc));
        var options = Microsoft.Extensions.Options.Options.Create(
            new DespatchOptions { NotificationReplyToEmail = "baggage@urgent.co.nz" });

        var sender = new TucManualMessageSender(db, renderer, options, time);

        await sender.SendAddressUnserviceableAsync(
            jobId: 77, recipient: "ops@airnz.co.nz", Context(), TestContext.Current.CancellationToken);

        var row = await db.TucManualMessages.AsNoTracking().SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(77, row.JobId);
        Assert.Equal("ops@airnz.co.nz", row.SendToEmailAddress);
        Assert.Equal("baggage@urgent.co.nz", row.ReplyToEmailAddress);
        Assert.Null(row.SendToMobile);
        Assert.Equal("Address check needed - URG-99123", row.Subject);
        Assert.Equal("body text", row.UcmmMessage);
        Assert.False(row.UcmmSent);
        Assert.Equal(time.GetUtcNow().UtcDateTime, row.UcmmDate);
    }
}
