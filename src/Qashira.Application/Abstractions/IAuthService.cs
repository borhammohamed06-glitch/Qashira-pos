using Qashira.Application.DTOs;
using Qashira.Shared.Results;

namespace Qashira.Application.Abstractions;

public interface IAuthService
{
    Task<Result<AuthSessionDto>> LoginAsync(string username, string password, CancellationToken cancellationToken = default);
    Task<Result> ChangeRequiredPasswordAsync(int userId, string newPassword, CancellationToken cancellationToken = default);
    Task LogoutAsync(CancellationToken cancellationToken = default);
}
