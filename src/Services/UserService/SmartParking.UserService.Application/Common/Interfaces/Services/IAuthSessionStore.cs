namespace UserService.Application.Common.Interfaces.Services;

public interface IAuthSessionStore
{
    void Add(string sessionId, Guid userId, DateTimeOffset expiresAt);
    bool IsActive(string sessionId, Guid userId);
    void Revoke(string sessionId);
}
