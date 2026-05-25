namespace Qashira.Application.DTOs;

public sealed record PrinterSettingsDto(
    string ReceiptPrinterName,
    string LabelPrinterName,
    string StoreName,
    string ReceiptTitle,
    string ReceiptFooter,
    string ReceiptPaperWidth,
    string BarcodeLabelSize,
    string BarcodePrinterProfile,
    double BarcodeLabelGapMm,
    double BarcodeHorizontalOffsetMm,
    double BarcodeVerticalOffsetMm);
