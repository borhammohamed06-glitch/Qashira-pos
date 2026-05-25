namespace Qashira.App.ViewModels;

public sealed class CartLineViewModel : ViewModelBase
{
    private decimal _quantity;

    public CartLineViewModel(
        int? productId,
        string name,
        string barcode,
        decimal unitPrice,
        decimal quantity = 1,
        int? printingServiceTemplateId = null,
        string? unitName = null)
    {
        ProductId = productId;
        Name = name;
        Barcode = barcode;
        UnitPrice = unitPrice;
        PrintingServiceTemplateId = printingServiceTemplateId;
        UnitName = unitName;
        _quantity = quantity;
    }

    public int? ProductId { get; }
    public int? PrintingServiceTemplateId { get; }
    public string Name { get; }
    public string Barcode { get; }
    public decimal UnitPrice { get; }
    public string? UnitName { get; }

    public decimal Quantity
    {
        get => _quantity;
        set
        {
            if (value <= 0)
            {
                value = 0.01m;
            }

            if (SetProperty(ref _quantity, value))
            {
                OnPropertyChanged(nameof(Total));
            }
        }
    }

    public decimal Total => Quantity * UnitPrice;
}
