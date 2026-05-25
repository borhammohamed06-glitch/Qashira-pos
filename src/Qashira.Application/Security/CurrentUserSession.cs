using Qashira.Application.Abstractions;

namespace Qashira.Application.Security;

public sealed class CurrentUserSession : ICurrentUserSession
{
    private HashSet<string> _permissions = new(StringComparer.Ordinal);

    public int? UserId { get; private set; }
    public string? Username { get; private set; }
    public string? FullName { get; private set; }
    public IReadOnlySet<string> Permissions => _permissions;
    public bool IsAuthenticated => UserId.HasValue;

    public void SignIn(int userId, string username, string fullName, IEnumerable<string> permissions)
    {
        UserId = userId;
        Username = username;
        FullName = fullName;
        _permissions = permissions.ToHashSet(StringComparer.Ordinal);
    }

    public void SignOut()
    {
        UserId = null;
        Username = null;
        FullName = null;
        _permissions.Clear();
    }
}
