using Qashira.Domain.Common;
using Qashira.Domain.Enums;

namespace Qashira.Domain.Entities;

public sealed class SuspendedInvoice : Entity
{
    public string HoldNumber { get; set; } = string.Empty;
    public int CashierId { get; set; }
    public User Cashier { get; set; } = null!;
    public int ShiftId { get; set; }
    public Shift Shift { get; set; } = null!;
    public decimal TotalAmount { get; set; }
    public decimal DiscountAmount { get; set; }
    public SuspendedInvoiceStatus Status { get; set; } = SuspendedInvoiceStatus.Active;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? UpdatedAt { get; set; }
    public ICollection<SuspendedInvoiceItem> Items { get; set; } = new List<SuspendedInvoiceItem>();
}

public sealed class SuspendedInvoiceItem : Entity
{
    public int SuspendedInvoiceId { get; set; }
    public SuspendedInvoice SuspendedInvoice { get; set; } = null!;
    public int? ProductId { get; set; }
    public Product? Product { get; set; }
    public int? PrintingServiceTemplateId { get; set; }
    public PrintingServiceTemplate? PrintingServiceTemplate { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public string Barcode { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TotalPrice { get; set; }
    public ItemType ItemType { get; set; }
}
