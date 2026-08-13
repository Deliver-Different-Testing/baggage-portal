using System.Text;
using BaggageDelivery.Api.Controllers.Pax;
using BaggageDelivery.Core.Http.Models;
using BaggageDelivery.Core.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using Xunit;

namespace BaggageDelivery.UnitTests.Controllers;

public class PaxTrackingControllerTests
{
    private const string Token = "ENCRYPTED-TOKEN";

    private static TrackingDto NewDto(string status = "Delivered") => new()
    {
        JobId = 4242,
        CurrentStatus = status,
        Events = []
    };

    private sealed class Harness
    {
        public IEncryptionService Encryption { get; } = Substitute.For<IEncryptionService>();
        public IPaxTrackingService Tracking { get; } = Substitute.For<IPaxTrackingService>();
        public PaxTrackingController Controller { get; }
        public MemoryStream ResponseBody { get; } = new();
        public DefaultHttpContext HttpContext { get; }

        public Harness(int? decryptsTo = 4242)
        {
            Encryption.DecryptId(Arg.Any<string>()).Returns(decryptsTo);

            HttpContext = new DefaultHttpContext();
            HttpContext.Response.Body = ResponseBody;

            Controller = new PaxTrackingController(Encryption, Tracking)
            {
                ControllerContext = new ControllerContext { HttpContext = HttpContext }
            };
        }

        public string WrittenBody => Encoding.UTF8.GetString(ResponseBody.ToArray());
    }

    // ---- GetTimeline --------------------------------------------------------

    [Fact]
    public async Task GetTimeline_with_an_undecryptable_id_is_not_found()
    {
        var harness = new Harness(decryptsTo: null);

        var result = await harness.Controller.GetTimeline(Token, TestContext.Current.CancellationToken);

        Assert.IsType<NotFoundResult>(result);
        await harness.Tracking.DidNotReceive().GetTimelineAsync(Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetTimeline_returns_not_found_when_the_job_has_no_timeline()
    {
        var harness = new Harness();
        harness.Tracking.GetTimelineAsync(4242, Arg.Any<CancellationToken>()).Returns((TrackingDto?)null);

        var result = await harness.Controller.GetTimeline(Token, TestContext.Current.CancellationToken);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task GetTimeline_returns_the_timeline_for_the_decrypted_job()
    {
        var harness = new Harness();
        var dto = NewDto();
        harness.Tracking.GetTimelineAsync(4242, Arg.Any<CancellationToken>()).Returns(dto);

        var result = await harness.Controller.GetTimeline(Token, TestContext.Current.CancellationToken);

        Assert.Same(dto, Assert.IsType<OkObjectResult>(result).Value);
        await harness.Tracking.Received(1).GetTimelineAsync(4242, Arg.Any<CancellationToken>());
    }

    // ---- Stream (SSE) -------------------------------------------------------

    [Fact]
    public async Task Stream_with_an_undecryptable_id_is_404_and_writes_nothing()
    {
        var harness = new Harness(decryptsTo: null);

        await harness.Controller.Stream(Token, TestContext.Current.CancellationToken);

        Assert.Equal(StatusCodes.Status404NotFound, harness.HttpContext.Response.StatusCode);
        Assert.Equal(string.Empty, harness.WrittenBody);
        await harness.Tracking.DidNotReceive().GetTimelineAsync(Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Stream_sets_the_event_stream_headers()
    {
        var harness = new Harness();
        using var cts = new CancellationTokenSource();
        // Cancel shortly after the first poll: late enough that the SSE write
        // still succeeds, early enough that the 10s idle delay is cut short
        // instead of holding the test open.
        harness.Tracking.GetTimelineAsync(4242, Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                cts.CancelAfter(TimeSpan.FromMilliseconds(250));
                return NewDto();
            });

        await harness.Controller.Stream(Token, cts.Token);

        var headers = harness.HttpContext.Response.Headers;
        Assert.Equal("text/event-stream", headers.ContentType);
        Assert.Equal("no-cache", headers.CacheControl);
        Assert.Equal("no", headers["X-Accel-Buffering"]);
    }

    [Fact]
    public async Task Stream_writes_the_timeline_as_a_camelCase_sse_event()
    {
        var harness = new Harness();
        using var cts = new CancellationTokenSource();
        harness.Tracking.GetTimelineAsync(4242, Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                cts.CancelAfter(TimeSpan.FromMilliseconds(250));
                return NewDto();
            });

        await harness.Controller.Stream(Token, cts.Token);

        var body = harness.WrittenBody;
        Assert.StartsWith("data: {", body);
        Assert.EndsWith("\n\n", body);
        Assert.Contains("\"currentStatus\":\"Delivered\"", body);
        Assert.Contains("\"jobId\":4242", body);
    }

    [Fact]
    public async Task Stream_writes_nothing_while_the_job_has_no_timeline()
    {
        var harness = new Harness();
        using var cts = new CancellationTokenSource();
        harness.Tracking.GetTimelineAsync(4242, Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                cts.Cancel();
                return (TrackingDto?)null;
            });

        await harness.Controller.Stream(Token, cts.Token);

        Assert.Equal(string.Empty, harness.WrittenBody);
    }

    [Fact]
    public async Task Stream_does_not_poll_at_all_when_already_cancelled()
    {
        var harness = new Harness();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await harness.Controller.Stream(Token, cts.Token);

        await harness.Tracking.DidNotReceive().GetTimelineAsync(Arg.Any<int>(), Arg.Any<CancellationToken>());
        Assert.Equal(string.Empty, harness.WrittenBody);
    }

    [Fact]
    public async Task Stream_swallows_a_tracking_failure_rather_than_faulting_the_response()
    {
        var harness = new Harness();
        harness.Tracking.GetTimelineAsync(4242, Arg.Any<CancellationToken>())
            .Returns<TrackingDto?>(_ => throw new InvalidOperationException("despatch exploded"));

        // Must not throw: the stream is best-effort and the client reconnects.
        await harness.Controller.Stream(Token, TestContext.Current.CancellationToken);

        Assert.Equal(string.Empty, harness.WrittenBody);
    }
}
