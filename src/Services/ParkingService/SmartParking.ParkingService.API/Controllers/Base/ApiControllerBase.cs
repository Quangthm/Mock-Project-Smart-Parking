using Microsoft.AspNetCore.Mvc;

namespace SmartParking.ParkingService.API.Controllers.Base;

[ApiController]
public abstract class ApiControllerBase : ControllerBase
{
    protected ActionResult<object> Success(object data) =>
        Ok(new { success = true, data });
}
