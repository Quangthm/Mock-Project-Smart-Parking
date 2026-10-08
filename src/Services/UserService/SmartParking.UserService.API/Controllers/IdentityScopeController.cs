using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UserService.Persistence;

namespace UserService.API.Controllers;

// Parking forwards the caller's bearer token. No shared DbContext or signing private key.
[ApiController, Authorize(Roles = "owner"), Route("api/auth/owner-scope")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class IdentityScopeController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct, string permission="PARKING_MANAGE")
    {
        if (!Guid.TryParse(User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value, out var userId)) return Unauthorized();
        if(permission is not ("PARKING_MANAGE" or "BACKUP_CONFIGURE" or "BACKUP_MARK"))return BadRequest();
        await UserService.Persistence.Repositories.AccountWorkflowService.RequirePermissionAsync(db,userId,permission,ct);
        var tenantIds = await db.Accounts.AsNoTracking().Where(a => a.UserId == userId && a.DeletedOn == null &&
            a.Status == "ACTIVE" && a.SiteId == null && a.TenantId != null &&
            a.AccountRoles.Any(r => r.RoleCode == "BUSINESS_OWNER")).Select(a => a.TenantId!.Value).Distinct().ToArrayAsync(ct);
        return Ok(new { userId, tenantIds });
    }
}
