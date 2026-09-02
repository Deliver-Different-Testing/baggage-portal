using System.Net;
using Mjml.Net;
using BaggageDelivery.Core.Interfaces;
using BaggageDelivery.Core.Models;

namespace BaggageDelivery.Core.Notifications;

internal sealed class NotificationRenderer(IMjmlRenderer mjml) : INotificationRenderer
{
    public Task<RenderedNotification> RenderBookingLinkAsync(BookingNotificationContext context, CancellationToken ct)
    {
        return context.Channel switch
        {
            NotificationChannel.Sms => Task.FromResult(RenderSms(context)),
            NotificationChannel.Email => Task.FromResult(RenderEmail(context)),
            _ => throw new ArgumentOutOfRangeException(nameof(context),
                $"Unknown channel '{context.Channel}'")
        };
    }

    public Task<RenderedNotification> RenderBookingConfirmedAsync(
        BookingConfirmedNotificationContext context, CancellationToken ct)
    {
        return context.Channel switch
        {
            NotificationChannel.Sms => Task.FromResult(RenderConfirmedSms(context)),
            NotificationChannel.Email => Task.FromResult(RenderConfirmedEmail(context)),
            _ => throw new ArgumentOutOfRangeException(nameof(context),
                $"Unknown channel '{context.Channel}'")
        };
    }

    private static RenderedNotification RenderSms(BookingNotificationContext c)
    {
        var body =
            $"Hi {c.PassengerName.Split(' ')[0]}, your {c.AirlineLabel} baggage is ready for delivery. " +
            $"Confirm your address and time slot: {c.BookingUrl}";
        return new RenderedNotification(Subject: "", Body: body);
    }

    private RenderedNotification RenderEmail(BookingNotificationContext c)
    {
        var headerLine = string.IsNullOrWhiteSpace(c.FileReference)
            ? Escape(c.AirlineLabel)
            : $"File Reference: {Escape(c.FileReference)} · {Escape(c.AirlineLabel)}";

        var mjmlTemplate = $"""
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
                    <mj-text color="#9eb2cf" font-size="13px">{headerLine}</mj-text>
                  </mj-column>
                </mj-section>
                <mj-section background-color="#ffffff" padding="32px">
                  <mj-column>
                    <mj-text font-size="16px">Hi {Escape(c.PassengerName)},</mj-text>
                    <mj-text font-size="15px" line-height="1.5">
                      Good news — your baggage has arrived and we're ready to deliver it. Please confirm
                      your delivery address, pick a time slot, and let us know if it's OK to leave the bag
                      unattended.
                    </mj-text>
                    <mj-button background-color="#00B0B9" color="#001F3D" font-weight="700"
                               border-radius="999px" padding="24px 0" href="{Escape(c.BookingUrl)}">
                      Confirm delivery details
                    </mj-button>
                  </mj-column>
                </mj-section>
                <mj-section background-color="#f4f2f1" padding="24px 32px">
                  <mj-column>
                    <mj-text align="center" color="#6b6764" font-size="12px" letter-spacing="0.04em">
                      Powered by Deliver DFRNT
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

    private static RenderedNotification RenderConfirmedSms(BookingConfirmedNotificationContext c)
    {
        var body =
            $"Thanks {c.PassengerName.Split(' ')[0]}, your {c.AirlineLabel} baggage delivery is booked for "
            + $"{c.DayLabel}, {c.WindowLabel}.";

        if (!string.IsNullOrWhiteSpace(c.TrackingUrl))
        {
            body += $" Track it: {c.TrackingUrl}";
        }

        return new RenderedNotification(Subject: "", Body: body);
    }

    private RenderedNotification RenderConfirmedEmail(BookingConfirmedNotificationContext c)
    {
        var headerLine = string.IsNullOrWhiteSpace(c.FileReference)
            ? Escape(c.AirlineLabel)
            : $"File Reference: {Escape(c.FileReference)} · {Escape(c.AirlineLabel)}";

        var trackingBlock = string.IsNullOrWhiteSpace(c.TrackingUrl)
            ? string.Empty
            : $"""
                   <mj-button background-color="#00B0B9" color="#001F3D" font-weight="700"
                              border-radius="999px" padding="24px 0" href="{Escape(c.TrackingUrl)}">
                     Track your delivery
                   </mj-button>
               """;

        var mjmlTemplate = $"""
            <mjml>
              <mj-head>
                <mj-title>Your baggage delivery is booked</mj-title>
                <mj-attributes>
                  <mj-all font-family="Plus Jakarta Sans, Arial, sans-serif" />
                </mj-attributes>
              </mj-head>
              <mj-body background-color="#f4f2f1">
                <mj-section background-color="#001F3D" padding="32px">
                  <mj-column>
                    <mj-text color="#ffffff" font-size="22px" font-weight="700">Your delivery is booked</mj-text>
                    <mj-text color="#9eb2cf" font-size="13px">{headerLine}</mj-text>
                  </mj-column>
                </mj-section>
                <mj-section background-color="#ffffff" padding="32px">
                  <mj-column>
                    <mj-text font-size="16px">Hi {Escape(c.PassengerName)},</mj-text>
                    <mj-text font-size="15px" line-height="1.5">
                      Thanks — we have everything we need. Your baggage is booked in for
                      <strong>{Escape(c.DayLabel)}</strong>, between <strong>{Escape(c.WindowLabel)}</strong>.
                    </mj-text>
            {trackingBlock}
                  </mj-column>
                </mj-section>
                <mj-section background-color="#f4f2f1" padding="24px 32px">
                  <mj-column>
                    <mj-text align="center" color="#6b6764" font-size="12px" letter-spacing="0.04em">
                      Powered by Deliver DFRNT
                    </mj-text>
                  </mj-column>
                </mj-section>
              </mj-body>
            </mjml>
            """;

        var (html, _) = mjml.Render(mjmlTemplate);
        return new RenderedNotification(
            Subject: $"Your {c.AirlineLabel} baggage delivery is booked",
            Body: html);
    }

    private static string Escape(string? value) => WebUtility.HtmlEncode(value ?? string.Empty);
}
