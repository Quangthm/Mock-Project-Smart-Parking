using System.Net.Mime;
using System.Security.Cryptography;
using System.Text;
using UserService.Application.Common.Interfaces.Services;
using UserService.Infrastructure.Services;

namespace SmartParking.UserService.Tests;

public sealed class OtpEmailTemplateTests
{
    [Theory]
    [InlineData("email")]
    [InlineData("sms")]
    public void Shared_message_keeps_existing_otp_text(string channel)
    {
        var message = OtpWorkflowMessage.Create(channel, "person@example.com", "012345");

        Assert.Equal(channel, message.Channel);
        Assert.Equal("SmartPark verification", message.Subject);
        Assert.Equal("Your code is 012345. Valid for 5 minutes.", message.Body);
        Assert.Equal("012345", message.OtpCode);
    }

    [Fact]
    public void Email_contains_plain_text_html_and_exact_smartparking_logo()
    {
        var message = OtpWorkflowMessage.Create("email", "person@example.com", "012345");
        using var mail = OtpEmailTemplate.Create("SmartParking <sender@example.com>", message);

        Assert.Equal(message.Body, mail.Body);
        Assert.Equal("person@example.com", mail.To.Single().Address);
        Assert.Equal(2, mail.AlternateViews.Count);
        Assert.Equal(MediaTypeNames.Text.Plain, mail.AlternateViews[0].ContentType.MediaType);
        var html = mail.AlternateViews[1];
        Assert.Equal(MediaTypeNames.Text.Html, html.ContentType.MediaType);
        using (var reader = new StreamReader(html.ContentStream, Encoding.UTF8, leaveOpen: true))
        {
            var content = reader.ReadToEnd();
            Assert.Contains("Your verification code", content);
            Assert.Contains("012345", content);
            Assert.Contains("valid for 5 minutes", content);
            Assert.Contains("cid:smartparking-logo", content);
            Assert.Contains("alt=\"SmartParking logo\"", content);
        }

        var logo = Assert.Single(html.LinkedResources);
        Assert.Equal(MediaTypeNames.Image.Png, logo.ContentType.MediaType);
        Assert.Equal("smartparking-logo", logo.ContentId);
        Assert.Equal("7011F89B2A5CA20E205A38898D4FAE438D5D4D8CC6D4925098D1E90628B02834",
            Convert.ToHexString(SHA256.HashData(logo.ContentStream)));
    }

    [Fact]
    public void Email_escapes_code_in_html()
    {
        var message = OtpWorkflowMessage.Create("email", "person@example.com", "12<345");
        using var mail = OtpEmailTemplate.Create("sender@example.com", message);
        using var reader = new StreamReader(mail.AlternateViews[1].ContentStream, Encoding.UTF8);

        Assert.Contains("12&lt;345", reader.ReadToEnd());
    }
}
