using System.Globalization;
using Qashira.Application.Abstractions;
using Qashira.Application.DTOs;
using Qashira.Application.Permissions;
using Qashira.Domain.Entities;
using Qashira.Domain.Enums;
using Qashira.Shared.Results;
using Microsoft.EntityFrameworkCore;

namespace Qashira.Application.Services;

public sealed class PrinterSettingsService(
    IApplicationDbContext dbContext,
    IPermissionService permissionService) : IPrinterSettingsService
{
    public async Task<PrinterSettingsDto> GetSettingsAsync(CancellationToken cancellationToken = default)
    {
        var settings = await dbContext.AppSettings
            .AsNoTracking()
            .ToDictionaryAsync(x => x.Key, x => x.Value, cancellationToken);
        var receiptPrinterName = settings.GetValueOrDefault("ReceiptPrinterName", string.Empty);
        var labelPrinterName = settings.GetValueOrDefault("LabelPrinterName", receiptPrinterName);
        var barcodePrinterProfile = NormalizeBarcodePrinterProfile(settings.GetValueOrDefault("BarcodePrinterProfile", "Auto"));
        var barcodeLabelSize = NormalizeBarcodeLabelSize(settings.GetValueOrDefault(
            GetProfileSettingKey("BarcodeLabelSize", labelPrinterName, barcodePrinterProfile),
            settings.GetValueOrDefault("BarcodeLabelSize", "38x50 mm")));
        var barcodeGap = ParseMeasurement(settings.GetValueOrDefault(
            GetProfileSettingKey("BarcodeLabelGapMm", labelPrinterName, barcodePrinterProfile),
            settings.GetValueOrDefault("BarcodeLabelGapMm", "2")), 0, 10, 2);
        var barcodeHorizontalOffset = ParseMeasurement(settings.GetValueOrDefault(
            GetProfileSettingKey("BarcodeHorizontalOffsetMm", labelPrinterName, barcodePrinterProfile),
            settings.GetValueOrDefault("BarcodeHorizontalOffsetMm", "0")), -15, 15, 0);
        var barcodeVerticalOffset = ParseMeasurement(settings.GetValueOrDefault(
            GetProfileSettingKey("BarcodeVerticalOffsetMm", labelPrinterName, barcodePrinterProfile),
            settings.GetValueOrDefault("BarcodeVerticalOffsetMm", "0")), -15, 15, 0);

        return new PrinterSettingsDto(
            receiptPrinterName,
            labelPrinterName,
            settings.GetValueOrDefault("StoreName", "Qashira - كاشيرا"),
            settings.GetValueOrDefault("ReceiptHeader", "فاتورة شراء"),
            settings.GetValueOrDefault("ReceiptFooter", "شكرًا لتعاملكم مع كاشيرا"),
            NormalizeReceiptPaperWidth(settings.GetValueOrDefault("ReceiptPaperWidth", "80mm")),
            barcodeLabelSize,
            barcodePrinterProfile,
            barcodeGap,
            barcodeHorizontalOffset,
            barcodeVerticalOffset);
    }

    public async Task<Result> SaveSettingsAsync(PrinterSettingsDto settings, int userId, CancellationToken cancellationToken = default)
    {
        permissionService.EnsureCurrentUserHas(PermissionCodes.CanChangePrinterSettings);

        var normalizedName = (settings.ReceiptPrinterName ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(normalizedName))
        {
            return Result.Failure("اختر طابعة الإيصالات أولاً.");
        }

        var labelPrinterName = string.IsNullOrWhiteSpace(settings.LabelPrinterName)
            ? normalizedName
            : settings.LabelPrinterName.Trim();
        var labelSize = NormalizeBarcodeLabelSize(settings.BarcodeLabelSize);
        var barcodeProfile = NormalizeBarcodePrinterProfile(settings.BarcodePrinterProfile);
        var barcodeGap = ClampMeasurement(settings.BarcodeLabelGapMm, 0, 10);
        var barcodeHorizontalOffset = ClampMeasurement(settings.BarcodeHorizontalOffsetMm, -15, 15);
        var barcodeVerticalOffset = ClampMeasurement(settings.BarcodeVerticalOffsetMm, -15, 15);

        await UpsertSettingAsync("ReceiptPrinterName", normalizedName, cancellationToken);
        await UpsertSettingAsync("LabelPrinterName", labelPrinterName, cancellationToken);
        await UpsertSettingAsync("StoreName", string.IsNullOrWhiteSpace(settings.StoreName) ? "Qashira - كاشيرا" : settings.StoreName.Trim(), cancellationToken);
        await UpsertSettingAsync("ReceiptHeader", string.IsNullOrWhiteSpace(settings.ReceiptTitle) ? "فاتورة شراء" : settings.ReceiptTitle.Trim(), cancellationToken);
        await UpsertSettingAsync("ReceiptFooter", string.IsNullOrWhiteSpace(settings.ReceiptFooter) ? "شكرًا لتعاملكم مع كاشيرا" : settings.ReceiptFooter.Trim(), cancellationToken);
        await UpsertSettingAsync("ReceiptPaperWidth", NormalizeReceiptPaperWidth(settings.ReceiptPaperWidth), cancellationToken);
        await UpsertSettingAsync("BarcodeLabelSize", labelSize, cancellationToken);
        await UpsertSettingAsync("BarcodePrinterProfile", barcodeProfile, cancellationToken);
        await UpsertSettingAsync("BarcodeLabelGapMm", barcodeGap.ToString("0.##", CultureInfo.InvariantCulture), cancellationToken);
        await UpsertSettingAsync("BarcodeHorizontalOffsetMm", barcodeHorizontalOffset.ToString("0.##", CultureInfo.InvariantCulture), cancellationToken);
        await UpsertSettingAsync("BarcodeVerticalOffsetMm", barcodeVerticalOffset.ToString("0.##", CultureInfo.InvariantCulture), cancellationToken);
        await UpsertSettingAsync(GetProfileSettingKey("BarcodeLabelSize", labelPrinterName, barcodeProfile), labelSize, cancellationToken);
        await UpsertSettingAsync(GetProfileSettingKey("BarcodeLabelGapMm", labelPrinterName, barcodeProfile), barcodeGap.ToString("0.##", CultureInfo.InvariantCulture), cancellationToken);
        await UpsertSettingAsync(GetProfileSettingKey("BarcodeHorizontalOffsetMm", labelPrinterName, barcodeProfile), barcodeHorizontalOffset.ToString("0.##", CultureInfo.InvariantCulture), cancellationToken);
        await UpsertSettingAsync(GetProfileSettingKey("BarcodeVerticalOffsetMm", labelPrinterName, barcodeProfile), barcodeVerticalOffset.ToString("0.##", CultureInfo.InvariantCulture), cancellationToken);

        dbContext.AuditLogs.Add(new AuditLog
        {
            UserId = userId,
            Action = AuditAction.PrinterSettingsChanged,
            EntityName = nameof(AppSetting),
            EntityId = "ReceiptPrinterName",
            Description = $"تم تغيير إعدادات الإيصال والطباعة. طابعة الإيصالات: {normalizedName}. طابعة الباركود: {labelPrinterName}. ملف الطابعة: {barcodeProfile}. مقاس الليبل: {labelSize}. الفاصل: {barcodeGap:0.##} مم. إزاحة الباركود: {barcodeHorizontalOffset:0.##}/{barcodeVerticalOffset:0.##} مم.",
            CreatedAt = DateTimeOffset.UtcNow
        });

        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success("تم حفظ إعدادات الإيصال والطباعة.");
    }

    private async Task UpsertSettingAsync(string key, string value, CancellationToken cancellationToken)
    {
        var setting = await dbContext.AppSettings.SingleOrDefaultAsync(x => x.Key == key, cancellationToken);
        if (setting is null)
        {
            dbContext.AppSettings.Add(new AppSetting { Key = key, Value = value });
        }
        else
        {
            setting.Value = value;
        }
    }

    private static string NormalizeBarcodeLabelSize(string value)
    {
        if (value == "38x25 mm - 2 per row")
        {
            return "38x25 mm - 2 stacked";
        }

        return IsSupportedBarcodeLabelSize(value)
            ? value
            : "38x50 mm";
    }

    private static bool IsSupportedBarcodeLabelSize(string value) =>
        value is "38x50 mm" or "40x55 mm" or "50x25 mm" or "60x40 mm" or "38x25 mm" or "38x25 mm - 2 stacked";

    private static string NormalizeReceiptPaperWidth(string value) =>
        value is "57mm" or "58mm" ? "58mm" : "80mm";

    private static string NormalizeBarcodePrinterProfile(string value)
    {
        var normalized = (value ?? string.Empty).Trim();
        return normalized switch
        {
            "Windows" or "Windows Driver" or "WindowsDriver" => "WindowsDriver",
            "TSPL" or "Tspl" => "TSPL",
            "ZPL" or "Zpl" => "ZPL",
            _ => "Auto"
        };
    }

    private static double ParseMeasurement(string value, double minimum, double maximum, double fallback)
    {
        var normalized = (value ?? string.Empty).Trim().Replace(',', '.');
        return double.TryParse(normalized, NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var offset)
            ? ClampMeasurement(offset, minimum, maximum)
            : fallback;
    }

    private static double ClampMeasurement(double value, double minimum, double maximum) => Math.Clamp(value, minimum, maximum);

    private static string GetProfileSettingKey(string settingName, string printerName, string profile)
    {
        var normalizedPrinter = string.Join('_', printerName
            .Trim()
            .ToLowerInvariant()
            .Select(character => char.IsLetterOrDigit(character) ? character : '_'));
        return $"PrinterProfile:{normalizedPrinter}:{profile}:{settingName}";
    }
}
