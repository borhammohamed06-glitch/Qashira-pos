using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Qashira.Application.Abstractions;
using Qashira.Application.DTOs;
using Qashira.Application.Services;
using Qashira.Domain.Entities;
using Qashira.Domain.Enums;
using Qashira.Infrastructure.Database;
using Xunit;

namespace Qashira.Application.Tests;

public sealed class SalesAndReturnsRegressionTests
{
    [Fact]
    public async Task CompleteSaleAsync_keeps_decimal_quantity_precision_and_applies_discount_amount()
    {
        await using var scope = await TestDatabaseScope.CreateAsync();
        var seed = await scope.SeedOpenShiftAsync(stockQuantity: 10m, salePrice: 20m);
        var service = new POSService(scope.DbContext, new AllowAllPermissionService());

        var result = await service.CompleteSaleAsync(new CompleteSaleRequest(
            seed.User.Id,
            seed.Shift.Id,
            2.50m,
            PaymentMethod.Cash,
            new[]
            {
                new SaleLineRequest(seed.Product.Id, seed.Product.Name, 1.25m, 20m, ItemType.Product)
            }));

        Assert.True(result.Succeeded, result.Message);
        Assert.NotNull(result.Value);
        Assert.Equal(22.50m, result.Value!.NetAmount);

        var product = await scope.DbContext.Products.SingleAsync(x => x.Id == seed.Product.Id);
        Assert.Equal(8.75m, product.StockQuantity);

        var invoice = await scope.DbContext.Invoices
            .Include(x => x.Items)
            .SingleAsync(x => x.Id == result.Value.InvoiceId);

        Assert.Equal(25m, invoice.TotalAmount);
        Assert.Equal(2.50m, invoice.DiscountAmount);
        Assert.Equal(22.50m, invoice.NetAmount);
        Assert.Equal(1.25m, invoice.Items.Single().Quantity);
    }

    [Fact]
    public async Task Returns_handle_decimal_quantities_and_existing_return_sums_on_sqlite()
    {
        await using var scope = await TestDatabaseScope.CreateAsync();
        var seed = await scope.SeedOpenShiftAsync(stockQuantity: 10m, salePrice: 50m);
        var permissionService = new AllowAllPermissionService();
        var posService = new POSService(scope.DbContext, permissionService);
        var sale = await posService.CompleteSaleAsync(new CompleteSaleRequest(
            seed.User.Id,
            seed.Shift.Id,
            10m,
            PaymentMethod.Cash,
            new[]
            {
                new SaleLineRequest(seed.Product.Id, seed.Product.Name, 2m, 50m, ItemType.Product)
            }));

        Assert.True(sale.Succeeded, sale.Message);
        Assert.NotNull(sale.Value);

        var invoice = await scope.DbContext.Invoices
            .Include(x => x.Items)
            .SingleAsync(x => x.Id == sale.Value!.InvoiceId);

        var invoiceItem = invoice.Items.Single();
        var returnService = new ReturnService(scope.DbContext, permissionService);
        var returned = await returnService.CreateReturnAsync(new CreateReturnRequest(
            invoice.Id,
            seed.User.Id,
            seed.Shift.Id,
            "Regression test",
            new[] { new ReturnLineRequest(invoiceItem.Id, 0.5m) }));

        Assert.True(returned.Succeeded, returned.Message);
        Assert.NotNull(returned.Value);
        Assert.Equal(22.50m, returned.Value!.TotalReturnedAmount);

        var productAfterReturn = await scope.DbContext.Products.SingleAsync(x => x.Id == seed.Product.Id);
        Assert.Equal(8.5m, productAfterReturn.StockQuantity);

        var lookup = await returnService.FindInvoiceAsync(invoice.InvoiceNumber);
        Assert.True(lookup.Succeeded, lookup.Message);
        Assert.NotNull(lookup.Value);

        var returnLine = lookup.Value!.Items.Single();
        Assert.Equal(2m, returnLine.SoldQuantity);
        Assert.Equal(0.5m, returnLine.AlreadyReturnedQuantity);
        Assert.Equal(1.5m, returnLine.ReturnableQuantity);
    }

