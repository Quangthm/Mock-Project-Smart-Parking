using UserService.Application.Common.Interfaces.Services;

namespace UserService.Infrastructure.Services;

public sealed class BcryptPasswordService : IPasswordService
{
    public string Hash(string password) => BCrypt.Net.BCrypt.HashPassword(password, workFactor: 12);

    public bool Verify(string password, string hash)
    {
        try { return BCrypt.Net.BCrypt.Verify(password, hash); }
        catch (Exception exception) when (exception is BCrypt.Net.SaltParseException or FormatException or ArgumentException)
        {
            // Legacy plaintext and malformed hashes are rejected, never compared directly.
            return false;
        }
    }
}
