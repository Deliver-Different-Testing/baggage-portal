using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using BaggageDelivery.Core.Enums;
using BaggageDelivery.Core.Interfaces;
using BaggageDelivery.Core.Models;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BaggageDelivery.IntegrationTests;

public class PaxBookingAntiforgeryTests(PaxApiFactory factory) : IClassFixture<PaxApiFactory>
{
    private const string AntiforgeryCookiePrefix = ".AspNetCore.Antiforgery.";
    private const string RequestTokenCookie = "XSRF-TOKEN";

    [Fact]
    public async Task Confirm_without_antiforgery_token_is_rejected_not_a_server_error()
    {
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync($"/api/v1/pax/{EncryptedId(4242)}/booking/confirm",
            ConfirmBody(), TestContext.Current.CancellationToken);

        Assert.NotEqual(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Token_endpoint_issues_a_readable_request_token_paired_with_a_protected_cookie()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/antiforgery/token", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var setCookies = response.Headers.GetValues("Set-Cookie").ToList();

        var cookieToken = Assert.Single(setCookies,
            c => c.StartsWith(AntiforgeryCookiePrefix, StringComparison.Ordinal));
        Assert.Contains("httponly", cookieToken, StringComparison.OrdinalIgnoreCase);

        var requestToken = Assert.Single(setCookies,
            c => c.StartsWith($"{RequestTokenCookie}=", StringComparison.Ordinal));
        Assert.DoesNotContain("httponly", requestToken, StringComparison.OrdinalIgnoreCase);

        Assert.NotEqual(CookieValue(cookieToken), CookieValue(requestToken));
    }

    [Fact]
    public async Task Confirm_succeeds_when_the_request_token_accompanies_the_cookie()
    {
        const int jobId = 4243;
        await SeedJobAsync(jobId);

        var client = factory.CreateClient();

        var tokenResponse = await client.GetAsync("/api/v1/antiforgery/token",
            TestContext.Current.CancellationToken);
        tokenResponse.EnsureSuccessStatusCode();

        var requestToken = CookieValue(tokenResponse.Headers.GetValues("Set-Cookie")
            .Single(c => c.StartsWith($"{RequestTokenCookie}=", StringComparison.Ordinal)));

        var request = new HttpRequestMessage(HttpMethod.Post,
            $"/api/v1/pax/{EncryptedId(jobId)}/booking/confirm")
        {
            Content = JsonContent.Create(ConfirmBody())
        };
        request.Headers.Add("X-XSRF-TOKEN", requestToken);

        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [InlineData("New Zealand", 4244)]
    [InlineData("NZL", 4246)]
    [InlineData("NZ", 4247)]
    public async Task Confirm_accepts_legacy_country_spellings(string country, int jobId)
    {
        await SeedJobAsync(jobId);

        var client = factory.CreateClient();

        var tokenResponse = await client.GetAsync("/api/v1/antiforgery/token",
            TestContext.Current.CancellationToken);
        tokenResponse.EnsureSuccessStatusCode();

        var requestToken = CookieValue(tokenResponse.Headers.GetValues("Set-Cookie")
            .Single(c => c.StartsWith($"{RequestTokenCookie}=", StringComparison.Ordinal)));

        var request = new HttpRequestMessage(HttpMethod.Post,
            $"/api/v1/pax/{EncryptedId(jobId)}/booking/confirm")
        {
            Content = JsonContent.Create(ConfirmBody(country))
        };
        request.Headers.Add("X-XSRF-TOKEN", requestToken);

        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.DoesNotContain("Address.Country", body, StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Confirm_rejects_an_unrecognisable_country_with_an_actionable_message()
    {
        const int jobId = 4245;
        await SeedJobAsync(jobId);

        var client = factory.CreateClient();

        var tokenResponse = await client.GetAsync("/api/v1/antiforgery/token",
            TestContext.Current.CancellationToken);
        tokenResponse.EnsureSuccessStatusCode();

        var requestToken = CookieValue(tokenResponse.Headers.GetValues("Set-Cookie")
            .Single(c => c.StartsWith($"{RequestTokenCookie}=", StringComparison.Ordinal)));

        var request = new HttpRequestMessage(HttpMethod.Post,
            $"/api/v1/pax/{EncryptedId(jobId)}/booking/confirm")
        {
            Content = JsonContent.Create(ConfirmBody("Wakanda"))
        };
        request.Headers.Add("X-XSRF-TOKEN", requestToken);

        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains("Address.Country", body, StringComparison.Ordinal);
        Assert.Contains("search", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Confirm_without_a_delivery_time_is_rejected_not_a_server_error()
    {
        const int jobId = 4248;
        await SeedJobAsync(jobId);

        var client = factory.CreateClient();

        var tokenResponse = await client.GetAsync("/api/v1/antiforgery/token",
            TestContext.Current.CancellationToken);
        tokenResponse.EnsureSuccessStatusCode();

        var requestToken = CookieValue(tokenResponse.Headers.GetValues("Set-Cookie")
            .Single(c => c.StartsWith($"{RequestTokenCookie}=", StringComparison.Ordinal)));

        var request = new HttpRequestMessage(HttpMethod.Post,
            $"/api/v1/pax/{EncryptedId(jobId)}/booking/confirm")
        {
            Content = JsonContent.Create(ConfirmBodyWithoutDeliveryTime())
        };
        request.Headers.Add("X-XSRF-TOKEN", requestToken);

        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.NotEqual(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains("DeliveryTimeUtc", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Confirm_a_second_time_is_refused_as_a_conflict()
    {
        const int jobId = 4249;
        await SeedJobAsync(jobId);

        var client = factory.CreateClient();

        var first = await PostConfirmAsync(client, jobId);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var second = await PostConfirmAsync(client, jobId);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task Booking_reports_the_confirmation_once_the_passenger_has_submitted()
    {
        const int jobId = 4250;
        await SeedJobAsync(jobId);

        var client = factory.CreateClient();

        var before = await client.GetFromJsonAsync<JsonElement>(
            $"/api/v1/pax/{EncryptedId(jobId)}/booking", TestContext.Current.CancellationToken);
        Assert.Equal(JsonValueKind.Null, before.GetProperty("confirmation").ValueKind);

        (await PostConfirmAsync(client, jobId)).EnsureSuccessStatusCode();

        var after = await client.GetFromJsonAsync<JsonElement>(
            $"/api/v1/pax/{EncryptedId(jobId)}/booking", TestContext.Current.CancellationToken);

        var confirmation = after.GetProperty("confirmation");
        Assert.Equal(JsonValueKind.Object, confirmation.ValueKind);
        Assert.Equal("Leave at the door", confirmation.GetProperty("accessNotes").GetString());
        Assert.False(string.IsNullOrWhiteSpace(confirmation.GetProperty("dayLabel").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(confirmation.GetProperty("windowLabel").GetString()));
    }

    private async Task<HttpResponseMessage> PostConfirmAsync(HttpClient client, int jobId)
    {
        var tokenResponse = await client.GetAsync("/api/v1/antiforgery/token",
            TestContext.Current.CancellationToken);
        tokenResponse.EnsureSuccessStatusCode();

        var requestToken = CookieValue(tokenResponse.Headers.GetValues("Set-Cookie")
            .Single(c => c.StartsWith($"{RequestTokenCookie}=", StringComparison.Ordinal)));

        var request = new HttpRequestMessage(HttpMethod.Post,
            $"/api/v1/pax/{EncryptedId(jobId)}/booking/confirm")
        {
            Content = JsonContent.Create(ConfirmBody())
        };
        request.Headers.Add("X-XSRF-TOKEN", requestToken);

        return await client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Confirm_persists_each_address_line_to_its_own_column()
    {
        const int jobId = 4251;
        await SeedJobAsync(jobId);

        var client = factory.CreateClient();

        (await PostConfirmAsync(client, jobId)).EnsureSuccessStatusCode();

        await factory.SeedAsync(async db =>
        {
            var job = await db.TucJobs.AsNoTracking()
                .SingleAsync(j => j.UcjbId == jobId, TestContext.Current.CancellationToken);
            Assert.Equal("1", job.DeliveryAddressLine3);
            Assert.Equal("Test Street", job.DeliveryAddressLine4);
            Assert.Equal("Ponsonby", job.DeliveryAddressLine5);
            Assert.Equal("Auckland", job.DeliveryAddressLine6);
            Assert.Equal("1011", job.DeliveryAddressLine7);
            Assert.Equal("NZ", job.DeliveryAddressLine8);
        });
    }

    [Fact]
    public async Task Confirm_rewrites_the_composite_address_Despatch_displays()
    {
        const int jobId = 4252;
        await factory.SeedAsync(async db =>
        {
            db.TucJobs.Add(new TucJob
            {
                UcjbId = jobId,
                UcjbNumber = $"TEST-{jobId}",
                UcjbToAddr = "Auckland Airport, Mangere, Auckland",
                UcjbStatus = (int)JobStatus.Dispatched
            });
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        });

        var client = factory.CreateClient();

        (await PostConfirmAsync(client, jobId)).EnsureSuccessStatusCode();

        await factory.SeedAsync(async db =>
        {
            var job = await db.TucJobs.AsNoTracking()
                .SingleAsync(j => j.UcjbId == jobId, TestContext.Current.CancellationToken);
            Assert.Equal("1, Test Street, Ponsonby, Auckland, 1011, NZ", job.UcjbToAddr);
        });
    }

    [Fact]
    public async Task Confirm_queues_a_confirmation_message_per_channel()
    {
        const int jobId = 4253;
        await SeedJobAsync(jobId);

        var client = factory.CreateClient();

        (await PostConfirmAsync(client, jobId)).EnsureSuccessStatusCode();

        await factory.SeedAsync(async db =>
        {
            var messages = await db.TucManualMessages.AsNoTracking()
                .Where(m => m.JobId == jobId)
                .ToListAsync(TestContext.Current.CancellationToken);

            var sms = Assert.Single(messages, m => m.SendToMobile == "+64211234567");
            Assert.Contains("booked", sms.UcmmMessage, StringComparison.OrdinalIgnoreCase);

            var email = Assert.Single(messages, m => m.SendToEmailAddress == "jane@example.com");
            Assert.Contains("booked", email.Subject, StringComparison.OrdinalIgnoreCase);
        });
    }

    [Fact]
    public async Task Confirm_leaves_the_suburb_alone_when_the_lookup_is_unavailable()
    {
        const int jobId = 4254;
        await factory.SeedAsync(async db =>
        {
            db.TucJobs.Add(new TucJob
            {
                UcjbId = jobId,
                UcjbNumber = $"TEST-{jobId}",
                UcjbStatus = (int)JobStatus.Dispatched
            });
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        });

        var client = factory.CreateClient();

        (await PostConfirmAsync(client, jobId)).EnsureSuccessStatusCode();

        await factory.SeedAsync(async db =>
        {
            var job = await db.TucJobs.AsNoTracking()
                .SingleAsync(j => j.UcjbId == jobId, TestContext.Current.CancellationToken);
            Assert.Null(job.UcjbTo);
            Assert.Equal("Ponsonby", job.DeliveryAddressLine5);
        });
    }

    private string EncryptedId(int jobId)
    {
        using var scope = factory.Services.CreateScope();
        var encryption = scope.ServiceProvider.GetRequiredService<IEncryptionService>();
        return encryption.EncryptId(jobId);
    }

    private Task SeedJobAsync(int jobId) => factory.SeedAsync(async db =>
    {
        db.TucJobs.Add(new TucJob
        {
            UcjbId = jobId,
            UcjbNumber = $"TEST-{jobId}",
            UcjbStatus = (int)JobStatus.Dispatched
        });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
    });

    private static object ConfirmBody(string country = "NZ") => new
    {
        address = new
        {
            line3 = "1",
            line4 = "Test Street",
            line5 = "Ponsonby",
            line6 = "Auckland",
            line7 = "1011",
            country
        },
        deliveryTimeUtc = new DateTime(2026, 6, 10, 21, 0, 0, DateTimeKind.Utc),
        atlOptionId = (int?)null,
        accessNotes = "Leave at the door",
        passengerName = "Jane Pax",
        passengerPhone = "+64211234567",
        passengerEmail = "jane@example.com"
    };

    private static object ConfirmBodyWithoutDeliveryTime() => new
    {
        address = new
        {
            line3 = "17",
            line4 = "Saleyards Road",
            line5 = "Otahuhu",
            line6 = "Auckland",
            line7 = "1062",
            country = "NZ"
        },
        atlOptionId = (int?)null,
        accessNotes = (string?)null,
        passengerName = "Jane Pax",
        passengerPhone = "021759012",
        passengerEmail = "jane@example.com"
    };

    private static string CookieValue(string setCookieHeader)
    {
        var pair = setCookieHeader.Split(';')[0];
        return pair[(pair.IndexOf('=') + 1)..];
    }
}
