using SmartParking.UserService.Domain.Entities;

namespace UserService.Application.Authorization;

/// <summary>Authoritative access for the exact authenticated account and session.</summary>
public sealed record CurrentAccessContext(User User, Account Account, AuthSession Session, string RoleCode, string Role);
