using System.Net;
using System.Net.Mail;
using System.Net.Sockets;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting.Internal;
using Microsoft.Extensions.Logging;
using UserService.Application.Common.Interfaces.Services;
using UserService.Infrastructure.Services;

namespace SmartParking.UserService.Tests;

public sealed class WorkflowSmtpTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SmtpRejectionIsLoggedWithoutPayloadAndPropagatedForRetry(bool otp)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        async Task Reject()
        {
            using var connection = await listener.AcceptTcpClientAsync(timeout.Token);
            await using var writer = new StreamWriter(connection.GetStream()) { AutoFlush = true, NewLine = "\r\n" };
            await writer.WriteLineAsync("421 Test service unavailable".AsMemory(), timeout.Token);
        }
        var server = Reject();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Otp:Smtp:Host"] = "127.0.0.1", ["Otp:Smtp:Port"] = port.ToString(),
            ["Otp:Smtp:From"] = "sender@example.test",
            ["Otp:Smtp:Username"] = "private-user", ["Otp:Smtp:Password"] = "private-password"
        }).Build();
        using var services = new ServiceCollection().AddHttpClient().BuildServiceProvider();
        var log = new CapturedLog();
        var transport = new WorkflowTransport(configuration, services.GetRequiredService<IHttpClientFactory>(), new HostingEnvironment(), log);
        var id = Guid.NewGuid();
        var message = otp ? OtpWorkflowMessage.Create("email", "recipient@example.test", "123456")
            : new WorkflowMessage("email", "recipient@example.test", "SmartPark application decision", "Your application is approved.");
        var error = await Assert.ThrowsAsync<SmtpException>(() => transport.SendAsync(message, id, timeout.Token));
        await server;
        Assert.Equal(SmtpStatusCode.ServiceNotAvailable, error.StatusCode);
        var entry = Assert.Single(log.Messages);
        Assert.Contains(id.ToString(), entry);
        Assert.Contains("421", entry);
        foreach (var secret in new[] { message.Destination, message.Body, "123456", "private-user", "private-password" })
            Assert.DoesNotContain(secret, entry);
    }

    private sealed class CapturedLog : ILogger<WorkflowTransport>
    {
        public List<string> Messages = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            Assert.Null(exception);
            Messages.Add(formatter(state, exception));
        }
    }
}
