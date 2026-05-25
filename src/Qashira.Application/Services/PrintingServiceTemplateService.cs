using System.Text.Json;
using Qashira.Application.Abstractions;
using Qashira.Application.DTOs;
using Qashira.Application.Permissions;
using Qashira.Domain.Entities;
using Qashira.Domain.Enums;
using Qashira.Shared.Arabic;
using Qashira.Shared.Results;
using Microsoft.EntityFrameworkCore;

namespace Qashira.Application.Services;

public sealed class PrintingServiceTemplateService(
    IApplicationDbContext dbContext,
    IPermissionService permissionService) : IPrintingServiceTemplateService
{
    public async Task<IReadOnlyList<PrintingServiceTemplateListItemDto>> SearchAsync(
        string searchText,
        bool includeInactive = false,
        CancellationToken cancellationToken = default)
    {
        permissionService.EnsureCurrentUserHas(PermissionCodes.CanManageSettings);

        var normalized = ArabicTextNormalizer.NormalizeForSearch(searchText);
        var query = dbContext.PrintingServiceTemplates.AsNoTracking();

        if (!includeInactive)
        {
            query = query.Where(x => x.IsActive);
        }

        if (!string.IsNullOrWhiteSpace(normalized))
        {
            query = query.Where(x =>
                x.SearchName.Contains(normalized) ||
                (x.ShortcutKey != null && x.ShortcutKey.Contains(searchText)));
        }

        return await query
            .OrderBy(x => x.ServiceName)
            .Take(200)
            .Select(x => new PrintingServiceTemplateListItemDto(
                x.Id,
                x.ServiceName,
                x.ServiceType,
                x.UnitName,
                x.SellingPricePerUnit,
                x.UsesPaper,
                x.UsesInk,
                x.EstimatedInkCostPerUnit,
                x.ShowInCashier,
                x.IsActive,
                x.ShortcutKey))
            .ToListAsync(cancellationToken);
    }

    public async Task<PrintingServiceTemplateDetailsDto?> GetAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        permissionService.EnsureCurrentUserHas(PermissionCodes.CanManageSettings);

        var template = await dbContext.PrintingServiceTemplates
            .AsNoTracking()
            .Include(x => x.MaterialConsumptions)
            .ThenInclude(x => x.Product)
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

        return template is null ? null : ToDetailsDto(template);
    }

    public async Task<IReadOnlyList<PrintingServiceTemplateListItemDto>> GetCashierTemplatesAsync(
        CancellationToken cancellationToken = default)
    {
        permissionService.EnsureCurrentUserHas(PermissionCodes.CanUsePOS);

        return await dbContext.PrintingServiceTemplates
            .AsNoTracking()
            .Where(x => x.IsActive && x.ShowInCashier)
            .OrderBy(x => x.ServiceName)
            .Select(x => new PrintingServiceTemplateListItemDto(
                x.Id,
                x.ServiceName,
                x.ServiceType,
                x.UnitName,
                x.SellingPricePerUnit,
                x.UsesPaper,
                x.UsesInk,
                x.EstimatedInkCostPerUnit,
                x.ShowInCashier,
                x.IsActive,
                x.ShortcutKey))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<PrintingMaterialProductOptionDto>> GetMaterialProductsAsync(
        string searchText = "",
        CancellationToken cancellationToken = default)
    {
        permissionService.EnsureCurrentUserHas(PermissionCodes.CanManageSettings);

        var normalized = ArabicTextNormalizer.NormalizeForSearch(searchText);
        var query = dbContext.Products
            .AsNoTracking()
            .Where(x => x.IsActive);

        if (!string.IsNullOrWhiteSpace(normalized))
        {
            query = query.Where(x =>
                x.SearchName.Contains(normalized) ||
                x.Barcode.Contains(searchText) ||
                x.InternalCode.Contains(searchText));
        }

        return await query
            .OrderBy(x => x.Name)
            .Take(200)
            .Select(x => new PrintingMaterialProductOptionDto(
                x.Id,
                x.Name,
                x.Barcode,
                x.StockQuantity,
                x.PurchasePrice))
            .ToListAsync(cancellationToken);
    }

    public async Task<Result<PrintingServiceTemplateDetailsDto>> SaveAsync(
        UpsertPrintingServiceTemplateRequest request,
        int userId,
        CancellationToken cancellationToken = default)
    {
        permissionService.EnsureCurrentUserHas(PermissionCodes.CanManageSettings);

        var validation = await ValidateAsync(request, cancellationToken);
        if (!validation.Succeeded)
        {
            return Result<PrintingServiceTemplateDetailsDto>.Failure(validation.Message);
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var now = DateTimeOffset.UtcNow;
            var normalizedName = ArabicTextNormalizer.NormalizeForSearch(request.ServiceName);
            var isNew = !request.Id.HasValue || request.Id.Value == 0;
            string? oldValuesJson = null;

            PrintingServiceTemplate template;
            if (isNew)
            {
                template = new PrintingServiceTemplate
                {
                    CreatedAt = now
                };
                dbContext.PrintingServiceTemplates.Add(template);
            }
            else
            {
                template = await dbContext.PrintingServiceTemplates
                    .Include(x => x.MaterialConsumptions)
                    .ThenInclude(x => x.Product)
                    .SingleAsync(x => x.Id == request.Id!.Value, cancellationToken);
                oldValuesJson = JsonSerializer.Serialize(ToAuditShape(template));
                template.UpdatedAt = now;
                dbContext.PrintingMaterialConsumptions.RemoveRange(template.MaterialConsumptions);
                template.MaterialConsumptions.Clear();
            }

            template.ServiceName = request.ServiceName.Trim();
            template.SearchName = normalizedName;
            template.ServiceType = request.ServiceType;
            template.UnitName = request.UnitName.Trim();
            template.SellingPricePerUnit = request.SellingPricePerUnit;
            template.UsesPaper = request.Materials.Count > 0;
            template.PaperConsumptionPerUnit = request.Materials.Count > 0 ? request.PaperConsumptionPerUnit : 0m;
            template.UsesInk = request.UsesInk;
            template.InkCostMode = request.UsesInk ? request.InkCostMode : InkCostMode.None;
            template.EstimatedInkCostPerUnit = request.UsesInk && request.InkCostMode == InkCostMode.FixedEstimatedCostPerUnit
                ? request.EstimatedInkCostPerUnit
                : 0m;
            template.ShowInCashier = request.ShowInCashier;
            template.IsActive = request.IsActive;
            template.ShortcutKey = string.IsNullOrWhiteSpace(request.ShortcutKey) ? null : request.ShortcutKey.Trim();
            template.Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim();

            foreach (var material in request.Materials)
            {
                template.MaterialConsumptions.Add(new PrintingMaterialConsumption
                {
                    ProductId = material.ProductId,
                    QuantityPerUnit = material.QuantityPerUnit,
                    Notes = string.IsNullOrWhiteSpace(material.Notes) ? null : material.Notes.Trim()
                });
            }

            dbContext.AuditLogs.Add(new AuditLog
            {
                UserId = userId,
                Action = AuditAction.SettingsChanged,
                EntityName = nameof(PrintingServiceTemplate),
                EntityId = isNew ? null : template.Id.ToString(),
                OldValuesJson = oldValuesJson,
                NewValuesJson = JsonSerializer.Serialize(ToAuditShape(template)),
                Description = isNew
                    ? $"تم إنشاء خدمة طباعة {template.ServiceName}."
                    : $"تم تعديل خدمة الطباعة {template.ServiceName}.",
                CreatedAt = now
            });

            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            var saved = await GetDetailsWithoutPermissionAsync(template.Id, cancellationToken);
            return Result<PrintingServiceTemplateDetailsDto>.Success(saved!, "تم حفظ خدمة الطباعة بنجاح.");
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<Result> ToggleActiveAsync(
        int templateId,
        int userId,
        CancellationToken cancellationToken = default)
    {
        permissionService.EnsureCurrentUserHas(PermissionCodes.CanManageSettings);

        var template = await dbContext.PrintingServiceTemplates
            .SingleOrDefaultAsync(x => x.Id == templateId, cancellationToken);

        if (template is null)
        {
            return Result.Failure("اختر خدمة طباعة صحيحة.");
        }

        var oldValuesJson = JsonSerializer.Serialize(ToAuditShape(template));
        template.IsActive = !template.IsActive;
        template.UpdatedAt = DateTimeOffset.UtcNow;

        dbContext.AuditLogs.Add(new AuditLog
        {
            UserId = userId,
            Action = AuditAction.SettingsChanged,
            EntityName = nameof(PrintingServiceTemplate),
            EntityId = template.Id.ToString(),
            OldValuesJson = oldValuesJson,
            NewValuesJson = JsonSerializer.Serialize(ToAuditShape(template)),
            Description = template.IsActive
                ? $"تم تفعيل خدمة الطباعة {template.ServiceName}."
                : $"تم إيقاف خدمة الطباعة {template.ServiceName}.",
            CreatedAt = template.UpdatedAt.Value
        });

        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success(template.IsActive ? "تم تفعيل خدمة الطباعة." : "تم إيقاف خدمة الطباعة.");
    }

    private async Task<Result> ValidateAsync(UpsertPrintingServiceTemplateRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.ServiceName))
        {
            return Result.Failure("اسم خدمة الطباعة مطلوب.");
        }

        if (string.IsNullOrWhiteSpace(request.UnitName))
        {
            return Result.Failure("اسم الوحدة مطلوب.");
        }

        if (request.SellingPricePerUnit < 0)
        {
            return Result.Failure("سعر البيع للوحدة لا يمكن أن يكون أقل من صفر.");
        }

        if (request.PaperConsumptionPerUnit < 0)
        {
            return Result.Failure("استهلاك الورق لا يمكن أن يكون أقل من صفر.");
        }

        if (request.EstimatedInkCostPerUnit < 0)
        {
            return Result.Failure("تكلفة الحبر التقديرية لا يمكن أن تكون أقل من صفر.");
        }

        if (request.Materials.Any(x => x.QuantityPerUnit <= 0))
        {
            return Result.Failure("كمية استهلاك الخامات يجب أن تكون أكبر من صفر.");
        }

        var materialIds = request.Materials.Select(x => x.ProductId).ToArray();
        if (materialIds.Length != materialIds.Distinct().Count())
        {
            return Result.Failure("لا يمكن تكرار نفس الخامة داخل خدمة الطباعة.");
        }

        if (materialIds.Length > 0)
        {
            var activeMaterialCount = await dbContext.Products
                .CountAsync(x => materialIds.Contains(x.Id) && x.IsActive, cancellationToken);

            if (activeMaterialCount != materialIds.Length)
            {
                return Result.Failure("توجد خامة غير موجودة أو غير نشطة.");
            }
        }

        var normalizedName = ArabicTextNormalizer.NormalizeForSearch(request.ServiceName);
        var duplicateExists = await dbContext.PrintingServiceTemplates.AnyAsync(
            x => x.SearchName == normalizedName && (!request.Id.HasValue || x.Id != request.Id.Value),
            cancellationToken);

        return duplicateExists
            ? Result.Failure("توجد خدمة طباعة بنفس الاسم.")
            : Result.Success();
    }

    private async Task<PrintingServiceTemplateDetailsDto?> GetDetailsWithoutPermissionAsync(int id, CancellationToken cancellationToken)
    {
        var template = await dbContext.PrintingServiceTemplates
            .AsNoTracking()
            .Include(x => x.MaterialConsumptions)
            .ThenInclude(x => x.Product)
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

        return template is null ? null : ToDetailsDto(template);
    }

    private static PrintingServiceTemplateDetailsDto ToDetailsDto(PrintingServiceTemplate template) =>
        new(
            template.Id,
            template.ServiceName,
            template.ServiceType,
            template.UnitName,
            template.SellingPricePerUnit,
            template.UsesPaper,
            template.PaperConsumptionPerUnit,
            template.UsesInk,
            template.InkCostMode,
            template.EstimatedInkCostPerUnit,
            template.ShowInCashier,
            template.IsActive,
            template.ShortcutKey,
            template.Notes,
            template.MaterialConsumptions
                .OrderBy(x => x.Product.Name)
                .Select(x => new PrintingMaterialConsumptionDto(
                    x.ProductId,
                    x.Product.Name,
                    x.Product.Barcode,
                    x.Product.StockQuantity,
                    x.Product.PurchasePrice,
                    x.QuantityPerUnit,
                    x.Notes))
                .ToArray());

    private static object ToAuditShape(PrintingServiceTemplate template) => new
    {
        template.Id,
        template.ServiceName,
        template.ServiceType,
        template.UnitName,
        template.SellingPricePerUnit,
        template.UsesPaper,
        template.PaperConsumptionPerUnit,
        template.UsesInk,
        template.InkCostMode,
        template.EstimatedInkCostPerUnit,
        template.ShowInCashier,
        template.IsActive,
        template.ShortcutKey,
        template.Notes,
        Materials = template.MaterialConsumptions.Select(x => new
        {
            x.ProductId,
            ProductName = x.Product?.Name,
            x.QuantityPerUnit,
            x.Notes
        })
    };
}
