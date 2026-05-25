using Qashira.Application.DTOs;

namespace Qashira.Application.Abstractions;

public interface IProductLookupService
{
    Task<IReadOnlyList<ProductLookupDto>> SearchAsync(string searchText, CancellationToken cancellationToken = default);
    Task<ProductLookupDto?> FindByBarcodeAsync(string barcode, CancellationToken cancellationToken = default);
}
