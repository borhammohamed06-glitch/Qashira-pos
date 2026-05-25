namespace Qashira.Application.Permissions;

public static class PermissionCodes
{
    public const string CanUsePOS = nameof(CanUsePOS);
    public const string CanApplyDiscount = nameof(CanApplyDiscount);
    public const string CanReturnInvoice = nameof(CanReturnInvoice);
    public const string CanEditProduct = nameof(CanEditProduct);
    public const string CanDeleteProduct = nameof(CanDeleteProduct);
    public const string CanEditPrice = nameof(CanEditPrice);
    public const string CanManageStock = nameof(CanManageStock);
    public const string CanViewReports = nameof(CanViewReports);
    public const string CanManageUsers = nameof(CanManageUsers);
    public const string CanManageSettings = nameof(CanManageSettings);
    public const string CanBackupRestore = nameof(CanBackupRestore);
    public const string CanCloseShift = nameof(CanCloseShift);
    public const string CanChangePrinterSettings = nameof(CanChangePrinterSettings);
    public const string CanViewAuditLogs = nameof(CanViewAuditLogs);

    public static readonly string[] All =
    [
        CanUsePOS,
        CanApplyDiscount,
        CanReturnInvoice,
        CanEditProduct,
        CanDeleteProduct,
        CanEditPrice,
        CanManageStock,
        CanViewReports,
        CanManageUsers,
        CanManageSettings,
        CanBackupRestore,
        CanCloseShift,
        CanChangePrinterSettings,
        CanViewAuditLogs
    ];
}
