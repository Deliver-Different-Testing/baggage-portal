using BaggageDelivery.Core.Interfaces;
using Microsoft.Extensions.Options;
using Twilio;
using Twilio.Rest.Api.V2010.Account;
using Twilio.Types;

namespace BaggageDelivery.Core.Notifications;

internal sealed class TwilioSmsSender(IOptions<TwilioOptions> options) : ISmsSender
{
    private readonly TwilioOptions _opts = options.Value;
    private bool _initialised;

    public async Task<NotificationResult> SendAsync(string toNumber, string body, CancellationToken ct)
    {
        EnsureInitialised();

        try
        {
            var message = await MessageResource.CreateAsync(
                to: new PhoneNumber(toNumber),
                from: new PhoneNumber(_opts.FromNumber),
                body: body);

            return new NotificationResult(true, message.Sid, null);
        }
        catch (Exception ex)
        {
            return new NotificationResult(false, null, ex.Message);
        }
    }

    private void EnsureInitialised()
    {
        if (_initialised)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(_opts.AccountSid) || string.IsNullOrWhiteSpace(_opts.AuthToken))
        {
            throw new InvalidOperationException(
                "Twilio:AccountSid and Twilio:AuthToken must be configured before sending SMS.");
        }

        TwilioClient.Init(_opts.AccountSid, _opts.AuthToken);
        _initialised = true;
    }
}
