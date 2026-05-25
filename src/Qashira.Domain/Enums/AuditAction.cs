namespace Qashira.Domain.Enums;

public enum AuditAction
{
    Login,
    Logout,
    OpenShift,
    CloseShift,
    HoldInvoice,
    ResumeHeldInvoice,
    CancelHeldInvoice,
    CreateInvoice,
    ReturnInvoice,
    ChangeProductPrice,
    ChangeStockQuantity,
    CreateProduct,
    EditProduct,
    DeleteProduct,
    ImportProducts,
    ExportProducts,
    CreateUser,
    EditUser,
    ChangePermissions,
    BackupCreated,
    BackupImported,
    BackupExported,
    BackupDeleted,
    RestorePerformed,
    LogsExported,
    PrinterSettingsChanged,
    SettingsChanged
}
