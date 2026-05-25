using Qashira.Domain.Common;
using Qashira.Domain.Enums;

namespace Qashira.Domain.Entities;

public sealed class Invoice : Entity
{
    public string InvoiceNumber { get; set; } = string.Empty;
    public int CashierId { get; set; }
    public User Cashier { get; set; } = null!;
    public int ShiftId { get; set; }
    public Shift Shift { get; set; } = null!;
    public decimal TotalAmount { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal NetAmount { get; set; }
    public PaymentMethod PaymentMethod { get; set; }
    public InvoiceStatus Status { get; set; } = InvoiceStatus.Completed;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public ICollection<InvoiceItem> Items { get; set; } = new List<InvoiceItem>();
    public ICollection<Payment> Payments { get; set; } = new List<Payment>();
}

public sealed class InvoiceItem : Entity
{
    public int InvoiceId { get; set; }
    public Invoice Invoice { get; set; } = null!;
    public int? ProductId { get; set; }
    public Product? Product { get; set; }
    public int? PrintingServiceTemplateId { get; set; }
    public PrintingServiceTemplate? PrintingServiceTemplate { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal UnitCost { get; set; }
    public decimal TotalPrice { get; set; }
    public decimal TotalCost { get; set; }
    public ItemType ItemType { get; set; }
}

public sealed class Payment : Entity
{
    public int InvoiceId { get; set; }
    public Invoice Invoice { get; set; } = null!;
    public PaymentMethod Method { get; set; }
    public decimal Amount { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class PrintOrder : Entity
{
    public int? InvoiceId { get; set; }
    public Invoice? Invoice { get; set; }
    public int PagesCount { get; set; }
    public int CopiesCount { get; set; }
    public PrintType PrintType { get; set; }
    public decimal PricePerPage { get; set; }
    public decimal TotalPrice { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
