using Qashira.Application.Abstractions;
using Qashira.Application.Permissions;
using Qashira.Domain.Entities;
using Qashira.Domain.Enums;
using Qashira.Shared.Arabic;
using Qashira.Shared.Constants;
using Microsoft.EntityFrameworkCore;

namespace Qashira.Infrastructure.Database;

public sealed class DatabaseInitializer(QashiraDbContext dbContext, IPasswordHasher passwordHasher)
{
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        AppPaths.EnsureDataDirectories();
        var dataSource = dbContext.Database.GetConnectionString()?.Replace("Data Source=", string.Empty, StringComparison.OrdinalIgnoreCase);
        var databaseDirectory = string.IsNullOrWhiteSpace(dataSource)
            ? null
            : Path.GetDirectoryName(dataSource);

        if (!string.IsNullOrWhiteSpace(databaseDirectory))
        {
            Directory.CreateDirectory(databaseDirectory);
        }

        await dbContext.Database.MigrateAsync(cancellationToken);
        await SeedSecurityAsync(cancellationToken);
        await SeedSettingsAsync(cancellationToken);
        await NormalizeExistingSearchNamesAsync(cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task SeedSecurityAsync(CancellationToken cancellationToken)
    {
        await UpsertRoleAsync(1, "Admin", "مدير النظام", cancellationToken);
        await UpsertRoleAsync(2, "Manager", "مدير الفرع", cancellationToken);
        await UpsertRoleAsync(3, "Cashier", "كاشير", cancellationToken);

        foreach (var (code, index) in PermissionCodes.All.Select((code, index) => (code, index + 1)))
        {
            var permission = await dbContext.Permissions.SingleOrDefaultAsync(x => x.Code == code, cancellationToken);
            if (permission is null)
            {
                dbContext.Permissions.Add(new Permission { Id = index, Code = code, Name = PermissionArabicName(code) });
            }
            else
            {
                permission.Name = PermissionArabicName(code);
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await EnsureRolePermissionsAsync(1, PermissionCodes.All, cancellationToken);
        await EnsureRolePermissionsAsync(2, ManagerPermissions, cancellationToken);
        await EnsureRolePermissionsAsync(3, CashierPermissions, cancellationToken);

        var adminUser = await dbContext.Users.SingleOrDefaultAsync(x => x.Username == "admin", cancellationToken);
        if (adminUser is null)
        {
            var hash = passwordHasher.HashPassword("Admin@123", out var salt);
            dbContext.Users.Add(new User
            {
                FullName = "مدير النظام",
                Username = "admin",
                PasswordHash = hash,
                PasswordSalt = salt,
                RoleId = 1,
                MustChangePassword = true,
                CreatedAt = DateTimeOffset.UtcNow
            });
        }
        else if (passwordHasher.Verify("Admin@123", adminUser.PasswordHash))
        {
            adminUser.MustChangePassword = true;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await EnsureExistingUserPermissionsAsync(cancellationToken);

        if (!await dbContext.Categories.AnyAsync(cancellationToken))
        {
            dbContext.Categories.Add(new Category
            {
                Name = "أدوات مكتبية",
                SearchName = ArabicTextNormalizer.NormalizeForSearch("أدوات مكتبية"),
                CreatedAt = DateTimeOffset.UtcNow
            });
        }
    }

    private async Task UpsertRoleAsync(int id, string name, string displayName, CancellationToken cancellationToken)
    {
        var role = await dbContext.Roles.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (role is null)
        {
            dbContext.Roles.Add(new Role { Id = id, Name = name, DisplayName = displayName });
            return;
        }

        role.Name = name;
        role.DisplayName = displayName;
    }

    private async Task EnsureRolePermissionsAsync(int roleId, IEnumerable<string> permissionCodes, CancellationToken cancellationToken)
    {
        var permissionIds = await dbContext.Permissions
            .Where(x => permissionCodes.Contains(x.Code))
            .Select(x => x.Id)
            .ToArrayAsync(cancellationToken);

        foreach (var permissionId in permissionIds)
        {
            if (!await dbContext.RolePermissions.AnyAsync(x => x.RoleId == roleId && x.PermissionId == permissionId, cancellationToken))
            {
                dbContext.RolePermissions.Add(new RolePermission { RoleId = roleId, PermissionId = permissionId });
            }
        }
    }

    private async Task EnsureExistingUserPermissionsAsync(CancellationToken cancellationToken)
    {
        var users = await dbContext.Users
            .Where(x => !x.UserPermissions.Any())
            .Select(x => new { x.Id, x.RoleId })
            .ToArrayAsync(cancellationToken);

        foreach (var user in users)
        {
            var rolePermissionIds = await dbContext.RolePermissions
                .Where(x => x.RoleId == user.RoleId)
                .Select(x => x.PermissionId)
                .ToArrayAsync(cancellationToken);

            foreach (var permissionId in rolePermissionIds)
            {
                dbContext.UserPermissions.Add(new UserPermission
                {
                    UserId = user.Id,
                    PermissionId = permissionId
                });
            }
        }
    }

    private async Task SeedSettingsAsync(CancellationToken cancellationToken)
    {
        var defaults = new Dictionary<string, string>
        {
            ["StoreName"] = "مكتبة",
            ["Currency"] = "ج.م",
            ["Language"] = "ar-EG",
            ["FlowDirection"] = "RightToLeft",
            ["Theme"] = "Light",
            ["DefaultLowStockThreshold"] = "3",
            ["AllowNegativeStock"] = "false",
            ["DiscountsEnabled"] = "true",
            ["ReceiptHeader"] = "فاتورة بيع",
            ["ReceiptFooter"] = "شكراً لزيارتكم",
            ["ReceiptPrinterName"] = string.Empty,
            ["ReceiptPaperWidth"] = "80mm",
            ["LabelPrinterName"] = string.Empty,
            ["BarcodeLabelSize"] = "38x50 mm",
            ["BarcodePrinterProfile"] = "Auto",
            ["BarcodeLabelGapMm"] = "2",
            ["BarcodeHorizontalOffsetMm"] = "0",
            ["BarcodeVerticalOffsetMm"] = "0"
        };

        foreach (var setting in defaults)
        {
            if (!await dbContext.AppSettings.AnyAsync(x => x.Key == setting.Key, cancellationToken))
            {
                dbContext.AppSettings.Add(new AppSetting { Key = setting.Key, Value = setting.Value });
            }
        }
    }

    private async Task NormalizeExistingSearchNamesAsync(CancellationToken cancellationToken)
    {
        var products = await dbContext.Products.ToListAsync(cancellationToken);
        foreach (var product in products)
        {
            var normalized = ArabicTextNormalizer.NormalizeForSearch(product.Name);
            if (product.SearchName != normalized)
            {
                product.SearchName = normalized;
                product.UpdatedAt = DateTimeOffset.UtcNow;
            }
        }

        var categories = await dbContext.Categories.ToListAsync(cancellationToken);
        foreach (var category in categories)
        {
            var normalized = ArabicTextNormalizer.NormalizeForSearch(category.Name);
            if (category.SearchName != normalized)
            {
                category.SearchName = normalized;
            }
        }
    }


    private static readonly string[] ManagerPermissions =
    [
        PermissionCodes.CanUsePOS,
        PermissionCodes.CanApplyDiscount,
        PermissionCodes.CanReturnInvoice,
        PermissionCodes.CanEditProduct,
        PermissionCodes.CanDeleteProduct,
        PermissionCodes.CanEditPrice,
        PermissionCodes.CanManageStock,
        PermissionCodes.CanViewReports,
        PermissionCodes.CanManageSettings,
        PermissionCodes.CanBackupRestore,
        PermissionCodes.CanCloseShift,
        PermissionCodes.CanChangePrinterSettings,
        PermissionCodes.CanViewAuditLogs
    ];

    private static readonly string[] CashierPermissions =
    [
        PermissionCodes.CanUsePOS,
        PermissionCodes.CanApplyDiscount,
        PermissionCodes.CanReturnInvoice,
        PermissionCodes.CanCloseShift
    ];

    private static string PermissionArabicName(string code) => code switch
    {
        PermissionCodes.CanUsePOS => "استخدام الكاشير",
        PermissionCodes.CanApplyDiscount => "تطبيق خصم",
        PermissionCodes.CanReturnInvoice => "إرجاع فاتورة",
        PermissionCodes.CanEditProduct => "تعديل المنتجات",
        PermissionCodes.CanDeleteProduct => "حذف المنتجات",
        PermissionCodes.CanEditPrice => "تعديل الأسعار",
        PermissionCodes.CanManageStock => "إدارة المخزون",
        PermissionCodes.CanViewReports => "عرض التقارير",
        PermissionCodes.CanManageUsers => "إدارة المستخدمين",
        PermissionCodes.CanManageSettings => "إدارة الإعدادات",
        PermissionCodes.CanBackupRestore => "النسخ الاحتياطي والاسترجاع",
        PermissionCodes.CanCloseShift => "إغلاق الشيفت",
        PermissionCodes.CanChangePrinterSettings => "تغيير إعدادات الطابعات",
        PermissionCodes.CanViewAuditLogs => "عرض سجل التدقيق",
        _ => code
    };
}
