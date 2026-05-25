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

public sealed class CategoryManagementService(
    IApplicationDbContext dbContext,
    IPermissionService permissionService) : ICategoryManagementService
{
    public async Task<IReadOnlyList<CategoryDetailsDto>> GetCategoriesAsync(bool includeInactive = true, CancellationToken cancellationToken = default)
    {
        permissionService.EnsureCurrentUserHas(PermissionCodes.CanManageSettings);

        IQueryable<Category> query = dbContext.Categories.AsNoTracking();
        if (!includeInactive)
        {
            query = query.Where(x => x.IsActive);
        }

        return await query
            .OrderByDescending(x => x.IsActive)
            .ThenBy(x => x.Name)
            .Select(x => new CategoryDetailsDto(
                x.Id,
                x.Name,
                x.MeasurementUnit,
                x.Products.Count,
                x.IsActive,
                x.CreatedAt))
            .ToListAsync(cancellationToken);
    }

    public async Task<Result<CategoryDetailsDto>> SaveCategoryAsync(UpsertCategoryRequest request, int userId, CancellationToken cancellationToken = default)
    {
        permissionService.EnsureCurrentUserHas(PermissionCodes.CanManageSettings);

        var name = request.Name.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            return Result<CategoryDetailsDto>.Failure("اكتب اسم التصنيف.");
        }

        var searchName = ArabicTextNormalizer.NormalizeForSearch(name);
        var duplicate = await dbContext.Categories.AnyAsync(
            x => x.Id != request.Id && x.SearchName == searchName,
            cancellationToken);

        if (duplicate)
        {
            return Result<CategoryDetailsDto>.Failure("يوجد تصنيف آخر بنفس الاسم أو بنفس كتابة عربية قريبة.");
        }

        var now = DateTimeOffset.UtcNow;
        Category category;
        string? oldValuesJson = null;
        var isNew = !request.Id.HasValue || request.Id.Value == 0;

        if (isNew)
        {
            category = new Category
            {
                CreatedAt = now
            };
            dbContext.Categories.Add(category);
        }
        else
        {
            var categoryId = request.Id.GetValueOrDefault();
            category = await dbContext.Categories.SingleOrDefaultAsync(x => x.Id == categoryId, cancellationToken)
                ?? throw new InvalidOperationException("لم يتم العثور على التصنيف.");
            oldValuesJson = CategoryValuesJson(category, await CountProductsAsync(category.Id, cancellationToken));
        }

        category.Name = name;
        category.SearchName = searchName;
        category.MeasurementUnit = request.MeasurementUnit;
        category.IsActive = request.IsActive;

        await dbContext.SaveChangesAsync(cancellationToken);

        var productCount = await CountProductsAsync(category.Id, cancellationToken);
        dbContext.AuditLogs.Add(new AuditLog
        {
            UserId = userId,
            Action = AuditAction.SettingsChanged,
            EntityName = nameof(Category),
            EntityId = category.Id.ToString(),
            OldValuesJson = oldValuesJson,
            NewValuesJson = CategoryValuesJson(category, productCount),
            Description = isNew
                ? $"تم إنشاء التصنيف {category.Name}."
                : $"تم تعديل التصنيف {category.Name}.",
            CreatedAt = now
        });

        await dbContext.SaveChangesAsync(cancellationToken);

        return Result<CategoryDetailsDto>.Success(
            new CategoryDetailsDto(category.Id, category.Name, category.MeasurementUnit, productCount, category.IsActive, category.CreatedAt),
            isNew ? "تم إنشاء التصنيف بنجاح." : "تم حفظ التصنيف بنجاح.");
    }

    public async Task<Result> SetCategoryActiveAsync(int categoryId, bool isActive, int userId, CancellationToken cancellationToken = default)
    {
        permissionService.EnsureCurrentUserHas(PermissionCodes.CanManageSettings);

        var category = await dbContext.Categories.SingleOrDefaultAsync(x => x.Id == categoryId, cancellationToken);
        if (category is null)
        {
            return Result.Failure("لم يتم العثور على التصنيف.");
        }

        if (category.IsActive == isActive)
        {
            return Result.Success(isActive ? "التصنيف نشط بالفعل." : "التصنيف متوقف بالفعل.");
        }

        var productCount = await CountProductsAsync(category.Id, cancellationToken);
        var oldValuesJson = CategoryValuesJson(category, productCount);
        category.IsActive = isActive;

        dbContext.AuditLogs.Add(new AuditLog
        {
            UserId = userId,
            Action = AuditAction.SettingsChanged,
            EntityName = nameof(Category),
            EntityId = category.Id.ToString(),
            OldValuesJson = oldValuesJson,
            NewValuesJson = CategoryValuesJson(category, productCount),
            Description = isActive
                ? $"تم تفعيل التصنيف {category.Name}."
                : $"تم إيقاف التصنيف {category.Name}.",
            CreatedAt = DateTimeOffset.UtcNow
        });

        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success(isActive ? "تم تفعيل التصنيف بنجاح." : "تم إيقاف التصنيف بنجاح.");
    }

    private Task<int> CountProductsAsync(int categoryId, CancellationToken cancellationToken) =>
        dbContext.Products.CountAsync(x => x.CategoryId == categoryId, cancellationToken);

    private static string CategoryValuesJson(Category category, int productCount) =>
        JsonSerializer.Serialize(new Dictionary<string, string>
        {
            ["اسم التصنيف"] = category.Name,
            ["اسم البحث العربي"] = category.SearchName,
            ["وحدة القياس"] = MeasurementUnitLabels.ToArabic(category.MeasurementUnit),
            ["عدد المنتجات"] = productCount.ToString(),
            ["نشط"] = category.IsActive ? "نعم" : "لا"
        });
}
