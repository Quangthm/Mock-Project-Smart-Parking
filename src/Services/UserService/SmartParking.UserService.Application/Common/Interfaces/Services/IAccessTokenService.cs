using SmartParking.UserService.Domain.Entities;

namespace UserService.Application.Common.Interfaces.Services;

public interface IAccessTokenService
{
    string GenerateAccessToken(User user, Guid accessTokenId, DateTimeOffset expiresAt);
    Task<bool> ValidateAccessTokenAsync(string accessToken, CancellationToken cancellationToken = default);
}
