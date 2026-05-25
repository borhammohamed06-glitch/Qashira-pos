using Qashira.Application.DTOs;

namespace Qashira.App.Services;

public interface IReceiptPrinter
{
    IReadOnlyList<string> GetInstalledPrinters();
    string? GetDefaultPrinterName();
    bool Print(ReceiptDto receipt, PrinterSettingsDto settings);
    bool PrintTestPage(PrinterSettingsDto settings);
    bool PrintBarcodeLabels(BarcodeLabelPrintRequest request, PrinterSettingsDto settings);
    bool PrintTestBarcodeLabel(PrinterSettingsDto settings);
}

public sealed record BarcodeLabelPrintRequest(
    string ProductName,
    string Barcode,
    decimal SalePrice,
    int Quantity);
