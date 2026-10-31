using System.Net;
using System.Net.Mail;
using System.Net.Mime;

namespace UserService.Infrastructure.Services;

internal static class SmartParkingEmailLayout
{
    private const string LogoResource = "SmartParking.UserService.Infrastructure.Assets.Logo.png";
    private const string LogoContentId = "smartparking-logo";
    private const string Font = "'Be Vietnam Pro',Arial,Helvetica,sans-serif";

    public static void AddLogo(AlternateView html)
    {
        var stream = typeof(SmartParkingEmailLayout).Assembly.GetManifestResourceStream(LogoResource)
            ?? throw new InvalidOperationException("SmartParking email logo is missing.");
        html.LinkedResources.Add(new LinkedResource(stream, MediaTypeNames.Image.Png)
        {
            ContentId = LogoContentId,
            TransferEncoding = TransferEncoding.Base64
        });
    }

    public static string Render(string preview, string category, string title, string introduction,
        string content, string closing)
    {
        var safePreview = WebUtility.HtmlEncode(preview);
        var safeCategory = WebUtility.HtmlEncode(category);
        var safeTitle = WebUtility.HtmlEncode(title);
        var safeIntroduction = WebUtility.HtmlEncode(introduction);
        var safeClosing = WebUtility.HtmlEncode(closing);

        return $"""
            <!doctype html>
            <html lang="en">
            <head>
              <meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1">
              <link href="https://fonts.googleapis.com/css2?family=Be+Vietnam+Pro:wght@400;500;600;700;800&amp;display=swap" rel="stylesheet">
              <title>{safeTitle} · SmartParking</title>
            </head>
            <body style="margin:0;padding:0;background:#f5f7f5;color:#152422;font-family:{Font};">
              <div style="display:none;max-height:0;overflow:hidden;opacity:0;mso-hide:all;">{safePreview}</div>
              <table role="presentation" cellpadding="0" cellspacing="0" border="0" width="100%" style="background:#f5f7f5;">
                <tr><td align="center" style="padding:28px 12px 36px;">
                  <table role="presentation" cellpadding="0" cellspacing="0" border="0" width="560" style="width:100%;max-width:560px;background:#fcfdfc;border:1px solid #d8dfda;border-radius:20px;">
                    <tr><td style="padding:28px 28px 22px;border-bottom:1px solid #e5ebe6;">
                      <img src="cid:{LogoContentId}" width="48" height="55" alt="SmartParking logo" style="display:block;width:48px;height:55px;border:0;object-fit:contain;">
                    </td></tr>
                    <tr><td style="padding:30px 28px 0;font-family:{Font};">
                      <p style="margin:0 0 12px;color:#146b61;font-size:11px;line-height:1.5;font-weight:800;letter-spacing:1.5px;text-transform:uppercase;">{safeCategory}</p>
                      <h1 style="margin:0 0 12px;color:#152422;font-family:{Font};font-size:25px;line-height:1.32;font-weight:800;letter-spacing:-.6px;">{safeTitle}</h1>
                      <p style="margin:0 0 24px;color:#526360;font-family:{Font};font-size:15px;line-height:1.7;">{safeIntroduction}</p>
                    </td></tr>
                    <tr><td style="padding:0 28px 28px;font-family:{Font};">{content}
                      <p style="margin:24px 0 0;color:#526360;font-family:{Font};font-size:13px;line-height:1.7;">{safeClosing}</p>
                    </td></tr>
                    <tr><td style="padding:18px 28px;border-top:1px solid #e5ebe6;color:#687975;font-family:{Font};font-size:11px;line-height:1.6;">
                      SmartParking · Parking made simple<br>For your security, SmartParking will never ask you to share a verification code or password by email.
                    </td></tr>
                  </table>
                </td></tr>
              </table>
            </body>
            </html>
            """;
    }
}
