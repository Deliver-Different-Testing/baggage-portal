using System.Net;
using BaggageDelivery.Core.Http;
using BaggageDelivery.UnitTests.Helpers;
using Microsoft.Extensions.Options;
using Xunit;

namespace BaggageDelivery.UnitTests.Http;

public class TrackingPageClientTests
{
    private static TrackingPageClient NewClient(
        StubHttpMessageHandler handler,
        string? baseUrl = "https://trackingpage.example.com/")
    {
        var options = Options.Create(new TrackingPageUrlsOptions
        {
            BaseUrl = baseUrl is null ? null : new Uri(baseUrl)
        });

        return new TrackingPageClient(handler.ToClient(), options);
    }

    private static string JobJson(
        string statusId = "7",
        string deliverByTime = "\"2026-06-10T15:30:00\"",
        string? deliverByTimeZoneCode = "Pacific/Auckland",
        string? timeZoneCode = null)
    {
        var deliverByTz = deliverByTimeZoneCode is null ? "null" : $"\"{deliverByTimeZoneCode}\"";
        var tz = timeZoneCode is null ? "null" : $"\"{timeZoneCode}\"";

        return $$"""
        {
          "success": true,
          "job": {
            "id": 42,
            "statusId": {{statusId}},
            "deliverByTime": {{deliverByTime}},
            "timeZoneCode": {{tz}},
            "deliverByTimeZoneCode": {{deliverByTz}}
          }
        }
        """;
    }

    [Fact]
    public async Task Unconfigured_base_url_throws()
    {
        var client = NewClient(StubHttpMessageHandler.Text(HttpStatusCode.OK), baseUrl: null);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            client.GetJobAsync(42, TestContext.Current.CancellationToken));

        Assert.Contains("TrackingPage base URL is not configured", ex.Message);
    }

    [Fact]
    public async Task Requests_the_byId_endpoint_for_the_job()
    {
        var handler = StubHttpMessageHandler.Json(HttpStatusCode.OK, JobJson());

        await NewClient(handler).GetJobAsync(42, TestContext.Current.CancellationToken);

        Assert.Equal(
            "https://trackingpage.example.com/api/Job/byId/42",
            handler.LastUrl?.ToString());
    }

    [Fact]
    public async Task Successful_response_maps_job_and_converts_eta_to_utc()
    {
        var handler = StubHttpMessageHandler.Json(HttpStatusCode.OK, JobJson());

        var dto = await NewClient(handler).GetJobAsync(42, TestContext.Current.CancellationToken);

        Assert.NotNull(dto);
        Assert.Equal(42, dto.JobId);
        Assert.Equal("7", dto.CurrentStatus);
        Assert.Empty(dto.Events);
        Assert.Equal(new DateTime(2026, 6, 10, 3, 30, 0, DateTimeKind.Utc), dto.EtaWindowStartUtc);
        Assert.Equal(dto.EtaWindowStartUtc, dto.EtaWindowEndUtc);
        Assert.Null(dto.CourierFirstName);
        Assert.Null(dto.VehicleLabel);
    }

    [Fact]
    public async Task StatusId_supplied_as_string_is_still_read()
    {
        var handler = StubHttpMessageHandler.Json(HttpStatusCode.OK, JobJson(statusId: "\"9\""));

        var dto = await NewClient(handler).GetJobAsync(42, TestContext.Current.CancellationToken);

        Assert.Equal("9", dto?.CurrentStatus);
    }

    [Fact]
    public async Task Missing_status_maps_to_Unknown()
    {
        var handler = StubHttpMessageHandler.Json(HttpStatusCode.OK, JobJson(statusId: "null"));

        var dto = await NewClient(handler).GetJobAsync(42, TestContext.Current.CancellationToken);

        Assert.Equal("Unknown", dto?.CurrentStatus);
    }

    [Fact]
    public async Task Falls_back_to_TimeZoneCode_when_delivery_zone_absent()
    {
        var handler = StubHttpMessageHandler.Json(HttpStatusCode.OK,
            JobJson(deliverByTimeZoneCode: null, timeZoneCode: "Pacific/Auckland"));

        var dto = await NewClient(handler).GetJobAsync(42, TestContext.Current.CancellationToken);

        Assert.Equal(new DateTime(2026, 6, 10, 3, 30, 0, DateTimeKind.Utc), dto?.EtaWindowStartUtc);
    }

    [Fact]
    public async Task Missing_deliver_by_time_yields_no_eta()
    {
        var handler = StubHttpMessageHandler.Json(HttpStatusCode.OK, JobJson(deliverByTime: "null"));

        var dto = await NewClient(handler).GetJobAsync(42, TestContext.Current.CancellationToken);

        Assert.NotNull(dto);
        Assert.Null(dto.EtaWindowStartUtc);
        Assert.Null(dto.EtaWindowEndUtc);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Mars/Olympus_Mons")]
    public async Task Unusable_timezone_yields_no_eta_but_still_returns_the_job(string? timeZone)
    {
        var handler = StubHttpMessageHandler.Json(HttpStatusCode.OK,
            JobJson(deliverByTimeZoneCode: timeZone));

        var dto = await NewClient(handler).GetJobAsync(42, TestContext.Current.CancellationToken);

        Assert.NotNull(dto);
        Assert.Equal(42, dto.JobId);
        Assert.Null(dto.EtaWindowStartUtc);
    }

    [Fact]
    public async Task Success_false_returns_null()
    {
        var handler = StubHttpMessageHandler.Json(HttpStatusCode.OK,
            """{ "success": false, "job": null }""");

        var dto = await NewClient(handler).GetJobAsync(42, TestContext.Current.CancellationToken);

        Assert.Null(dto);
    }

    [Fact]
    public async Task Success_true_without_job_returns_null()
    {
        var handler = StubHttpMessageHandler.Json(HttpStatusCode.OK,
            """{ "success": true, "job": null }""");

        var dto = await NewClient(handler).GetJobAsync(42, TestContext.Current.CancellationToken);

        Assert.Null(dto);
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.Unauthorized)]
    public async Task Non_success_status_returns_null(HttpStatusCode status)
    {
        var handler = StubHttpMessageHandler.Json(status, """{ "success": false }""");

        var dto = await NewClient(handler).GetJobAsync(42, TestContext.Current.CancellationToken);

        Assert.Null(dto);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not json at all")]
    [InlineData("{ \"success\": ")]
    public async Task Unparseable_body_returns_null(string body)
    {
        var handler = StubHttpMessageHandler.Text(HttpStatusCode.OK, body);

        var dto = await NewClient(handler).GetJobAsync(42, TestContext.Current.CancellationToken);

        Assert.Null(dto);
    }

    [Fact]
    public async Task Transport_failure_returns_null()
    {
        var handler = StubHttpMessageHandler.Throws(new HttpRequestException("connection refused"));

        var dto = await NewClient(handler).GetJobAsync(42, TestContext.Current.CancellationToken);

        Assert.Null(dto);
    }

    [Fact]
    public async Task Timeout_returns_null_when_the_caller_did_not_cancel()
    {
        var handler = StubHttpMessageHandler.Throws(new TaskCanceledException("timed out"));

        var dto = await NewClient(handler).GetJobAsync(42, TestContext.Current.CancellationToken);

        Assert.Null(dto);
    }

    [Fact]
    public async Task Caller_cancellation_propagates_rather_than_being_swallowed()
    {
        var handler = StubHttpMessageHandler.Throws(new TaskCanceledException("cancelled"));
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAsync<TaskCanceledException>(() =>
            NewClient(handler).GetJobAsync(42, cts.Token));
    }
}
