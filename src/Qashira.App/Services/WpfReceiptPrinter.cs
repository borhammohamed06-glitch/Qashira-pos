using System.Globalization;
using System.Printing;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Shapes;
using Qashira.Application.DTOs;
using Serilog;

namespace Qashira.App.Services;

public sealed class WpfReceiptPrinter : IReceiptPrinter
{
    private static readonly double Receipt80PaperWidth = Mm(80);
    private static readonly double Receipt58PaperWidth = Mm(58);
    private static readonly double Receipt80BodyWidth = Mm(76);
    private static readonly double Receipt58BodyWidth = Mm(54);
    private static readonly double Receipt80Padding = Mm(3.2);
    private static readonly double Receipt58Padding = Mm(2.1);
    private static readonly double LabelPrinterMediaWidth = Mm(58);
    private static readonly string[] Code128Patterns =
    [
        "212222", "222122", "222221", "121223", "121322", "131222", "122213", "122312", "132212", "221213",
        "221312", "231212", "112232", "122132", "122231", "113222", "123122", "123221", "223211", "221132",
        "221231", "213212", "223112", "312131", "311222", "321122", "321221", "312212", "322112", "322211",
        "212123", "212321", "232121", "111323", "131123", "131321", "112313", "132113", "132311", "211313",
        "231113", "231311", "112133", "112331", "132131", "113123", "113321", "133121", "313121", "211331",
        "231131", "213113", "213311", "213131", "311123", "311321", "331121", "312113", "312311", "332111",
        "314111", "221411", "431111", "111224", "111422", "121124", "121421", "141122", "141221", "112214",
        "112412", "122114", "122411", "142112", "142211", "241211", "221114", "413111", "241112", "134111",
        "111242", "121142", "121241", "114212", "124112", "124211", "411212", "421112", "421211", "212141",
        "214121", "412121", "111143", "111341", "131141", "114113", "114311", "411113", "411311", "113141",
        "114131", "311141", "411131", "211412", "211214", "211232", "2331112"
    ];

    public IReadOnlyList<string> GetInstalledPrinters()
    {
        using var server = new LocalPrintServer();
        return server.GetPrintQueues()
            .Select(x => x.Name)
            .OrderBy(x => x)
            .ToArray();
    }

