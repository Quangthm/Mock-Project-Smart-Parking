using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UserService.API.Common;
using UserService.Application.Media;
using UserService.Application.Common.Models.Exceptions;

namespace UserService.API.Controllers;

[ApiController, Authorize]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class MediaImagesController(IMediator mediator) : ControllerBase
{
    public sealed class ImageUploadForm
    {
        public IFormFile? File { get; set; }
        public Guid? VehicleId { get; set; }
        public Guid? DraftIntentId { get; set; }
    }

    [HttpPost("api/vehicles/images")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(UploadImageValidator.MaximumBytes + 65536)]
    [RequestFormLimits(MultipartBodyLengthLimit = UploadImageValidator.MaximumBytes + 1, MemoryBufferThreshold = 65536)]
    public Task<IActionResult> Vehicle([FromForm] ImageUploadForm request, CancellationToken ct) => UploadAsync("vehicle", request, ct);

    [HttpPost("api/auth/me/avatar/images")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(UploadImageValidator.MaximumBytes + 65536)]
    [RequestFormLimits(MultipartBodyLengthLimit = UploadImageValidator.MaximumBytes + 1, MemoryBufferThreshold = 65536)]
    public Task<IActionResult> Avatar([FromForm] ImageUploadForm request, CancellationToken ct) => UploadAsync("avatar", request, ct);

    private async Task<IActionResult> UploadAsync(string purpose, ImageUploadForm request, CancellationToken ct)
    {
        if (request.File is null || request.File.Length is <= 0 or > UploadImageValidator.MaximumBytes)
            throw new AuthException("MEDIA_INVALID_IMAGE", "Invalid image size.", 400);
        await using var input = request.File.OpenReadStream();
        using var output = new MemoryStream();
        var buffer = new byte[81920];
        int read;
        while ((read = await input.ReadAsync(buffer, ct)) != 0)
        {
            if (output.Length + read > UploadImageValidator.MaximumBytes) throw new AuthException("MEDIA_INVALID_IMAGE", "Invalid image size.", 400);
            await output.WriteAsync(buffer.AsMemory(0, read), ct);
        }
        var result = await mediator.Send(new UploadImageCommand(purpose, output.ToArray(),
            Request.Headers["Idempotency-Key"].ToString(), request.VehicleId, request.DraftIntentId), ct);
        return StatusCode(result.State == "ready" ? 200 : 202, new { success = true, data = result });
    }

    [HttpPut("api/auth/me/avatar")]
    public async Task<IActionResult> AttachAvatar(AttachImageRequest request, CancellationToken ct) =>
        ApiResponseFactory.Success(await mediator.Send(new AttachImageCommand("avatar", null, request.AssetId, request.ExpectedAssetId), ct)).ToActionResult();

    [HttpPut("api/vehicles/{vehicleId:guid}/image")]
    public async Task<IActionResult> AttachVehicle(Guid vehicleId, AttachImageRequest request, CancellationToken ct) =>
        ApiResponseFactory.Success(await mediator.Send(new AttachImageCommand("vehicle", vehicleId, request.AssetId, request.ExpectedAssetId), ct)).ToActionResult();

    [HttpGet("api/media/images/{assetId:guid}")]
    public async Task<IActionResult> Status(Guid assetId, CancellationToken ct) =>
        ApiResponseFactory.Success(await mediator.Send(new GetImageStatusQuery(assetId), ct)).ToActionResult();

    [HttpGet("api/media/images/{assetId:guid}/content")]
    public async Task<IActionResult> Content(Guid assetId, CancellationToken ct)
    {
        var content = await mediator.Send(new GetImageQuery(assetId), ct);
        Response.Headers.CacheControl = "private, no-store";
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        Response.Headers.ContentDisposition = "inline; filename=\"image\"";
        return File(content.Stream, content.MimeType, enableRangeProcessing: false);
    }
}
