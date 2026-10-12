using SmartParking.UserService.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using SmartParking.UserService.Domain.Entities;
using UserService.Application.Common.Models.Exceptions;
using UserService.Application.DTOs;
using UserService.Application.Common.Interfaces.Services;
namespace UserService.Persistence.Repositories;

public sealed class VehicleRegistrationService(AppDbContext db,TimeProvider clock):IVehicleRegistration
{
    public static string NormalizePlate(string raw,string type)
    {
        if (!LicensePlateNormalizer.TryNormalize(raw,type,out var canonical))throw new AuthException("INVALID_PLATE","Plate does not match the supported Vietnamese application format.",400);
        return canonical;
    }
    private async Task<Guid[]> RequireDriver(Guid id,CancellationToken ct)
    {
        var allowed=await db.Database.SqlQuery<Guid>($"""
            SELECT a.id AS "Value" FROM users u JOIN accounts a ON a.user_id=u.id JOIN account_roles r ON r.account_id=a.id
            WHERE u.id={id} AND u.status='ACTIVE' AND u.deleted_at IS NULL AND a.status='ACTIVE' AND a.deleted_at IS NULL
            AND r.role_code='DRIVER' ORDER BY a.id FOR SHARE OF u,a,r
            """).ToListAsync(ct);
        if(allowed.Count==0)throw new AuthException("FORBIDDEN","Current Driver access is required.",403);
        return allowed.Distinct().ToArray();
    }
    public async Task<IReadOnlyList<RegisteredVehicle>> ListAsync(Guid id,CancellationToken ct)
    {
        await using var tx=await db.Database.BeginTransactionAsync(ct);var accounts=await RequireDriver(id,ct);
        var result=await db.RegisteredVehicles.AsNoTracking().Where(v=>v.AccountId!=null && accounts.Contains(v.AccountId.Value) && v.DeletedAt==null && (v.VehicleType=="CAR" || v.VehicleType=="MOTORCYCLE") && v.CanonicalPlate!="").OrderBy(v=>v.CreatedAt).ToArrayAsync(ct);await tx.CommitAsync(ct);return result;
    }
    public async Task<RegisteredVehicle> SaveAsync(Guid owner,Guid? id,VehicleDto body,CancellationToken ct)
    {
        AccountWorkflowService.Validate(body);var normalized=NormalizePlate(body.Plate,body.VehicleType);
        if(body.ImageReference is not null && (!Uri.TryCreate(body.ImageReference,UriKind.Absolute,out var image)||image.Scheme!="https"))throw new AuthException("VALIDATION_FAILED","Image reference must be an HTTPS URL.",400);
        await using var tx=await db.Database.BeginTransactionAsync(ct);var accounts=await RequireDriver(owner,ct);
        RegisteredVehicle vehicle;
        if(id is {} existing)
        {
            vehicle=await db.RegisteredVehicles.FromSqlInterpolated($"SELECT * FROM vehicles WHERE id={existing} AND account_id=ANY({accounts}) AND deleted_at IS NULL AND vehicle_type IN ('CAR','MOTORCYCLE') AND normalized_plate<>'' FOR UPDATE").SingleOrDefaultAsync(ct)
                ??throw new AuthException("VEHICLE_NOT_FOUND","Vehicle was not found.",404);
        }
        else {vehicle=new(){Id=Guid.NewGuid(),AccountId=accounts[0],CreatedAt=clock.GetUtcNow()};db.RegisteredVehicles.Add(vehicle);}
        vehicle.RawPlate=body.Plate;vehicle.CanonicalPlate=normalized;vehicle.VehicleType=body.VehicleType;vehicle.ImageReference=body.ImageReference;vehicle.UpdatedAt=clock.GetUtcNow();
        await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);return vehicle;
    }
}
