namespace Qashira.Application.DTOs;

public sealed record SystemSettingsDto(
    string StoreName,
    string Currency,
    int DefaultLowStockThreshold,
    bool AllowNegativeStock,
    bool DiscountsEnabled);
