using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;
using BaggageDelivery.Core.Interfaces;
using Microsoft.Extensions.Options;

namespace BaggageDelivery.Core.Notifications;

internal sealed class SnsSmsSender(
    IAmazonSimpleNotificationService sns,
    IOptions<SnsOptions> options) : ISmsSender
{
    private readonly SnsOptions _opts = options.Value;

    public async Task<NotificationResult> SendAsync(string toNumber, string body, CancellationToken ct)
    {
        var request = new PublishRequest
        {
            PhoneNumber = toNumber,
            Message = body,
            MessageAttributes = new Dictionary<string, MessageAttributeValue>
            {
                ["AWS.SNS.SMS.SMSType"] = new()
                {
                    DataType = "String",
                    StringValue = string.IsNullOrWhiteSpace(_opts.SmsType) ? "Transactional" : _opts.SmsType
                }
            }
        };

        if (!string.IsNullOrWhiteSpace(_opts.SenderId))
        {
            request.MessageAttributes["AWS.SNS.SMS.SenderID"] = new MessageAttributeValue
            {
                DataType = "String",
                StringValue = _opts.SenderId
            };
        }

        try
        {
            var response = await sns.PublishAsync(request, ct);
            return new NotificationResult(true, response.MessageId, null);
        }
        catch (AmazonSimpleNotificationServiceException ex)
        {
            return new NotificationResult(false, null, $"SNS {ex.StatusCode}: {ex.Message}");
        }
    }
}
