using AutoMapper;
using MediatR;
using SmartParking.UserService.Domain.Entities;
using UserService.Application.Common.Interfaces.Persistence;
using UserService.Application.Common.Interfaces.Services;
using UserService.Application.Common.Models.Exceptions;
using UserService.Application.Media;

namespace UserService.Application.Usecase.Vehicles;

public sealed record SaveOwnVehicleCommand(Guid? VehicleId, string Plate, string VehicleType,
    Guid? ImageAssetId, Guid? ExpectedImageAssetId) : IRequest<VehicleSaveOutcome>;

public sealed class SaveOwnVehicleCommandHandler(IVehicleRepository vehicles, IRegistrationClaimRepository claims,
    IMediaAssetRepository media, IUnitOfWork unitOfWork, IAccountAuthorizationService authorization,
    ICurrentActor actor, MediaSettings mediaSettings, TimeProvider clock, IMapper mapper)
    : IRequestHandler<SaveOwnVehicleCommand, VehicleSaveOutcome>
{
    private static AuthException PlateConflict() => new("PLATE_EXISTS", "A vehicle with this plate is already registered.", 409);

    public async Task<VehicleSaveOutcome> Handle(SaveOwnVehicleCommand request, CancellationToken ct)
    {
        var normalized = RegisteredVehicle.Canonicalize(request.Plate, request.VehicleType);
        var id = request.VehicleId;
        await using var transaction = await unitOfWork.BeginTransactionAsync(ct);
        var current = await authorization.RequireDriverAsync(actor.UserId ?? throw AuthException.InvalidToken(), true, ct);
        var accountId = current.Account.Id!.Value;
        await vehicles.LockPlateAsync(normalized, ct);
        // A recorded claim is never promoted by retry, even after incumbent removal.
        if (id is null && await claims.FindPendingAsync(accountId, normalized, ct) is not null)
            return new(null);
        var active = await vehicles.FindActiveAsync(normalized, ct);
        RegisteredVehicle vehicle;
        if (id is { } existing)
        {
            vehicle = await vehicles.FindForUpdateAsync(existing, accountId, ct)
                ?? throw new AuthException("VEHICLE_NOT_FOUND", "Vehicle was not found.", 404);
            if (active is not null && active.Id != vehicle.Id) throw PlateConflict();
            if (!vehicle.MatchesIdentity(normalized, request.VehicleType))
                throw new AuthException("VEHICLE_DEPENDENCY_UNAVAILABLE", "Vehicle identity changes are unavailable until active dependencies can be checked.", 503);
        }
        else
        {
            if (active?.AccountId == accountId) return new(null);
            if (active is null && await vehicles.HasHistoryAsync(normalized, ct))
                throw new AuthException("VEHICLE_RECONCILIATION_REQUIRED", "Vehicle binding requires reconciliation.", 409);
            vehicle = RegisteredVehicle.Register(accountId, request.Plate, request.VehicleType, clock.GetUtcNow());
        }
        MediaAsset? asset = null;
        if (vehicle.ImageAssetId != request.ImageAssetId)
        {
            if (!mediaSettings.Enabled) throw new AuthException("MEDIA_DISABLED", "Media is disabled.", 503);
            if (vehicle.ImageAssetId != request.ExpectedImageAssetId) throw new AuthException("MEDIA_CONFLICT", "Image changed.", 409);
            asset = await media.FindAsync(request.ImageAssetId!.Value, true, ct);
            if (asset is null || asset.UserId != current.User.Id || asset.CreatorAccountId != accountId || asset.Purpose != "vehicle")
                throw new AuthException("MEDIA_NOT_FOUND", "Image was not found.", 404);
            if (asset.Expired(clock.GetUtcNow())) throw new AuthException("MEDIA_EXPIRED", "Image has expired.", 410);
            if (asset.Environment != mediaSettings.Environment || !asset.CanAttach(clock.GetUtcNow(), vehicle.Id))
                throw new AuthException("MEDIA_CONFLICT", "Image cannot be attached.", 409);
        }
        if (id is null)
        {
            if (active is null && !await vehicles.TryAddAsync(vehicle, ct))
            {
                active = await vehicles.FindActiveAsync(normalized, ct);
                if (active is null) throw new AuthException("VEHICLE_RECONCILIATION_REQUIRED", "Vehicle binding requires reconciliation.", 409);
            }
            if (active is not null)
            {
                if (active.AccountId == accountId) return new(null);
                var claim = VehicleRegistrationClaim.Submit(accountId, request.Plate, request.VehicleType,
                    asset!.Id, clock.GetUtcNow());
                claims.Add(claim);
                await unitOfWork.SaveChangesAsync(ct);
                asset.AttachToRegistrationClaim(claim.Id, clock.GetUtcNow());
                await unitOfWork.SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);
                return new(null); // Conflict follows a successful pending-submission commit.
            }
        }
        if (asset is not null) asset.Attach(vehicle.Id, clock.GetUtcNow());
        vehicle.UpdateMetadata(request.Plate, request.ImageAssetId!.Value, clock.GetUtcNow());
        await unitOfWork.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return new(mapper.Map<VehicleResponse>(vehicle));
    }
}
