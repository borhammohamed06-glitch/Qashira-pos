namespace Qashira.App.ViewModels;

public sealed class PrintingMaterialConsumptionViewModel(
    int productId,
    string productName,
    string barcode,
    decimal currentStockQuantity,
    decimal purchasePrice,
    decimal quantityPerUnit,
    string? notes = null) : ViewModelBase
{
    private decimal _quantityPerUnit = quantityPerUnit;
    private string _notes = notes ?? string.Empty;

    public int ProductId { get; } = productId;
    public string ProductName { get; } = productName;
    public string Barcode { get; } = barcode;
    public decimal CurrentStockQuantity { get; } = currentStockQuantity;
    public decimal PurchasePrice { get; } = purchasePrice;

    public decimal QuantityPerUnit
    {
        get => _quantityPerUnit;
        set => SetProperty(ref _quantityPerUnit, value);
    }

    public string Notes
    {
        get => _notes;
        set => SetProperty(ref _notes, value);
    }

    public string DisplayStockText => $"{CurrentStockQuantity:0.###}";
}
