using Qashira.Domain.Common;
using Qashira.Domain.Enums;

namespace Qashira.Domain.Entities;

public sealed class StockMovement : Entity
{
    public int ProductId { get; set; }
    public Product Product { get; set; } = null!;
    public StockMovementType MovementType { get; set; }
    public decimal Quantity { get; set; }
    public decimal OldQuantity { get; set; }
    public decimal NewQuantity { get; set; }
    public string? ReferenceType { get; set; }
    public int? ReferenceId { get; set; }
    public int UserId { get; set; }
    public User User { get; set; } = null!;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class Notification : Entity
{
    public int ProductId { get; set; }
    public Product Product { get; set; } = null!;
    public NotificationType Type { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public decimal CurrentQuantity { get; set; }
    public int Threshold { get; set; }
    public bool IsResolved { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? UpdatedAt { get; set; }
}
