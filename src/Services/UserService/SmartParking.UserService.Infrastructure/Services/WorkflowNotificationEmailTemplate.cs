using System.Net;
using System.Net.Mail;
using System.Net.Mime;
using System.Text;
using UserService.Application.Common.Interfaces.Services;

namespace UserService.Infrastructure.Services;

public static class WorkflowNotificationEmailTemplate
{
    public static bool CanRender(WorkflowMessage message) =>
        message.EmailTemplate is WorkflowEmailTemplates.OwnerApproved or WorkflowEmailTemplates.OwnerRejected or
            WorkflowEmailTemplates.MfaSecurityChange or WorkflowEmailTemplates.OperatorInvitation ||
        (message.Subject == "SmartPark application decision" &&
            (message.Body == "Your application is approved." || message.Body == "Your application is rejected.")) ||
        (message.Subject == "SmartPark security notification" &&
            (message.Body.StartsWith("Two-factor authentication enabled at ", StringComparison.Ordinal) ||
             message.Body.StartsWith("Authenticator replaced at ", StringComparison.Ordinal) ||
             message.Body.StartsWith("Two-factor authentication disabled at ", StringComparison.Ordinal) ||
             message.Body.StartsWith("Recovery codes replaced at ", StringComparison.Ordinal))) ||
        (message.Subject == "Verify your SmartPark Operator account" &&
            message.Body.Contains("/change-password?", StringComparison.Ordinal));

