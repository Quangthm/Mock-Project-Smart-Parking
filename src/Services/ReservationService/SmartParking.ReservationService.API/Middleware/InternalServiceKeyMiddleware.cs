using System.Security.Cryptography;
using System.Text;

namespace SmartParking.ReservationService.API.Middleware;

public sealed class InternalServiceKeyMiddleware(RequestDelegate next, IConfiguration configuration)
{
    public async Task InvokeAsync(HttpContext context)
    {
        if (context.Request.Path.StartsWithSegments("/internal"))
        {
            var key = configuration["Services:Key"]
                ?? throw new InvalidOperationException("Configure Services:Key.");
            var providedKey = context.Request.Headers["X-Service-Key"].ToString();
            if (!CryptographicOperations.FixedTimeEquals(
                    Encoding.UTF8.GetBytes(providedKey),
                    Encoding.UTF8.GetBytes(key)))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return;
            }
        }

        await next(context);
    }
}
