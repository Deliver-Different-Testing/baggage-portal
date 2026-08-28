using BaggageDelivery.Api.Controllers.Admin;
using BaggageDelivery.Api.DTOs.Admin;
using BaggageDelivery.Core.Interfaces;
using BaggageDelivery.Core.Models;
using BaggageDelivery.Core.Notifications;
using BaggageDelivery.Core.Security;
using BaggageDelivery.Core.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace BaggageDelivery.UnitTests.Controllers;

public class BookingLinksControllerTests
{
    private const string Token = "ENCRYPTED-TOKEN";
    private static readonly string[] Expected = ["+64211234567", "pax@example.com"];

    private sealed class Harness
    {
        public IEncryptionService Encryption { get; } = Substitute.For<IEncryptionService>();
        private INotificationService Notifications { get; } = Substitute.For<INotificationService>();
        private IPaxBookingService Bookings { get; } = Substitute.For<IPaxBookingService>();
        public ProblemDetailsFactory ProblemFactory { get; } = Substitute.For<ProblemDetailsFactory>();
        private BookingLinksController Controller { get; }

        public Harness(
            string publicBaseUrl = "https://bags.example.com",
            string fileReference = "AKLNZ12345",
            string airlineName = "Air New Zealand",
            bool despatchKnowsJob = true)
        {
            Encryption.EncryptId(Arg.Any<int>()).Returns(Token);
            Bookings.GetNotificationDetailsAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
                .Returns(despatchKnowsJob
                    ? new BookingNotificationDetails(fileReference, airlineName)
                    : null);

            ProblemFactory.CreateProblemDetails(
                    Arg.Any<HttpContext>(), Arg.Any<int?>(), Arg.Any<string>(),
                    Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())
                .Returns(new ProblemDetails { Status = StatusCodes.Status500InternalServerError });

            Controller = new BookingLinksController(
                Encryption,
                Notifications,
                Bookings,
                Options.Create(new BookingLinkOptions { PublicBaseUrl = publicBaseUrl }))
            {
                ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() },
                ProblemDetailsFactory = ProblemFactory
            };
        }

        public Task<ActionResult<MintBookingLinkResponse>> Mint(
            string channel = NotificationChannel.Sms,
            int jobId = 4242,
            string? phone = "+64211234567",
            string? email = "pax@example.com",
            string? passengerName = "Jane Pax",
            string? airlineLabel = "Air New Zealand",
            string? reference = "REF-1") =>
            Controller.Mint(
                new MintBookingLinkRequest(jobId, passengerName, airlineLabel, reference, phone, email, channel),
                TestContext.Current.CancellationToken);

