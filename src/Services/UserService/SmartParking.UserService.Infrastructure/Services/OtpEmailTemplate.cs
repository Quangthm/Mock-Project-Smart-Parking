using System.Net;
using System.Net.Mail;
using System.Net.Mime;
using System.Text;
using UserService.Application.Common.Interfaces.Services;

namespace UserService.Infrastructure.Services;

public static class OtpEmailTemplate
{
    private const string LogoResource = "SmartParking.UserService.Infrastructure.Assets.Logo.png";
    private const string LogoContentId = "smartparking-logo";

    public static MailMessage Create(string from, WorkflowMessage message)
    {
        ArgumentNullException.ThrowIfNull(message.OtpCode);

        var mail = new MailMessage(from, message.Destination, message.Subject, message.Body)
        {
            BodyEncoding = Encoding.UTF8,
            SubjectEncoding = Encoding.UTF8
        };

        try
        {
            mail.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(message.Body, Encoding.UTF8, MediaTypeNames.Text.Plain));
            var html = AlternateView.CreateAlternateViewFromString(Render(message.OtpCode), Encoding.UTF8, MediaTypeNames.Text.Html);
            var logo = typeof(OtpEmailTemplate).Assembly.GetManifestResourceStream(LogoResource)
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

    private static string Render(string code)
    {
        var safeCode = WebUtility.HtmlEncode(code);
        return $"""
            <!doctype html>
            <html lang="en">
            <head><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1"></head>
            <body style="margin:0;padding:0;background:#f3f6f9;font-family:Arial,Helvetica,sans-serif;color:#17324d;">
              <div style="display:none;max-height:0;overflow:hidden;opacity:0;">Your SmartParking code is {safeCode}. It expires in 5 minutes.</div>
              <table role="presentation" cellpadding="0" cellspacing="0" border="0" width="100%" style="background:#f3f6f9;">
                <tr><td align="center" style="padding:32px 16px;">
                  <table role="presentation" cellpadding="0" cellspacing="0" border="0" width="560" style="width:100%;max-width:560px;background:#ffffff;border:1px solid #dce5ed;border-radius:12px;">
                    <tr><td align="center" style="padding:32px 24px 8px;">
                      <img src="cid:{LogoContentId}" width="112" height="112" alt="SmartParking logo" style="display:block;width:112px;height:112px;border:0;">
                    </td></tr>
                    <tr><td align="center" style="padding:0 24px 24px;font-size:17px;font-weight:700;letter-spacing:.2px;color:#07558d;">SmartParking</td></tr>
                    <tr><td style="padding:0 40px;">
                      <h1 style="margin:0 0 12px;font-size:24px;line-height:1.3;font-weight:700;color:#17324d;">Your verification code</h1>
                      <p style="margin:0 0 24px;font-size:15px;line-height:1.6;color:#496175;">Enter this code to continue with SmartParking.</p>
                    </td></tr>
                    <tr><td align="center" style="padding:0 24px 24px;">
                      <div style="display:inline-block;padding:16px 24px;background:#edf5fb;border:1px solid #c9dfee;border-radius:8px;font-family:Arial,Helvetica,sans-serif;font-size:32px;line-height:1.2;font-weight:700;letter-spacing:7px;color:#07558d;">{safeCode}</div>
                    </td></tr>
                    <tr><td style="padding:0 40px 32px;">
                      <p style="margin:0 0 12px;font-size:14px;line-height:1.6;color:#496175;">This code is valid for 5 minutes. Do not share it with anyone.</p>
                      <p style="margin:0;font-size:14px;line-height:1.6;color:#496175;">If you did not request this code, you can ignore this email.</p>
                    </td></tr>
                  </table>
                  <p style="margin:18px 0 0;font-size:12px;line-height:1.5;color:#718598;">SmartParking · Secure account verification</p>
                </td></tr>
              </table>
            </body>
            </html>
            """;
    }
}
