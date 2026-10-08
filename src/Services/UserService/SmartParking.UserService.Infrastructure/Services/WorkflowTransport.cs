using System.Net;
using System.Net.Mail;
using System.Net.Http.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using UserService.Application.Common.Interfaces.Services;
namespace UserService.Infrastructure.Services;
public sealed class WorkflowProtector(IDataProtectionProvider provider):IWorkflowProtector
{
    private readonly IDataProtector protector=provider.CreateProtector("SmartPark.AccountWorkflow.v1");
    public string Protect(string value)=>protector.Protect(value);
    public string Unprotect(string value)=>protector.Unprotect(value);
}
public sealed class WorkflowTransport(IConfiguration config,IHttpClientFactory clients):IWorkflowTransport
{
    public async Task SendAsync(WorkflowMessage message,Guid deliveryId,CancellationToken ct)
    {
        var body=message.Body;
        if(body.Contains("/change-password?"))
        {
            if(!Uri.TryCreate(config["Onboarding:PublicOrigin"],UriKind.Absolute,out var origin) || origin.Scheme!="https")throw new InvalidOperationException("Configure HTTPS Onboarding:PublicOrigin.");
            body=body.Replace("/change-password?",origin.GetLeftPart(UriPartial.Authority)+"/change-password?");
        }
        if(message.Channel=="email")
        {
            if(string.IsNullOrWhiteSpace(config["Otp:Smtp:Host"]))throw new InvalidOperationException("Configure SMTP delivery.");
            using var smtp=new SmtpClient(config["Otp:Smtp:Host"],config.GetValue("Otp:Smtp:Port",587)){EnableSsl=true,Credentials=new NetworkCredential(config["Otp:Smtp:Username"],config["Otp:Smtp:Password"])};
            using var mail=new MailMessage(config["Otp:Smtp:From"]!,message.Destination,message.Subject,body);mail.Headers.Add("X-SmartPark-Delivery-Id",deliveryId.ToString());await smtp.SendMailAsync(mail,ct);return;
        }
        if(message.Channel=="sms" && Uri.TryCreate(config["Otp:Sms:Url"],UriKind.Absolute,out var url) && url.Scheme=="https")
        {
            using var request=new HttpRequestMessage(HttpMethod.Post,url);request.Headers.Authorization=new("Bearer",config["Otp:Sms:ApiKey"]);request.Headers.Add("Idempotency-Key",deliveryId.ToString());request.Content=JsonContent.Create(new{to=message.Destination,message=body});
            using var response=await clients.CreateClient("Otp").SendAsync(request,ct);response.EnsureSuccessStatusCode();return;
        }
        throw new InvalidOperationException("Configure delivery provider.");
    }
}
