using FluentValidation;
using Npgsql;
using SmartParking.OperationService.Application.Common.Models.Exceptions;
using SmartParking.OperationService.Domain;

namespace SmartParking.OperationService.API.Middleware;

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
        catch (ValidationException error)
        {
            await WriteErrorAsync(context, StatusCodes.Status400BadRequest, new
            {
                success = false,
                code = "VALIDATION_FAILED",
                message = "One or more validation errors occurred.",
                errors = error.Errors
                    .GroupBy(e => e.PropertyName)
                    .Select(group => new
                    {
                        property = group.Key,
                        message = string.Join(" ", group.Select(e => e.ErrorMessage))
                    }),
                traceId = context.TraceIdentifier
            });
        }
        catch (InvalidStructureEditException error)
        {
            await WriteErrorAsync(context, StatusCodes.Status400BadRequest, new
            {
                success = false,
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
            await WriteErrorAsync(context, StatusCodes.Status409Conflict, new
            {
                success = false,
                code = "STRUCTURE_BUSY",
                message = "Another request is managing this site."
            });
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