    [Fact]
    public async Task CompleteSaleAsync_deducts_printing_service_materials_and_tracks_estimated_cost()
    {
        await using var scope = await TestDatabaseScope.CreateAsync();
        var seed = await scope.SeedOpenShiftAsync(stockQuantity: 100m, salePrice: 1m);
        seed.Product.ProductType = ProductType.PrintingMaterial;
        seed.Product.PurchasePrice = 0.5m;
        await scope.DbContext.SaveChangesAsync();

        var serviceTemplate = new PrintingServiceTemplate
        {
            ServiceName = "طباعة أبيض وأسود",
            SearchName = "طباعه ابيض واسود",
            ServiceType = PrintingServiceType.BlackAndWhitePrint,
            UnitName = "صفحة",
            SellingPricePerUnit = 1.5m,
            UsesPaper = true,
            PaperConsumptionPerUnit = 1m,
            UsesInk = true,
            InkCostMode = InkCostMode.FixedEstimatedCostPerUnit,
            EstimatedInkCostPerUnit = 0.05m,
            ShowInCashier = true,
            IsActive = true
        };
        serviceTemplate.MaterialConsumptions.Add(new PrintingMaterialConsumption
        {
            ProductId = seed.Product.Id,
            QuantityPerUnit = 1m
        });

        scope.DbContext.PrintingServiceTemplates.Add(serviceTemplate);
        await scope.DbContext.SaveChangesAsync();

        var service = new POSService(scope.DbContext, new AllowAllPermissionService());
        var result = await service.CompleteSaleAsync(new CompleteSaleRequest(
            seed.User.Id,
            seed.Shift.Id,
            0m,
            PaymentMethod.Cash,
            new[]
            {
                new SaleLineRequest(null, serviceTemplate.ServiceName, 20m, 1.5m, ItemType.PrintingService, serviceTemplate.Id)
            }));

        Assert.True(result.Succeeded, result.Message);
        Assert.NotNull(result.Value);

        var material = await scope.DbContext.Products.SingleAsync(x => x.Id == seed.Product.Id);
        Assert.Equal(80m, material.StockQuantity);

        var materialMovement = await scope.DbContext.StockMovements.SingleAsync();
        Assert.Equal(StockMovementType.Sale, materialMovement.MovementType);
        Assert.Equal("PrintingServiceMaterial", materialMovement.ReferenceType);
        Assert.Equal(result.Value!.InvoiceId, materialMovement.ReferenceId);
        Assert.Equal(20m, materialMovement.Quantity);

        var invoice = await scope.DbContext.Invoices
            .Include(x => x.Items)
            .SingleAsync(x => x.Id == result.Value.InvoiceId);

        var item = invoice.Items.Single();
        Assert.Equal(ItemType.PrintingService, item.ItemType);
        Assert.Equal(serviceTemplate.Id, item.PrintingServiceTemplateId);
        Assert.Equal(0.55m, item.UnitCost);
        Assert.Equal(11m, item.TotalCost);
        Assert.Equal(30m, item.TotalPrice);
    }

