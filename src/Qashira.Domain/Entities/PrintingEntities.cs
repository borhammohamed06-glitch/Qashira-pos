using Qashira.Domain.Common;
using Qashira.Domain.Enums;

namespace Qashira.Domain.Entities;

public sealed class PrintingServiceTemplate : Entity
{
    public string ServiceName { get; set; } = string.Empty;
    public string SearchName { get; set; } = string.Empty;
    public PrintingServiceType ServiceType { get; set; } = PrintingServiceType.Other;
    public string UnitName { get; set; } = "صفحة";
    public decimal SellingPricePerUnit { get; set; }
    public bool UsesPaper { get; set; }
    public decimal PaperConsumptionPerUnit { get; set; }
    public bool UsesInk { get; set; }
    public InkCostMode InkCostMode { get; set; } = InkCostMode.None;
    public decimal EstimatedInkCostPerUnit { get; set; }
    public bool ShowInCashier { get; set; } = true;
    public bool IsActive { get; set; } = true;
    public string? ShortcutKey { get; set; }
    public string? Notes { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? UpdatedAt { get; set; }
    public ICollection<PrintingMaterialConsumption> MaterialConsumptions { get; set; } = new List<PrintingMaterialConsumption>();
}

public sealed class PrintingMaterialConsumption : Entity
{
    public int PrintingServiceTemplateId { get; set; }
    public PrintingServiceTemplate PrintingServiceTemplate { get; set; } = null!;
    public int ProductId { get; set; }
    public Product Product { get; set; } = null!;
    public decimal QuantityPerUnit { get; set; }
    public string? Notes { get; set; }
}
