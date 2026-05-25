namespace Qashira.Application.DTOs;

public sealed record ProductLookupDto(
    int Id,
    string Name,
    string Barcode,
    decimal SalePrice,
    decimal StockQuantity);
