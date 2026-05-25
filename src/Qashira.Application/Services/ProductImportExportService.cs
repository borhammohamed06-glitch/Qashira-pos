using System.Globalization;
using System.Text;
using ClosedXML.Excel;
using ExcelDataReader;
using Qashira.Application.Abstractions;
using Qashira.Application.DTOs;
using Qashira.Application.Permissions;
using Qashira.Domain.Entities;
using Qashira.Domain.Enums;
using Qashira.Shared.Arabic;
using Qashira.Shared.Results;
using Microsoft.EntityFrameworkCore;

namespace Qashira.Application.Services;

public sealed class ProductImportExportService(
    IApplicationDbContext dbContext,
    IPermissionService permissionService,
    IBackupStorage backupStorage,
    IBarcodeService barcodeService) : IProductImportExportService
{
    private static readonly string[] SupportedSpreadsheetExtensions = [".xlsx", ".xls", ".xlsm", ".xlsb"];
    private static readonly string[] SupportedDelimitedExtensions = [".csv", ".txt", ".tsv"];
    private static readonly ProductImportColumn[] RequiredColumns =
    [
        ProductImportColumn.Name,
        ProductImportColumn.Category,
        ProductImportColumn.PurchasePrice,
        ProductImportColumn.SalePrice,
        ProductImportColumn.StockQuantity
    ];

    public async Task<Result<ProductImportResultDto>> ImportProductsAsync(string productFilePath, int userId, CancellationToken cancellationToken = default)
    {
        permissionService.EnsureCurrentUserHas(PermissionCodes.CanEditProduct);
        permissionService.EnsureCurrentUserHas(PermissionCodes.CanEditPrice);
        permissionService.EnsureCurrentUserHas(PermissionCodes.CanManageStock);

        if (string.IsNullOrWhiteSpace(productFilePath) || !File.Exists(productFilePath))
        {
            return Result<ProductImportResultDto>.Failure("اختر ملف منتجات صحيح أولاً.");
        }

        var parsed = await ReadImportRowsAsync(productFilePath, cancellationToken);
        if (!parsed.Succeeded || parsed.Value is null)
        {
            return Result<ProductImportResultDto>.Failure(parsed.Message);
        }

        var existingProducts = await dbContext.Products
            .Include(x => x.Category)
            .ToListAsync(cancellationToken);
        var existingCategories = await dbContext.Categories
            .Where(x => x.IsActive)
            .ToListAsync(cancellationToken);
        var defaultLowStockThreshold = await GetDefaultLowStockThresholdAsync(cancellationToken);

        var validation = ValidateRows(
            parsed.Value,
            existingProducts,
            existingCategories,
            defaultLowStockThreshold,
            cancellationToken);

        if (validation.Rows.Count == 0)
        {
            return Result<ProductImportResultDto>.Success(
                new ProductImportResultDto(parsed.Value.Count, 0, 0, validation.RejectedRows.Count, string.Empty, validation.RejectedRows),
                BuildImportMessage(0, validation.RejectedRows.Count, string.Empty, validation.RejectedRows));
        }

        var safetyBackupPath = await backupStorage.CreateImportSafetyBackupAsync(cancellationToken);
        var now = DateTimeOffset.UtcNow;
        var createdProducts = new List<Product>();

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        foreach (var category in validation.NewCategories)
        {
            dbContext.Categories.Add(category);
        }

        foreach (var row in validation.Rows)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var product = new Product
            {
                Name = row.Name,
                SearchName = ArabicTextNormalizer.NormalizeForSearch(row.Name),
                Category = row.Category,
                CategoryId = row.Category?.Id > 0 ? row.Category.Id : null,
                ProductType = ProductType.NormalProduct,
                PurchasePrice = row.PurchasePrice,
                SalePrice = row.SalePrice,
                StockQuantity = row.StockQuantity,
                LowStockThreshold = defaultLowStockThreshold,
                IsActive = true,
                CreatedAt = now,
                Barcode = await GenerateBarcodeAsync(validation.UsedBarcodes, cancellationToken),
                InternalCode = GenerateInternalCode(validation.UsedInternalCodes)
            };

            dbContext.Products.Add(product);
            createdProducts.Add(product);
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        foreach (var product in createdProducts)
        {
            dbContext.AuditLogs.Add(new AuditLog
            {
                UserId = userId,
                Action = AuditAction.CreateProduct,
                EntityName = nameof(Product),
                EntityId = product.Id.ToString(),
                Description = $"تم إنشاء المنتج {product.Name} من ملف استيراد المنتجات.",
                CreatedAt = now
            });

            if (product.StockQuantity > 0)
            {
                dbContext.StockMovements.Add(new StockMovement
                {
                    ProductId = product.Id,
                    MovementType = StockMovementType.ManualIncrease,
                    Quantity = product.StockQuantity,
                    OldQuantity = 0,
                    NewQuantity = product.StockQuantity,
                    ReferenceType = "ProductFileImport",
                    UserId = userId,
                    CreatedAt = now
                });

                dbContext.AuditLogs.Add(new AuditLog
                {
                    UserId = userId,
                    Action = AuditAction.ChangeStockQuantity,
                    EntityName = nameof(Product),
                    EntityId = product.Id.ToString(),
                    Description = $"تم تسجيل مخزون افتتاحي للمنتج {product.Name} بكمية {product.StockQuantity} من ملف الاستيراد.",
                    CreatedAt = now
                });
            }

            await StockNotificationSynchronizer.SyncLowStockAsync(dbContext, product, cancellationToken);
        }

        dbContext.AuditLogs.Add(new AuditLog
        {
            UserId = userId,
            Action = AuditAction.ImportProducts,
            EntityName = nameof(Product),
            EntityId = Path.GetFileName(productFilePath),
            Description = $"تم استيراد ملف منتجات. جديد: {createdProducts.Count}، مرفوض: {validation.RejectedRows.Count}. نسخة الأمان: {Path.GetFileName(safetyBackupPath)}",
            CreatedAt = now
        });

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Result<ProductImportResultDto>.Success(
            new ProductImportResultDto(parsed.Value.Count, createdProducts.Count, 0, validation.RejectedRows.Count, safetyBackupPath, validation.RejectedRows),
            BuildImportMessage(createdProducts.Count, validation.RejectedRows.Count, safetyBackupPath, validation.RejectedRows));
    }

    public async Task<Result<ProductExportResultDto>> ExportProductsAsync(string exportPath, bool includeInactive, int userId, CancellationToken cancellationToken = default)
    {
        permissionService.EnsureCurrentUserHas(PermissionCodes.CanEditProduct);

        if (string.IsNullOrWhiteSpace(exportPath))
        {
            return Result<ProductExportResultDto>.Failure("اختر مكان حفظ ملف المنتجات أولاً.");
        }

        var products = await dbContext.Products
            .AsNoTracking()
            .Include(x => x.Category)
            .Where(x => (x.ProductType == ProductType.NormalProduct || x.ProductType == ProductType.PrintedProduct) &&
                (includeInactive || x.IsActive))
            .OrderBy(x => x.Name)
            .ToListAsync(cancellationToken);

        var xlsxPath = EnsureXlsxPath(exportPath);
        var directory = Path.GetDirectoryName(xlsxPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("المنتجات");
        worksheet.RightToLeft = true;

        var headers = new[] { "الاسم", "التصنيف", "سعر الشراء", "سعر البيع", "المخزون" };
        for (var column = 0; column < headers.Length; column++)
        {
            var cell = worksheet.Cell(1, column + 1);
            cell.Value = headers[column];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#E7F3EF");
            cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        }

        var row = 2;
        foreach (var product in products)
        {
            cancellationToken.ThrowIfCancellationRequested();

            worksheet.Cell(row, 1).Value = product.Name;
            worksheet.Cell(row, 2).Value = product.Category?.Name ?? string.Empty;
            worksheet.Cell(row, 3).Value = product.PurchasePrice;
            worksheet.Cell(row, 4).Value = product.SalePrice;
            worksheet.Cell(row, 5).Value = product.StockQuantity;
            row++;
        }

        worksheet.Column(1).Width = 34;
        worksheet.Column(2).Width = 24;
        worksheet.Column(3).Width = 14;
        worksheet.Column(4).Width = 14;
        worksheet.Column(5).Width = 12;
        worksheet.Columns(3, 4).Style.NumberFormat.Format = "0.00";
        worksheet.Column(5).Style.NumberFormat.Format = "0.###";
        worksheet.Range(1, 1, Math.Max(1, row - 1), headers.Length).Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        worksheet.Range(1, 1, Math.Max(1, row - 1), headers.Length).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
        worksheet.SheetView.FreezeRows(1);

        workbook.SaveAs(xlsxPath);

        dbContext.AuditLogs.Add(new AuditLog
        {
            UserId = userId,
            Action = AuditAction.ExportProducts,
            EntityName = nameof(Product),
            EntityId = xlsxPath,
            Description = $"تم تصدير {products.Count} منتج إلى ملف Excel.",
            CreatedAt = DateTimeOffset.UtcNow
        });
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result<ProductExportResultDto>.Success(
            new ProductExportResultDto(xlsxPath, products.Count),
            $"تم تصدير {products.Count} منتج بنجاح: {xlsxPath}");
    }

    private async Task<Result<List<RawImportRow>>> ReadImportRowsAsync(string filePath, CancellationToken cancellationToken)
    {
        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        try
        {
            if (SupportedSpreadsheetExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
            {
                return await ReadSpreadsheetRowsAsync(filePath, cancellationToken);
            }

            if (SupportedDelimitedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
            {
                return await ReadDelimitedRowsAsync(filePath, cancellationToken);
            }

            var spreadsheetResult = await ReadSpreadsheetRowsAsync(filePath, cancellationToken);
            if (spreadsheetResult.Succeeded)
            {
                return spreadsheetResult;
            }

            return await ReadDelimitedRowsAsync(filePath, cancellationToken);
        }
        catch (IOException)
        {
            return Result<List<RawImportRow>>.Failure("تعذر قراءة الملف. تأكد أن الملف غير مفتوح في Excel ثم حاول مرة أخرى.");
        }
        catch (UnauthorizedAccessException)
        {
            return Result<List<RawImportRow>>.Failure("لا توجد صلاحية لقراءة ملف المنتجات المختار.");
        }
        catch
        {
            return Result<List<RawImportRow>>.Failure("تعذر قراءة ملف المنتجات. استخدم ملف Excel يحتوي على الأعمدة: الاسم، التصنيف، سعر الشراء، سعر البيع، المخزون.");
        }
    }

    private static async Task<Result<List<RawImportRow>>> ReadSpreadsheetRowsAsync(string filePath, CancellationToken cancellationToken)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        return await Task.Run(() =>
        {
            using var stream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = ExcelReaderFactory.CreateReader(stream);
            var lastFailure = "لم يتم العثور على جدول منتجات صالح داخل ملف Excel.";

            do
            {
                cancellationToken.ThrowIfCancellationRequested();
                var sheetRows = new List<IReadOnlyList<string>>();
                while (reader.Read())
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var values = new string[reader.FieldCount];
                    for (var index = 0; index < reader.FieldCount; index++)
                    {
                        values[index] = ConvertCellToText(reader.GetValue(index));
                    }

                    if (values.Any(x => !string.IsNullOrWhiteSpace(x)))
                    {
                        sheetRows.Add(values);
                    }
                }

                var sheetResult = BuildRowsFromGrid(sheetRows);
                if (sheetResult.Succeeded)
                {
                    return sheetResult;
                }

                lastFailure = sheetResult.Message;
            }
            while (reader.NextResult());

            return Result<List<RawImportRow>>.Failure(lastFailure);
        }, cancellationToken);
    }

    private static async Task<Result<List<RawImportRow>>> ReadDelimitedRowsAsync(string filePath, CancellationToken cancellationToken)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var text = await ReadDelimitedTextAsync(filePath, cancellationToken);
        using var reader = new StringReader(text);

        var headerLine = reader.ReadLine();
        if (string.IsNullOrWhiteSpace(headerLine))
        {
            return Result<List<RawImportRow>>.Failure("ملف المنتجات فارغ.");
        }

        var delimiter = DetectDelimiter(headerLine);
        if (headerLine.StartsWith("sep=", StringComparison.OrdinalIgnoreCase))
        {
            delimiter = headerLine.Length >= 5 ? headerLine[4] : ',';
            headerLine = reader.ReadLine();
            if (string.IsNullOrWhiteSpace(headerLine))
            {
                return Result<List<RawImportRow>>.Failure("ملف المنتجات لا يحتوي على صف عناوين.");
            }
        }

        var grid = new List<IReadOnlyList<string>> { ParseDelimitedLine(headerLine, delimiter) };
        while (reader.ReadLine() is { } line)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!string.IsNullOrWhiteSpace(line))
            {
                grid.Add(ParseDelimitedLine(line, delimiter));
            }
        }

        return BuildRowsFromGrid(grid);
    }

    private static async Task<string> ReadDelimitedTextAsync(string filePath, CancellationToken cancellationToken)
    {
        var bytes = await File.ReadAllBytesAsync(filePath, cancellationToken);
        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
        {
            return Encoding.UTF8.GetString(bytes);
        }

        try
        {
            return new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true).GetString(bytes);
        }
        catch (DecoderFallbackException)
        {
            return Encoding.GetEncoding(1256).GetString(bytes);
        }
    }

    private static Result<List<RawImportRow>> BuildRowsFromGrid(IReadOnlyList<IReadOnlyList<string>> grid)
    {
        if (grid.Count == 0)
        {
            return Result<List<RawImportRow>>.Failure("ملف المنتجات فارغ.");
        }

        var headerIndex = -1;
        Dictionary<ProductImportColumn, int>? headerMap = null;
        for (var index = 0; index < grid.Count; index++)
        {
            var map = BuildHeaderMap(grid[index]);
            if (HasRequiredColumns(map))
            {
                headerIndex = index;
                headerMap = map;
                break;
            }
        }

        if (headerIndex < 0 || headerMap is null)
        {
            var firstText = string.Join(" ", grid.Take(5).SelectMany(x => x));
            if (LooksCorruptedText(firstText))
            {
                return Result<List<RawImportRow>>.Failure("ملف المنتجات تالف: النص العربي تحول إلى علامات استفهام. صدّر ملف Excel جديد من البرنامج أو احفظ الملف بصيغة Excel صحيحة.");
            }

            return Result<List<RawImportRow>>.Failure("ملف المنتجات يجب أن يحتوي على الأعمدة: الاسم، التصنيف، سعر الشراء، سعر البيع، المخزون.");
        }

        var rows = new List<RawImportRow>();
        for (var index = headerIndex + 1; index < grid.Count; index++)
        {
            var fields = grid[index];
            if (fields.All(string.IsNullOrWhiteSpace))
            {
                continue;
            }

            rows.Add(new RawImportRow(
                index + 1,
                GetField(fields, headerMap, ProductImportColumn.Name),
                GetField(fields, headerMap, ProductImportColumn.Category),
                GetField(fields, headerMap, ProductImportColumn.PurchasePrice),
                GetField(fields, headerMap, ProductImportColumn.SalePrice),
                GetField(fields, headerMap, ProductImportColumn.StockQuantity)));
        }

        return rows.Count == 0
            ? Result<List<RawImportRow>>.Failure("لا توجد منتجات داخل الملف.")
            : Result<List<RawImportRow>>.Success(rows);
    }

    private ImportValidationResult ValidateRows(
        IReadOnlyList<RawImportRow> rows,
        IReadOnlyList<Product> existingProducts,
        IReadOnlyList<Category> existingCategories,
        int defaultLowStockThreshold,
        CancellationToken cancellationToken)
    {
        var rejectedRows = new List<string>();
        var validRows = new List<ValidatedImportRow>();
        var usedBarcodes = existingProducts
            .Select(x => x.Barcode)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var usedInternalCodes = existingProducts
            .Select(x => x.InternalCode)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var fileNames = new HashSet<string>(StringComparer.Ordinal);

        var productsBySearchName = existingProducts
            .GroupBy(x => ProductSearchName(x), StringComparer.Ordinal)
            .ToDictionary(x => x.Key, x => x.First(), StringComparer.Ordinal);

        var categoriesBySearchName = existingCategories
            .GroupBy(x => x.SearchName, StringComparer.Ordinal)
            .ToDictionary(x => x.Key, x => x.First(), StringComparer.Ordinal);

        var newCategories = new List<Category>();
        foreach (var row in rows)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var rowErrors = new List<string>();
            var name = CleanImportText(row.Name);
            if (string.IsNullOrWhiteSpace(name))
            {
                rowErrors.Add("اسم المنتج مطلوب");
            }
            else if (LooksCorruptedText(name))
            {
                rowErrors.Add("اسم المنتج تالف لأن النص العربي تحول إلى علامات استفهام");
            }

            var categoryName = CleanImportText(row.Category);
            if (string.IsNullOrWhiteSpace(categoryName))
            {
                rowErrors.Add("التصنيف مطلوب");
            }
            else if (LooksCorruptedText(categoryName))
            {
                rowErrors.Add("اسم التصنيف تالف لأن النص العربي تحول إلى علامات استفهام");
            }

            if (!TryParseMoney(CleanImportText(row.PurchasePrice), out var purchasePrice) || purchasePrice < 0)
            {
                rowErrors.Add("سعر الشراء غير صحيح");
            }

            if (!TryParseMoney(CleanImportText(row.SalePrice), out var salePrice) || salePrice < 0)
            {
                rowErrors.Add("سعر البيع غير صحيح");
            }

            var stockQuantity = 0m;
            var stockText = CleanImportText(row.StockQuantity);
            if (string.IsNullOrWhiteSpace(stockText) || !TryParseNonNegativeDecimal(stockText, out stockQuantity))
            {
                rowErrors.Add("كمية المخزون غير صحيحة");
            }

            var searchName = ArabicTextNormalizer.NormalizeForSearch(name);
            if (rowErrors.Count > 0)
            {
                rejectedRows.Add($"صف {row.RowNumber}: {string.Join("، ", rowErrors)}.");
                continue;
            }

            if (!string.IsNullOrWhiteSpace(searchName))
            {
                if (productsBySearchName.TryGetValue(searchName, out var existingProduct))
                {
                    rejectedRows.Add($"صف {row.RowNumber}: لم يتم استيراد {name} لأن المنتج موجود بالفعل بنفس الاسم ({existingProduct.Name}).");
                    continue;
                }

                if (!fileNames.Add(searchName))
                {
                    rejectedRows.Add($"صف {row.RowNumber}: لم يتم استيراد {name} لأن الاسم مكرر داخل الملف.");
                    continue;
                }
            }

            Category? category = null;
            if (!string.IsNullOrWhiteSpace(categoryName))
            {
                var categorySearchName = ArabicTextNormalizer.NormalizeForSearch(categoryName);
                if (!categoriesBySearchName.TryGetValue(categorySearchName, out category))
                {
                    category = new Category
                    {
                        Name = categoryName,
                        SearchName = categorySearchName,
                        IsActive = true,
                        CreatedAt = DateTimeOffset.UtcNow
                    };
                    categoriesBySearchName[categorySearchName] = category;
                    newCategories.Add(category);
                }
            }

            validRows.Add(new ValidatedImportRow(
                row.RowNumber,
                name,
                category,
                purchasePrice,
                salePrice,
                stockQuantity,
                defaultLowStockThreshold));
        }

        return new ImportValidationResult(validRows, newCategories, rejectedRows, usedBarcodes, usedInternalCodes);
    }

    private async Task<int> GetDefaultLowStockThresholdAsync(CancellationToken cancellationToken)
    {
        var value = await dbContext.AppSettings
            .AsNoTracking()
            .Where(x => x.Key == "DefaultLowStockThreshold")
            .Select(x => x.Value)
            .SingleOrDefaultAsync(cancellationToken);

        return int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var threshold) && threshold >= 0
            ? threshold
            : 3;
    }

    private static Dictionary<ProductImportColumn, int> BuildHeaderMap(IReadOnlyList<string> headers)
    {
        var map = new Dictionary<ProductImportColumn, int>();
        for (var index = 0; index < headers.Count; index++)
        {
            var normalized = NormalizeHeader(headers[index]);
            var column = normalized switch
            {
                "name" or "productname" or "item" or "itemname" or "اسم" or "الاسم" or "اسمالمنتج" or "المنتج" or "الصنف" => ProductImportColumn.Name,
                "category" or "categoryname" or "department" or "section" or "التصنيف" or "اسمالتصنيف" or "القسم" or "الفئة" => ProductImportColumn.Category,
                "purchaseprice" or "purchase" or "cost" or "costprice" or "buyprice" or "سعرالشراء" or "سعرالتكلفه" or "سعرالتكلفة" or "التكلفه" or "التكلفة" => ProductImportColumn.PurchasePrice,
                "saleprice" or "sellingprice" or "sellprice" or "price" or "سعرالبيع" or "سعرالبيعالمستهلك" or "السعر" => ProductImportColumn.SalePrice,
                "stockquantity" or "stock" or "quantity" or "qty" or "المخزون" or "الكميه" or "الكمية" or "كميةالمخزون" => ProductImportColumn.StockQuantity,
                _ => ProductImportColumn.Unknown
            };

            if (column != ProductImportColumn.Unknown && !map.ContainsKey(column))
            {
                map[column] = index;
            }
        }

        return map;
    }

    private static bool HasRequiredColumns(IReadOnlyDictionary<ProductImportColumn, int> map) =>
        RequiredColumns.All(map.ContainsKey);

    private static string GetField(IReadOnlyList<string> fields, IReadOnlyDictionary<ProductImportColumn, int> map, ProductImportColumn column) =>
        map.TryGetValue(column, out var index) && index < fields.Count ? fields[index].Trim() : string.Empty;

    private static char DetectDelimiter(string line)
    {
        var candidates = new[] { ',', ';', '\t' };
        return candidates
            .Select(delimiter => new { Delimiter = delimiter, Count = CountDelimiterOutsideQuotes(line, delimiter) })
            .OrderByDescending(x => x.Count)
            .First().Delimiter;
    }

    private static int CountDelimiterOutsideQuotes(string line, char delimiter)
    {
        var count = 0;
        var inQuotes = false;
        for (var index = 0; index < line.Length; index++)
        {
            var ch = line[index];
            if (ch == '"')
            {
                if (inQuotes && index + 1 < line.Length && line[index + 1] == '"')
                {
                    index++;
                    continue;
                }

                inQuotes = !inQuotes;
                continue;
            }

            if (ch == delimiter && !inQuotes)
            {
                count++;
            }
        }

        return count;
    }

    private static List<string> ParseDelimitedLine(string line, char delimiter)
    {
        var fields = new List<string>();
        var current = new StringBuilder();
        var inQuotes = false;

        for (var index = 0; index < line.Length; index++)
        {
            var ch = line[index];
            if (ch == '"')
            {
                if (inQuotes && index + 1 < line.Length && line[index + 1] == '"')
                {
                    current.Append('"');
                    index++;
                    continue;
                }

                inQuotes = !inQuotes;
                continue;
            }

            if (ch == delimiter && !inQuotes)
            {
                fields.Add(current.ToString());
                current.Clear();
                continue;
            }

            current.Append(ch);
        }

        fields.Add(current.ToString());
        return fields;
    }

    private static string CleanImportText(string? value)
    {
        var cleaned = (value ?? string.Empty).Trim().TrimStart('\uFEFF');
        if (cleaned.StartsWith("=\"", StringComparison.Ordinal) &&
            cleaned.EndsWith("\"", StringComparison.Ordinal) &&
            cleaned.Length >= 3)
        {
            cleaned = cleaned[2..^1].Replace("\"\"", "\"");
        }

        if (cleaned.StartsWith('\''))
        {
            cleaned = cleaned[1..];
        }

        return cleaned.Trim();
    }

    private static string ConvertCellToText(object? value)
    {
        return value switch
        {
            null => string.Empty,
            DateTime date => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            double number => FormatNumberCell(number),
            float number => FormatNumberCell(number),
            decimal number => number.ToString("0.##########", CultureInfo.InvariantCulture),
            int number => number.ToString(CultureInfo.InvariantCulture),
            long number => number.ToString(CultureInfo.InvariantCulture),
            _ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty
        };
    }

    private static string FormatNumberCell(double value) =>
        Math.Abs(value % 1) < 0.0000001
            ? value.ToString("0", CultureInfo.InvariantCulture)
            : value.ToString("0.##########", CultureInfo.InvariantCulture);

    private static bool LooksCorruptedText(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || !value.Contains("??", StringComparison.Ordinal))
        {
            return false;
        }

        var hasArabicLetters = value.Any(ch => ch is >= '\u0600' and <= '\u06FF');
        var questionMarkCount = value.Count(ch => ch == '?');
        return !hasArabicLetters && questionMarkCount >= Math.Max(3, value.Length / 3);
    }

    private async Task<string> GenerateBarcodeAsync(HashSet<string> usedBarcodes, CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 50; attempt++)
        {
            var barcode = await barcodeService.GenerateUniqueBarcodeAsync(cancellationToken);
            if (usedBarcodes.Add(barcode))
            {
                return barcode;
            }
        }

        throw new InvalidOperationException("تعذر إنشاء باركود فريد أثناء الاستيراد.");
    }

    private static string GenerateInternalCode(HashSet<string> usedInternalCodes)
    {
        for (var index = usedInternalCodes.Count + 1; index < usedInternalCodes.Count + 10000; index++)
        {
            var candidate = $"PRD-{index:000000}";
            if (usedInternalCodes.Add(candidate))
            {
                return candidate;
            }
        }

        throw new InvalidOperationException("تعذر إنشاء كود داخلي فريد أثناء الاستيراد.");
    }

    private static bool TryParseMoney(string value, out decimal amount)
    {
        return decimal.TryParse(
            NormalizeDecimalText(value),
            NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign,
            CultureInfo.InvariantCulture,
            out amount);
    }

    private static bool TryParseNonNegativeDecimal(string value, out decimal number)
    {
        number = 0;
        if (!decimal.TryParse(
                NormalizeDecimalText(value),
                NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign,
                CultureInfo.InvariantCulture,
                out var amount) ||
            amount < 0)
        {
            return false;
        }

        number = amount;
        return true;
    }

    private static string NormalizeDecimalText(string? value)
    {
        var normalized = NormalizeNumericText(value);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return string.Empty;
        }

        var lastComma = normalized.LastIndexOf(',');
        var lastDot = normalized.LastIndexOf('.');

        if (lastComma >= 0 && lastDot >= 0)
        {
            return lastComma > lastDot
                ? normalized.Replace(".", string.Empty).Replace(',', '.')
                : normalized.Replace(",", string.Empty);
        }

        if (lastComma >= 0)
        {
            var digitsAfterComma = normalized.Length - lastComma - 1;
            var digitsBeforeComma = normalized[..lastComma].Count(char.IsDigit);
            return digitsAfterComma == 3 && digitsBeforeComma > 1
                ? normalized.Replace(",", string.Empty)
                : normalized.Replace(',', '.');
        }

        return normalized;
    }

    private static string NormalizeNumericText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return new string(value
            .Trim()
            .Select(ch => ch switch
            {
                >= '\u0660' and <= '\u0669' => (char)('0' + ch - '\u0660'),
                >= '\u06F0' and <= '\u06F9' => (char)('0' + ch - '\u06F0'),
                '\u066B' => '.',
                '\u066C' => ',',
                _ => ch
            })
            .Where(ch => char.IsDigit(ch) || ch is '.' or ',' or '-' or '+')
            .ToArray());
    }

    private static string NormalizeHeader(string value)
    {
        var normalized = ArabicTextNormalizer.NormalizeForSearch(CleanImportText(value))
            .ToLowerInvariant();

        return new string(normalized.Where(char.IsLetterOrDigit).ToArray());
    }

    private static string ProductSearchName(Product product) =>
        string.IsNullOrWhiteSpace(product.SearchName)
            ? ArabicTextNormalizer.NormalizeForSearch(product.Name)
            : product.SearchName;

    private static string EnsureXlsxPath(string exportPath) =>
        string.Equals(Path.GetExtension(exportPath), ".xlsx", StringComparison.OrdinalIgnoreCase)
            ? exportPath
            : Path.ChangeExtension(exportPath, ".xlsx");

    private static string BuildImportMessage(int createdCount, int rejectedCount, string safetyBackupPath, IReadOnlyList<string> rejectedRows)
    {
        var message = createdCount > 0
            ? $"تم استيراد {createdCount} منتج جديد. لم يتم استيراد {rejectedCount} صف."
            : $"لم يتم استيراد منتجات جديدة. عدد الصفوف غير المستوردة: {rejectedCount}.";

        if (!string.IsNullOrWhiteSpace(safetyBackupPath))
        {
            message += " تم إنشاء نسخة أمان قبل الاستيراد.";
        }

        if (rejectedRows.Count > 0)
        {
            var visibleRows = string.Join(Environment.NewLine, rejectedRows.Take(8));
            var suffix = rejectedRows.Count > 8 ? $"{Environment.NewLine}ويوجد {rejectedRows.Count - 8} صفوف أخرى غير مستوردة." : string.Empty;
            message += $"{Environment.NewLine}{visibleRows}{suffix}";
        }

        return message;
    }

    private enum ProductImportColumn
    {
        Unknown,
        Name,
        Category,
        PurchasePrice,
        SalePrice,
        StockQuantity
    }

    private sealed record RawImportRow(
        int RowNumber,
        string Name,
        string Category,
        string PurchasePrice,
        string SalePrice,
        string StockQuantity);

    private sealed record ValidatedImportRow(
        int RowNumber,
        string Name,
        Category? Category,
        decimal PurchasePrice,
        decimal SalePrice,
        decimal StockQuantity,
        int LowStockThreshold);

    private sealed record ImportValidationResult(
        IReadOnlyList<ValidatedImportRow> Rows,
        IReadOnlyList<Category> NewCategories,
        IReadOnlyList<string> RejectedRows,
        HashSet<string> UsedBarcodes,
        HashSet<string> UsedInternalCodes);
}
