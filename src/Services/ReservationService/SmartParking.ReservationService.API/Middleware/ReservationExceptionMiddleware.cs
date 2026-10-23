using Npgsql;
using SmartParking.ReservationService.Application.Common.Models.Exceptions;
using SmartParking.ReservationService.Domain;

namespace SmartParking.ReservationService.API.Middleware;

public sealed class ReservationExceptionMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        context.Response.Headers.CacheControl = "no-store";
        try
        {
            await next(context);
        }
        catch (PostgresException error) when (
            (context.Request.Path.StartsWithSegments("/api/reservations") ||
             context.Request.Path.StartsWithSegments("/api/parking-sessions")) &&
            error.SqlState is PostgresErrorCodes.UndefinedTable or PostgresErrorCodes.UndefinedColumn)
        {
            await WriteErrorAsync(context, StatusCodes.Status503ServiceUnavailable, new
            {
                success = false,
                code = "PROTOTYPE_SCHEMA_NOT_READY",
                message = "Reservation data is unavailable until database reconciliation is complete."
            });
        }
        catch (InvalidStructureEditException error)
        {
            await WriteErrorAsync(context, StatusCodes.Status400BadRequest, new
            {
                code = "INVALID_STRUCTURE",
                message = error.Message
            });
        }
        catch (ReservationGateException error)
        {
            await WriteErrorAsync(context, error.StatusCode, new
            {
                success = false,
                code = error.Code,
                message = error.Message
            });
        }
        catch (HoldConflictException)
        {
            await WriteErrorAsync(context, StatusCodes.Status409Conflict, new { code = "STRUCTURE_BUSY" });
        }
    }

    private static async Task WriteErrorAsync(HttpContext context, int statusCode, object body)
    {
        if (context.Response.HasStarted)
            throw new InvalidOperationException("Cannot write an API error after the response has started.");

        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsJsonAsync(body, context.RequestAborted);
    }
}
