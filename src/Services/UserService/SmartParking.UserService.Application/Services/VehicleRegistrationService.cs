using System.ComponentModel.DataAnnotations;
using SmartParking.UserService.Domain.Entities;
using SmartParking.UserService.Domain.ValueObjects;
using UserService.Application.Common.Interfaces.Persistence;
using UserService.Application.Common.Interfaces.Services;
using UserService.Application.Common.Models.Exceptions;
using UserService.Application.DTOs;

namespace UserService.Application.Services;

public sealed class VehicleRegistrationService(
    IVehicleRepository vehicles, IUnitOfWork unitOfWork, TimeProvider clock) : IVehicleRegistration
{
    public static string NormalizePlate(string raw, string type)
    {
        if (!LicensePlateNormalizer.TryNormalize(raw, type, out var canonical))
            throw new AuthException("INVALID_PLATE", "Plate does not match the supported Vietnamese application format.", 400);
        return canonical;
    }

    private async Task<Guid[]> RequireDriverAsync(Guid actor, CancellationToken ct)
    {
        var accounts = await vehicles.LockActiveDriverAccountsAsync(actor, ct);
        if (accounts.Length == 0)
            throw new AuthException("FORBIDDEN", "Current Driver access is required.", 403);
        return accounts;
    }

    public async Task<IReadOnlyList<RegisteredVehicle>> ListAsync(Guid actor, CancellationToken ct)
    {
        await using var transaction = await unitOfWork.BeginTransactionAsync(ct);
        var accounts = await RequireDriverAsync(actor, ct);
        var result = await vehicles.ListAsync(accounts, ct);
        await transaction.CommitAsync(ct);
        return result;
    }

    public async Task<RegisteredVehicle> SaveAsync(Guid actor, Guid? id, VehicleDto body, CancellationToken ct)
    {
        var errors = new List<ValidationResult>();
        if (!Validator.TryValidateObject(body, new ValidationContext(body), errors, true))
            throw new AuthException("VALIDATION_FAILED", string.Join(" ", errors.Select(e => e.ErrorMessage)), 400);
        var normalized = NormalizePlate(body.Plate, body.VehicleType);
        if (body.ImageReference is not null &&
            (!Uri.TryCreate(body.ImageReference, UriKind.Absolute, out var image) || image.Scheme != "https"))
            throw new AuthException("VALIDATION_FAILED", "Image reference must be an HTTPS URL.", 400);

        await using var transaction = await unitOfWork.BeginTransactionAsync(ct);
        var accounts = await RequireDriverAsync(actor, ct);
        RegisteredVehicle vehicle;
        if (id is { } existing)
        {
            vehicle = await vehicles.FindForUpdateAsync(existing, accounts, ct)
                ?? throw new AuthException("VEHICLE_NOT_FOUND", "Vehicle was not found.", 404);
        }
        else
        {
            vehicle = new RegisteredVehicle
            {
                Id = Guid.NewGuid(), AccountId = accounts[0], CreatedAt = clock.GetUtcNow()
            };
            vehicles.Add(vehicle);
        }

        vehicle.RawPlate = body.Plate;
        vehicle.CanonicalPlate = normalized;
        vehicle.VehicleType = body.VehicleType;
        vehicle.ImageReference = body.ImageReference;
        vehicle.UpdatedAt = clock.GetUtcNow();
        await unitOfWork.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return vehicle;
    }
}
