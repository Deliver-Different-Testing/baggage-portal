using Mjml.Net;
using BaggageDelivery.Core.Models.Entities;

namespace BaggageDelivery.Core.Notifications;

internal sealed class NotificationRenderer(IMjmlRenderer mjml) : INotificationRenderer
{
    public Task<RenderedNotification> RenderMagicLinkAsync(MagicLinkRenderContext context, CancellationToken ct)
    {
        return context.Channel switch
        {
            NotificationChannel.Sms => Task.FromResult(RenderSms(context)),
            NotificationChannel.Email => Task.FromResult(RenderEmail(context)),
            _ => throw new ArgumentOutOfRangeException(nameof(context),
                $"Unknown channel '{context.Channel}'")
        };
    }

    private static RenderedNotification RenderSms(MagicLinkRenderContext c)
    {
        var body =
            $"Hi {c.PassengerName.Split(' ')[0]}, your {c.AirlineLabel} baggage is ready for delivery. " +
            $"Confirm your address and time slot: {c.MagicLinkUrl}";
        return new RenderedNotification(Subject: "", Body: body);
    }

    private RenderedNotification RenderEmail(MagicLinkRenderContext c)
    {
        var mjmlTemplate = $$"""
            <mjml>
              <mj-head>
                <mj-title>Confirm your baggage delivery</mj-title>
                <mj-attributes>
                  <mj-all font-family="Plus Jakarta Sans, Arial, sans-serif" />
                </mj-attributes>
              </mj-head>
              <mj-body background-color="#f4f2f1">
                <mj-section background-color="#001F3D" padding="32px">
                  <mj-column>
                    <mj-text color="#ffffff" font-size="22px" font-weight="700">Your baggage is ready</mj-text>
                    <mj-text color="#9eb2cf" font-size="13px">Ref: {{c.Reference}} · {{c.AirlineLabel}}</mj-text>
                  </mj-column>
                </mj-section>
                <mj-section background-color="#ffffff" padding="32px">
                  <mj-column>
                    <mj-text font-size="16px">Hi {{c.PassengerName}},</mj-text>
                    <mj-text font-size="15px" line-height="1.5">
                      Good news — your baggage has arrived and we're ready to deliver it. Please confirm
                      your delivery address, pick a time slot, and let us know if it's OK to leave the bag
                      unattended.
                    </mj-text>
                    <mj-button background-color="#00B0B9" color="#001F3D" font-weight="700"
                               border-radius="999px" padding="24px 0" href="{{c.MagicLinkUrl}}">
                      Confirm delivery details
                    </mj-button>
                    <mj-text color="#6e6d80" font-size="12px">
                      This link is for you only and expires in 7 days.
                    </mj-text>
                  </mj-column>
                </mj-section>
              </mj-body>
            </mjml>
            """;

        var (html, _) = mjml.Render(mjmlTemplate);
        return new RenderedNotification(
            Subject: $"Your {c.AirlineLabel} baggage is ready for delivery",
            Body: html);
    }
}
