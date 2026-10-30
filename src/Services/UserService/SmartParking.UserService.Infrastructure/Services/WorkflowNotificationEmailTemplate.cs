using System.Net;
using System.Net.Mail;
using System.Net.Mime;
using System.Text;
using UserService.Application.Common.Interfaces.Services;

namespace UserService.Infrastructure.Services;

public static class WorkflowNotificationEmailTemplate
{
    private const string LogoResource = "SmartParking.UserService.Infrastructure.Assets.Logo.png";
    private const string LogoContentId = "smartparking-logo";

    public static bool CanRender(WorkflowMessage message) => message.EmailTemplate is not null ||
        (message.Subject == "SmartPark application decision" &&
            (message.Body == "Your application is approved." || message.Body == "Your application is rejected.")) ||
        (message.Subject == "SmartPark security notification" &&
            (message.Body.StartsWith("Two-factor authentication enabled at ", StringComparison.Ordinal) ||
             message.Body.StartsWith("Authenticator replaced at ", StringComparison.Ordinal) ||
             message.Body.StartsWith("Two-factor authentication disabled at ", StringComparison.Ordinal) ||
             message.Body.StartsWith("Recovery codes replaced at ", StringComparison.Ordinal)));

    public static MailMessage Create(string from, WorkflowMessage message)
    {
        var template = message.EmailTemplate ?? (message.Subject switch
        {
            "SmartPark application decision" when message.Body == "Your application is approved." => WorkflowEmailTemplates.OwnerApproved,
            "SmartPark application decision" when message.Body == "Your application is rejected." => WorkflowEmailTemplates.OwnerRejected,
            "SmartPark security notification" when CanRender(message) => WorkflowEmailTemplates.MfaSecurityChange,
            _ => null
        });
        var enabled = message.Body.StartsWith("Two-factor authentication enabled at ", StringComparison.Ordinal);
        var (heading, label, accent, tint, border, summary, footer) = template switch
        {
            WorkflowEmailTemplates.OwnerApproved => (
                "Your owner application is approved", "APPLICATION APPROVED", "#116c56", "#eaf7f1", "#b8e4ce",
                "Your SmartParking owner workspace is ready. Sign in to continue setting up your parking site.",
                "Thank you for building with SmartParking."),
            WorkflowEmailTemplates.OwnerRejected => (
                "Your owner application was not approved", "APPLICATION UPDATE", "#8c4c17", "#fff5e8", "#efd4b1",
                "Your SmartParking owner application has been reviewed. Contact support if you need help with the decision.",
                "SmartParking · Owner application review"),
            WorkflowEmailTemplates.MfaSecurityChange => (
                enabled ? "Two-factor authentication is on" : "Your account security changed",
                enabled ? "2FA ENABLED" : "SECURITY NOTICE", "#07558d", "#edf5fb", "#c9dfee",
                enabled ? "Your SmartParking account now has an extra layer of protection." : "A two-factor authentication setting on your SmartParking account was changed.",
                "If you did not make this change, contact support immediately."),
            _ => throw new ArgumentOutOfRangeException(nameof(message), "Unknown email template.")
        };

        var mail = new MailMessage(from, message.Destination, message.Subject, message.Body)
        {
            BodyEncoding = Encoding.UTF8,
            SubjectEncoding = Encoding.UTF8
        };

        try
        {
            mail.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(
                message.Body, Encoding.UTF8, MediaTypeNames.Text.Plain));
            var html = AlternateView.CreateAlternateViewFromString(
                Render(heading, label, accent, tint, border, summary, footer, message.Body),
                Encoding.UTF8, MediaTypeNames.Text.Html);
            var logo = typeof(WorkflowNotificationEmailTemplate).Assembly.GetManifestResourceStream(LogoResource)
                ?? throw new InvalidOperationException("SmartParking email logo is missing.");
            html.LinkedResources.Add(new LinkedResource(logo, MediaTypeNames.Image.Png)
            {
                ContentId = LogoContentId,
                TransferEncoding = TransferEncoding.Base64
            });
            mail.AlternateViews.Add(html);
            return mail;
        }
        catch
        {
            mail.Dispose();
            throw;
        }
    }

    private static string Render(string heading, string label, string accent, string tint, string border,
        string summary, string footer, string body)
    {
        var safeBody = WebUtility.HtmlEncode(body);
        return $"""
            <!doctype html>
            <html lang="en">
            <head><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1"></head>
            <body style="margin:0;padding:0;background:#f3f6f9;font-family:Arial,Helvetica,sans-serif;color:#17324d;">
              <div style="display:none;max-height:0;overflow:hidden;opacity:0;">{WebUtility.HtmlEncode(heading)}. {safeBody}</div>
              <table role="presentation" cellpadding="0" cellspacing="0" border="0" width="100%" style="background:#f3f6f9;">
                <tr><td align="center" style="padding:32px 16px;">
                  <table role="presentation" cellpadding="0" cellspacing="0" border="0" width="560" style="width:100%;max-width:560px;background:#ffffff;border:1px solid #dce5ed;border-radius:12px;">
                    <tr><td align="center" style="padding:32px 24px 8px;">
                      <img src="cid:{LogoContentId}" width="56" height="64" alt="SmartParking logo" style="display:block;width:56px;height:64px;border:0;">
                    </td></tr>
                    <tr><td align="center" style="padding:0 24px 24px;font-size:17px;font-weight:700;letter-spacing:.2px;"><span style="color:#1f3450;">Smart</span><span style="color:#1bb782;">Parking</span></td></tr>
                    <tr><td style="padding:0 40px 20px;">
                      <span style="display:inline-block;padding:7px 12px;background:{tint};border:1px solid {border};border-radius:16px;font-size:11px;line-height:1.4;font-weight:700;letter-spacing:1px;color:{accent};">{label}</span>
                    </td></tr>
                    <tr><td style="padding:0 40px;">
                      <h1 style="margin:0 0 12px;font-size:24px;line-height:1.3;font-weight:700;color:#17324d;">{heading}</h1>
                      <p style="margin:0 0 24px;font-size:15px;line-height:1.6;color:#496175;">{summary}</p>
                    </td></tr>
                    <tr><td style="padding:0 40px 24px;">
                      <table role="presentation" cellpadding="0" cellspacing="0" border="0" width="100%" style="background:{tint};border:1px solid {border};border-radius:8px;">
                        <tr><td style="padding:18px 20px;font-size:15px;line-height:1.6;color:#17324d;">{safeBody}</td></tr>
                      </table>
                    </td></tr>
                    <tr><td style="padding:0 40px 32px;">
                      <p style="margin:0;font-size:14px;line-height:1.6;color:#496175;">{footer}</p>
                    </td></tr>
                  </table>
                  <p style="margin:18px 0 0;font-size:12px;line-height:1.5;color:#718598;">SmartParking · Account notifications</p>
                </td></tr>
              </table>
            </body>
            </html>
            """;
    }
}
