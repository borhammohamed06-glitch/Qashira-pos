using Qashira.Application.Abstractions;
using Qashira.Domain.Entities;
using Qashira.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Qashira.Infrastructure.Database;

public sealed class QashiraDbContext(DbContextOptions<QashiraDbContext> options)
    : DbContext(options), IApplicationDbContext
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<UserPermission> UserPermissions => Set<UserPermission>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<PrintingServiceTemplate> PrintingServiceTemplates => Set<PrintingServiceTemplate>();
    public DbSet<PrintingMaterialConsumption> PrintingMaterialConsumptions => Set<PrintingMaterialConsumption>();
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<InvoiceItem> InvoiceItems => Set<InvoiceItem>();
    public DbSet<SuspendedInvoice> SuspendedInvoices => Set<SuspendedInvoice>();
    public DbSet<SuspendedInvoiceItem> SuspendedInvoiceItems => Set<SuspendedInvoiceItem>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<PrintOrder> PrintOrders => Set<PrintOrder>();
    public DbSet<StockMovement> StockMovements => Set<StockMovement>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<Return> Returns => Set<Return>();
    public DbSet<ReturnItem> ReturnItems => Set<ReturnItem>();
    public DbSet<Shift> Shifts => Set<Shift>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<AppSetting> AppSettings => Set<AppSetting>();
    public DbSet<ErrorLog> ErrorLogs => Set<ErrorLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasIndex(x => x.Username).IsUnique();
            entity.Property(x => x.FullName).HasMaxLength(160).IsRequired();
            entity.Property(x => x.Username).HasMaxLength(80).IsRequired();
            entity.Property(x => x.PasswordHash).HasMaxLength(255).IsRequired();
            entity.Property(x => x.PasswordSalt).HasMaxLength(120).IsRequired();
            entity.Property(x => x.MustChangePassword).HasDefaultValue(false);
            entity.HasOne(x => x.Role).WithMany(x => x.Users).HasForeignKey(x => x.RoleId);
        });

        modelBuilder.Entity<Role>(entity =>
        {
            entity.HasIndex(x => x.Name).IsUnique();
            entity.Property(x => x.Name).HasMaxLength(80).IsRequired();
            entity.Property(x => x.DisplayName).HasMaxLength(120).IsRequired();
        });

        modelBuilder.Entity<Permission>(entity =>
        {
            entity.HasIndex(x => x.Code).IsUnique();
            entity.Property(x => x.Code).HasMaxLength(120).IsRequired();
            entity.Property(x => x.Name).HasMaxLength(140).IsRequired();
        });

        modelBuilder.Entity<RolePermission>(entity =>
        {
            entity.HasKey(x => new { x.RoleId, x.PermissionId });
            entity.HasOne(x => x.Role).WithMany(x => x.RolePermissions).HasForeignKey(x => x.RoleId);
            entity.HasOne(x => x.Permission).WithMany(x => x.RolePermissions).HasForeignKey(x => x.PermissionId);
        });

        modelBuilder.Entity<UserPermission>(entity =>
        {
            entity.HasKey(x => new { x.UserId, x.PermissionId });
            entity.HasOne(x => x.User).WithMany(x => x.UserPermissions).HasForeignKey(x => x.UserId);
            entity.HasOne(x => x.Permission).WithMany().HasForeignKey(x => x.PermissionId);
        });

        modelBuilder.Entity<Category>(entity =>
        {
            entity.HasIndex(x => x.Name).IsUnique();
            entity.HasIndex(x => x.SearchName);
            entity.Property(x => x.Name).HasMaxLength(180).IsRequired();
            entity.Property(x => x.SearchName).HasMaxLength(180).IsRequired();
            entity.Property(x => x.MeasurementUnit).HasConversion<string>().HasMaxLength(40);
        });

        modelBuilder.Entity<Product>(entity =>
        {
            entity.HasIndex(x => x.Name).IsUnique();
            entity.HasIndex(x => x.SearchName);
            entity.HasIndex(x => x.Barcode).IsUnique();
            entity.HasIndex(x => x.InternalCode).IsUnique();
            entity.Property(x => x.Name).HasMaxLength(220).IsRequired();
            entity.Property(x => x.SearchName).HasMaxLength(220).IsRequired();
            entity.Property(x => x.InternalCode).HasMaxLength(80).IsRequired();
            entity.Property(x => x.Barcode).HasMaxLength(80).IsRequired();
            entity.Property(x => x.PurchasePrice).HasPrecision(18, 2);
            entity.Property(x => x.SalePrice).HasPrecision(18, 2);
            entity.Property(x => x.StockQuantity).HasPrecision(18, 3);
            entity.Property(x => x.ProductType)
                .HasConversion<string>()
                .HasMaxLength(40)
                .HasDefaultValue(ProductType.NormalProduct);
            entity.HasOne(x => x.Category).WithMany(x => x.Products).HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<PrintingServiceTemplate>(entity =>
        {
            entity.HasIndex(x => x.SearchName).IsUnique();
            entity.HasIndex(x => new { x.IsActive, x.ShowInCashier });
            entity.Property(x => x.ServiceName).HasMaxLength(180).IsRequired();
            entity.Property(x => x.SearchName).HasMaxLength(180).IsRequired();
            entity.Property(x => x.ServiceType).HasConversion<string>().HasMaxLength(60);
            entity.Property(x => x.UnitName).HasMaxLength(60).IsRequired();
            entity.Property(x => x.SellingPricePerUnit).HasPrecision(18, 2);
            entity.Property(x => x.PaperConsumptionPerUnit).HasPrecision(18, 3);
            entity.Property(x => x.InkCostMode).HasConversion<string>().HasMaxLength(60);
            entity.Property(x => x.EstimatedInkCostPerUnit).HasPrecision(18, 2);
            entity.Property(x => x.ShortcutKey).HasMaxLength(40);
            entity.Property(x => x.Notes).HasMaxLength(800);
        });

        modelBuilder.Entity<PrintingMaterialConsumption>(entity =>
        {
            entity.HasIndex(x => new { x.PrintingServiceTemplateId, x.ProductId }).IsUnique();
            entity.Property(x => x.QuantityPerUnit).HasPrecision(18, 3);
            entity.Property(x => x.Notes).HasMaxLength(400);
            entity.HasOne(x => x.PrintingServiceTemplate)
                .WithMany(x => x.MaterialConsumptions)
                .HasForeignKey(x => x.PrintingServiceTemplateId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Product)
                .WithMany(x => x.PrintingMaterialConsumptions)
                .HasForeignKey(x => x.ProductId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Invoice>(entity =>
        {
            entity.HasIndex(x => x.InvoiceNumber).IsUnique();
            entity.HasIndex(x => x.CreatedAt);
            entity.Property(x => x.InvoiceNumber).HasMaxLength(80).IsRequired();
            entity.Property(x => x.TotalAmount).HasPrecision(18, 2);
            entity.Property(x => x.DiscountAmount).HasPrecision(18, 2);
            entity.Property(x => x.NetAmount).HasPrecision(18, 2);
            entity.Property(x => x.PaymentMethod).HasConversion<string>().HasMaxLength(40);
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(40);
            entity.HasOne(x => x.Cashier).WithMany().HasForeignKey(x => x.CashierId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Shift).WithMany(x => x.Invoices).HasForeignKey(x => x.ShiftId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<InvoiceItem>(entity =>
        {
            entity.Property(x => x.ItemName).HasMaxLength(220).IsRequired();
            entity.Property(x => x.Quantity).HasPrecision(18, 3);
            entity.Property(x => x.UnitPrice).HasPrecision(18, 2);
            entity.Property(x => x.UnitCost).HasPrecision(18, 2);
            entity.Property(x => x.TotalPrice).HasPrecision(18, 2);
            entity.Property(x => x.TotalCost).HasPrecision(18, 2);
            entity.Property(x => x.ItemType).HasConversion<string>().HasMaxLength(40);
            entity.HasOne(x => x.Invoice).WithMany(x => x.Items).HasForeignKey(x => x.InvoiceId);
            entity.HasOne(x => x.Product).WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(x => x.PrintingServiceTemplate)
                .WithMany()
                .HasForeignKey(x => x.PrintingServiceTemplateId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<SuspendedInvoice>(entity =>
        {
            entity.HasIndex(x => x.HoldNumber).IsUnique();
            entity.HasIndex(x => new { x.CashierId, x.Status });
            entity.HasIndex(x => new { x.ShiftId, x.Status });
            entity.Property(x => x.HoldNumber).HasMaxLength(80).IsRequired();
            entity.Property(x => x.TotalAmount).HasPrecision(18, 2);
            entity.Property(x => x.DiscountAmount).HasPrecision(18, 2);
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(40);
            entity.HasOne(x => x.Cashier).WithMany().HasForeignKey(x => x.CashierId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Shift).WithMany().HasForeignKey(x => x.ShiftId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<SuspendedInvoiceItem>(entity =>
        {
            entity.Property(x => x.ItemName).HasMaxLength(220).IsRequired();
            entity.Property(x => x.Barcode).HasMaxLength(80);
            entity.Property(x => x.Quantity).HasPrecision(18, 3);
            entity.Property(x => x.UnitPrice).HasPrecision(18, 2);
            entity.Property(x => x.TotalPrice).HasPrecision(18, 2);
            entity.Property(x => x.ItemType).HasConversion<string>().HasMaxLength(40);
            entity.HasOne(x => x.SuspendedInvoice).WithMany(x => x.Items).HasForeignKey(x => x.SuspendedInvoiceId);
            entity.HasOne(x => x.Product).WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(x => x.PrintingServiceTemplate)
                .WithMany()
                .HasForeignKey(x => x.PrintingServiceTemplateId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<Payment>(entity =>
        {
            entity.Property(x => x.Method).HasConversion<string>().HasMaxLength(40);
            entity.Property(x => x.Amount).HasPrecision(18, 2);
            entity.HasOne(x => x.Invoice).WithMany(x => x.Payments).HasForeignKey(x => x.InvoiceId);
        });

        modelBuilder.Entity<PrintOrder>(entity =>
        {
            entity.Property(x => x.PrintType).HasConversion<string>().HasMaxLength(40);
            entity.Property(x => x.PricePerPage).HasPrecision(18, 2);
            entity.Property(x => x.TotalPrice).HasPrecision(18, 2);
            entity.HasOne(x => x.Invoice).WithMany().HasForeignKey(x => x.InvoiceId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<StockMovement>(entity =>
        {
            entity.HasIndex(x => x.ProductId);
            entity.Property(x => x.MovementType).HasConversion<string>().HasMaxLength(40);
            entity.Property(x => x.Quantity).HasPrecision(18, 3);
            entity.Property(x => x.OldQuantity).HasPrecision(18, 3);
            entity.Property(x => x.NewQuantity).HasPrecision(18, 3);
            entity.Property(x => x.ReferenceType).HasMaxLength(80);
            entity.HasOne(x => x.Product).WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Notification>(entity =>
        {
            entity.HasIndex(x => x.ProductId);
            entity.HasIndex(x => new { x.ProductId, x.Type, x.IsResolved });
            entity.Property(x => x.Type).HasConversion<string>().HasMaxLength(40);
            entity.Property(x => x.Title).HasMaxLength(140).IsRequired();
            entity.Property(x => x.Message).HasMaxLength(500).IsRequired();
            entity.Property(x => x.CurrentQuantity).HasPrecision(18, 3);
            entity.HasOne(x => x.Product).WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Return>(entity =>
        {
            entity.HasIndex(x => x.ShiftId);
            entity.Property(x => x.TotalReturnedAmount).HasPrecision(18, 2);
            entity.HasOne(x => x.Invoice).WithMany().HasForeignKey(x => x.InvoiceId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Shift).WithMany().HasForeignKey(x => x.ShiftId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ReturnItem>(entity =>
        {
            entity.Property(x => x.Quantity).HasPrecision(18, 3);
            entity.Property(x => x.UnitPrice).HasPrecision(18, 2);
            entity.Property(x => x.TotalPrice).HasPrecision(18, 2);
            entity.HasOne(x => x.Return).WithMany(x => x.Items).HasForeignKey(x => x.ReturnId);
            entity.HasOne(x => x.InvoiceItem).WithMany().HasForeignKey(x => x.InvoiceItemId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Product).WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<Shift>(entity =>
        {
            entity.HasIndex(x => new { x.CashierId, x.Status });
            entity.Property(x => x.OpeningCash).HasPrecision(18, 2);
            entity.Property(x => x.ClosingCash).HasPrecision(18, 2);
            entity.Property(x => x.ExpectedCash).HasPrecision(18, 2);
            entity.Property(x => x.Difference).HasPrecision(18, 2);
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(40);
            entity.HasOne(x => x.Cashier).WithMany().HasForeignKey(x => x.CashierId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.HasIndex(x => x.CreatedAt);
            entity.Property(x => x.Action).HasConversion<string>().HasMaxLength(80);
            entity.Property(x => x.Description).HasMaxLength(1000).IsRequired();
        });

        modelBuilder.Entity<AppSetting>(entity =>
        {
            entity.HasKey(x => x.Key);
            entity.Property(x => x.Key).HasMaxLength(160);
            entity.Property(x => x.Value).IsRequired();
        });

        modelBuilder.Entity<ErrorLog>(entity =>
        {
            entity.Property(x => x.Level).HasMaxLength(40).IsRequired();
            entity.Property(x => x.Message).HasMaxLength(2000).IsRequired();
        });
    }
}
