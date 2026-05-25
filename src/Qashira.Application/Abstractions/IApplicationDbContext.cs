using Qashira.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Qashira.Application.Abstractions;

public interface IApplicationDbContext
{
    DbSet<User> Users { get; }
    DbSet<Role> Roles { get; }
    DbSet<Permission> Permissions { get; }
    DbSet<RolePermission> RolePermissions { get; }
    DbSet<UserPermission> UserPermissions { get; }
    DbSet<Category> Categories { get; }
    DbSet<Product> Products { get; }
    DbSet<PrintingServiceTemplate> PrintingServiceTemplates { get; }
    DbSet<PrintingMaterialConsumption> PrintingMaterialConsumptions { get; }
    DbSet<Invoice> Invoices { get; }
    DbSet<InvoiceItem> InvoiceItems { get; }
    DbSet<SuspendedInvoice> SuspendedInvoices { get; }
    DbSet<SuspendedInvoiceItem> SuspendedInvoiceItems { get; }
    DbSet<Payment> Payments { get; }
    DbSet<PrintOrder> PrintOrders { get; }
    DbSet<StockMovement> StockMovements { get; }
    DbSet<Notification> Notifications { get; }
    DbSet<Return> Returns { get; }
    DbSet<ReturnItem> ReturnItems { get; }
    DbSet<Shift> Shifts { get; }
    DbSet<AuditLog> AuditLogs { get; }
    DbSet<AppSetting> AppSettings { get; }
    DbSet<ErrorLog> ErrorLogs { get; }
    DatabaseFacade Database { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
