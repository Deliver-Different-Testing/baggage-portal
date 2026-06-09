using Amazon.SimpleEmailV2;
using Amazon.SimpleEmailV2.Model;
using BaggageDelivery.Core.Interfaces;
using Microsoft.Extensions.Options;

namespace BaggageDelivery.Core.Notifications;

internal sealed class SesEmailSender(
    IAmazonSimpleEmailServiceV2 ses,
    IOptions<SesOptions> options) : IEmailSender
{
    private readonly SesOptions _opts = options.Value;

    public async Task<NotificationResult> SendAsync(string toAddress, string subject, string htmlBody, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_opts.FromAddress))
        {
            return new NotificationResult(false, null, "Ses:FromAddress is not configured");
        }

        var request = new SendEmailRequest
        {
            FromEmailAddress = string.IsNullOrWhiteSpace(_opts.FromName)
                ? _opts.FromAddress
                : $"{_opts.FromName} <{_opts.FromAddress}>",
            Destination = new Destination { ToAddresses = [toAddress] },
            Content = new EmailContent
            {
                Simple = new Message
                {
                    Subject = new Content { Data = subject, Charset = "UTF-8" },
                    Body = new Body
                    {
                        Html = new Content { Data = htmlBody, Charset = "UTF-8" }
                    }
                }
            },
            ConfigurationSetName = string.IsNullOrWhiteSpace(_opts.ConfigurationSetName)
                ? null
                : _opts.ConfigurationSetName
        };

        try
        {
            var response = await ses.SendEmailAsync(request, ct);
            return new NotificationResult(true, response.MessageId, null);
        }
        catch (AmazonSimpleEmailServiceV2Exception ex)
        {
            return new NotificationResult(false, null, $"SES {ex.StatusCode}: {ex.Message}");
        }
    }
}
