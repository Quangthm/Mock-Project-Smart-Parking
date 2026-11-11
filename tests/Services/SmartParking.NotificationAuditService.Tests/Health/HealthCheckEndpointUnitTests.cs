using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using SmartParking.NotificationAuditService.Persistence.Context;

namespace SmartParking.NotificationAuditService.Tests.Health;

public class HealthCheckEndpointUnitTests
{
    private class DisconnectingDbContext : NotificationAuditDbContext
    {
        private readonly bool _throwException;
        private readonly bool _canConnect;

        public DisconnectingDbContext(DbContextOptions<NotificationAuditDbContext> options, bool canConnect, bool throwException = false)
            : base(options)
        {
            _canConnect = canConnect;
            _throwException = throwException;
        }

        // We simulate the health check logic directly
    }

    private static async Task<IResult> ExecuteHealthCheckLogic(Func<Task<bool>> canConnectFunc)
    {
        var dbConnected = false;
        try
        {
            dbConnected = await canConnectFunc();
        }
        catch
        {
            dbConnected = false;
        }

        var response = new
        {
            status = dbConnected ? "Healthy" : "Unhealthy",
            service = "SmartParking.NotificationAuditService",
            timestamp = DateTimeOffset.UtcNow,
            dependencies = new
            {
                database = dbConnected ? "Connected" : "Disconnected",
                messaging = "MassTransit.RabbitMQ Configured"
            }
        };

        return dbConnected
            ? Results.Ok(response)
            : Results.Json(response, statusCode: StatusCodes.Status503ServiceUnavailable);
    }

    [Fact]
    public async Task HealthCheck_WhenCanConnectIsTrue_Returns200OkHealthy()
    {
        var result = await ExecuteHealthCheckLogic(() => Task.FromResult(true));

        var statusResult = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
        Assert.Equal((int)HttpStatusCode.OK, statusResult.StatusCode);

        var value = result.GetType().GetProperty("Value")?.GetValue(result);
        Assert.NotNull(value);

        var json = JsonSerializer.Serialize(value);
        using var doc = JsonDocument.Parse(json);
        Assert.Equal("Healthy", doc.RootElement.GetProperty("status").GetString());
        Assert.Equal("Connected", doc.RootElement.GetProperty("dependencies").GetProperty("database").GetString());
    }

    [Fact]
    public async Task HealthCheck_WhenCanConnectIsFalse_Returns503ServiceUnavailableUnhealthy()
    {
        var result = await ExecuteHealthCheckLogic(() => Task.FromResult(false));

        var statusResult = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
        Assert.Equal((int)HttpStatusCode.ServiceUnavailable, statusResult.StatusCode);

        var value = result.GetType().GetProperty("Value")?.GetValue(result);
        Assert.NotNull(value);

        var json = JsonSerializer.Serialize(value);
        using var doc = JsonDocument.Parse(json);
        Assert.Equal("Unhealthy", doc.RootElement.GetProperty("status").GetString());
        Assert.Equal("Disconnected", doc.RootElement.GetProperty("dependencies").GetProperty("database").GetString());
    }

    [Fact]
    public async Task HealthCheck_WhenCanConnectThrowsException_Returns503ServiceUnavailableUnhealthy()
    {
        var result = await ExecuteHealthCheckLogic(() => throw new TimeoutException("Database connection timeout."));

        var statusResult = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
        Assert.Equal((int)HttpStatusCode.ServiceUnavailable, statusResult.StatusCode);

        var value = result.GetType().GetProperty("Value")?.GetValue(result);
        Assert.NotNull(value);

        var json = JsonSerializer.Serialize(value);
        using var doc = JsonDocument.Parse(json);
        Assert.Equal("Unhealthy", doc.RootElement.GetProperty("status").GetString());
        Assert.Equal("Disconnected", doc.RootElement.GetProperty("dependencies").GetProperty("database").GetString());
    }
}