        public IReadOnlyList<(int JobId, string Recipient, BookingNotificationContext Context)> Sends =>
        [
            .. Notifications.ReceivedCalls()
                .Where(c => c.GetMethodInfo().Name == nameof(INotificationService.SendBookingLinkAsync))
                .Select(c => c.GetArguments())
                .Select(a => ((int)a[0]!, (string)a[1]!, (BookingNotificationContext)a[2]!))
        ];
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("/")]
    public async Task Unconfigured_public_base_url_is_a_problem_and_mints_nothing(string publicBaseUrl)
    {
        var harness = new Harness(publicBaseUrl);

        var result = await harness.Mint();

        Assert.IsType<ObjectResult>(result.Result);
        harness.ProblemFactory.Received(1).CreateProblemDetails(
            Arg.Any<HttpContext>(), Arg.Any<int?>(), Arg.Any<string>(), Arg.Any<string>(),
            "BookingLinks:PublicBaseUrl is not configured", Arg.Any<string>());
        Assert.Empty(harness.Sends);
        harness.Encryption.DidNotReceive().EncryptId(Arg.Any<int>());
    }

    [Theory]
    [InlineData("https://bags.example.com")]
    [InlineData("https://bags.example.com/")]
    [InlineData("https://bags.example.com///")]
    public async Task Returns_the_token_and_both_urls_with_a_single_separator(string publicBaseUrl)
    {
        var harness = new Harness(publicBaseUrl);

        var result = await harness.Mint(jobId: 4242);

        var response = Assert.IsType<MintBookingLinkResponse>(
            Assert.IsType<OkObjectResult>(result.Result).Value);
        Assert.Equal(Token, response.EncryptedId);
        Assert.Equal($"https://bags.example.com/c/{Token}", response.ConfirmUrl);
        Assert.Equal($"https://bags.example.com/t/{Token}", response.TrackUrl);
    }

    [Fact]
    public async Task Encrypts_the_requested_job_id()
    {
        var harness = new Harness();

        await harness.Mint(jobId: 987);

        harness.Encryption.Received(1).EncryptId(987);
    }

    [Fact]
    public async Task Sms_channel_sends_one_sms_to_the_phone()
    {
        var harness = new Harness();

        await harness.Mint(channel: NotificationChannel.Sms, jobId: 4242);

        var send = Assert.Single(harness.Sends);
        Assert.Equal(4242, send.JobId);
        Assert.Equal("+64211234567", send.Recipient);
        Assert.Equal(NotificationChannel.Sms, send.Context.Channel);
        Assert.Equal($"https://bags.example.com/c/{Token}", send.Context.BookingUrl);
    }

    [Fact]
    public async Task Email_channel_sends_one_email_to_the_address()
    {
        var harness = new Harness();

        await harness.Mint(channel: NotificationChannel.Email);

        var send = Assert.Single(harness.Sends);
        Assert.Equal("pax@example.com", send.Recipient);
        Assert.Equal(NotificationChannel.Email, send.Context.Channel);
    }

    [Fact]
    public async Task Both_channel_sends_sms_and_email()
    {
        var harness = new Harness();

        await harness.Mint(channel: "both");

        Assert.Equal(2, harness.Sends.Count);
        Assert.Equal(
            new[] { NotificationChannel.Sms, NotificationChannel.Email },
            harness.Sends.Select(s => s.Context.Channel).ToArray());
        Assert.Equal(
            Expected,
            harness.Sends.Select(s => s.Recipient).ToArray());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Sms_channel_without_a_usable_phone_sends_nothing_but_still_returns_links(string? phone)
    {
        var harness = new Harness();

        var result = await harness.Mint(channel: NotificationChannel.Sms, phone: phone);

        Assert.Empty(harness.Sends);
        Assert.IsType<OkObjectResult>(result.Result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Email_channel_without_a_usable_address_sends_nothing(string? email)
    {
        var harness = new Harness();

        await harness.Mint(channel: NotificationChannel.Email, email: email);

        Assert.Empty(harness.Sends);
    }

    [Fact]
    public async Task Both_channel_with_only_a_phone_sends_only_sms()
    {
        var harness = new Harness();

        await harness.Mint(channel: "both", email: null);

        Assert.Equal(NotificationChannel.Sms, Assert.Single(harness.Sends).Context.Channel);
    }

    [Fact]
    public async Task Unrecognised_channel_sends_nothing()
    {
        var harness = new Harness();

        await harness.Mint(channel: "carrier-pigeon");

        Assert.Empty(harness.Sends);
    }

    [Fact]
    public async Task Passes_the_supplied_passenger_and_airline_through()
    {
        var harness = new Harness();

        await harness.Mint(passengerName: "Jane Pax", airlineLabel: "Air New Zealand");

        var context = Assert.Single(harness.Sends).Context;
        Assert.Equal("Jane Pax", context.PassengerName);
        Assert.Equal("Air New Zealand", context.AirlineLabel);
    }

    [Fact]
    public async Task Quotes_the_file_reference_despatch_holds_for_the_job()
    {
        // The portal shows ucjbClientRefa as the File Reference, so the message that
        // sends the passenger there has to quote the same value.
        var harness = new Harness(fileReference: "AKLNZ12345");

        await harness.Mint(jobId: 4242, reference: "URG-4242");

        Assert.Equal("AKLNZ12345", Assert.Single(harness.Sends).Context.FileReference);
    }

    [Fact]
    public async Task Carries_no_reference_when_the_job_holds_none()
    {
        // The portal hides the tag rather than showing the internal id behind the
        // link, and the caller's own reference is not a stand-in for it.
        var harness = new Harness(fileReference: "");

        await harness.Mint(jobId: 4242, reference: "URG-4242");

        Assert.Equal(string.Empty, Assert.Single(harness.Sends).Context.FileReference);
    }

    [Fact]
    public async Task Carries_no_reference_when_despatch_does_not_know_the_job()
    {
        var harness = new Harness(despatchKnowsJob: false);

        await harness.Mint(jobId: 4242, reference: "URG-4242");

        Assert.Equal(string.Empty, Assert.Single(harness.Sends).Context.FileReference);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Falls_back_to_the_airline_despatch_holds_for_the_job(string? airlineLabel)
    {
        // "Deliver DFRNT" used to fill this slot, which put the platform's name in
        // the subject line where the carrier's belongs.
        var harness = new Harness(airlineName: "Air New Zealand");

        await harness.Mint(airlineLabel: airlineLabel);

        Assert.Equal("Air New Zealand", Assert.Single(harness.Sends).Context.AirlineLabel);
    }

    [Fact]
    public async Task Falls_back_to_defaults_when_despatch_does_not_know_the_job()
    {
        var harness = new Harness(despatchKnowsJob: false);

        await harness.Mint(passengerName: null, airlineLabel: null);

        var context = Assert.Single(harness.Sends).Context;
        Assert.Equal("Unknown Passenger", context.PassengerName);
        Assert.Equal("Your Airline", context.AirlineLabel);
    }
}
