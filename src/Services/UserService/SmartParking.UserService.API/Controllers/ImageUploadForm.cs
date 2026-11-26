namespace UserService.API.Controllers;

public sealed class ImageUploadForm
{
    public IFormFile? File { get; set; }
    public Guid? VehicleId { get; set; }
    public Guid? DraftIntentId { get; set; }
}
