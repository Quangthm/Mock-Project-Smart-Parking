using System.Net;
using System.Net.Mail;
using System.Net.Http.Json;
using UserService.Application.Common.Interfaces.Services;
using UserService.Application.Common.Models.Exceptions;

namespace UserService.API.Services;

public sealed class OtpSender(IConfiguration config, IWebHostEnvironment environment,
    IHttpClientFactory clients, ILogger<OtpSender> logger) : IOtpSender
{
    public async Task SendAsync(string channel, string destination, string code, CancellationToken ct)
    {
        if (environment.IsDevelopment() && config.GetValue<bool>("Otp:DevelopmentLogCodes"))
        {
            logger.LogWarning("Development OTP for {Destination}: {Code} (expires in 5 minutes)", destination, code);
            return;
        }
        try
        {
            if (channel == "email" && !string.IsNullOrWhiteSpace(config["Otp:Smtp:Host"]))
            {
                using var smtp = new SmtpClient(config["Otp:Smtp:Host"], config.GetValue("Otp:Smtp:Port", 587))
                {
                    EnableSsl = true,
                    Credentials = new NetworkCredential(config["Otp:Smtp:Username"], config["Otp:Smtp:Password"])
                };
                using var mail = new MailMessage(config["Otp:Smtp:From"]!, destination,
                    "SmartParking verification code", $"Your verification code is {code}. It expires in 5 minutes.");
                await smtp.SendMailAsync(mail, ct);
                return;
            }
            if (channel == "sms" && Uri.TryCreate(config["Otp:Sms:Url"], UriKind.Absolute, out var uri)
                && uri.Scheme == "https")
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, uri);
                request.Headers.Authorization = new("Bearer", config["Otp:Sms:ApiKey"]);
                request.Content = JsonContent.Create(new { to = destination, message = $"SmartParking code: {code}. Valid for 5 minutes." });
                using var response = await clients.CreateClient("Otp").SendAsync(request, ct);
                response.EnsureSuccessStatusCode();
                return;
            }
            throw new InvalidOperationException("OTP delivery is not configured.");
        }
        catch (Exception e) when (e is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            // Do not log message bodies, codes, credentials or provider responses.
            logger.LogError("OTP delivery failed ({Type})", e.GetType().Name);
            throw new AuthException("OTP_DELIVERY_FAILED", "Cannot send verification code. Please try again later.", 503);
        }
    }
}
