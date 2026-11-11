using System.Security.Cryptography;
using SmartParking.UserService.Domain.Entities;
using SmartParking.UserService.Domain.Enum;
using UserService.Application.Common.Interfaces.Persistence;
using UserService.Application.Common.Interfaces.Services;
using UserService.Application.Common.Models.Exceptions;
using UserService.Application.DTOs;

namespace UserService.Application.Services;

public sealed class VNeIdSimulationService(
    IIdentityWorkflowStore db,
    IIdentityAccessRepository identityRepo,
    TimeProvider clock) : IVNeIdSimulationService
{
    private static AuthException Error(string code, string message, int status = 400) => new(code, message, status);

    private static (Guid Guid, string MockNumber, string MockName) GenerateMockIdentity(string fullName)
    {
        var guid = Guid.NewGuid();
        var randomDigits = RandomNumberGenerator.GetInt32(100000, 999999).ToString("D6");
        var mockDoc = $"001090{randomDigits}";
        var mockName = string.IsNullOrWhiteSpace(fullName) ? "SIMULATED CITIZEN" : fullName.Trim().ToUpperInvariant();
        return (guid, mockDoc, mockName);
    }

    public async Task<SimulatedVNeIdResult> CompleteDriverSimulationAsync(SimulatedVNeIdRequest request, CancellationToken ct)
    {
        if (request.RegistrationId == Guid.Empty)
            throw Error("VALIDATION_FAILED", "Registration ID is required.");

        if (!request.ConsentConfirmed)
            throw Error("VNEID_CONSENT_REQUIRED", "Explicit consent for simulated VNeID information-sharing is required.", 422);

        await using var tx = await db.BeginTransactionAsync(ct);
        var reg = await db.LockDriverRegistrationAsync(request.RegistrationId, ct);
        if (reg is null)
            throw Error("REGISTRATION_NOT_FOUND", "Pending driver registration was not found.", 404);

        var user = await db.LockUserAsync(reg.UserId, ct);
        if (user.Status != UserStatus.PendingVerification || reg.VerifiedAt is not null)
            throw Error("REGISTRATION_CLOSED", "Registration is no longer pending.", 409);

        // Check if existing simulation exists (idempotency/replay)
        var existing = await db.LockSimulatedIdentityAsync(user.Id!.Value, ct);
        if (existing is not null)
        {
            if (existing.SimulationStatus == "COMPLETED")
            {
                await tx.CommitAsync(ct);
                return new SimulatedVNeIdResult(
                    existing.SimulatedVNeIdGuid,
                    existing.SimulationStatus,
                    existing.MockFullName ?? user.FullName,
                    existing.MockDocumentNumber ?? "001090******",
                    existing.ConsentTimestamp
                );
            }
        }

        var now = clock.GetUtcNow();
        var (guid, mockDoc, mockName) = GenerateMockIdentity(user.FullName);

        var identity = existing ?? new UserSimulatedIdentity
        {
            Id = Guid.NewGuid(),
            UserId = user.Id.Value,
            User = user
        };

        identity.SimulatedVNeIdGuid = guid;
        identity.ConsentGiven = true;
        identity.ConsentTimestamp = now;
        identity.ConsentVersion = request.ConsentVersion ?? "v1.1-simulated";
        identity.SimulationStatus = "COMPLETED";
        identity.MockDocumentNumber = mockDoc;
        identity.MockFullName = mockName;
        identity.CreatedOn = existing?.CreatedOn ?? now;
        identity.ModifiedOn = now;

        if (existing is null)
        {
            db.Stage(identity);
        }

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return new SimulatedVNeIdResult(guid, "COMPLETED", mockName, mockDoc, now);
    }

    public async Task<SimulatedVNeIdResult> CompleteOwnerSimulationAsync(SimulatedVNeIdRequest request, CancellationToken ct)
    {
        if (request.RegistrationId == Guid.Empty)
            throw Error("VALIDATION_FAILED", "Registration / Application ID is required.");

        if (!request.ConsentConfirmed)
            throw Error("VNEID_CONSENT_REQUIRED", "Explicit consent for simulated VNeID information-sharing is required.", 422);

        await using var tx = await db.BeginTransactionAsync(ct);
        var app = await db.LockOwnerApplicationAsync(request.RegistrationId, ct);
        if (app is null)
            throw Error("APPLICATION_NOT_FOUND", "Pending owner application was not found.", 404);

        var user = await db.LockUserAsync(app.UserId, ct);
        if (app.Status != "pending" || user.Status != UserStatus.PendingVerification)
            throw Error("APPLICATION_CLOSED", "Application is no longer pending.", 409);

        var existing = await db.LockSimulatedIdentityAsync(user.Id!.Value, ct);
        if (existing is not null && existing.SimulationStatus == "COMPLETED")
        {
            await tx.CommitAsync(ct);
            return new SimulatedVNeIdResult(
                existing.SimulatedVNeIdGuid,
                existing.SimulationStatus,
                existing.MockFullName ?? user.FullName,
                existing.MockDocumentNumber ?? "001090******",
                existing.ConsentTimestamp
            );
        }

        var now = clock.GetUtcNow();
        var (guid, mockDoc, mockName) = GenerateMockIdentity(user.FullName);

        var identity = existing ?? new UserSimulatedIdentity
        {
            Id = Guid.NewGuid(),
            UserId = user.Id.Value,
            User = user
        };

        identity.SimulatedVNeIdGuid = guid;
        identity.ConsentGiven = true;
        identity.ConsentTimestamp = now;
        identity.ConsentVersion = request.ConsentVersion ?? "v1.1-simulated";
        identity.SimulationStatus = "COMPLETED";
        identity.MockDocumentNumber = mockDoc;
        identity.MockFullName = mockName;
        identity.CreatedOn = existing?.CreatedOn ?? now;
        identity.ModifiedOn = now;

        if (existing is null)
        {
            db.Stage(identity);
        }

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return new SimulatedVNeIdResult(guid, "COMPLETED", mockName, mockDoc, now);
    }

    public async Task<SimulatedVNeIdResult> CompleteOperatorSimulationAsync(Guid challengeId, bool consentConfirmed, string? consentVersion, CancellationToken ct)
    {
        if (challengeId == Guid.Empty)
            throw Error("VALIDATION_FAILED", "Challenge ID is required.");

        if (!consentConfirmed)
            throw Error("VNEID_CONSENT_REQUIRED", "Explicit consent for simulated VNeID information-sharing is required.", 422);

        await using var tx = await db.BeginTransactionAsync(ct);
        var invitation = await identityRepo.LockInvitationAsync(challengeId, ct);
        var now = clock.GetUtcNow();

        if (invitation is not { } value || !OperatorActivation.CanActivate(value.User, value.Challenge, now))
            throw Error("INVALID_TOKEN", "Operator invitation link is invalid or expired.", 401);

        var user = value.User;
        var existing = await db.LockSimulatedIdentityAsync(user.Id!.Value, ct);
        if (existing is not null && existing.SimulationStatus == "COMPLETED")
        {
            await tx.CommitAsync(ct);
            return new SimulatedVNeIdResult(
                existing.SimulatedVNeIdGuid,
                existing.SimulationStatus,
                existing.MockFullName ?? user.FullName,
                existing.MockDocumentNumber ?? "001090******",
                existing.ConsentTimestamp
            );
        }

        var (guid, mockDoc, mockName) = GenerateMockIdentity(user.FullName);

        var identity = existing ?? new UserSimulatedIdentity
        {
            Id = Guid.NewGuid(),
            UserId = user.Id.Value,
            User = user
        };

        identity.SimulatedVNeIdGuid = guid;
        identity.ConsentGiven = true;
        identity.ConsentTimestamp = now;
        identity.ConsentVersion = consentVersion ?? "v1.1-simulated";
        identity.SimulationStatus = "COMPLETED";
        identity.MockDocumentNumber = mockDoc;
        identity.MockFullName = mockName;
        identity.CreatedOn = existing?.CreatedOn ?? now;
        identity.ModifiedOn = now;

        if (existing is null)
        {
            db.Stage(identity);
        }

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return new SimulatedVNeIdResult(guid, "COMPLETED", mockName, mockDoc, now);
    }

    public async Task<VNeIdSimulationStatusDto> GetStatusByUserIdAsync(Guid userId, CancellationToken ct)
    {
        if (userId == Guid.Empty)
            throw Error("VALIDATION_FAILED", "User ID is required.");

        var identity = await db.FindSimulatedIdentityAsync(userId, ct);
        if (identity is null || identity.SimulationStatus != "COMPLETED")
        {
            return new VNeIdSimulationStatusDto(false, null, null, null, null);
        }

        return new VNeIdSimulationStatusDto(
            true,
            identity.SimulatedVNeIdGuid,
            identity.MockFullName,
            identity.MockDocumentNumber,
            identity.ConsentTimestamp
        );
    }

    public async Task<bool> HasCompletedSimulationAsync(Guid userId, CancellationToken ct)
    {
        if (userId == Guid.Empty) return false;
        var identity = await db.FindSimulatedIdentityAsync(userId, ct);
        return identity is not null && identity.SimulationStatus == "COMPLETED";
    }
}
