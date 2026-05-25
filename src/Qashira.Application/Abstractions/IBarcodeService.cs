namespace Qashira.Application.Abstractions;

public interface IBarcodeService
{
    Task<string> GenerateUniqueBarcodeAsync(CancellationToken cancellationToken = default);
    Task<string> GenerateUniqueInternalCodeAsync(CancellationToken cancellationToken = default);
}
