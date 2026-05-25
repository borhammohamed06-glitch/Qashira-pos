using Qashira.Application.DTOs;
using Qashira.Shared.Results;

namespace Qashira.Application.Abstractions;

public interface IPrinterSettingsService
{
    Task<PrinterSettingsDto> GetSettingsAsync(CancellationToken cancellationToken = default);
    Task<Result> SaveSettingsAsync(PrinterSettingsDto settings, int userId, CancellationToken cancellationToken = default);
}
