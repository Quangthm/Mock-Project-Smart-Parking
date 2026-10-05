using FluentValidation;
using UserService.Application;
using UserService.Persistence;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddControllers();
builder.Services.AddApplicationServices(builder.Configuration);
builder.Services.AddPersistenceServices();
builder.Services.AddJWTAuthentication(builder.Configuration);
builder.Services.AddAuthorization();
// Only allow the local frontend during this demo. Production uses explicit configured origins.
builder.Services.AddCors(options => options.AddPolicy("Frontend", policy => policy
    .WithOrigins("http://localhost:5173", "http://127.0.0.1:5173")
    .AllowAnyHeader()
    .AllowAnyMethod()));

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// Translate the demo use case's failures into HTTP responses.
app.Use(async (context, next) =>
{
    try
    {
        await next(context);
    }
    catch (ValidationException exception)
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        await context.Response.WriteAsJsonAsync(new
        {
            success = false,
            message = "Validation failed.",
            errors = exception.Errors.Select(error => new
            {
                field = error.PropertyName,
                message = error.ErrorMessage
            })
        });
    }
    catch (UnauthorizedAccessException exception)
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        await context.Response.WriteAsJsonAsync(new { success = false, message = exception.Message });
    }
});

app.UseCors("Frontend");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.Run();