    [Fact]
    public async Task Printing_service_returns_refund_without_restoring_consumed_materials()
    {
        await using var scope = await TestDatabaseScope.CreateAsync();
        var seed = await scope.SeedOpenShiftAsync(stockQuantity: 100m, salePrice: 1m);
        seed.Product.ProductType = ProductType.PrintingMaterial;
        seed.Product.PurchasePrice = 0.5m;
        await scope.DbContext.SaveChangesAsync();

        var serviceTemplate = new PrintingServiceTemplate
        {
            ServiceName = "Black and white print",
            SearchName = "black and white print",
            ServiceType = PrintingServiceType.BlackAndWhitePrint,
            UnitName = "page",
            SellingPricePerUnit = 1.5m,
            UsesPaper = true,
            PaperConsumptionPerUnit = 1m,
            UsesInk = false,
            InkCostMode = InkCostMode.None,
            EstimatedInkCostPerUnit = 0m,
            ShowInCashier = true,
            IsActive = true
        };
        serviceTemplate.MaterialConsumptions.Add(new PrintingMaterialConsumption
        {
            ProductId = seed.Product.Id,
            QuantityPerUnit = 1m
        });

        scope.DbContext.PrintingServiceTemplates.Add(serviceTemplate);
        await scope.DbContext.SaveChangesAsync();

        var permissionService = new AllowAllPermissionService();
        var posService = new POSService(scope.DbContext, permissionService);
        var sale = await posService.CompleteSaleAsync(new CompleteSaleRequest(
            seed.User.Id,
            seed.Shift.Id,
            0m,
            PaymentMethod.Cash,
            new[]
            {
                new SaleLineRequest(null, serviceTemplate.ServiceName, 20m, 1.5m, ItemType.PrintingService, serviceTemplate.Id)
            }));

        Assert.True(sale.Succeeded, sale.Message);
        Assert.NotNull(sale.Value);

        var materialAfterSale = await scope.DbContext.Products.SingleAsync(x => x.Id == seed.Product.Id);
        Assert.Equal(80m, materialAfterSale.StockQuantity);

        var invoice = await scope.DbContext.Invoices
            .Include(x => x.Items)
            .SingleAsync(x => x.Id == sale.Value!.InvoiceId);
        var serviceLine = invoice.Items.Single();

        var returnService = new ReturnService(scope.DbContext, permissionService);
        var returned = await returnService.CreateReturnAsync(new CreateReturnRequest(
            invoice.Id,
            seed.User.Id,
            seed.Shift.Id,
            "Service refund",
            new[] { new ReturnLineRequest(serviceLine.Id, 20m) }));

        Assert.True(returned.Succeeded, returned.Message);
        Assert.NotNull(returned.Value);
        Assert.Equal(30m, returned.Value!.TotalReturnedAmount);

        var materialAfterReturn = await scope.DbContext.Products.SingleAsync(x => x.Id == seed.Product.Id);
        Assert.Equal(80m, materialAfterReturn.StockQuantity);

        var returnItem = await scope.DbContext.ReturnItems.SingleAsync();
        Assert.Null(returnItem.ProductId);

        var returnStockMovements = await scope.DbContext.StockMovements
            .Where(x => x.MovementType == StockMovementType.Return)
            .ToListAsync();
        Assert.Empty(returnStockMovements);
    }

    [Fact]
    public async Task Product_lookup_and_printing_template_material_picker_keep_materials_separate()
    {
        await using var scope = await TestDatabaseScope.CreateAsync();
        var seed = await scope.SeedOpenShiftAsync(stockQuantity: 10m, salePrice: 5m);

        var material = new Product
        {
            Name = $"A4 paper {Guid.NewGuid():N}",
            SearchName = "a4 paper",
            InternalCode = Guid.NewGuid().ToString("N")[..12],
            Barcode = Guid.NewGuid().ToString("N")[..12],
            ProductType = ProductType.PrintingMaterial,
            PurchasePrice = 250m,
            SalePrice = 0m,
            StockQuantity = 500m,
            LowStockThreshold = 20,
            IsActive = true
        };
        var printedProduct = new Product
        {
            Name = $"Printed memo {Guid.NewGuid():N}",
            SearchName = "printed memo",
            InternalCode = Guid.NewGuid().ToString("N")[..12],
            Barcode = Guid.NewGuid().ToString("N")[..12],
            ProductType = ProductType.PrintedProduct,
            PurchasePrice = 15m,
            SalePrice = 30m,
            StockQuantity = 10m,
            LowStockThreshold = 2,
            IsActive = true
        };

        scope.DbContext.Products.AddRange(material, printedProduct);
        await scope.DbContext.SaveChangesAsync();

        var permissionService = new AllowAllPermissionService();
        var cashierProducts = await new ProductLookupService(scope.DbContext, permissionService).SearchAsync("");
        Assert.Contains(cashierProducts, x => x.Id == seed.Product.Id);
        Assert.Contains(cashierProducts, x => x.Id == printedProduct.Id);
        Assert.DoesNotContain(cashierProducts, x => x.Id == material.Id);

        var materialOptions = await new PrintingServiceTemplateService(scope.DbContext, permissionService)
            .GetMaterialProductsAsync("");
        Assert.Contains(materialOptions, x => x.Id == material.Id);
        Assert.DoesNotContain(materialOptions, x => x.Id == seed.Product.Id);
        Assert.DoesNotContain(materialOptions, x => x.Id == printedProduct.Id);
    }

