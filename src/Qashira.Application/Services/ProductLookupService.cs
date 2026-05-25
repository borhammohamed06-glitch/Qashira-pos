using Qashira.Application.Abstractions;
using Qashira.Application.DTOs;
using Qashira.Application.Permissions;
using Qashira.Domain.Enums;
using Qashira.Shared.Arabic;
using Microsoft.EntityFrameworkCore;

namespace Qashira.Application.Services;

public sealed class ProductLookupService(
    IApplicationDbContext dbContext,
    IPermissionService permissionService) : IProductLookupService
{
    public async Task<IReadOnlyList<ProductLookupDto>> SearchAsync(string searchText, CancellationToken cancellationToken = default)
    {
        permissionService.EnsureCurrentUserHas(PermissionCodes.CanUsePOS);

        var normalized = ArabicTextNormalizer.NormalizeForSearch(searchText);

        var query = dbContext.Products
            .AsNoTracking()
            .Where(x => x.IsActive && (x.ProductType == ProductType.NormalProduct || x.ProductType == ProductType.PrintedProduct));

        if (!string.IsNullOrWhiteSpace(normalized))
        {
            query = query.Where(x =>
                x.SearchName.Contains(normalized) ||
                x.Barcode.Contains(searchText) ||
                x.InternalCode.Contains(searchText));
        }

        return await query
            .OrderBy(x => x.Name)
            .Take(30)
            .Select(x => new ProductLookupDto(x.Id, x.Name, x.Barcode, x.SalePrice, x.StockQuantity))
            .ToListAsync(cancellationToken);
    }

    public async Task<ProductLookupDto?> FindByBarcodeAsync(string barcode, CancellationToken cancellationToken = default)
    {
        permissionService.EnsureCurrentUserHas(PermissionCodes.CanUsePOS);

        var trimmed = barcode.Trim();

        return await dbContext.Products
            .AsNoTracking()
            .Where(x => x.IsActive &&
                (x.ProductType == ProductType.NormalProduct || x.ProductType == ProductType.PrintedProduct) &&
                x.Barcode == trimmed)
            .Select(x => new ProductLookupDto(x.Id, x.Name, x.Barcode, x.SalePrice, x.StockQuantity))
            .SingleOrDefaultAsync(cancellationToken);
    }
}