    public static MailMessage Create(string from, WorkflowMessage message)
    {
        var template = message.EmailTemplate ?? (message.Subject switch
        {
            "SmartPark application decision" when message.Body == "Your application is approved." => WorkflowEmailTemplates.OwnerApproved,
            "SmartPark application decision" when message.Body == "Your application is rejected." => WorkflowEmailTemplates.OwnerRejected,
            "SmartPark security notification" when CanRender(message) => WorkflowEmailTemplates.MfaSecurityChange,
            "Verify your SmartPark Operator account" when CanRender(message) => WorkflowEmailTemplates.OperatorInvitation,
            _ => null
        });
        if (template is null) throw new ArgumentOutOfRangeException(nameof(message), "Unknown email template.");

        var mail = new MailMessage(from, message.Destination, message.Subject, message.Body)
        {
            BodyEncoding = Encoding.UTF8,
            SubjectEncoding = Encoding.UTF8
        };
        try
        {
            mail.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(
                message.Body, Encoding.UTF8, MediaTypeNames.Text.Plain));
            var html = AlternateView.CreateAlternateViewFromString(Render(template, message.Body),
                Encoding.UTF8, MediaTypeNames.Text.Html);
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

    private static string Render(string template, string body)
    {
        var safeBody = WebUtility.HtmlEncode(body);
        const string cardStart = "<table role=\"presentation\" cellpadding=\"0\" cellspacing=\"0\" border=\"0\" width=\"100%\" style=\"background:#edf6f1;border:1px solid #c9e7d8;border-radius:14px;\"><tr><td style=\"padding:18px 20px;\">";
        const string cardEnd = "</td></tr></table>";
        switch (template)
        {
            case WorkflowEmailTemplates.OwnerApproved:
                return SmartParkingEmailLayout.Render(
                    "Your SmartParking owner application has been approved.", "APPLICATION APPROVED",
                    "Your owner application is approved",
                    "Your parking site has passed review. You can now sign in to your Owner workspace.",
                    cardStart + "<strong style=\"color:#146b61;font-size:15px;\">Approved for Owner access</strong>" +
                    "<p style=\"margin:8px 0 0;color:#344b45;font-size:14px;line-height:1.6;\">" + safeBody + "</p>" + cardEnd +
                    "<p style=\"margin:18px 0 0;color:#344b45;font-size:14px;line-height:1.7;\">Next: sign in and review your parking site, pricing and operating settings before accepting bookings.</p>",
                    "Keep this email for your records. Thank you for joining SmartParking.");

            case WorkflowEmailTemplates.OwnerRejected:
                return SmartParkingEmailLayout.Render(
                    "There is an update on your SmartParking owner application.", "APPLICATION UPDATE",
                    "Your owner application was not approved",
                    "The review of your Owner application is complete. Your parking site has not been activated.",
                    "<table role=\"presentation\" cellpadding=\"0\" cellspacing=\"0\" border=\"0\" width=\"100%\" style=\"background:#fff5e9;border:1px solid #f0d9b7;border-radius:14px;\"><tr><td style=\"padding:18px 20px;\">" +
                    "<strong style=\"color:#874b19;font-size:15px;\">Application update</strong>" +
                    "<p style=\"margin:8px 0 0;color:#5b4c39;font-size:14px;line-height:1.6;\">" + safeBody + "</p></td></tr></table>" +
                    "<p style=\"margin:18px 0 0;color:#344b45;font-size:14px;line-height:1.7;\">For the review details or help with a new application, contact SmartParking support. This email does not include a specific rejection reason.</p>",
                    "You can keep this email as a record of the decision.");

            case WorkflowEmailTemplates.MfaSecurityChange:
                var (title, label, introduction) = body switch
                {
                    var value when value.StartsWith("Two-factor authentication enabled at ", StringComparison.Ordinal) =>
                        ("Two-factor authentication is on", "2FA ENABLED", "Your account now requires an extra verification step when you sign in."),
                    var value when value.StartsWith("Authenticator replaced at ", StringComparison.Ordinal) =>
                        ("Your authenticator was changed", "AUTHENTICATOR CHANGED", "A new authenticator was connected to your SmartParking account."),
                    var value when value.StartsWith("Two-factor authentication disabled at ", StringComparison.Ordinal) =>
                        ("Two-factor authentication is off", "2FA DISABLED", "Your SmartParking account no longer requires an authenticator code at sign-in."),
                    var value when value.StartsWith("Recovery codes replaced at ", StringComparison.Ordinal) =>
                        ("Your recovery codes were replaced", "RECOVERY CODES CHANGED", "Your previous recovery codes can no longer be used."),
                    _ => ("Your account security changed", "SECURITY NOTICE", "A two-factor authentication setting on your SmartParking account changed.")
                };
                return SmartParkingEmailLayout.Render(
                    "A SmartParking account security setting changed.", label, title, introduction,
                    cardStart + "<strong style=\"color:#146b61;font-size:14px;\">Activity recorded</strong>" +
                    "<p style=\"margin:8px 0 0;color:#344b45;font-size:14px;line-height:1.6;\">" + safeBody + "</p>" + cardEnd,
                    "If you did not make this change, sign in to review your security settings and contact support immediately.");

            case WorkflowEmailTemplates.OperatorInvitation:
                var safeLink = WebUtility.HtmlEncode(ExtractActivationLink(body));
                return SmartParkingEmailLayout.Render(
                    "Activate your SmartParking Operator account within 24 hours.", "Operator access · Invitation",
                    "Activate your Operator account",
                    "Your parking site Owner invited you to SmartParking. Verify your email and choose your own password to activate your Operator access.",
                    "<table role=\"presentation\" cellpadding=\"0\" cellspacing=\"0\" border=\"0\" width=\"100%\"><tr><td align=\"center\" bgcolor=\"#146b61\" style=\"border-radius:999px;background:#146b61;\">" +
                    "<a href=\"" + safeLink + "\" style=\"display:block;padding:14px 24px;color:#ffffff;font-family:'Be Vietnam Pro',Arial,Helvetica,sans-serif;font-size:14px;font-weight:700;line-height:1.4;text-align:center;text-decoration:none;\">Verify email and set password</a>" +
                    "</td></tr></table>" +
                    "<p style=\"margin:16px 0 0;color:#526360;font-size:13px;line-height:1.7;\">This invitation expires after 24 hours. If the button does not open, copy this address into your browser:</p>" +
                    "<p style=\"margin:6px 0 0;color:#146b61;font-size:12px;line-height:1.6;overflow-wrap:anywhere;word-break:break-all;\">" + safeLink + "</p>",
                    "If you were not expecting this invitation, you can ignore this email.");

            default:
                throw new ArgumentOutOfRangeException(nameof(template), "Unknown email template.");
        }
    }

    private static string ExtractActivationLink(string body)
    {
        var start = Math.Max(body.LastIndexOf("http://", StringComparison.OrdinalIgnoreCase),
            body.LastIndexOf("https://", StringComparison.OrdinalIgnoreCase));
        if (start < 0) throw new ArgumentException("Operator invitation requires an absolute activation link.", nameof(body));
        var candidate = body[start..].Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)[0];
        if (!Uri.TryCreate(candidate, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttps && !(uri.Scheme == Uri.UriSchemeHttp && uri.IsLoopback)) ||
            uri.AbsolutePath != "/change-password" || string.IsNullOrEmpty(uri.Query))
            throw new ArgumentException("Operator invitation activation link is invalid.", nameof(body));
        return uri.AbsoluteUri;
    }
}
