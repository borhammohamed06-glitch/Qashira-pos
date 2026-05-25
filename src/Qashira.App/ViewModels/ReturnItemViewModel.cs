using Qashira.Application.DTOs;

namespace Qashira.App.ViewModels;

public sealed class ReturnItemViewModel : ViewModelBase
{
    private decimal _returnQuantity;

    public ReturnItemViewModel(InvoiceItemForReturnDto item)
    {
        InvoiceItemId = item.InvoiceItemId;
        ProductId = item.ProductId;
        ItemName = item.ItemName;
        SoldQuantity = item.SoldQuantity;
        AlreadyReturnedQuantity = item.AlreadyReturnedQuantity;
        ReturnableQuantity = item.ReturnableQuantity;
        UnitPrice = item.UnitPrice;
    }

    public int InvoiceItemId { get; }
    public int? ProductId { get; }
    public string ItemName { get; }
    public decimal SoldQuantity { get; }
    public decimal AlreadyReturnedQuantity { get; }
    public decimal ReturnableQuantity { get; }
    public decimal UnitPrice { get; }

    public decimal ReturnQuantity
    {
        get => _returnQuantity;
        set
        {
            if (value < 0)
            {
                value = 0;
            }

            if (value > ReturnableQuantity)
            {
                value = ReturnableQuantity;
            }

            if (SetProperty(ref _returnQuantity, value))
            {
                OnPropertyChanged(nameof(ReturnTotal));
            }
        }
    }

    public decimal ReturnTotal => ReturnQuantity * UnitPrice;
}
