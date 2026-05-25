using Qashira.Application.DTOs;
using Qashira.Shared.Results;

namespace Qashira.Application.Abstractions;

public interface ISystemSettingsService
{
    Task<SystemSettingsDto> GetSettingsAsync(CancellationToken cancellationToken = default);
    Task<Result> SaveSettingsAsync(SystemSettingsDto settings, int userId, CancellationToken cancellationToken = default);
}
