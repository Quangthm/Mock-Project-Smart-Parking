using Microsoft.AspNetCore.Mvc;

namespace UserService.API.Common;

/// <summary>A status code and the body to serialize for it.</summary>
public readonly record struct ApiResponse(int StatusCode, object Body)
{
    /// <summary>Materializes the response for a controller action.</summary>
    public IActionResult ToActionResult() => StatusCode >= 400
        ? new ApiErrorActionResult(this) : new ObjectResult(Body) { StatusCode = StatusCode };
}
