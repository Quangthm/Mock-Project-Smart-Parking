using System.Net;
using System.Net.Mail;
using System.Net.Mime;
using System.Text;
using UserService.Application.Common.Interfaces.Services;

namespace UserService.Infrastructure.Services;

public static class OtpEmailTemplate
{
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
            SmartParkingEmailLayout.AddLogo(html);
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
        var content = $"""
            <table role="presentation" cellpadding="0" cellspacing="0" border="0" width="100%" style="background:#edf6f1;border:1px solid #c9e7d8;border-radius:14px;">
              <tr><td align="center" style="padding:20px 12px 8px;color:#526360;font-size:11px;font-weight:700;letter-spacing:1px;text-transform:uppercase;">One-time code</td></tr>
              <tr><td align="center" style="padding:0 12px 20px;color:#146b61;font-family:'Be Vietnam Pro',Arial,Helvetica,sans-serif;font-size:31px;line-height:1.4;font-weight:800;letter-spacing:4px;">{safeCode}</td></tr>
            </table>
            <p style="margin:16px 0 0;color:#526360;font-size:13px;line-height:1.7;">This code is valid for 5 minutes. Do not share it with anyone.</p>
            """;
        return SmartParkingEmailLayout.Render(
            "Your SmartParking verification code expires in 5 minutes.",
            "Account verification", "Your verification code",
            "Enter the one-time code below to continue with your SmartParking account.",
            content, "If you did not request this code, you can ignore this email.");
    }
}
