using System.Net.Mime;
using System.Security.Cryptography;
using System.Text;
using UserService.Application.Common.Interfaces.Services;
using UserService.Infrastructure.Services;

namespace SmartParking.UserService.Tests;

public sealed class WorkflowNotificationEmailTemplateTests
{
    [Theory]
    [InlineData(WorkflowEmailTemplates.OwnerApproved, "Your owner application is approved", "APPLICATION APPROVED")]
    [InlineData(WorkflowEmailTemplates.OwnerRejected, "Your owner application was not approved", "APPLICATION UPDATE")]
    [InlineData(WorkflowEmailTemplates.MfaSecurityChange, "Two-factor authentication is on", "2FA ENABLED")]
    public void Notification_has_matching_content_plain_text_and_embedded_logo(string template, string heading, string label)
    {
        var message = new WorkflowMessage("email", "person@example.com", "SmartPark notification",
            "Two-factor authentication enabled at 2026-10-30 12:00:00Z.", EmailTemplate: template);
        using var mail = WorkflowNotificationEmailTemplate.Create("sender@example.com", message);

        Assert.Equal(message.Body, mail.Body);
        Assert.Equal(2, mail.AlternateViews.Count);
        Assert.Equal(MediaTypeNames.Text.Plain, mail.AlternateViews[0].ContentType.MediaType);
        var html = mail.AlternateViews[1];
        Assert.Equal(MediaTypeNames.Text.Html, html.ContentType.MediaType);
        using var reader = new StreamReader(html.ContentStream, Encoding.UTF8, leaveOpen: true);
        var content = reader.ReadToEnd();
        Assert.Contains(heading, content);
        Assert.Contains(label, content);
        Assert.Contains(message.Body, content);
        Assert.Contains("cid:smartparking-logo", content);
        Assert.Contains("color:#1f3450;\">Smart</span><span style=\"color:#1bb782;\">Parking", content);
        var logo = Assert.Single(html.LinkedResources);
        Assert.Equal("smartparking-logo", logo.ContentId);
        Assert.Equal("7011F89B2A5CA20E205A38898D4FAE438D5D4D8CC6D4925098D1E90628B02834",
            Convert.ToHexString(SHA256.HashData(logo.ContentStream)));
    }

    [Fact]
    public void Notification_escapes_untrusted_message_body()
    {
        var message = new WorkflowMessage("email", "person@example.com", "SmartPark notification",
            "<script>alert('x')</script>", EmailTemplate: WorkflowEmailTemplates.MfaSecurityChange);
        using var mail = WorkflowNotificationEmailTemplate.Create("sender@example.com", message);
        using var reader = new StreamReader(mail.AlternateViews[1].ContentStream, Encoding.UTF8);
        var content = reader.ReadToEnd();

        Assert.DoesNotContain("<script>", content);
        Assert.Contains("&lt;script&gt;", content);
    }

    [Theory]
    [InlineData("SmartPark application decision", "Your application is approved.", "APPLICATION APPROVED")]
    [InlineData("SmartPark application decision", "Your application is rejected.", "APPLICATION UPDATE")]
    [InlineData("SmartPark security notification", "Two-factor authentication enabled at 2026-10-30 12:00:00Z.", "2FA ENABLED")]
    public void Pending_legacy_notifications_receive_the_html_template(string subject, string body, string label)
    {
        var message = new WorkflowMessage("email", "person@example.com", subject, body);
        Assert.True(WorkflowNotificationEmailTemplate.CanRender(message));

        using var mail = WorkflowNotificationEmailTemplate.Create("sender@example.com", message);
        Assert.Equal(2, mail.AlternateViews.Count);
        using var reader = new StreamReader(mail.AlternateViews[1].ContentStream, Encoding.UTF8);
        Assert.Contains(label, reader.ReadToEnd());
    }
}
