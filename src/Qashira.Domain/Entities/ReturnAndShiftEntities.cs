using Qashira.Domain.Common;
using Qashira.Domain.Enums;

namespace Qashira.Domain.Entities;

public sealed class Return : Entity
{
    public int InvoiceId { get; set; }
    public Invoice Invoice { get; set; } = null!;
    public int UserId { get; set; }
    public User User { get; set; } = null!;
    public int ShiftId { get; set; }
    public Shift Shift { get; set; } = null!;
    public decimal TotalReturnedAmount { get; set; }
    public string? Reason { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public ICollection<ReturnItem> Items { get; set; } = new List<ReturnItem>();
}

public sealed class ReturnItem : Entity
{
    public int ReturnId { get; set; }
    public Return Return { get; set; } = null!;
    public int InvoiceItemId { get; set; }
    public InvoiceItem InvoiceItem { get; set; } = null!;
    public int? ProductId { get; set; }
    public Product? Product { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TotalPrice { get; set; }
}

public sealed class Shift : Entity
{
    public int CashierId { get; set; }
    public User Cashier { get; set; } = null!;
    public decimal OpeningCash { get; set; }
    public decimal? ClosingCash { get; set; }
    public decimal? ExpectedCash { get; set; }
    public decimal? Difference { get; set; }
    public ShiftStatus Status { get; set; } = ShiftStatus.Open;
    public DateTimeOffset OpenedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ClosedAt { get; set; }
    public ICollection<Invoice> Invoices { get; set; } = new List<Invoice>();
}
