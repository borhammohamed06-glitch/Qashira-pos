using Qashira.Application.Abstractions;

namespace Qashira.Infrastructure.Security;

public sealed class BCryptPasswordHasher : IPasswordHasher
{
    public string HashPassword(string password, out string salt)
    {
        salt = BCrypt.Net.BCrypt.GenerateSalt(12);
        return BCrypt.Net.BCrypt.HashPassword(password, salt);
    }

    public bool Verify(string password, string passwordHash) =>
        BCrypt.Net.BCrypt.Verify(password, passwordHash);
}
