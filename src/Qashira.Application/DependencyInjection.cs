using Qashira.Application.Abstractions;
using Qashira.Application.Security;
using Qashira.Application.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Qashira.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddSingleton<ICurrentUserSession, CurrentUserSession>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IAuditService, AuditService>();
        services.AddScoped<IAuditLogQueryService, AuditLogQueryService>();
        services.AddScoped<IPermissionService, PermissionService>();
        services.AddScoped<IPOSService, POSService>();
        services.AddScoped<IProductLookupService, ProductLookupService>();
        services.AddScoped<IProductManagementService, ProductManagementService>();
        services.AddScoped<ICategoryManagementService, CategoryManagementService>();
        services.AddScoped<IProductImportExportService, ProductImportExportService>();
        services.AddScoped<IBarcodeService, BarcodeService>();
        services.AddScoped<IShiftService, ShiftService>();
        services.AddScoped<IInventoryService, InventoryService>();
        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped<IReturnService, ReturnService>();
        services.AddScoped<IReportService, ReportService>();
        services.AddScoped<IInvoiceHistoryService, InvoiceHistoryService>();
        services.AddScoped<IReceiptService, ReceiptService>();
        services.AddScoped<IPrinterSettingsService, PrinterSettingsService>();
        services.AddScoped<IPrintingServiceTemplateService, PrintingServiceTemplateService>();
        services.AddScoped<ISystemSettingsService, SystemSettingsService>();
        services.AddScoped<IBackupService, BackupService>();
        services.AddScoped<IAutomaticBackupService, AutomaticBackupService>();
        services.AddScoped<ILogExportService, LogExportService>();
        services.AddScoped<IUserManagementService, UserManagementService>();
        return services;
    }
}
