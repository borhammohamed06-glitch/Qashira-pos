using System.Text.Json;
using Qashira.Application.Abstractions;
using Qashira.Application.DTOs;
using Qashira.Application.Permissions;
using Qashira.Domain.Entities;
using Qashira.Domain.Enums;
using Qashira.Shared.Results;
using Microsoft.EntityFrameworkCore;

namespace Qashira.Application.Services;

public sealed class AuditLogQueryService(
    IApplicationDbContext dbContext,
    IPermissionService permissionService) : IAuditLogQueryService
{
    public async Task<Result<AuditLogFilterOptionsDto>> GetFilterOptionsAsync(
        int userId,
        CancellationToken cancellationToken = default)
    {
        if (!await permissionService.HasPermissionAsync(userId, PermissionCodes.CanViewAuditLogs, cancellationToken))
        {
            return Result<AuditLogFilterOptionsDto>.Failure("ليس لديك صلاحية لعرض سجل التدقيق.");
        }

        var users = await dbContext.Users
            .AsNoTracking()
            .OrderBy(x => x.FullName)
            .Select(x => new AuditUserFilterOptionDto(x.Id, x.FullName))
            .ToArrayAsync(cancellationToken);

        var userOptions = new List<AuditUserFilterOptionDto> { new(null, "كل المستخدمين") };
        userOptions.AddRange(users);

        var actionOptions = new List<AuditActionFilterOptionDto> { new(null, "كل العمليات") };
        actionOptions.AddRange(Enum.GetValues<AuditAction>()
            .Select(action => new AuditActionFilterOptionDto(action, ActionName(action))));

        return Result<AuditLogFilterOptionsDto>.Success(new AuditLogFilterOptionsDto(userOptions, actionOptions));
    }

    public async Task<Result<IReadOnlyList<AuditLogEntryDto>>> SearchAsync(
        AuditLogSearchRequest request,
        int userId,
        CancellationToken cancellationToken = default)
    {
        if (!await permissionService.HasPermissionAsync(userId, PermissionCodes.CanViewAuditLogs, cancellationToken))
        {
            return Result<IReadOnlyList<AuditLogEntryDto>>.Failure("ليس لديك صلاحية لعرض سجل التدقيق.");
        }

        var take = Math.Clamp(request.Take, 50, 1000);
        var search = request.SearchText?.Trim();

        var rows = await dbContext.AuditLogs
            .AsNoTracking()
            .Include(x => x.User)
            .OrderByDescending(x => x.Id)
            .ToListAsync(cancellationToken);

        var logs = rows
            .Where(x => x.CreatedAt >= request.From && x.CreatedAt <= request.To)
            .Where(x => !request.UserId.HasValue || x.UserId == request.UserId.Value)
            .Where(x => !request.Action.HasValue || x.Action == request.Action.Value)
            .Where(x => string.IsNullOrWhiteSpace(search) ||
                x.Description.Contains(search, StringComparison.CurrentCultureIgnoreCase) ||
                (x.EntityName?.Contains(search, StringComparison.CurrentCultureIgnoreCase) ?? false) ||
                (x.EntityId?.Contains(search, StringComparison.CurrentCultureIgnoreCase) ?? false) ||
                (x.User?.FullName.Contains(search, StringComparison.CurrentCultureIgnoreCase) ?? false) ||
                (x.User?.Username.Contains(search, StringComparison.CurrentCultureIgnoreCase) ?? false))
            .OrderByDescending(x => x.CreatedAt)
            .Take(take)
            .Select(x => new AuditLogEntryDto(
                x.Id,
                x.CreatedAt,
                x.User?.FullName ?? "النظام",
                x.Action,
                ActionName(x.Action),
                x.EntityName ?? "-",
                x.EntityId ?? "-",
                x.Description))
            .ToArray();

        return Result<IReadOnlyList<AuditLogEntryDto>>.Success(logs);
    }

    public async Task<Result<AuditOperationDetailDto>> GetDetailsAsync(
        int auditLogId,
        int userId,
        CancellationToken cancellationToken = default)
    {
        if (!await permissionService.HasPermissionAsync(userId, PermissionCodes.CanViewAuditLogs, cancellationToken))
        {
            return Result<AuditOperationDetailDto>.Failure("ليس لديك صلاحية لعرض تفاصيل سجل التدقيق.");
        }

        var log = await dbContext.AuditLogs
            .AsNoTracking()
            .Include(x => x.User)
            .SingleOrDefaultAsync(x => x.Id == auditLogId, cancellationToken);

        if (log is null)
        {
            return Result<AuditOperationDetailDto>.Failure("لم يتم العثور على العملية المحددة.");
        }

        var detail = log.Action switch
        {
            AuditAction.CreateInvoice when TryParseId(log.EntityId, out var invoiceId) =>
                await BuildInvoiceDetailAsync(log, invoiceId, cancellationToken),
            AuditAction.ReturnInvoice when TryParseId(log.EntityId, out var returnId) =>
                await BuildReturnDetailAsync(log, returnId, cancellationToken),
            AuditAction.HoldInvoice or AuditAction.ResumeHeldInvoice or AuditAction.CancelHeldInvoice
                when TryParseId(log.EntityId, out var suspendedInvoiceId) =>
                await BuildSuspendedInvoiceDetailAsync(log, suspendedInvoiceId, cancellationToken),
            AuditAction.OpenShift or AuditAction.CloseShift or AuditAction.Login or AuditAction.Logout =>
                await BuildShiftLifecycleDetailAsync(log, cancellationToken),
            _ when string.Equals(log.EntityName, nameof(Product), StringComparison.Ordinal) && TryParseId(log.EntityId, out var productId) =>
                await BuildProductDetailAsync(log, productId, cancellationToken),
            _ => await BuildDefaultDetailAsync(log, cancellationToken)
        };

        return Result<AuditOperationDetailDto>.Success(detail);
    }

    private async Task<AuditOperationDetailDto> BuildInvoiceDetailAsync(AuditLog log, int invoiceId, CancellationToken cancellationToken)
    {
        var invoice = await dbContext.Invoices
            .AsNoTracking()
            .Include(x => x.Cashier)
            .Include(x => x.Shift)
            .Include(x => x.Items)
            .SingleOrDefaultAsync(x => x.Id == invoiceId, cancellationToken);

        if (invoice is null)
        {
            return await BuildDefaultDetailAsync(log, cancellationToken);
        }

        var returns = await dbContext.Returns
            .AsNoTracking()
            .Include(x => x.Items)
            .Where(x => x.InvoiceId == invoice.Id)
            .ToListAsync(cancellationToken);
        var returnAmount = returns.Sum(x => x.TotalReturnedAmount);

        var fields = BaseFields(log);
        fields.AddRange([
            new("رقم الفاتورة", invoice.InvoiceNumber),
            new("الكاشير", invoice.Cashier.FullName),
            new("الشيفت", invoice.ShiftId.ToString()),
            new("الحالة", InvoiceStatusName(invoice.Status)),
            new("الإجمالي قبل الخصم", Money(invoice.TotalAmount)),
            new("الخصم", Money(invoice.DiscountAmount)),
            new("الصافي", Money(invoice.NetAmount)),
            new("المرتجعات", Money(returnAmount)),
            new("عدد الأصناف", invoice.Items.Sum(x => x.Quantity).ToString())
        ]);

        return new AuditOperationDetailDto(
            $"تفاصيل الفاتورة {invoice.InvoiceNumber}",
            log.Description,
            fields,
            invoice.Items
                .OrderBy(x => x.Id)
                .Select(x => new AuditDetailLineDto(
                    x.ItemName,
                    ItemTypeName(x.ItemType),
                    x.Quantity.ToString(),
                    Money(x.UnitPrice),
                    Money(x.TotalPrice),
                    Money(x.TotalPrice - x.TotalCost)))
                .ToArray(),
            await BuildRelatedTimelineAsync(log, nameof(Invoice), invoice.Id.ToString(), cancellationToken));
    }

    private async Task<AuditOperationDetailDto> BuildSuspendedInvoiceDetailAsync(
        AuditLog log,
        int suspendedInvoiceId,
        CancellationToken cancellationToken)
    {
        var suspendedInvoice = await dbContext.SuspendedInvoices
            .AsNoTracking()
            .Include(x => x.Cashier)
            .Include(x => x.Items)
            .SingleOrDefaultAsync(x => x.Id == suspendedInvoiceId, cancellationToken);

        if (suspendedInvoice is null)
        {
            return await BuildDefaultDetailAsync(log, cancellationToken);
        }

        var fields = BaseFields(log);
        AddJsonComparisonFields(fields, log.OldValuesJson, log.NewValuesJson);
        fields.AddRange([
            new("رقم التعليق", suspendedInvoice.HoldNumber),
            new("الكاشير", suspendedInvoice.Cashier.FullName),
            new("الشيفت", suspendedInvoice.ShiftId.ToString()),
            new("الحالة الحالية", SuspendedInvoiceStatusName(suspendedInvoice.Status)),
            new("الإجمالي قبل الخصم", Money(suspendedInvoice.TotalAmount)),
            new("الخصم", Money(suspendedInvoice.DiscountAmount)),
            new("الصافي", Money(suspendedInvoice.TotalAmount - suspendedInvoice.DiscountAmount)),
            new("عدد الأصناف", suspendedInvoice.Items.Sum(x => x.Quantity).ToString())
        ]);

        return new AuditOperationDetailDto(
            $"تفاصيل الفاتورة المعلقة {suspendedInvoice.HoldNumber}",
            log.Description,
            fields,
            suspendedInvoice.Items
                .OrderBy(x => x.Id)
                .Select(x => new AuditDetailLineDto(
                    x.ItemName,
                    ItemTypeName(x.ItemType),
                    x.Quantity.ToString(),
                    Money(x.UnitPrice),
                    Money(x.TotalPrice),
                    "-"))
                .ToArray(),
            await BuildRelatedTimelineAsync(log, nameof(SuspendedInvoice), suspendedInvoice.Id.ToString(), cancellationToken));
    }

    private async Task<AuditOperationDetailDto> BuildReturnDetailAsync(AuditLog log, int returnId, CancellationToken cancellationToken)
    {
        var returnEntity = await dbContext.Returns
            .AsNoTracking()
            .Include(x => x.User)
            .Include(x => x.Invoice)
            .Include(x => x.Shift)
            .Include(x => x.Items)
            .ThenInclude(x => x.InvoiceItem)
            .SingleOrDefaultAsync(x => x.Id == returnId, cancellationToken);

        if (returnEntity is null)
        {
            return await BuildDefaultDetailAsync(log, cancellationToken);
        }

        var fields = BaseFields(log);
        fields.AddRange([
            new("رقم الفاتورة", returnEntity.Invoice.InvoiceNumber),
            new("المستخدم", returnEntity.User.FullName),
            new("الشيفت", returnEntity.ShiftId.ToString()),
            new("قيمة المرتجع", Money(returnEntity.TotalReturnedAmount)),
            new("السبب", string.IsNullOrWhiteSpace(returnEntity.Reason) ? "-" : returnEntity.Reason)
        ]);

        return new AuditOperationDetailDto(
            $"تفاصيل مرتجع الفاتورة {returnEntity.Invoice.InvoiceNumber}",
            log.Description,
            fields,
            returnEntity.Items
                .OrderBy(x => x.Id)
                .Select(x => new AuditDetailLineDto(
                    x.InvoiceItem.ItemName,
                    ItemTypeName(x.InvoiceItem.ItemType),
                    x.Quantity.ToString(),
                    Money(x.UnitPrice),
                    Money(x.TotalPrice),
                    Money(x.TotalPrice - (x.InvoiceItem.UnitCost * x.Quantity))))
                .ToArray(),
            await BuildRelatedTimelineAsync(log, nameof(Return), returnEntity.Id.ToString(), cancellationToken));
    }

    private async Task<AuditOperationDetailDto> BuildProductDetailAsync(AuditLog log, int productId, CancellationToken cancellationToken)
    {
        var product = await dbContext.Products
            .AsNoTracking()
            .Include(x => x.Category)
            .SingleOrDefaultAsync(x => x.Id == productId, cancellationToken);

        var fields = BaseFields(log);
        AddJsonComparisonFields(fields, log.OldValuesJson, log.NewValuesJson);

        if (product is not null)
        {
            fields.AddRange([
                new("المنتج الحالي", product.Name),
                new("التصنيف الحالي", product.Category?.Name ?? "-"),
                new("الباركود", product.Barcode),
                new("الكود الداخلي", product.InternalCode),
                new("سعر الشراء الحالي", Money(product.PurchasePrice)),
                new("سعر البيع الحالي", Money(product.SalePrice)),
                new("المخزون الحالي", product.StockQuantity.ToString()),
                new("حد التنبيه", product.LowStockThreshold.ToString()),
                new("نشط", product.IsActive ? "نعم" : "لا")
            ]);
        }

        return new AuditOperationDetailDto(
            product is null ? ActionName(log.Action) : $"تفاصيل المنتج {product.Name}",
            log.Description,
            fields,
            [],
            await BuildRelatedTimelineAsync(log, nameof(Product), productId.ToString(), cancellationToken));
    }

    private async Task<AuditOperationDetailDto> BuildShiftLifecycleDetailAsync(AuditLog log, CancellationToken cancellationToken)
    {
        Shift? shift = null;
        if (string.Equals(log.EntityName, nameof(Shift), StringComparison.Ordinal) && TryParseId(log.EntityId, out var shiftId))
        {
            shift = await dbContext.Shifts
                .AsNoTracking()
                .SingleOrDefaultAsync(x => x.Id == shiftId, cancellationToken);
        }

        if (shift is null && log.UserId.HasValue)
        {
            var dayStart = log.CreatedAt.Date;
            var dayEnd = dayStart.AddDays(1);
            var candidateShifts = await dbContext.Shifts
                .AsNoTracking()
                .Where(x => x.CashierId == log.UserId.Value && x.OpenedAt < dayEnd && (x.ClosedAt == null || x.ClosedAt >= dayStart))
                .ToListAsync(cancellationToken);
            shift = candidateShifts
                .OrderBy(x => Math.Abs((x.OpenedAt - log.CreatedAt).TotalMinutes))
                .FirstOrDefault();
        }

        var fields = BaseFields(log);
        var timeline = await BuildUserLifecycleTimelineAsync(log, shift, cancellationToken);

        if (shift is not null)
        {
            fields.AddRange([
                new("رقم الشيفت", shift.Id.ToString()),
                new("افتتاح الشيفت", Money(shift.OpeningCash)),
                new("الحالة", shift.Status == ShiftStatus.Open ? "مفتوح" : "مغلق"),
                new("وقت الفتح", shift.OpenedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm")),
                new("وقت الإغلاق", shift.ClosedAt?.ToLocalTime().ToString("yyyy-MM-dd HH:mm") ?? "-"),
                new("المتوقع", shift.ExpectedCash.HasValue ? Money(shift.ExpectedCash.Value) : "-"),
                new("الفعلي", shift.ClosingCash.HasValue ? Money(shift.ClosingCash.Value) : "-"),
                new("الفرق", shift.Difference.HasValue ? Money(shift.Difference.Value) : "-")
            ]);
        }

        return new AuditOperationDetailDto(
            "تفاصيل دورة الدخول والشيفت",
            log.Description,
            fields,
            [],
            timeline);
    }

    private async Task<AuditOperationDetailDto> BuildDefaultDetailAsync(AuditLog log, CancellationToken cancellationToken)
    {
        var fields = BaseFields(log);
        AddJsonComparisonFields(fields, log.OldValuesJson, log.NewValuesJson);

        return new AuditOperationDetailDto(
            ActionName(log.Action),
            log.Description,
            fields,
            [],
            string.IsNullOrWhiteSpace(log.EntityName) || string.IsNullOrWhiteSpace(log.EntityId)
                ? []
                : await BuildRelatedTimelineAsync(log, log.EntityName, log.EntityId, cancellationToken));
    }

    private async Task<IReadOnlyList<AuditTimelineEntryDto>> BuildRelatedTimelineAsync(
        AuditLog log,
        string entityName,
        string entityId,
        CancellationToken cancellationToken)
    {
        var rows = await dbContext.AuditLogs
            .AsNoTracking()
            .Where(x => x.EntityName == entityName && x.EntityId == entityId)
            .ToListAsync(cancellationToken);

        return rows
            .OrderBy(x => x.CreatedAt)
            .Select(x => new AuditTimelineEntryDto(x.CreatedAt, ActionName(x.Action), x.Description))
            .ToArray();
    }

    private async Task<IReadOnlyList<AuditTimelineEntryDto>> BuildUserLifecycleTimelineAsync(
        AuditLog log,
        Shift? shift,
        CancellationToken cancellationToken)
    {
        if (!log.UserId.HasValue)
        {
            return [];
        }

        var from = shift?.OpenedAt.AddHours(-2) ?? log.CreatedAt.Date;
        var to = shift?.ClosedAt?.AddHours(2) ?? log.CreatedAt.Date.AddDays(1);
        var rows = await dbContext.AuditLogs
            .AsNoTracking()
            .Where(x => x.UserId == log.UserId.Value &&
                x.CreatedAt >= from &&
                x.CreatedAt <= to &&
                (x.Action == AuditAction.Login ||
                 x.Action == AuditAction.Logout ||
                 x.Action == AuditAction.OpenShift ||
                 x.Action == AuditAction.CloseShift))
            .ToListAsync(cancellationToken);

        return rows
            .OrderBy(x => x.CreatedAt)
            .Select(x => new AuditTimelineEntryDto(x.CreatedAt, ActionName(x.Action), x.Description))
            .ToArray();
    }

    private static List<AuditDetailFieldDto> BaseFields(AuditLog log) =>
    [
        new("التاريخ", log.CreatedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm")),
        new("المستخدم", log.User?.FullName ?? "النظام"),
        new("العملية", ActionName(log.Action)),
        new("الكيان", log.EntityName ?? "-"),
        new("رقم الكيان", log.EntityId ?? "-")
    ];

    private static void AddJsonComparisonFields(List<AuditDetailFieldDto> fields, string? oldJson, string? newJson)
    {
        var oldValues = ParseJsonValues(oldJson);
        var newValues = ParseJsonValues(newJson);
        foreach (var key in oldValues.Keys.Union(newValues.Keys).OrderBy(x => x))
        {
            oldValues.TryGetValue(key, out var oldValue);
            newValues.TryGetValue(key, out var newValue);
            fields.Add(new(key, $"قبل: {oldValue ?? "-"} | بعد: {newValue ?? "-"}"));
        }
    }

    private static Dictionary<string, string> ParseJsonValues(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            return document.RootElement.ValueKind == JsonValueKind.Object
                ? document.RootElement
                    .EnumerateObject()
                    .ToDictionary(x => x.Name, x => JsonValueToString(x.Value), StringComparer.Ordinal)
                : [];
        }
        catch
        {
            return new Dictionary<string, string> { ["بيانات مسجلة"] = json };
        }
    }

    private static string JsonValueToString(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.String => value.GetString() ?? string.Empty,
        JsonValueKind.Number => value.ToString(),
        JsonValueKind.True => "نعم",
        JsonValueKind.False => "لا",
        JsonValueKind.Null => "-",
        _ => value.ToString()
    };

    private static bool TryParseId(string? value, out int id) =>
        int.TryParse(value, out id);

    private static string Money(decimal value) =>
        $"{value:0.00} ج.م";

    private static string InvoiceStatusName(InvoiceStatus status) => status switch
    {
        InvoiceStatus.Completed => "مكتملة",
        InvoiceStatus.PartiallyReturned => "مرتجع جزئي",
        InvoiceStatus.Returned => "مرتجعة",
        _ => status.ToString()
    };

    private static string ItemTypeName(ItemType itemType) => itemType switch
    {
        ItemType.Product => "منتج",
        ItemType.PrintingService => "خدمة طباعة",
        _ => itemType.ToString()
    };

    private static string SuspendedInvoiceStatusName(SuspendedInvoiceStatus status) => status switch
    {
        SuspendedInvoiceStatus.Active => "معلقة",
        SuspendedInvoiceStatus.Resumed => "تم استرجاعها",
        SuspendedInvoiceStatus.Cancelled => "ملغاة",
        _ => status.ToString()
    };

    private static string ActionName(AuditAction action) => action switch
    {
        AuditAction.Login => "تسجيل دخول",
        AuditAction.Logout => "تسجيل خروج",
        AuditAction.OpenShift => "فتح شيفت",
        AuditAction.CloseShift => "إغلاق شيفت",
        AuditAction.HoldInvoice => "تعليق فاتورة",
        AuditAction.ResumeHeldInvoice => "استرجاع فاتورة معلقة",
        AuditAction.CancelHeldInvoice => "إلغاء فاتورة معلقة",
        AuditAction.CreateInvoice => "إنشاء فاتورة",
        AuditAction.ReturnInvoice => "إرجاع فاتورة",
        AuditAction.ChangeProductPrice => "تغيير سعر منتج",
        AuditAction.ChangeStockQuantity => "تعديل مخزون",
        AuditAction.CreateProduct => "إنشاء منتج",
        AuditAction.EditProduct => "تعديل منتج",
        AuditAction.DeleteProduct => "إيقاف منتج",
        AuditAction.ImportProducts => "استيراد منتجات",
        AuditAction.ExportProducts => "تصدير منتجات",
        AuditAction.CreateUser => "إنشاء مستخدم",
        AuditAction.EditUser => "تعديل مستخدم",
        AuditAction.ChangePermissions => "تغيير صلاحيات",
        AuditAction.BackupCreated => "إنشاء نسخة احتياطية",
        AuditAction.BackupImported => "استيراد نسخة احتياطية",
        AuditAction.BackupExported => "تصدير نسخة احتياطية",
        AuditAction.BackupDeleted => "حذف نسخة احتياطية",
        AuditAction.RestorePerformed => "استرجاع نسخة احتياطية",
        AuditAction.LogsExported => "تصدير ملفات السجل",
        AuditAction.PrinterSettingsChanged => "تغيير إعدادات الطباعة",
        AuditAction.SettingsChanged => "تغيير إعدادات",
        _ => action.ToString()
    };
}
