using BaggageDelivery.Core.Http;
using BaggageDelivery.Core.Http.Models;
using BaggageDelivery.Core.MagicLink;
using BaggageDelivery.Core.Models.Entities;
using BaggageDelivery.Core.MultiTenant;
using BaggageDelivery.Core.Services;
using BaggageDelivery.UnitTests.Helpers;
using BaggageDelivery.UnitTests.MagicLink;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace BaggageDelivery.UnitTests.Services;

public class PaxBookingServiceTests
{
    [Fact]
    public async Task Confirm_persists_confirmation_outbox_and_marks_token_used()
    {
        var db = InMemoryDb.NewContext();
        var despatch = Substitute.For<IDespatchApiClient>();
        var tenants = Substitute.For<ITenantResolver>();
        var magicLink = Substitute.For<IMagicLinkService>();
        var time = new FakeTimeProvider(new DateTime(2026, 6, 10, 12, 0, 0, DateTimeKind.Utc));

        var svc = new PaxBookingService(db, despatch, tenants, magicLink, time,
            NullLogger<PaxBookingService>.Instance);

        var id = await svc.ConfirmAsync(new ConfirmBookingInput(
            JobId: 42, TenantId: 1, TokenId: 7,
            Address: new AddressUpdateDto { Line1 = "1 Queen St", City = "Auckland", Country = "NZ" },
            TimeSlotStartUtc: time.GetUtcNow().UtcDateTime.AddHours(2),
            TimeSlotEndUtc: time.GetUtcNow().UtcDateTime.AddHours(5),
            AtlOption: AtlOption.FrontDoor,
            AccessNotes: null, PhoneOverride: null), CancellationToken.None);

        Assert.True(id > 0);
        Assert.Single(db.BookingConfirmations);
        Assert.Single(db.ConfirmationOutbox);
        Assert.Equal(OutboxStatus.Pending, db.ConfirmationOutbox.First().Status);
        await magicLink.Received(1).MarkUsedAsync(7, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Confirming_same_job_twice_throws()
    {
        var db = InMemoryDb.NewContext();
        var despatch = Substitute.For<IDespatchApiClient>();
        var tenants = Substitute.For<ITenantResolver>();
        var magicLink = Substitute.For<IMagicLinkService>();
        var time = new FakeTimeProvider(DateTime.UtcNow);

        var svc = new PaxBookingService(db, despatch, tenants, magicLink, time,
            NullLogger<PaxBookingService>.Instance);

        var input = new ConfirmBookingInput(1, 1, 1,
            new AddressUpdateDto { Line1 = "x", City = "y", Country = "NZ" },
            time.GetUtcNow().UtcDateTime, time.GetUtcNow().UtcDateTime.AddHours(1),
            AtlOption.None, null, null);

        await svc.ConfirmAsync(input, CancellationToken.None);

        await Assert.ThrowsAsync<ConfirmationAlreadyExistsException>(
            () => svc.ConfirmAsync(input, CancellationToken.None));
    }
}
