using System.Text.Json;
using Qashira.Application.Abstractions;
using Qashira.Application.DTOs;
using Qashira.Application.Permissions;
using Qashira.Domain.Entities;
using Qashira.Domain.Enums;
using Qashira.Shared.Results;
using Microsoft.EntityFrameworkCore;

namespace Qashira.Application.Services;

public sealed class ShiftService(
    IApplicationDbContext dbContext,
    IPermissionService permissionService) : IShiftService
{
    public async Task<int?> GetOpenShiftIdAsync(int cashierId, CancellationToken cancellationToken = default)
    {
        return await dbContext.Shifts
            .Where(x => x.CashierId == cashierId && x.Status == ShiftStatus.Open)
            .Select(x => (int?)x.Id)
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<Result<int>> OpenShiftAsync(int cashierId, decimal openingCash, CancellationToken cancellationToken = default)
    {
        permissionService.EnsureCurrentUserHas(PermissionCodes.CanUsePOS);

        if (openingCash < 0)
        {
            return Result<int>.Failure("مبلغ بداية الشيفت لا يمكن أن يكون أقل من صفر.");
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var hasOpenShift = await dbContext.Shifts
                .AnyAsync(x => x.CashierId == cashierId && x.Status == ShiftStatus.Open, cancellationToken);

            if (hasOpenShift)
            {
                return Result<int>.Failure("يوجد شيفت مفتوح بالفعل لهذا المستخدم.");
            }

            var now = DateTimeOffset.UtcNow;
            var shift = new Shift
            {
                CashierId = cashierId,
                OpeningCash = openingCash,
                Status = ShiftStatus.Open,
                OpenedAt = now
            };

            dbContext.Shifts.Add(shift);
            await dbContext.SaveChangesAsync(cancellationToken);

            dbContext.AuditLogs.Add(new AuditLog
            {
                UserId = cashierId,
                Action = AuditAction.OpenShift,
                EntityName = nameof(Shift),
                EntityId = shift.Id.ToString(),
                NewValuesJson = ShiftValuesJson(shift),
                Description = $"تم فتح شيفت بمبلغ افتتاحي {openingCash:0.00} ج.م.",
                CreatedAt = now
            });

            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Result<int>.Success(shift.Id, "تم فتح الشيفت بنجاح.");
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<Result<ShiftSummaryDto>> GetOpenShiftSummaryAsync(int cashierId, CancellationToken cancellationToken = default)
    {
        permissionService.EnsureCurrentUserHas(PermissionCodes.CanCloseShift);

        var shift = await dbContext.Shifts
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.CashierId == cashierId && x.Status == ShiftStatus.Open, cancellationToken);

        if (shift is null)
        {
            return Result<ShiftSummaryDto>.Failure("لا يوجد شيفت مفتوح لهذا المستخدم.");
        }

        var cashSaleAmounts = await dbContext.Payments
            .AsNoTracking()
            .Where(x => x.Invoice.ShiftId == shift.Id && x.Method == PaymentMethod.Cash)
            .Select(x => x.Amount)
            .ToListAsync(cancellationToken);

        var returnAmounts = await dbContext.Returns
            .AsNoTracking()
            .Where(x => x.ShiftId == shift.Id)
            .Select(x => x.TotalReturnedAmount)
            .ToListAsync(cancellationToken);

        var invoiceCount = await dbContext.Invoices
            .AsNoTracking()
            .CountAsync(x => x.ShiftId == shift.Id, cancellationToken);

        var cashSales = cashSaleAmounts.Sum();
        var returnsAmount = returnAmounts.Sum();
        var expectedCash = shift.OpeningCash + cashSales - returnsAmount;

        return Result<ShiftSummaryDto>.Success(new ShiftSummaryDto(
            shift.Id,
            shift.OpeningCash,
            cashSales,
            returnsAmount,
            expectedCash,
            invoiceCount,
            shift.OpenedAt));
    }

    public async Task<Result<CloseShiftResultDto>> CloseShiftAsync(int cashierId, decimal closingCash, CancellationToken cancellationToken = default)
    {
        permissionService.EnsureCurrentUserHas(PermissionCodes.CanCloseShift);

        if (closingCash < 0)
        {
            return Result<CloseShiftResultDto>.Failure("مبلغ إغلاق الشيفت لا يمكن أن يكون أقل من صفر.");
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            var shift = await dbContext.Shifts
                .SingleOrDefaultAsync(x => x.CashierId == cashierId && x.Status == ShiftStatus.Open, cancellationToken);

            if (shift is null)
            {
                return Result<CloseShiftResultDto>.Failure("لا يوجد شيفت مفتوح لهذا المستخدم.");
            }

            var activeSuspendedInvoiceCount = await dbContext.SuspendedInvoices
                .CountAsync(x => x.ShiftId == shift.Id && x.Status == SuspendedInvoiceStatus.Active, cancellationToken);

            if (activeSuspendedInvoiceCount > 0)
            {
                return Result<CloseShiftResultDto>.Failure($"يوجد {activeSuspendedInvoiceCount} فاتورة معلقة في هذا الشيفت. استرجعها أو ألغها قبل إغلاق الشيفت.");
            }

            var cashSaleAmounts = await dbContext.Payments
                .Where(x => x.Invoice.ShiftId == shift.Id && x.Method == PaymentMethod.Cash)
                .Select(x => x.Amount)
                .ToListAsync(cancellationToken);

            var returnAmounts = await dbContext.Returns
                .Where(x => x.ShiftId == shift.Id)
                .Select(x => x.TotalReturnedAmount)
                .ToListAsync(cancellationToken);

            var cashSales = cashSaleAmounts.Sum();
            var returnsAmount = returnAmounts.Sum();
            var expectedCash = shift.OpeningCash + cashSales - returnsAmount;
            var difference = closingCash - expectedCash;
            var oldValuesJson = ShiftValuesJson(shift);
            var closedAt = DateTimeOffset.UtcNow;

            shift.ClosingCash = closingCash;
            shift.ExpectedCash = expectedCash;
            shift.Difference = difference;
            shift.Status = ShiftStatus.Closed;
            shift.ClosedAt = closedAt;

            dbContext.AuditLogs.Add(new AuditLog
            {
                UserId = cashierId,
                Action = AuditAction.CloseShift,
                EntityName = nameof(Shift),
                EntityId = shift.Id.ToString(),
                OldValuesJson = oldValuesJson,
                NewValuesJson = ShiftValuesJson(shift),
                Description = $"تم إغلاق الشيفت. المتوقع {expectedCash:0.00} ج.م، الفعلي {closingCash:0.00} ج.م، الفرق {difference:0.00} ج.م.",
                CreatedAt = closedAt
            });

            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return Result<CloseShiftResultDto>.Success(
                new CloseShiftResultDto(shift.Id, expectedCash, closingCash, difference),
                "تم إغلاق الشيفت بنجاح.");
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private static string ShiftValuesJson(Shift shift) =>
        JsonSerializer.Serialize(new Dictionary<string, string>
        {
            ["رصيد الافتتاح"] = Money(shift.OpeningCash),
            ["رصيد الإغلاق"] = shift.ClosingCash.HasValue ? Money(shift.ClosingCash.Value) : "-",
            ["المتوقع"] = shift.ExpectedCash.HasValue ? Money(shift.ExpectedCash.Value) : "-",
            ["الفرق"] = shift.Difference.HasValue ? Money(shift.Difference.Value) : "-",
            ["الحالة"] = shift.Status == ShiftStatus.Open ? "مفتوح" : "مغلق",
            ["وقت الفتح"] = shift.OpenedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm"),
            ["وقت الإغلاق"] = shift.ClosedAt?.ToLocalTime().ToString("yyyy-MM-dd HH:mm") ?? "-"
        });

    private static string Money(decimal value) => $"{value:0.00} ج.م";
}
