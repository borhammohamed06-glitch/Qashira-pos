namespace Qashira.Application.Abstractions;

public interface ICurrentUserSession
{
    int? UserId { get; }
    string? Username { get; }
    string? FullName { get; }
    IReadOnlySet<string> Permissions { get; }
    bool IsAuthenticated { get; }
    void SignIn(int userId, string username, string fullName, IEnumerable<string> permissions);
    void SignOut();
}
