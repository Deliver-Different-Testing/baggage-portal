using BaggageDelivery.Core.Http;
using BaggageDelivery.Core.Interfaces;
using BaggageDelivery.Core.Models;
using BaggageDelivery.Core.Notifications;
using BaggageDelivery.UnitTests.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace BaggageDelivery.UnitTests.Notifications;

public class TucManualMessageSenderTests
{
    private static IOptions<DespatchOptions> Options(
        string replyTo = "baggage@urgent.co.nz") =>
        Microsoft.Extensions.Options.Options.Create(
            new DespatchOptions { NotificationReplyToEmail = replyTo });

    [Fact]
    public async Task Sms_writes_row_with_SendToMobile_and_rendered_body()
    {
        await using var db = InMemoryDb.NewContext();
        var renderer = Substitute.For<INotificationRenderer>();
        renderer.RenderBookingLinkAsync(Arg.Any<BookingNotificationContext>(), Arg.Any<CancellationToken>())
            .Returns(new RenderedNotification(Subject: "", Body: "Hi Jane, confirm here: https://x"));
        var time = new FakeTimeProvider(new DateTime(2026, 6, 10, 12, 0, 0, DateTimeKind.Utc));

        var sender = new TucManualMessageSender(db, renderer, Options(), time);

        await sender.SendBookingLinkAsync(
            jobId: 42, recipient: "+64211234567",
            context: new BookingNotificationContext(
                NotificationChannel.Sms, "Jane Pax", "Air NZ", "REF-1", "https://x"),
            ct: TestContext.Current.CancellationToken);

        var row = await db.TucManualMessages.AsNoTracking().SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(42, row.JobId);
        Assert.Equal("+64211234567", row.SendToMobile);
        Assert.Null(row.SendToEmailAddress);
        Assert.Null(row.ReplyToEmailAddress);
        Assert.Equal("", row.Subject);
        Assert.Equal("Hi Jane, confirm here: https://x", row.UcmmMessage);
        Assert.False(row.UcmmSent);
        Assert.False(row.Read);
        Assert.Equal(0, row.UcmmAttempts);
        Assert.Equal(time.GetUtcNow().UtcDateTime, row.UcmmDate);
    }

    [Fact]
    public async Task Email_writes_row_with_SendToEmailAddress_and_subject()
    {
        await using var db = InMemoryDb.NewContext();
        var renderer = Substitute.For<INotificationRenderer>();
        renderer.RenderBookingLinkAsync(Arg.Any<BookingNotificationContext>(), Arg.Any<CancellationToken>())
            .Returns(new RenderedNotification(
                Subject: "Your Air NZ baggage is ready for delivery",
                Body: "<html>hi</html>"));
        var time = new FakeTimeProvider(new DateTime(2026, 6, 10, 12, 0, 0, DateTimeKind.Utc));

        var sender = new TucManualMessageSender(db, renderer, Options(), time);

        await sender.SendBookingLinkAsync(
            jobId: 99, recipient: "jane@example.com",
            context: new BookingNotificationContext(
                NotificationChannel.Email, "Jane Pax", "Air NZ", "REF-1", "https://x"),
            ct: TestContext.Current.CancellationToken);

        var row = await db.TucManualMessages.AsNoTracking().SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(99, row.JobId);
        Assert.Equal("jane@example.com", row.SendToEmailAddress);
        Assert.Null(row.SendToMobile);
        Assert.Equal("Your Air NZ baggage is ready for delivery", row.Subject);
        Assert.Equal("<html>hi</html>", row.UcmmMessage);
        Assert.Equal("baggage@urgent.co.nz", row.ReplyToEmailAddress);
    }

    [Fact]
    public async Task Email_reply_to_comes_from_configuration()
    {
        await using var db = InMemoryDb.NewContext();
        var renderer = Substitute.For<INotificationRenderer>();
        renderer.RenderBookingLinkAsync(Arg.Any<BookingNotificationContext>(), Arg.Any<CancellationToken>())
            .Returns(new RenderedNotification(Subject: "s", Body: "b"));
        var time = new FakeTimeProvider(new DateTime(2026, 6, 10, 12, 0, 0, DateTimeKind.Utc));

        var sender = new TucManualMessageSender(db, renderer, Options("bags@example.com"), time);

        await sender.SendBookingLinkAsync(
            jobId: 7, recipient: "jane@example.com",
            context: new BookingNotificationContext(
                NotificationChannel.Email, "Jane Pax", "Air NZ", "REF-1", "https://x"),
            ct: TestContext.Current.CancellationToken);

        var row = await db.TucManualMessages.AsNoTracking().SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal("bags@example.com", row.ReplyToEmailAddress);
    }

    [Fact]
    public async Task Unknown_channel_throws()
    {
        await using var db = InMemoryDb.NewContext();
        var renderer = Substitute.For<INotificationRenderer>();
        renderer.RenderBookingLinkAsync(Arg.Any<BookingNotificationContext>(), Arg.Any<CancellationToken>())
            .Returns(new RenderedNotification(Subject: "x", Body: "y"));
        var time = new FakeTimeProvider(new DateTime(2026, 6, 10, 12, 0, 0, DateTimeKind.Utc));

        var sender = new TucManualMessageSender(db, renderer, Options(), time);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            sender.SendBookingLinkAsync(
                jobId: 1, recipient: "n/a",
                context: new BookingNotificationContext(
                    "carrier-pigeon", "Jane", "Air NZ", "REF-1", "https://x"),
                ct: TestContext.Current.CancellationToken));

        Assert.Equal(0, await db.TucManualMessages.CountAsync(TestContext.Current.CancellationToken));
    }
}