    public string? GetDefaultPrinterName()
    {
        try
        {
            using var server = new LocalPrintServer();
            return server.DefaultPrintQueue?.Name;
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "Could not read Windows default printer.");
            return null;
        }
    }

    public bool Print(ReceiptDto receipt, PrinterSettingsDto settings)
    {
        var printDialog = CreatePrintDialog(settings.ReceiptPrinterName);
        if (printDialog is null)
        {
            return false;
        }

        var layout = GetReceiptPrintLayout(settings.ReceiptPaperWidth, printDialog.PrintableAreaWidth);
        var document = BuildDocument(receipt, settings, layout, out var pageHeight);
        printDialog.PrintTicket.PageMediaSize = new PageMediaSize(layout.PageWidth, pageHeight);
        printDialog.PrintDocument(document.DocumentPaginator, $"فاتورة {receipt.InvoiceNumber}");
        return true;
    }

    public bool PrintTestPage(PrinterSettingsDto settings)
    {
        var receipt = new ReceiptDto(
            0,
            "TEST-000001",
            "TEST-000001",
            DateTimeOffset.Now,
            "محمد علي",
            223m,
            223m,
            10m,
            0m,
            213m,
            [
                new ReceiptLineDto("دفتر 100 ورقة", 2, 35m, 70m),
                new ReceiptLineDto("قلم جاف أزرق", 3, 12m, 36m),
                new ReceiptLineDto("آلة حاسبة", 1, 85m, 85m),
                new ReceiptLineDto("مسطرة", 4, 8m, 32m)
            ]);

        return Print(receipt, settings);
    }

    public bool PrintBarcodeLabels(BarcodeLabelPrintRequest request, PrinterSettingsDto settings)
    {
        if (string.IsNullOrWhiteSpace(request.Barcode) || request.Quantity <= 0)
        {
            return false;
        }

        try
        {
            using var queue = FindPrintQueue(settings.LabelPrinterName);
            if (queue is null)
            {
                return false;
            }

            var profile = ResolveBarcodePrinterProfile(settings.BarcodePrinterProfile, queue);
            if (profile is BarcodePrinterProfile.Tspl or BarcodePrinterProfile.Zpl)
            {
                var dpi = DetectPrinterDpi(queue);
                var nativeLayout = GetNativeLabelLayout(
                    settings.BarcodeLabelSize,
                    settings.BarcodeLabelGapMm,
                    settings.BarcodeHorizontalOffsetMm,
                    settings.BarcodeVerticalOffsetMm,
                    dpi);
                var command = profile == BarcodePrinterProfile.Tspl
                    ? BuildTsplBarcodeCommand(request, nativeLayout)
                    : BuildZplBarcodeCommand(request, nativeLayout);

                if (RawPrinterWriter.WriteAscii(queue.Name, $"Barcode {request.ProductName}", command))
                {
                    return true;
                }

                Log.Warning("Native barcode printing failed for {PrinterName} using {Profile}. Falling back to Windows driver.", queue.Name, profile);
            }

            return PrintBarcodeLabelsWithWindowsDriver(queue, request, settings);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Barcode label printing failed.");
            return false;
        }
    }

    public bool PrintTestBarcodeLabel(PrinterSettingsDto settings)
    {
        return PrintBarcodeLabels(
            new BarcodeLabelPrintRequest("قلم جاف أزرق", "100000000001", 8.50m, 1),
            settings);
    }

    private static PrintDialog? CreatePrintDialog(string? printerName)
    {
        if (string.IsNullOrWhiteSpace(printerName))
        {
            return null;
        }

        var queue = FindPrintQueue(printerName);
        if (queue is null)
        {
            return null;
        }

        var printDialog = new PrintDialog();
        printDialog.PrintQueue = queue;
        return printDialog;
    }

    private static PrintQueue? FindPrintQueue(string? printerName)
    {
        if (string.IsNullOrWhiteSpace(printerName))
        {
            return null;
        }

        using var server = new LocalPrintServer();
        return server.GetPrintQueues()
            .FirstOrDefault(x => string.Equals(x.Name, printerName, StringComparison.OrdinalIgnoreCase));
    }

    private static bool PrintBarcodeLabelsWithWindowsDriver(PrintQueue queue, BarcodeLabelPrintRequest request, PrinterSettingsDto settings)
    {
        var printDialog = new PrintDialog { PrintQueue = queue };
        var layout = GetLabelLayout(
            settings.BarcodeLabelSize,
            printDialog.PrintableAreaWidth,
            settings.BarcodeHorizontalOffsetMm);
        printDialog.PrintTicket.PageMediaSize = new PageMediaSize(layout.PageSize.Width, layout.PageSize.Height);
        var document = BuildBarcodeLabelDocument(request, layout);
        printDialog.PrintDocument(document.DocumentPaginator, $"باركود {request.ProductName}");
        return true;
    }

    private static ReceiptPrintLayout GetReceiptPrintLayout(string paperWidth, double printableAreaWidth)
    {
        var compact = IsCompactReceiptPaper(paperWidth);
        var requestedPaperWidth = compact ? Receipt58PaperWidth : Receipt80PaperWidth;
        var requestedBodyWidth = compact ? Receipt58BodyWidth : Receipt80BodyWidth;
        var hasUsablePrinterWidth = printableAreaWidth >= Mm(50) && printableAreaWidth <= requestedPaperWidth + Mm(10);
        var pageWidth = hasUsablePrinterWidth
            ? Math.Clamp(printableAreaWidth, requestedBodyWidth, requestedPaperWidth)
            : requestedPaperWidth;
        var bodyWidth = Math.Min(requestedBodyWidth, pageWidth);
        var left = Math.Max(0, (pageWidth - bodyWidth) / 2d);
        var padding = compact ? Receipt58Padding : Receipt80Padding;
        return new ReceiptPrintLayout(pageWidth, bodyWidth, left, padding, compact);
    }

    private static bool IsCompactReceiptPaper(string paperWidth) =>
        paperWidth is "57mm" or "58mm";

    private static FixedDocument BuildDocument(ReceiptDto receipt, PrinterSettingsDto settings, ReceiptPrintLayout layout, out double pageHeight)
    {
        var receiptBody = BuildReceiptVisual(receipt, settings, layout);
        receiptBody.Measure(new Size(layout.PageWidth, double.PositiveInfinity));
        receiptBody.Arrange(new Rect(0, 0, layout.PageWidth, receiptBody.DesiredSize.Height));
        receiptBody.UpdateLayout();

        pageHeight = Math.Max(receiptBody.DesiredSize.Height, 120);
        var page = new FixedPage
        {
            Width = layout.PageWidth,
            Height = pageHeight,
            Background = Brushes.White,
            FlowDirection = FlowDirection.RightToLeft
        };

        page.Children.Add(receiptBody);
        FixedPage.SetLeft(receiptBody, layout.BodyLeft);
        FixedPage.SetTop(receiptBody, 0);

        var pageContent = new PageContent();
        ((IAddChild)pageContent).AddChild(page);

        var document = new FixedDocument();
        document.Pages.Add(pageContent);
        return document;
    }

    private static FixedDocument BuildBarcodeLabelDocument(BarcodeLabelPrintRequest request, BarcodeLabelLayout layout)
    {
        var document = new FixedDocument();
        var quantity = Math.Clamp(request.Quantity, 1, 500);
        var printedLabels = 0;

        while (printedLabels < quantity)
        {
            var page = new FixedPage
            {
                Width = layout.PageSize.Width,
                Height = layout.PageSize.Height,
                Background = Brushes.White,
                FlowDirection = FlowDirection.LeftToRight
            };

            var slotsToPrint = layout.FillAllSlotsPerRequestedLabel
                ? layout.LabelsPerPage
                : Math.Min(layout.LabelsPerPage, quantity - printedLabels);

            for (var slot = 0; slot < slotsToPrint; slot++)
            {
                var label = BuildBarcodeLabelVisual(request, layout.LabelSize);
                label.Measure(layout.LabelSize);
                label.Arrange(new Rect(0, 0, layout.LabelSize.Width, layout.LabelSize.Height));
                label.UpdateLayout();

                var position = GetLabelPosition(layout, slot);
                page.Children.Add(label);
                FixedPage.SetLeft(label, position.X);
                FixedPage.SetTop(label, position.Y);
            }

            printedLabels += layout.FillAllSlotsPerRequestedLabel
                ? 1
                : slotsToPrint;

            var pageContent = new PageContent();
            ((IAddChild)pageContent).AddChild(page);
            document.Pages.Add(pageContent);
        }

        return document;
    }

    private static Point GetLabelPosition(BarcodeLabelLayout layout, int slot)
    {
        var left = Math.Max(0, (layout.PageSize.Width - layout.LabelSize.Width) / 2d);
        var top = Math.Max(0, (layout.PageSize.Height - layout.LabelSize.Height) / 2d);
        if (layout.LabelsPerPage <= 1)
        {
            return new Point(ApplyHorizontalOffset(left, layout), top);
        }

        if (layout.StackVertically)
        {
            var verticalGap = Math.Max(0, (layout.PageSize.Height - (layout.LabelSize.Height * layout.LabelsPerPage)) / (layout.LabelsPerPage + 1));
            top = verticalGap + (slot * (layout.LabelSize.Height + verticalGap));
            return new Point(ApplyHorizontalOffset(left, layout), top);
        }

        var horizontalGap = Math.Max(0, (layout.PageSize.Width - (layout.LabelSize.Width * layout.LabelsPerPage)) / (layout.LabelsPerPage + 1));
        left = horizontalGap + (slot * (layout.LabelSize.Width + horizontalGap));
        return new Point(ApplyHorizontalOffset(left, layout), top);
    }

    private static double ApplyHorizontalOffset(double left, BarcodeLabelLayout layout)
    {
        var maxLeft = Math.Max(0, layout.PageSize.Width - layout.LabelSize.Width);
        return Math.Clamp(left + layout.HorizontalOffset, 0, maxLeft);
    }

    private static Border BuildBarcodeLabelVisual(BarcodeLabelPrintRequest request, Size labelSize)
    {
        var padding = GetBarcodeLabelPadding(labelSize);
        var showText = true;
        var barcodeTextSize = showText ? GetBarcodeTextSize(labelSize, request.Barcode) : 0;
        var barcodeHeight = Math.Max(Cm(0.9), labelSize.Height - (padding * 2) - barcodeTextSize - (showText ? 4 : 0));
        var barcodeWidth = GetAdaptiveBarcodeWidth(labelSize.Width - (padding * 2), request.Barcode);

        var root = new StackPanel
        {
            Width = labelSize.Width - (padding * 2),
            FlowDirection = FlowDirection.LeftToRight,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };

        root.Children.Add(BuildBarcodeVisual(request.Barcode, barcodeWidth, barcodeHeight));
        if (showText)
        {
            root.Children.Add(new TextBlock
            {
                Text = request.Barcode,
                FontFamily = new FontFamily("Consolas, Tahoma"),
                FontSize = barcodeTextSize,
                TextAlignment = TextAlignment.Center,
                FlowDirection = FlowDirection.LeftToRight,
                TextWrapping = TextWrapping.NoWrap,
                LineHeight = barcodeTextSize + 1,
                Margin = new Thickness(0, 0.5, 0, 0),
                TextTrimming = TextTrimming.CharacterEllipsis
            });
        }

        return new Border
        {
            Width = labelSize.Width,
            Height = labelSize.Height,
            Background = Brushes.White,
            Padding = new Thickness(padding),
            FlowDirection = FlowDirection.LeftToRight,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Child = root
        };
    }

    private static double GetBarcodeLabelPadding(Size labelSize)
    {
        if (labelSize.Height <= Cm(1.5))
        {
            return Mm(1.7);
        }

        if (labelSize.Height <= Cm(2.5))
        {
            return Mm(3);
        }

        return Mm(4);
    }

    private static double GetBarcodeTextSize(Size labelSize, string barcode)
    {
        var isLong = barcode.Length > 13;
        if (labelSize.Height <= Cm(1.5))
        {
            return isLong ? 5.5 : 6.3;
        }

        if (labelSize.Height <= Cm(2.5))
        {
            return isLong ? 6.6 : 8;
        }

        return isLong ? 8.2 : 10.5;
    }

    private static double GetAdaptiveBarcodeWidth(double availableWidth, string barcode)
    {
        var safetyRatio = barcode.Length switch
        {
            <= 12 => 0.86,
            <= 13 => 0.82,
            <= 16 => 0.74,
            <= 20 => 0.68,
            _ => 0.62
        };
        return Math.Max(Mm(22), availableWidth * safetyRatio);
    }

    private static Canvas BuildBarcodeVisual(string value, double width, double height)
    {
        if (IsAsciiDigits(value) && value.Length == 13 && IsValidEan13(value))
        {
            return BuildEan13Visual(value, width, height);
        }

        var patterns = BuildCode128Patterns(value);
        var totalModules = patterns.Sum(pattern => pattern.Sum(digit => digit - '0'));
        var quietZone = Math.Clamp(width * 0.06, 5, 10);
        var moduleWidth = (width - (quietZone * 2)) / totalModules;
        var canvas = new Canvas
        {
            Width = width,
            Height = height,
            Background = Brushes.White,
            FlowDirection = FlowDirection.LeftToRight,
            ClipToBounds = true
        };

        var x = quietZone;
        foreach (var pattern in patterns)
        {
            var drawBar = true;
            foreach (var digit in pattern)
            {
                var barWidth = (digit - '0') * moduleWidth;
                if (drawBar)
                {
                    var rect = new Rectangle
                    {
                        Width = Math.Max(0.35, barWidth),
                        Height = height,
                        Fill = Brushes.Black
                    };
                    Canvas.SetLeft(rect, x);
                    Canvas.SetTop(rect, 0);
                    canvas.Children.Add(rect);
                }

                x += barWidth;
                drawBar = !drawBar;
            }
        }

        return canvas;
    }

    private static string[] BuildCode128Patterns(string value)
    {
        if (IsAsciiDigits(value) && value.Length >= 4)
        {
            return BuildCompactNumericCode128Patterns(value);
        }

        var values = new List<int> { 104 };
        foreach (var character in value)
        {
            if (character is < (char)32 or > (char)127)
            {
                throw new InvalidOperationException("Barcode contains unsupported characters.");
            }

            values.Add(character - 32);
        }

        var checksum = values[0];
        for (var index = 1; index < values.Count; index++)
        {
            checksum += values[index] * index;
        }

        values.Add(checksum % 103);
        values.Add(106);
        return values.Select(x => Code128Patterns[x]).ToArray();
    }

    private static string[] BuildCompactNumericCode128Patterns(string value)
    {
        var values = new List<int>();
        var startIndex = 0;

        if (value.Length % 2 == 0)
        {
            values.Add(105);
        }
        else
        {
            values.Add(104);
            values.Add(value[0] - 32);
            values.Add(99);
            startIndex = 1;
        }

        for (var index = startIndex; index < value.Length; index += 2)
        {
            values.Add(int.Parse(value.Substring(index, 2), CultureInfo.InvariantCulture));
        }

        AddCode128ChecksumAndStop(values);
        return values.Select(x => Code128Patterns[x]).ToArray();
    }

    private static void AddCode128ChecksumAndStop(List<int> values)
    {
        var checksum = values[0];
        for (var index = 1; index < values.Count; index++)
        {
            checksum += values[index] * index;
        }

        values.Add(checksum % 103);
        values.Add(106);
    }

    private static Canvas BuildEan13Visual(string value, double width, double height)
    {
        var bits = BuildEan13Bits(value);
        var quietZone = Math.Clamp(width * 0.06, 5, 10);
        var moduleWidth = (width - (quietZone * 2)) / bits.Length;
        var canvas = new Canvas
        {
            Width = width,
            Height = height,
            Background = Brushes.White,
            FlowDirection = FlowDirection.LeftToRight,
            ClipToBounds = true
        };

        var x = quietZone;
        foreach (var bit in bits)
        {
            if (bit == '1')
            {
                var rect = new Rectangle
                {
                    Width = Math.Max(0.75, moduleWidth),
                    Height = height,
                    Fill = Brushes.Black
                };
                Canvas.SetLeft(rect, x);
                Canvas.SetTop(rect, 0);
                canvas.Children.Add(rect);
            }

            x += moduleWidth;
        }

        return canvas;
    }

    private static string BuildEan13Bits(string value)
    {
        string[] leftOdd =
        [
            "0001101", "0011001", "0010011", "0111101", "0100011",
            "0110001", "0101111", "0111011", "0110111", "0001011"
        ];
        string[] leftEven =
        [
            "0100111", "0110011", "0011011", "0100001", "0011101",
            "0111001", "0000101", "0010001", "0001001", "0010111"
        ];
        string[] right =
        [
            "1110010", "1100110", "1101100", "1000010", "1011100",
            "1001110", "1010000", "1000100", "1001000", "1110100"
        ];
        string[] parity =
        [
            "LLLLLL", "LLGLGG", "LLGGLG", "LLGGGL", "LGLLGG",
            "LGGLLG", "LGGGLL", "LGLGLG", "LGLGGL", "LGGLGL"
        ];

        var firstDigit = value[0] - '0';
        var bits = "101";
        var selectedParity = parity[firstDigit];

        for (var index = 1; index <= 6; index++)
        {
            var digit = value[index] - '0';
            bits += selectedParity[index - 1] == 'L'
                ? leftOdd[digit]
                : leftEven[digit];
        }

        bits += "01010";

        for (var index = 7; index < 13; index++)
        {
            bits += right[value[index] - '0'];
        }

        return bits + "101";
    }

    private static bool IsAsciiDigits(string value) => value.All(character => character is >= '0' and <= '9');

    private static bool IsValidEan13(string value)
    {
        var sum = 0;
        for (var index = 0; index < 12; index++)
        {
            var digit = value[index] - '0';
            sum += index % 2 == 0 ? digit : digit * 3;
        }

        var checkDigit = (10 - (sum % 10)) % 10;
        return checkDigit == value[12] - '0';
    }

    private static BarcodePrinterProfile ResolveBarcodePrinterProfile(string configuredProfile, PrintQueue queue)
    {
        return (configuredProfile ?? string.Empty).Trim() switch
        {
            "TSPL" => BarcodePrinterProfile.Tspl,
            "ZPL" => BarcodePrinterProfile.Zpl,
            "WindowsDriver" or "Windows Driver" or "Windows" => BarcodePrinterProfile.WindowsDriver,
            _ => DetectBarcodePrinterProfile(queue)
        };
    }

    private static BarcodePrinterProfile DetectBarcodePrinterProfile(PrintQueue queue)
    {
        var identity = $"{queue.Name} {queue.QueueDriver?.Name}".ToLowerInvariant();
        if (ContainsAny(identity, "zebra", "zdesigner", "gk420", "gc420", "gt800", "zd220", "zd230", "zd420", "zd421", "zt230", "zt410", "zt411"))
        {
            return BarcodePrinterProfile.Zpl;
        }

        if (ContainsAny(identity, "xprinter", "xp-", "tsc", "gprinter", "hprt", "rongta", "rpp", "tsp", "bixolon xd", "label"))
        {
            return BarcodePrinterProfile.Tspl;
        }

        return BarcodePrinterProfile.WindowsDriver;
    }

    private static bool ContainsAny(string value, params string[] needles) =>
        needles.Any(value.Contains);

    private static int DetectPrinterDpi(PrintQueue queue)
    {
        try
        {
            var resolution = queue.GetPrintCapabilities()
                .PageResolutionCapability
                .Where(x => x.X.HasValue && x.X.Value is >= 150 and <= 600)
                .OrderBy(x => Math.Abs(x.X!.Value - 203))
                .FirstOrDefault();

            if (resolution?.X is { } dpi)
            {
                return dpi;
            }
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "Could not read printer DPI capabilities for {PrinterName}.", queue.Name);
        }

        var identity = $"{queue.Name} {queue.QueueDriver?.Name}".ToLowerInvariant();
        if (identity.Contains("600"))
        {
            return 600;
        }

        if (identity.Contains("300"))
        {
            return 300;
        }

        return 203;
    }

    private static NativeBarcodeLabelLayout GetNativeLabelLayout(
        string labelSizeName,
        double gapMm,
        double horizontalOffsetMm,
        double verticalOffsetMm,
        int dpi)
    {
        var safeGap = Math.Clamp(gapMm, 0, 10);
        var safeHorizontalOffset = Math.Clamp(horizontalOffsetMm, -15, 15);
        var safeVerticalOffset = Math.Clamp(verticalOffsetMm, -15, 15);

        if (labelSizeName is "38x25 mm - 2 stacked" or "38x25 mm - 2 per row")
        {
            return new NativeBarcodeLabelLayout(
                38,
                25,
                38,
                12.5,
                2,
                true,
                true,
                safeGap,
                safeHorizontalOffset,
                safeVerticalOffset,
                dpi);
        }

        var labelSize = labelSizeName switch
        {
            "40x55 mm" => (Width: 40d, Height: 55d),
            "50x25 mm" => (Width: 50d, Height: 25d),
            "60x40 mm" => (Width: 60d, Height: 40d),
            "38x25 mm" => (Width: 38d, Height: 25d),
            _ => (Width: 38d, Height: 50d)
        };

        return new NativeBarcodeLabelLayout(
            labelSize.Width,
            labelSize.Height,
            labelSize.Width,
            labelSize.Height,
            1,
            false,
            false,
            safeGap,
            safeHorizontalOffset,
            safeVerticalOffset,
            dpi);
    }

    private static string BuildTsplBarcodeCommand(BarcodeLabelPrintRequest request, NativeBarcodeLabelLayout layout)
    {
        var command = new StringBuilder();
        var quantity = Math.Clamp(request.Quantity, 1, 500);
        var printedLabels = 0;

        while (printedLabels < quantity)
        {
            var slotsToPrint = layout.FillAllSlotsPerRequestedLabel
                ? layout.LabelsPerPage
                : Math.Min(layout.LabelsPerPage, quantity - printedLabels);

            command.AppendLine($"SIZE {FormatMm(layout.PageWidthMm)} mm,{FormatMm(layout.PageHeightMm)} mm");
            command.AppendLine($"GAP {FormatMm(layout.GapMm)} mm,0 mm");
            command.AppendLine("DENSITY 8");
            command.AppendLine("SPEED 4");
            command.AppendLine("DIRECTION 1");
            command.AppendLine("REFERENCE 0,0");
            command.AppendLine("CLS");

            for (var slot = 0; slot < slotsToPrint; slot++)
            {
                var placement = GetNativeBarcodePlacement(request.Barcode, layout, slot);
                foreach (var bar in placement.Bars)
                {
                    command.AppendLine(
                        string.Create(
                            CultureInfo.InvariantCulture,
                            $"BAR {bar.X},{bar.Y},{bar.Width},{bar.Height}"));
                }

                if (placement.ShowText)
                {
                    command.AppendLine(
                        string.Create(
                            CultureInfo.InvariantCulture,
                            $"TEXT {placement.TextX},{placement.TextY},\"{placement.TextFont}\",0,1,1,\"{EscapeTspl(request.Barcode)}\""));
                }
            }

            command.AppendLine("PRINT 1,1");
            printedLabels += layout.FillAllSlotsPerRequestedLabel
                ? 1
                : slotsToPrint;
        }

        return command.ToString();
    }

    private static string BuildZplBarcodeCommand(BarcodeLabelPrintRequest request, NativeBarcodeLabelLayout layout)
    {
        var command = new StringBuilder();
        var quantity = Math.Clamp(request.Quantity, 1, 500);
        var printedLabels = 0;

        while (printedLabels < quantity)
        {
            var slotsToPrint = layout.FillAllSlotsPerRequestedLabel
                ? layout.LabelsPerPage
                : Math.Min(layout.LabelsPerPage, quantity - printedLabels);

            command.AppendLine("^XA");
            command.AppendLine(string.Create(CultureInfo.InvariantCulture, $"^PW{MmToDots(layout.PageWidthMm, layout.Dpi)}"));
            command.AppendLine(string.Create(CultureInfo.InvariantCulture, $"^LL{MmToDots(layout.PageHeightMm, layout.Dpi)}"));
            command.AppendLine("^LH0,0");

            for (var slot = 0; slot < slotsToPrint; slot++)
            {
                var placement = GetNativeBarcodePlacement(request.Barcode, layout, slot);
                foreach (var bar in placement.Bars)
                {
                    command.AppendLine(
                        string.Create(
                            CultureInfo.InvariantCulture,
                            $"^FO{bar.X},{bar.Y}^GB{bar.Width},{bar.Height},{Math.Max(bar.Width, bar.Height)},B,0^FS"));
                }

                if (placement.ShowText)
                {
                    command.AppendLine(string.Create(CultureInfo.InvariantCulture, $"^FO{placement.TextX},{placement.TextY}^A0N,{placement.TextHeight},{placement.TextWidth}^FD{EscapeZpl(request.Barcode)}^FS"));
                }
            }

            command.AppendLine("^XZ");
            printedLabels += layout.FillAllSlotsPerRequestedLabel
                ? 1
                : slotsToPrint;
        }

        return command.ToString();
    }

    private static NativeBarcodePlacement GetNativeBarcodePlacement(string barcode, NativeBarcodeLabelLayout layout, int slot)
    {
        var labelWidthDots = MmToDots(layout.LabelWidthMm, layout.Dpi);
        var labelHeightDots = MmToDots(layout.LabelHeightMm, layout.Dpi);
        var slotTopDots = layout.StackVertically
            ? MmToDots(layout.LabelHeightMm * slot, layout.Dpi)
            : 0;
        var horizontalOffsetDots = MmToDots(layout.HorizontalOffsetMm, layout.Dpi);
        var verticalOffsetDots = MmToDots(layout.VerticalOffsetMm, layout.Dpi);
        var showText = true;
        var textMetrics = GetNativeTextMetrics(barcode, layout.LabelHeightMm, layout.Dpi);
        var readableTextReserveDots = showText
            ? textMetrics.Height + MmToDots(layout.LabelHeightMm <= 12.5 ? 1 : 1.6, layout.Dpi)
            : 0;
        var minimumHeightDots = MmToDots(layout.LabelHeightMm <= 12.5 ? 5.8 : 8, layout.Dpi);
        var maximumHeightDots = MmToDots(layout.LabelHeightMm <= 25 ? 13 : 18, layout.Dpi);
        var availableBarcodeHeight = Math.Max(minimumHeightDots, labelHeightDots - readableTextReserveDots - MmToDots(1.2, layout.Dpi));
        var barcodeHeightDots = (int)Math.Clamp(availableBarcodeHeight, minimumHeightDots, maximumHeightDots);
        var barcodeImage = BuildNativeBarcodeImage(barcode, labelWidthDots);
        var x = (labelWidthDots - barcodeImage.Width) / 2 + horizontalOffsetDots;
        var y = slotTopDots + ((labelHeightDots - barcodeHeightDots - readableTextReserveDots) / 2) + verticalOffsetDots;
        var textX = (labelWidthDots - textMetrics.Width) / 2 + horizontalOffsetDots;
        var textY = y + barcodeHeightDots + MmToDots(0.6, layout.Dpi);

        x = Math.Clamp(x, 0, Math.Max(0, labelWidthDots - barcodeImage.Width));
        y = Math.Clamp(y, slotTopDots, slotTopDots + Math.Max(0, labelHeightDots - barcodeHeightDots - readableTextReserveDots));
        textX = Math.Clamp(textX, 0, Math.Max(0, labelWidthDots - textMetrics.Width));
        textY = Math.Clamp(textY, slotTopDots, slotTopDots + Math.Max(0, labelHeightDots - textMetrics.Height));
        var bars = barcodeImage.Bars
            .Select(bar => new NativeBarcodeBar(x + bar.X, y, bar.Width, barcodeHeightDots))
            .ToArray();

        return new NativeBarcodePlacement(
            x,
            y,
            barcodeHeightDots,
            textX,
            textY,
            textMetrics.TsplFont,
            textMetrics.ZplHeight,
            textMetrics.ZplWidth,
            showText,
            bars);
    }

    private static NativeBarcodeImage BuildNativeBarcodeImage(string barcode, int labelWidthDots)
    {
        var modules = BuildNativeBarcodeModules(barcode);
        var maxWidth = Math.Max(1, (int)Math.Floor(labelWidthDots * 0.94));
        var moduleWidth = Math.Max(1, maxWidth / modules.TotalModules);
        if (barcode.Length > 13 && modules.TotalModules * moduleWidth > labelWidthDots * 0.72)
        {
            moduleWidth = 1;
        }

        var imageWidth = modules.TotalModules * moduleWidth;
        var bars = modules.Bars
            .Select(bar => new NativeBarcodeBar(bar.StartModule * moduleWidth, 0, Math.Max(1, bar.WidthModules * moduleWidth), 0))
            .ToArray();
        return new NativeBarcodeImage(imageWidth, bars);
    }

    private static NativeBarcodeModules BuildNativeBarcodeModules(string barcode)
    {
        if (IsAsciiDigits(barcode) && barcode.Length == 13 && IsValidEan13(barcode))
        {
            return BuildBinaryBarcodeModules(BuildEan13Bits(barcode), 9);
        }

        return BuildCode128BarcodeModules(barcode, 10);
    }

    private static NativeBarcodeModules BuildBinaryBarcodeModules(string bits, int quietModules)
    {
        var bars = new List<NativeBarcodeModuleBar>();
        var cursor = quietModules;
        var index = 0;
        while (index < bits.Length)
        {
            var value = bits[index];
            var start = index;
            while (index < bits.Length && bits[index] == value)
            {
                index++;
            }

            if (value == '1')
            {
                bars.Add(new NativeBarcodeModuleBar(cursor + start, index - start));
            }
        }

        return new NativeBarcodeModules(bits.Length + (quietModules * 2), bars);
    }

    private static NativeBarcodeModules BuildCode128BarcodeModules(string barcode, int quietModules)
    {
        var patterns = BuildCode128Patterns(barcode);
        var bars = new List<NativeBarcodeModuleBar>();
        var cursor = quietModules;

        foreach (var pattern in patterns)
        {
            var drawBar = true;
            foreach (var digit in pattern)
            {
                var width = digit - '0';
                if (drawBar)
                {
                    bars.Add(new NativeBarcodeModuleBar(cursor, width));
                }

                cursor += width;
                drawBar = !drawBar;
            }
        }

        return new NativeBarcodeModules(cursor + quietModules, bars);
    }

    private static NativeTextMetrics GetNativeTextMetrics(string barcode, double labelHeightMm, int dpi)
    {
        var compact = labelHeightMm <= 12.5 || barcode.Length > 13;
        var tsplFont = compact ? "1" : "2";
        var charWidthDots = compact ? 8 : 12;
        var charHeightDots = compact ? 12 : 20;
        var zplHeightDots = compact ? MmToDots(1.8, dpi) : MmToDots(2.5, dpi);
        var zplWidthDots = Math.Max(5, (int)Math.Round(zplHeightDots * 0.62));
        var widthDots = barcode.Length * charWidthDots;
        return new NativeTextMetrics(widthDots, charHeightDots, tsplFont, zplHeightDots, zplWidthDots);
    }

    private static int EstimateCode128Modules(string value)
    {
        try
        {
            return BuildCode128Patterns(value).Sum(pattern => pattern.Sum(digit => digit - '0'));
        }
        catch
        {
            return 110;
        }
    }

    private static int MmToDots(double value, int dpi) =>
        (int)Math.Round(value / 25.4d * dpi, MidpointRounding.AwayFromZero);

    private static string FormatMm(double value) =>
        value.ToString("0.##", CultureInfo.InvariantCulture);

    private static string EscapeTspl(string value) =>
        (value ?? string.Empty).Replace("\"", string.Empty);

    private static string EscapeZpl(string value) =>
        (value ?? string.Empty).Replace("^", string.Empty).Replace("~", string.Empty);

    private static BarcodeLabelLayout GetLabelLayout(string labelSizeName, double printableAreaWidth, double horizontalOffsetMm)
    {
        var horizontalOffset = Mm(horizontalOffsetMm);

        if (labelSizeName is "38x25 mm - 2 stacked" or "38x25 mm - 2 per row")
        {
            var stackedLabelSize = new Size(Mm(38), Mm(12.5));
            return new BarcodeLabelLayout(
                new Size(GetLabelPageWidth(printableAreaWidth, stackedLabelSize.Width), Mm(25)),
                stackedLabelSize,
                2,
                true,
                true,
                horizontalOffset);
        }

        var labelSize = labelSizeName switch
        {
            "40x55 mm" => new Size(Mm(40), Mm(55)),
            "50x25 mm" => new Size(Mm(50), Mm(25)),
            "60x40 mm" => new Size(Mm(60), Mm(40)),
            "38x25 mm" => new Size(Mm(38), Mm(25)),
            _ => new Size(Mm(38), Mm(50))
        };

        return new BarcodeLabelLayout(
            new Size(GetLabelPageWidth(printableAreaWidth, labelSize.Width), labelSize.Height),
            labelSize,
            1,
            false,
            false,
            horizontalOffset);
    }

    private static double GetLabelPageWidth(double printableAreaWidth, double labelWidth)
    {
        if (printableAreaWidth > labelWidth && printableAreaWidth <= Mm(65))
        {
            return printableAreaWidth;
        }

        return Math.Max(labelWidth, LabelPrinterMediaWidth);
    }

    private sealed record BarcodeLabelLayout(
        Size PageSize,
        Size LabelSize,
        int LabelsPerPage,
        bool StackVertically,
        bool FillAllSlotsPerRequestedLabel,
        double HorizontalOffset);

    private sealed record ReceiptPrintLayout(
        double PageWidth,
        double BodyWidth,
        double BodyLeft,
        double Padding,
        bool Compact);

    private enum BarcodePrinterProfile
    {
        WindowsDriver,
        Tspl,
        Zpl
    }

    private sealed record NativeBarcodeLabelLayout(
        double PageWidthMm,
        double PageHeightMm,
        double LabelWidthMm,
        double LabelHeightMm,
        int LabelsPerPage,
        bool StackVertically,
        bool FillAllSlotsPerRequestedLabel,
        double GapMm,
        double HorizontalOffsetMm,
        double VerticalOffsetMm,
        int Dpi);

    private sealed record NativeBarcodePlacement(
        int X,
        int Y,
        int Height,
        int TextX,
        int TextY,
        string TextFont,
        int TextHeight,
        int TextWidth,
        bool ShowText,
        IReadOnlyList<NativeBarcodeBar> Bars);

    private sealed record NativeBarcodeImage(
        int Width,
        IReadOnlyList<NativeBarcodeBar> Bars);

    private sealed record NativeBarcodeBar(
        int X,
        int Y,
        int Width,
        int Height);

    private sealed record NativeBarcodeModules(
        int TotalModules,
        IReadOnlyList<NativeBarcodeModuleBar> Bars);

    private sealed record NativeBarcodeModuleBar(
        int StartModule,
        int WidthModules);

    private sealed record NativeBarcodeSpec(
        string Type,
        string Content,
        string HumanReadable,
        int ModuleCount);

    private sealed record NativeTextMetrics(
        int Width,
        int Height,
        string TsplFont,
        int ZplHeight,
        int ZplWidth);

    private static double Cm(double value) => value / 2.54d * 96d;

    private static double Mm(double value) => Cm(value / 10d);

    private static Border BuildReceiptVisual(ReceiptDto receipt, PrinterSettingsDto settings, ReceiptPrintLayout layout)
    {
        var isCompact = layout.Compact;
        var padding = layout.Padding;
        var receiptWidth = layout.BodyWidth;
        var contentWidth = Math.Max(120, receiptWidth - (padding * 2));
        var root = new StackPanel
        {
            Width = contentWidth,
            FlowDirection = FlowDirection.RightToLeft
        };

        root.Children.Add(Text(settings.StoreName, isCompact ? 20 : 28, FontWeights.Bold, TextAlignment.Center, margin: new Thickness(0)));
        root.Children.Add(Text(settings.ReceiptTitle, isCompact ? 12 : 17, FontWeights.Normal, TextAlignment.Center, Brushes.DimGray, new Thickness(0, 2, 0, isCompact ? 8 : 14)));

        root.Children.Add(InfoRow("التاريخ", receipt.CreatedAt.LocalDateTime.ToString("dd-MM-yyyy hh:mm tt", CultureInfo.GetCultureInfo("ar-EG")), true, isCompact));
        root.Children.Add(InfoRow("الكاشير", receipt.CashierName, compact: isCompact));
        root.Children.Add(InfoRow("رقم الفاتورة", receipt.InvoiceNumber, true, isCompact));
        root.Children.Add(InfoRow("عدد العملاء", "1", true, isCompact));

        root.Children.Add(ItemsTable(receipt, layout));
        if (receipt.ReturnedAmount > 0)
        {
            root.Children.Add(SummaryRow("قبل المرتجع", receipt.OriginalTotalAmount, new Thickness(0, isCompact ? 8 : 14, 0, isCompact ? 3 : 6), isCompact));
            root.Children.Add(SummaryRow("المرتجعات", receipt.ReturnedAmount, new Thickness(0, 0, 0, isCompact ? 3 : 6), isCompact));
            root.Children.Add(SummaryRow("صافي المبيعات", receipt.TotalAmount, new Thickness(0, 0, 0, isCompact ? 3 : 6), isCompact));
        }
        else
        {
            root.Children.Add(SummaryRow("صافي المبيعات", receipt.TotalAmount, new Thickness(0, isCompact ? 8 : 14, 0, isCompact ? 3 : 6), isCompact));
        }

        root.Children.Add(SummaryRow("الخصم", receipt.DiscountAmount, new Thickness(0, 0, 0, isCompact ? 3 : 6), isCompact));
        root.Children.Add(TotalDue(receipt.NetAmount, isCompact));
        root.Children.Add(Text(settings.ReceiptFooter, isCompact ? 12 : 14, FontWeights.Normal, TextAlignment.Center, Brushes.DimGray, new Thickness(0, isCompact ? 8 : 14, 0, 0)));

        return new Border
        {
            Width = receiptWidth,
            Padding = new Thickness(padding),
            Background = Brushes.White,
            Child = root
        };
    }

    private static TextBlock Text(
        string text,
        double fontSize,
        FontWeight weight,
        TextAlignment alignment,
        Brush? foreground = null,
        Thickness? margin = null)
    {
        return new TextBlock
        {
            Text = text,
            FontFamily = new FontFamily("Tahoma, Arial, Cairo, Tajawal, Segoe UI"),
            FontSize = fontSize,
            FontWeight = weight,
            Foreground = foreground ?? Brushes.Black,
            TextAlignment = alignment,
            TextWrapping = TextWrapping.Wrap,
            Margin = margin ?? new Thickness(0, 2, 0, 2)
        };
    }

    private static Grid InfoRow(string label, string value, bool leftToRight = false, bool compact = false)
    {
        var fontSize = compact ? 10.8 : 13.8;
        var grid = new Grid
        {
            Margin = new Thickness(0, 0, 0, compact ? 3 : 5),
            FlowDirection = FlowDirection.LeftToRight
        };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(compact ? 8 : 10) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(compact ? 68 : 88) });

        var labelBlock = Text(label, fontSize, FontWeights.Normal, TextAlignment.Right, Brushes.Black, new Thickness(0));
        labelBlock.FlowDirection = FlowDirection.RightToLeft;
        labelBlock.TextWrapping = TextWrapping.NoWrap;
        Grid.SetColumn(labelBlock, 2);

        var valueFontSize = value.Length > 17
            ? compact ? 9.4 : 12.4
            : fontSize;
        var valueBlock = Text(value, valueFontSize, FontWeights.Bold, TextAlignment.Left, margin: new Thickness(2, 0, 0, 0));
        valueBlock.FlowDirection = leftToRight ? FlowDirection.LeftToRight : FlowDirection.RightToLeft;
        valueBlock.TextWrapping = TextWrapping.NoWrap;
        valueBlock.TextTrimming = TextTrimming.None;
        Grid.SetColumn(valueBlock, 0);

        grid.Children.Add(labelBlock);
        grid.Children.Add(valueBlock);
        return grid;
    }

    private static Grid ItemsTable(ReceiptDto receipt, ReceiptPrintLayout layout)
    {
        var compact = layout.Compact;
        var grid = new Grid
        {
            Margin = new Thickness(0, 12, 0, 0),
            FlowDirection = FlowDirection.LeftToRight
        };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(compact ? 42 : 60) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(compact ? 30 : 40) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(compact ? 40 : 54) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        AddItemRow(grid, 0, "الكمية", "السعر", "الصنف", "الإجمالي", true, compact);

        var rowIndex = 1;
        foreach (var line in receipt.Lines)
        {
            AddItemRow(
                grid,
                rowIndex++,
                line.Quantity.ToString(CultureInfo.InvariantCulture),
                $"{line.UnitPrice:0.00}",
                line.ItemName,
                $"{line.TotalPrice:0.00}",
                false,
                compact);
        }

        return grid;
    }

    private static void AddItemRow(Grid grid, int rowIndex, string quantity, string price, string item, string total, bool header, bool compact)
    {
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        AddCell(grid, total, rowIndex, 0, header, TextAlignment.Center, FlowDirection.LeftToRight, compact, false);
        AddCell(grid, quantity, rowIndex, 1, header, TextAlignment.Center, FlowDirection.LeftToRight, compact, false);
        AddCell(grid, price, rowIndex, 2, header, TextAlignment.Center, FlowDirection.LeftToRight, compact, false);
        AddCell(grid, item, rowIndex, 3, header, TextAlignment.Right, FlowDirection.RightToLeft, compact, !header);
    }

    private static void AddCell(Grid grid, string text, int row, int column, bool header, TextAlignment alignment, FlowDirection flowDirection, bool compact, bool canWrap)
    {
        var fontSize = compact
            ? header ? 8.8 : 10.8
            : header ? 10.8 : 13.2;
        var block = Text(text, fontSize, header ? FontWeights.Bold : FontWeights.Normal, alignment, margin: new Thickness(0));
        block.FlowDirection = header ? FlowDirection.RightToLeft : flowDirection;
        block.LineHeight = compact ? 14 : 18;
        block.TextWrapping = canWrap ? TextWrapping.Wrap : TextWrapping.NoWrap;
        block.TextTrimming = TextTrimming.None;

        var verticalPadding = compact ? 2 : 4;
        var headerBottomPadding = compact ? 3 : 5;
        var horizontalPadding = compact ? 1 : 3;

        var border = new Border
        {
            BorderBrush = Brushes.Gray,
            BorderThickness = header ? new Thickness(0, 0, 0, 1) : new Thickness(0),
            Padding = column == 3
                ? new Thickness(horizontalPadding, header ? 0 : verticalPadding, horizontalPadding, header ? headerBottomPadding : verticalPadding)
                : new Thickness(0, header ? 0 : verticalPadding, 0, header ? headerBottomPadding : verticalPadding),
            Child = block
        };

        Grid.SetRow(border, row);
        Grid.SetColumn(border, column);
        grid.Children.Add(border);
    }

    private static Grid SummaryRow(string label, decimal amount, Thickness margin, bool compact = false)
    {
        var fontSize = compact ? 12.2 : 15;
        var grid = new Grid
        {
            Margin = margin,
            FlowDirection = FlowDirection.LeftToRight
        };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(compact ? 72 : 96) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var labelBlock = Text(label, fontSize, FontWeights.Normal, TextAlignment.Right, margin: new Thickness(0));
        labelBlock.FlowDirection = FlowDirection.RightToLeft;
        labelBlock.TextWrapping = TextWrapping.NoWrap;
        Grid.SetColumn(labelBlock, 1);

        var amountBlock = Text($"{amount:0.00}", fontSize, FontWeights.Normal, TextAlignment.Left, margin: new Thickness(4, 0, 0, 0));
        amountBlock.FlowDirection = FlowDirection.LeftToRight;
        amountBlock.TextWrapping = TextWrapping.NoWrap;
        amountBlock.TextTrimming = TextTrimming.None;
        Grid.SetColumn(amountBlock, 0);

        grid.Children.Add(labelBlock);
        grid.Children.Add(amountBlock);
        return grid;
    }

    private static Grid TotalDue(decimal amount, bool compact = false)
    {
        var grid = new Grid
        {
            Margin = new Thickness(0, compact ? 8 : 14, 0, 0),
            FlowDirection = FlowDirection.LeftToRight
        };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(compact ? 100 : 132) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var label = Text("الإجمالي", compact ? 17 : 24, FontWeights.Bold, TextAlignment.Right, margin: new Thickness(0, compact ? 6 : 10, 0, 0));
        label.FlowDirection = FlowDirection.RightToLeft;
        label.TextWrapping = TextWrapping.NoWrap;
        Grid.SetColumn(label, 1);

        var value = Text($"{amount:0.00}", compact ? 26 : 32, FontWeights.Bold, TextAlignment.Left, margin: new Thickness(4, compact ? 2 : 8, 0, 0));
        value.FlowDirection = FlowDirection.LeftToRight;
        value.TextWrapping = TextWrapping.NoWrap;
        value.TextTrimming = TextTrimming.None;
        Grid.SetColumn(value, 0);

        var border = new Border
        {
            BorderBrush = Brushes.Gray,
            BorderThickness = new Thickness(0, 1, 0, 0),
            Child = grid
        };

        grid.Children.Add(label);
        grid.Children.Add(value);

        var wrapper = new Grid();
        wrapper.Children.Add(border);
        return wrapper;
    }
}
