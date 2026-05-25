using Qashira.Application.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace Qashira.Application.Services;

public sealed class BarcodeService(IApplicationDbContext dbContext) : IBarcodeService
{
    public async Task<string> GenerateUniqueBarcodeAsync(CancellationToken cancellationToken = default)
    {
        for (var attempt = 0; attempt < 20; attempt++)
        {
            var candidate = $"20{DateTimeOffset.UtcNow:yyMMddHHmmss}{Random.Shared.Next(10, 99)}";
            if (!await dbContext.Products.AnyAsync(x => x.Barcode == candidate, cancellationToken))
            {
                return candidate;
            }
        }

        throw new InvalidOperationException("تعذر إنشاء باركود فريد. برجاء المحاولة مرة أخرى.");
    }

    public async Task<string> GenerateUniqueInternalCodeAsync(CancellationToken cancellationToken = default)
    {
        var nextId = await dbContext.Products.CountAsync(cancellationToken) + 1;

        for (var attempt = 0; attempt < 200; attempt++)
        {
            var candidate = $"PRD-{nextId + attempt:000000}";
            if (!await dbContext.Products.AnyAsync(x => x.InternalCode == candidate, cancellationToken))
            {
                return candidate;
            }
        }

        throw new InvalidOperationException("تعذر إنشاء كود داخلي فريد. برجاء المحاولة مرة أخرى.");
    }
}
