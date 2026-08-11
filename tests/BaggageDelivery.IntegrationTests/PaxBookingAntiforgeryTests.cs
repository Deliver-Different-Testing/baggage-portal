using System.Net;
using System.Net.Http.Json;
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

        // Regression guard: [ValidateAntiForgeryToken] used to throw
        // InvalidOperationException because AddControllers doesn't register the
        // ViewFeatures filter, so every confirm returned 500 before reaching the action.
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

        // The two halves of an ASP.NET Core antiforgery token are distinct values.
        // Echoing the cookie token back as the header — what the SPA used to do —
        // can never validate.
        Assert.NotEqual(CookieValue(cookieToken), CookieValue(requestToken));
    }

    [Fact]
    public async Task Confirm_succeeds_when_the_request_token_accompanies_the_cookie()
    {
        const int jobId = 4243;
        await SeedJobAsync(jobId);

        // HandleCookies is on by default, so the antiforgery cookie set by the
        // token call is replayed on the confirm POST.
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

    private static object ConfirmBody() => new
    {
        address = new
        {
            line1 = "1 Test Street",
            suburb = "Ponsonby",
            city = "Auckland",
            postCode = "1011",
            country = "NZ"
        },
        timeSlotStartUtc = new DateTime(2026, 6, 10, 21, 0, 0, DateTimeKind.Utc),
        timeSlotEndUtc = new DateTime(2026, 6, 11, 0, 0, 0, DateTimeKind.Utc),
        atlOptionId = (int?)null,
        accessNotes = "Leave at the door",
        passengerName = "Jane Pax",
        passengerPhone = "+64211234567",
        passengerEmail = "jane@example.com"
    };

    private static string CookieValue(string setCookieHeader)
    {
        var pair = setCookieHeader.Split(';')[0];
        return pair[(pair.IndexOf('=') + 1)..];
    }
}
