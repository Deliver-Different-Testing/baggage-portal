using Microsoft.Extensions.Options;
using SendGrid;
using SendGrid.Helpers.Mail;

namespace BaggageDelivery.Core.Notifications;

internal sealed class SendGridEmailSender(IOptions<SendGridOptions> options) : IEmailSender
{
    private readonly SendGridOptions _opts = options.Value;

    public async Task<NotificationResult> SendAsync(string toAddress, string subject, string htmlBody, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_opts.ApiKey))
        {
            return new NotificationResult(false, null, "SendGrid:ApiKey is not configured");
        }

        var client = new SendGridClient(_opts.ApiKey);
        var from = new EmailAddress(_opts.FromAddress, _opts.FromName);
        var to = new EmailAddress(toAddress);
        var msg = MailHelper.CreateSingleEmail(from, to, subject, plainTextContent: null, htmlContent: htmlBody);

        var response = await client.SendEmailAsync(msg, ct);

        if (response.IsSuccessStatusCode)
        {
            response.Headers.TryGetValues("X-Message-Id", out var ids);
            return new NotificationResult(true, ids?.FirstOrDefault(), null);
        }

        var body = await response.Body.ReadAsStringAsync(ct);
        return new NotificationResult(false, null, $"SendGrid {(int)response.StatusCode}: {body}");
    }
}
