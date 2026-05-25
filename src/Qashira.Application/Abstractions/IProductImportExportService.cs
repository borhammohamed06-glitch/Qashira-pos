using Qashira.Application.DTOs;
using Qashira.Shared.Results;

namespace Qashira.Application.Abstractions;

public interface IProductImportExportService
{
    Task<Result<ProductImportResultDto>> ImportProductsAsync(string productFilePath, int userId, CancellationToken cancellationToken = default);
    Task<Result<ProductExportResultDto>> ExportProductsAsync(string exportPath, bool includeInactive, int userId, CancellationToken cancellationToken = default);
}