    [Fact]
    public async Task CompleteSaleAsync_uses_stock_unit_cost_for_carton_products_and_printing_materials()
    {
        await using var scope = await TestDatabaseScope.CreateAsync();
        var seed = await scope.SeedOpenShiftAsync(stockQuantity: 0m, salePrice: 1m);

        var cartonCategory = new Category
        {
            Name = "Raw materials",
            SearchName = "raw materials",
            MeasurementUnit = MeasurementUnit.Carton,
            IsActive = true
        };
        var paper = new Product
        {
            Name = "A4 paper",
            SearchName = "a4 paper",
            InternalCode = Guid.NewGuid().ToString("N")[..12],
            Barcode = Guid.NewGuid().ToString("N")[..12],
            Category = cartonCategory,
            ProductType = ProductType.PrintingMaterial,
            PurchasePrice = 1000m,
            SalePrice = 0.5m,
            StockQuantity = 6000m,
            PackageCount = 1,
            UnitsPerPackage = 6000,
            LowStockThreshold = 1,
            IsActive = true
        };
        var boxedPen = new Product
        {
            Name = "Boxed pen",
            SearchName = "boxed pen",
            InternalCode = Guid.NewGuid().ToString("N")[..12],
            Barcode = Guid.NewGuid().ToString("N")[..12],
            Category = cartonCategory,
            PurchasePrice = 120m,
            SalePrice = 15m,
            StockQuantity = 12m,
            PackageCount = 1,
            UnitsPerPackage = 12,
            LowStockThreshold = 1,
            IsActive = true
        };
        var serviceTemplate = new PrintingServiceTemplate
        {
            ServiceName = "Printed booklet",
            SearchName = "printed booklet",
            ServiceType = PrintingServiceType.Other,
            UnitName = "booklet",
            SellingPricePerUnit = 80m,
            UsesPaper = true,
            PaperConsumptionPerUnit = 50m,
            UsesInk = false,
            InkCostMode = InkCostMode.None,
            EstimatedInkCostPerUnit = 0m,
            ShowInCashier = true,
            IsActive = true
        };
        serviceTemplate.MaterialConsumptions.Add(new PrintingMaterialConsumption
        {
            Product = paper,
            QuantityPerUnit = 50m
        });

        scope.DbContext.Products.AddRange(paper, boxedPen);
        scope.DbContext.PrintingServiceTemplates.Add(serviceTemplate);
        await scope.DbContext.SaveChangesAsync();

        var service = new POSService(scope.DbContext, new AllowAllPermissionService());
        var result = await service.CompleteSaleAsync(new CompleteSaleRequest(
            seed.User.Id,
            seed.Shift.Id,
            0m,
            PaymentMethod.Cash,
            new[]
            {
                new SaleLineRequest(boxedPen.Id, boxedPen.Name, 1m, 15m, ItemType.Product),
                new SaleLineRequest(null, serviceTemplate.ServiceName, 1m, 80m, ItemType.PrintingService, serviceTemplate.Id)
            }));

        Assert.True(result.Succeeded, result.Message);
        Assert.NotNull(result.Value);

        var invoice = await scope.DbContext.Invoices
            .Include(x => x.Items)
            .SingleAsync(x => x.Id == result.Value!.InvoiceId);

        var productLine = invoice.Items.Single(x => x.ItemType == ItemType.Product);
        Assert.Equal(10m, productLine.UnitCost);
        Assert.Equal(10m, productLine.TotalCost);

        var serviceLine = invoice.Items.Single(x => x.ItemType == ItemType.PrintingService);
        Assert.InRange(serviceLine.UnitCost, 8.33m, 8.34m);
        Assert.InRange(serviceLine.TotalCost, 8.33m, 8.34m);

        var paperAfterSale = await scope.DbContext.Products.SingleAsync(x => x.Id == paper.Id);
        Assert.Equal(5950m, paperAfterSale.StockQuantity);
    }

    [Fact]
    public async Task Reports_and_invoice_history_respect_requested_date_range()
    {
        await using var scope = await TestDatabaseScope.CreateAsync();
        var seed = await scope.SeedOpenShiftAsync(stockQuantity: 20m, salePrice: 10m);
        var permissionService = new AllowAllPermissionService();
        var posService = new POSService(scope.DbContext, permissionService);

        var oldSale = await posService.CompleteSaleAsync(new CompleteSaleRequest(
            seed.User.Id,
            seed.Shift.Id,
            0m,
            PaymentMethod.Cash,
            new[] { new SaleLineRequest(seed.Product.Id, seed.Product.Name, 1m, 10m, ItemType.Product) }));
        Assert.True(oldSale.Succeeded, oldSale.Message);

        var currentSale = await posService.CompleteSaleAsync(new CompleteSaleRequest(
            seed.User.Id,
            seed.Shift.Id,
            0m,
            PaymentMethod.Cash,
            new[] { new SaleLineRequest(seed.Product.Id, seed.Product.Name, 2m, 10m, ItemType.Product) }));
        Assert.True(currentSale.Succeeded, currentSale.Message);
        Assert.NotNull(oldSale.Value);
        Assert.NotNull(currentSale.Value);

        var oldInvoice = await scope.DbContext.Invoices.SingleAsync(x => x.Id == oldSale.Value!.InvoiceId);
        oldInvoice.CreatedAt = DateTimeOffset.UtcNow.AddDays(-10);
        await scope.DbContext.SaveChangesAsync();

        var from = DateTimeOffset.UtcNow.AddDays(-1);
        var to = DateTimeOffset.UtcNow.AddDays(1);

        var report = await new ReportService(scope.DbContext, permissionService)
            .GetSalesReportAsync(new SalesReportRequest(from, to));
        Assert.True(report.Succeeded, report.Message);
        Assert.NotNull(report.Value);
        Assert.Equal(1, report.Value!.InvoiceCount);
        Assert.Equal(20m, report.Value.GrossSales);

        var history = await new InvoiceHistoryService(scope.DbContext, permissionService)
            .SearchAsync(new InvoiceHistorySearchRequest(from, to, Take: 500));
        Assert.True(history.Succeeded, history.Message);
        Assert.NotNull(history.Value);
        Assert.Single(history.Value!);
        Assert.Equal(currentSale.Value!.InvoiceId, history.Value![0].InvoiceId);
    }

    private sealed class AllowAllPermissionService : IPermissionService
    {
        public Task<bool> HasPermissionAsync(int userId, string permissionCode, CancellationToken cancellationToken = default) =>
            Task.FromResult(true);

        public void EnsureCurrentUserHas(string permissionCode)
        {
        }
    }

    private sealed class TestDatabaseScope : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;

        private TestDatabaseScope(SqliteConnection connection, QashiraDbContext dbContext)
        {
            _connection = connection;
            DbContext = dbContext;
        }

        public QashiraDbContext DbContext { get; }

        public static async Task<TestDatabaseScope> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();

            var options = new DbContextOptionsBuilder<QashiraDbContext>()
                .UseSqlite(connection)
                .EnableSensitiveDataLogging()
                .Options;

            var dbContext = new QashiraDbContext(options);
            await dbContext.Database.EnsureCreatedAsync();
            return new TestDatabaseScope(connection, dbContext);
        }

        public async Task<(User User, Shift Shift, Product Product)> SeedOpenShiftAsync(decimal stockQuantity, decimal salePrice)
        {
            var role = new Role { Name = "Admin", DisplayName = "Admin" };
            var user = new User
            {
                FullName = "Cashier",
                Username = Guid.NewGuid().ToString("N"),
                PasswordHash = "hashed",
                PasswordSalt = "salt",
                Role = role,
                IsActive = true
            };
            var product = new Product
            {
                Name = $"Product {Guid.NewGuid():N}",
                SearchName = "product",
                InternalCode = Guid.NewGuid().ToString("N")[..12],
                Barcode = Guid.NewGuid().ToString("N")[..12],
                PurchasePrice = 10m,
                SalePrice = salePrice,
                StockQuantity = stockQuantity,
                LowStockThreshold = 1,
                IsActive = true
            };

            DbContext.Roles.Add(role);
            DbContext.Users.Add(user);
            DbContext.Products.Add(product);
            await DbContext.SaveChangesAsync();

            var shift = new Shift
            {
                CashierId = user.Id,
                OpeningCash = 0,
                Status = ShiftStatus.Open,
                OpenedAt = DateTimeOffset.UtcNow
            };

            DbContext.Shifts.Add(shift);
            await DbContext.SaveChangesAsync();

            return (user, shift, product);
        }

        public async ValueTask DisposeAsync()
        {
            await DbContext.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }
}
