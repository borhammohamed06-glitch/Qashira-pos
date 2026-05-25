using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Input;
using Qashira.Application.Abstractions;
using Qashira.Application.DTOs;
using Qashira.Application.Permissions;
using Qashira.App.Services;
using Qashira.Domain.Enums;
using Qashira.Shared.Arabic;
using Qashira.Shared.Results;
using Microsoft.Win32;
using Serilog;

namespace Qashira.App.ViewModels;

public sealed class MainWindowViewModel : ViewModelBase
{
    private const string DiscountTypeAmount = "مبلغ";
    private const string DiscountTypePercentage = "نسبة مئوية";

    private readonly IAuthService _authService;
    private readonly IShiftService _shiftService;
    private readonly IProductLookupService _productLookupService;
    private readonly IProductManagementService _productManagementService;
    private readonly ICategoryManagementService _categoryManagementService;
    private readonly IProductImportExportService _productImportExportService;
    private readonly IPrintingServiceTemplateService _printingServiceTemplateService;
    private readonly IPrintingMaterialService _printingMaterialService;
    private readonly IInventoryService _inventoryService;
    private readonly INotificationService _notificationService;
    private readonly IPOSService _posService;
    private readonly IReturnService _returnService;
    private readonly IReportService _reportService;
    private readonly IInvoiceHistoryService _invoiceHistoryService;
    private readonly IReceiptService _receiptService;
    private readonly IPrinterSettingsService _printerSettingsService;
    private readonly ISystemSettingsService _systemSettingsService;
    private readonly IBackupService _backupService;
    private readonly ILogExportService _logExportService;
    private readonly IAuditLogQueryService _auditLogQueryService;
    private readonly IUserManagementService _userManagementService;
    private readonly ICurrentUserSession _currentUserSession;
    private readonly IReceiptPrinter _receiptPrinter;
    private string _username = "admin";
    private string _password = string.Empty;
    private string _newPassword = string.Empty;
    private string _confirmPassword = string.Empty;
    private string _loginMessage = string.Empty;
    private string _passwordChangeMessage = string.Empty;
    private string _shiftMessage = string.Empty;
    private string _posMessage = string.Empty;
    private string _searchText = string.Empty;
    private string _openingCash = string.Empty;
    private int? _cashierId;
    private int? _shiftId;
    private bool _isAuthenticated;
    private bool _mustChangePassword;
    private string _activeWorkspace = "POS";
    private bool _isQuickNavigationOpen;
    private string _quickNavigationSearchText = string.Empty;
    private QuickNavigationItemViewModel? _selectedQuickNavigationItem;
    private HashSet<string> _currentPermissions = new(StringComparer.Ordinal);
    private string _sessionText = "لم يتم تسجيل الدخول";
    private ProductLookupDto? _selectedProduct;
    private readonly List<ProductLookupDto> _selectedProducts = new();
    private CartLineViewModel? _selectedCartLine;
    private SuspendedInvoiceSummaryDto? _selectedSuspendedInvoice;
    private ProductDetailsDto? _selectedManagedProduct;
    private CategoryDetailsDto? _selectedManagedCategory;
    private string _managedProductsSearchText = string.Empty;
    private bool _showInactiveProducts;
    private string _productMessage = string.Empty;
    private string _categoryName = string.Empty;
    private string _categoryMessage = string.Empty;
    private MeasurementUnitOptionDto? _selectedCategoryMeasurementUnit;
    private bool _categoryIsActive = true;
    private int? _editingCategoryId;
    private int? _editingProductId;
    private string _productName = string.Empty;
    private string _productBarcode = string.Empty;
    private string _productInternalCode = string.Empty;
    private ProductTypeOptionDto? _selectedProductType;
    private CategoryOptionDto? _selectedCategory;
    private string _productPurchasePrice = string.Empty;
    private string _productSalePrice = string.Empty;
    private decimal _productStockQuantity;
    private int _productPackageCount;
    private int _productUnitsPerPackage;
    private int _productLowStockThreshold = 5;
    private InventoryProductDto? _selectedInventoryProduct;
    private string _inventorySearchText = string.Empty;
    private string _inventoryMessage = string.Empty;
    private string _newStockQuantity = string.Empty;
    private string _stockAdjustmentReason = string.Empty;
    private int _lowStockCount;
    private string _discountAmount = string.Empty;
    private string _selectedDiscountType = DiscountTypeAmount;
    private bool _isNotificationsPopupOpen;
    private string? _lastAcceptedBarcode;
    private DateTimeOffset _lastAcceptedBarcodeAt = DateTimeOffset.MinValue;
    private int _barcodeScanCooldownMilliseconds = 500;
    private string _printPages = "1";
    private string _printCopies = "1";
    private string _printPricePerPage = string.Empty;
    private string _printingServiceSearchText = string.Empty;
    private bool _showInactivePrintingServices;
    private PrintingServiceTemplateListItemDto? _selectedPrintingServiceTemplate;
    private PrintingServiceTemplateListItemDto? _selectedCashierPrintingTemplate;
    private int? _editingPrintingServiceTemplateId;
    private string _printingServiceName = string.Empty;
    private PrintingServiceTypeOptionDto? _selectedPrintingServiceType;
    private string _printingServiceUnitName = "صفحة";
    private string _printingServiceSellingPricePerUnit = string.Empty;
    private bool _printingServiceUsesPaper;
    private string _printingServicePaperConsumptionPerUnit = "1";
    private bool _printingServiceUsesInk;
    private InkCostModeOptionDto? _selectedPrintingServiceInkCostMode;
    private string _printingServiceEstimatedInkCostPerUnit = string.Empty;
    private bool _printingServiceShowInCashier = true;
    private bool _printingServiceIsActive = true;
    private string _printingServiceShortcutKey = string.Empty;
    private string _printingServiceNotes = string.Empty;
    private string _printingServiceMessage = string.Empty;
    private PrintingMaterialProductOptionDto? _selectedPrintingMaterialProduct;
    private string _printingMaterialQuantityPerUnit = "1";
    private string _printingMaterialNotes = string.Empty;
    private PrintingMaterialConsumptionViewModel? _selectedPrintingTemplateMaterial;
    private PrintingMaterialDto? _selectedManagedPrintingMaterial;
    private string _printingMaterialsSearchText = string.Empty;
    private bool _showInactivePrintingMaterials;
    private int? _editingPrintingMaterialId;
    private string _managedPrintingMaterialName = string.Empty;
    private string _managedPrintingMaterialBarcode = string.Empty;
    private string _managedPrintingMaterialInternalCode = string.Empty;
    private CategoryOptionDto? _selectedManagedPrintingMaterialCategory;
    private string _managedPrintingMaterialPurchasePrice = string.Empty;
    private decimal _managedPrintingMaterialStockQuantity;
    private int _managedPrintingMaterialLowStockThreshold = 5;
    private string _managedPrintingMaterialMessage = string.Empty;
    private string _cashierPrintingServiceQuantity = "1";
    private string _printServiceName = "خدمة طباعة";
    private string _returnInvoiceNumber = string.Empty;
    private string _returnMessage = string.Empty;
    private string _returnReason = string.Empty;
    private int? _returnInvoiceId;
    private string _returnInvoiceSummary = string.Empty;
    private ReturnInvoiceMatchDto? _selectedPossibleReturnInvoice;
    private string _shiftSummaryText = string.Empty;
    private string _closingCash = string.Empty;
    private string _closeShiftMessage = string.Empty;
    private DateTime? _reportFromDateValue = DateTime.Today;
    private DateTime? _reportToDateValue = DateTime.Today;
    private string _reportSummaryText = string.Empty;
    private string _reportMessage = string.Empty;
    private DateTime? _invoiceHistoryFromDateValue = DateTime.Today;
    private DateTime? _invoiceHistoryToDateValue = DateTime.Today;
    private string _invoiceHistorySearchText = string.Empty;
    private string _invoiceHistoryMessage = string.Empty;
    private string _invoiceHistorySummary = string.Empty;
    private InvoiceHistoryListItemDto? _selectedInvoiceHistoryItem;
    private int? _lastInvoiceId;
    private string _selectedReceiptPrinterName = string.Empty;
    private string _selectedLabelPrinterName = string.Empty;
    private string _receiptStoreName = string.Empty;
    private string _receiptTitle = string.Empty;
    private string _receiptFooter = string.Empty;
    private string _receiptPaperWidth = "80mm";
    private string _barcodeLabelSize = "38x50 mm";
    private string _barcodePrinterProfile = "Auto";
    private string _barcodeLabelGapMm = "2";
    private string _barcodeHorizontalOffsetMm = "0";
    private string _barcodeVerticalOffsetMm = "0";
    private string _barcodeLabelQuantity = "1";
    private string _printerSettingsMessage = string.Empty;
    private string _printerSettingsSummary = string.Empty;
    private string _systemStoreName = string.Empty;
    private string _systemCurrency = "ج.م";
    private int _systemLowStockThreshold = 3;
    private bool _systemAllowNegativeStock;
    private bool _systemDiscountsEnabled = true;
    private string _systemSettingsMessage = string.Empty;
    private BackupFileDto? _selectedBackupFile;
    private string _backupMessage = string.Empty;
    private string _backupStorageSummary = string.Empty;
    private DateTime? _auditFromDateValue = DateTime.Today;
    private DateTime? _auditToDateValue = DateTime.Today;
    private string _auditSearchText = string.Empty;
    private string _auditMessage = string.Empty;
    private AuditUserFilterOptionDto? _selectedAuditUserFilter;
    private AuditActionFilterOptionDto? _selectedAuditActionFilter;
    private AuditLogEntryDto? _selectedAuditLog;
    private string _auditDetailTitle = string.Empty;
    private string _auditDetailSummary = string.Empty;
    private string _auditDetailLinesMessage = string.Empty;
    private string _auditDetailTimelineMessage = string.Empty;
    private UserDetailsDto? _selectedUser;
    private RoleOptionDto? _selectedUserRole;
    private int? _editingUserId;
    private string _userFullName = string.Empty;
    private string _userUsername = string.Empty;
    private string _userPassword = string.Empty;
    private bool _userIsActive = true;
    private string _userMessage = string.Empty;
    private string _permissionMessage = string.Empty;
    private bool _suppressPermissionReload;

    public MainWindowViewModel(
        IAuthService authService,
        IShiftService shiftService,
        IProductLookupService productLookupService,
        IProductManagementService productManagementService,
        ICategoryManagementService categoryManagementService,
        IProductImportExportService productImportExportService,
        IPrintingServiceTemplateService printingServiceTemplateService,
        IPrintingMaterialService printingMaterialService,
        IInventoryService inventoryService,
        INotificationService notificationService,
        IReturnService returnService,
        IReportService reportService,
        IInvoiceHistoryService invoiceHistoryService,
        IReceiptService receiptService,
        IPrinterSettingsService printerSettingsService,
        ISystemSettingsService systemSettingsService,
        IBackupService backupService,
        ILogExportService logExportService,
        IAuditLogQueryService auditLogQueryService,
        IUserManagementService userManagementService,
        ICurrentUserSession currentUserSession,
        IReceiptPrinter receiptPrinter,
        IPOSService posService)
    {
        _authService = authService;
        _shiftService = shiftService;
        _productLookupService = productLookupService;
        _productManagementService = productManagementService;
        _categoryManagementService = categoryManagementService;
        _productImportExportService = productImportExportService;
        _printingServiceTemplateService = printingServiceTemplateService;
        _printingMaterialService = printingMaterialService;
        _inventoryService = inventoryService;
        _notificationService = notificationService;
        _returnService = returnService;
        _reportService = reportService;
        _invoiceHistoryService = invoiceHistoryService;
        _receiptService = receiptService;
        _printerSettingsService = printerSettingsService;
        _systemSettingsService = systemSettingsService;
        _backupService = backupService;
        _logExportService = logExportService;
        _auditLogQueryService = auditLogQueryService;
        _userManagementService = userManagementService;
        _currentUserSession = currentUserSession;
        _receiptPrinter = receiptPrinter;
        _posService = posService;
        LoginCommand = new AsyncRelayCommand(LoginAsync);
        ChangeRequiredPasswordCommand = new AsyncRelayCommand(ChangeRequiredPasswordAsync);
        OpenShiftCommand = new AsyncRelayCommand(OpenShiftAsync);
        SearchCommand = new AsyncRelayCommand(SearchProductsAsync);
        SearchOrAddProductCommand = new AsyncRelayCommand(SearchOrAddProductAsync);
        AddSelectedProductCommand = new AsyncRelayCommand(AddSelectedProductAsync);
        RemoveSelectedCartLineCommand = new AsyncRelayCommand(RemoveSelectedCartLineAsync);
        HoldInvoiceCommand = new AsyncRelayCommand(HoldInvoiceAsync);
        ResumeSuspendedInvoiceCommand = new AsyncRelayCommand(ResumeSuspendedInvoiceAsync);
        CancelSuspendedInvoiceCommand = new AsyncRelayCommand(CancelSuspendedInvoiceAsync);
        RefreshSuspendedInvoicesCommand = new AsyncRelayCommand(LoadSuspendedInvoicesAsync);
        ToggleNotificationsCommand = new AsyncRelayCommand(ToggleNotificationsAsync);
        AddPrintServiceCommand = new AsyncRelayCommand(AddPrintServiceAsync);
        AddPrintingTemplateToCartCommand = new AsyncRelayCommand(AddPrintingTemplateToCartAsync);
        CompleteSaleCommand = new AsyncRelayCommand(CompleteSaleAsync);
        CompleteSaleAndPrintCommand = new AsyncRelayCommand(CompleteSaleAndPrintAsync);
        PrintLastReceiptCommand = new AsyncRelayCommand(PrintLastReceiptAsync);
        ShowPosCommand = new AsyncRelayCommand(ShowPosAsync);
        ShowProductsCommand = new AsyncRelayCommand(ShowProductsAsync);
        ShowBarcodeCommand = new AsyncRelayCommand(ShowBarcodeAsync);
        ShowCategoriesCommand = new AsyncRelayCommand(ShowCategoriesAsync);
        ShowPrintingMaterialsCommand = new AsyncRelayCommand(ShowPrintingMaterialsAsync);
        ShowProductionCommand = new AsyncRelayCommand(ShowProductionAsync);
        SearchPrintingMaterialsCommand = new AsyncRelayCommand(LoadManagedPrintingMaterialsAsync);
        NewPrintingMaterialCommand = new AsyncRelayCommand(NewPrintingMaterialAsync);
        EditSelectedPrintingMaterialCommand = new AsyncRelayCommand(EditSelectedPrintingMaterialAsync);
        SavePrintingMaterialCommand = new AsyncRelayCommand(SavePrintingMaterialAsync);
        TogglePrintingMaterialActiveCommand = new AsyncRelayCommand(TogglePrintingMaterialActiveAsync);
        SearchManagedProductsCommand = new AsyncRelayCommand(LoadManagedProductsAsync);
        SaveCategoryCommand = new AsyncRelayCommand(SaveCategoryAsync);
        NewCategoryCommand = new AsyncRelayCommand(NewCategoryAsync);
        EditSelectedCategoryCommand = new AsyncRelayCommand(EditSelectedCategoryAsync);
        ToggleCategoryActiveCommand = new AsyncRelayCommand(ToggleCategoryActiveAsync);
        NewProductCommand = new AsyncRelayCommand(NewProductAsync);
        EditSelectedProductCommand = new AsyncRelayCommand(EditSelectedProductAsync);
        SaveProductCommand = new AsyncRelayCommand(SaveProductAsync);
        ToggleProductActiveCommand = new AsyncRelayCommand(ToggleProductActiveAsync);
        PrintSelectedProductBarcodeCommand = new AsyncRelayCommand(PrintSelectedProductBarcodeAsync);
        ImportProductsCommand = new AsyncRelayCommand(ImportProductsAsync);
        ExportProductsCommand = new AsyncRelayCommand(ExportProductsAsync);
        ShowPrintingServicesCommand = new AsyncRelayCommand(ShowPrintingServicesAsync);
        SearchPrintingServicesCommand = new AsyncRelayCommand(LoadPrintingServicesAsync);
        NewPrintingServiceTemplateCommand = new AsyncRelayCommand(NewPrintingServiceTemplateAsync);
        EditSelectedPrintingServiceTemplateCommand = new AsyncRelayCommand(EditSelectedPrintingServiceTemplateAsync);
        SavePrintingServiceTemplateCommand = new AsyncRelayCommand(SavePrintingServiceTemplateAsync);
        TogglePrintingServiceTemplateActiveCommand = new AsyncRelayCommand(TogglePrintingServiceTemplateActiveAsync);
        LoadPrintingMaterialProductsCommand = new AsyncRelayCommand(LoadPrintingMaterialProductsAsync);
        AddPrintingMaterialCommand = new AsyncRelayCommand(AddPrintingMaterialAsync);
        RemovePrintingMaterialCommand = new AsyncRelayCommand(RemovePrintingMaterialAsync);
        ShowInventoryCommand = new AsyncRelayCommand(ShowInventoryAsync);
        SearchInventoryCommand = new AsyncRelayCommand(LoadInventoryAsync);
        AdjustStockCommand = new AsyncRelayCommand(AdjustStockAsync);
        ShowShiftCommand = new AsyncRelayCommand(ShowShiftAsync);
        CloseShiftCommand = new AsyncRelayCommand(CloseShiftAsync);
        ShowReportsCommand = new AsyncRelayCommand(ShowReportsAsync);
        ShowProfitReportsCommand = new AsyncRelayCommand(ShowProfitReportsAsync);
        LoadReportsCommand = new AsyncRelayCommand(LoadReportsAsync);
        ReportTodayCommand = new AsyncRelayCommand(LoadTodayReportAsync);
        ReportWeekCommand = new AsyncRelayCommand(LoadCurrentWeekReportAsync);
        ReportMonthCommand = new AsyncRelayCommand(LoadCurrentMonthReportAsync);
        ShowInvoiceHistoryCommand = new AsyncRelayCommand(ShowInvoiceHistoryAsync);
        LoadInvoiceHistoryCommand = new AsyncRelayCommand(LoadInvoiceHistoryAsync);
        InvoiceHistoryTodayCommand = new AsyncRelayCommand(LoadTodayInvoiceHistoryAsync);
        InvoiceHistoryWeekCommand = new AsyncRelayCommand(LoadCurrentWeekInvoiceHistoryAsync);
        InvoiceHistoryMonthCommand = new AsyncRelayCommand(LoadCurrentMonthInvoiceHistoryAsync);
        PrintSelectedInvoiceHistoryCommand = new AsyncRelayCommand(PrintSelectedInvoiceHistoryAsync);
        ShowPrinterSettingsCommand = new AsyncRelayCommand(ShowPrinterSettingsAsync);
        SavePrinterSettingsCommand = new AsyncRelayCommand(SavePrinterSettingsAsync);
        TestReceiptPrinterCommand = new AsyncRelayCommand(TestReceiptPrinterAsync);
        TestBarcodeLabelPrinterCommand = new AsyncRelayCommand(TestBarcodeLabelPrinterAsync);
        RefreshPrintersCommand = new AsyncRelayCommand(LoadPrinterSettingsAsync);
        ShowSystemSettingsCommand = new AsyncRelayCommand(ShowSystemSettingsAsync);
        SaveSystemSettingsCommand = new AsyncRelayCommand(SaveSystemSettingsAsync);
        ShowBackupCommand = new AsyncRelayCommand(ShowBackupAsync);
        CreateBackupCommand = new AsyncRelayCommand(CreateBackupAsync);
        RestoreBackupCommand = new AsyncRelayCommand(RestoreBackupAsync);
        ImportBackupCommand = new AsyncRelayCommand(ImportBackupAsync);
        ExportSelectedBackupCommand = new AsyncRelayCommand(ExportSelectedBackupAsync);
        DeleteSelectedBackupCommand = new AsyncRelayCommand(DeleteSelectedBackupAsync);
        ExportLogsCommand = new AsyncRelayCommand(ExportLogsAsync);
        ShowAuditLogsCommand = new AsyncRelayCommand(ShowAuditLogsAsync);
        LoadAuditLogsCommand = new AsyncRelayCommand(LoadAuditLogsAsync);
        ShowUsersCommand = new AsyncRelayCommand(ShowUsersAsync);
        NewUserCommand = new AsyncRelayCommand(NewUserAsync);
        EditSelectedUserCommand = new AsyncRelayCommand(EditSelectedUserAsync);
        SaveUserCommand = new AsyncRelayCommand(SaveUserAsync);
        ToggleUserActiveCommand = new AsyncRelayCommand(ToggleUserActiveAsync);
        ShowReturnsCommand = new AsyncRelayCommand(ShowReturnsAsync);
        FindInvoiceForReturnCommand = new AsyncRelayCommand(FindInvoiceForReturnAsync);
        SelectPossibleReturnInvoiceCommand = new AsyncRelayCommand(SelectPossibleReturnInvoiceAsync);
        PrintReturnInvoiceCommand = new AsyncRelayCommand(PrintReturnInvoiceAsync);
        ReturnAllItemsCommand = new AsyncRelayCommand(ReturnAllItemsAsync);
        SaveReturnCommand = new AsyncRelayCommand(SaveReturnAsync);
        RefreshCurrentWorkspaceCommand = new AsyncRelayCommand(RefreshCurrentWorkspaceAsync);
        OpenQuickNavigationCommand = new AsyncRelayCommand(OpenQuickNavigationAsync);
        CloseQuickNavigationCommand = new AsyncRelayCommand(CloseQuickNavigationAsync);
        NavigateToSelectedQuickNavigationCommand = new AsyncRelayCommand(NavigateToSelectedQuickNavigationAsync);
        SelectedCategoryMeasurementUnit = CategoryMeasurementUnits.FirstOrDefault();
        SelectedProductType = ProductTypes.FirstOrDefault();
        SelectedPrintingServiceType = PrintingServiceTypes.LastOrDefault();
        SelectedPrintingServiceInkCostMode = InkCostModes.FirstOrDefault();
        CartLines.CollectionChanged += CartLines_OnCollectionChanged;
        ReturnItems.CollectionChanged += ReturnItems_OnCollectionChanged;
    }

    public string Username
    {
        get => _username;
        set => SetProperty(ref _username, value);
    }

    public string Password
    {
        get => _password;
        set => SetProperty(ref _password, value);
    }

    public string NewPassword
    {
        get => _newPassword;
        set => SetProperty(ref _newPassword, value);
    }

    public string ConfirmPassword
    {
        get => _confirmPassword;
        set => SetProperty(ref _confirmPassword, value);
    }

    public string LoginMessage
    {
        get => _loginMessage;
        set => SetProperty(ref _loginMessage, value);
    }

    public string PasswordChangeMessage
    {
        get => _passwordChangeMessage;
        set => SetProperty(ref _passwordChangeMessage, value);
    }

    public string ShiftMessage
    {
        get => _shiftMessage;
        set => SetProperty(ref _shiftMessage, value);
    }

    public string PosMessage
    {
        get => _posMessage;
        set => SetProperty(ref _posMessage, value);
    }

    public string OpeningCash
    {
        get => _openingCash;
        set => SetProperty(ref _openingCash, value);
    }

    public string SearchText
    {
        get => _searchText;
        set => SetProperty(ref _searchText, value);
    }

    public ProductLookupDto? SelectedProduct
    {
        get => _selectedProduct;
        set => SetProperty(ref _selectedProduct, value);
    }

    public CartLineViewModel? SelectedCartLine
    {
        get => _selectedCartLine;
        set => SetProperty(ref _selectedCartLine, value);
    }

    public SuspendedInvoiceSummaryDto? SelectedSuspendedInvoice
    {
        get => _selectedSuspendedInvoice;
        set => SetProperty(ref _selectedSuspendedInvoice, value);
    }

    public ProductDetailsDto? SelectedManagedProduct
    {
        get => _selectedManagedProduct;
        set
        {
            if (SetProperty(ref _selectedManagedProduct, value))
            {
                OnPropertyChanged(nameof(CanToggleManagedProductActive));
            }
        }
    }

    public CategoryDetailsDto? SelectedManagedCategory
    {
        get => _selectedManagedCategory;
        set
        {
            if (SetProperty(ref _selectedManagedCategory, value))
            {
                OnPropertyChanged(nameof(CanToggleManagedCategoryActive));
            }
        }
    }

    public string ManagedProductsSearchText
    {
        get => _managedProductsSearchText;
        set => SetProperty(ref _managedProductsSearchText, value);
    }

    public bool ShowInactiveProducts
    {
        get => _showInactiveProducts;
        set => SetProperty(ref _showInactiveProducts, value);
    }

    public string ProductMessage
    {
        get => _productMessage;
        set => SetProperty(ref _productMessage, value);
    }

    public string CategoryName
    {
        get => _categoryName;
        set => SetProperty(ref _categoryName, value);
    }

    public string CategoryMessage
    {
        get => _categoryMessage;
        set => SetProperty(ref _categoryMessage, value);
    }

    public MeasurementUnitOptionDto? SelectedCategoryMeasurementUnit
    {
        get => _selectedCategoryMeasurementUnit;
        set => SetProperty(ref _selectedCategoryMeasurementUnit, value);
    }

    public bool CategoryIsActive
    {
        get => _categoryIsActive;
        set => SetProperty(ref _categoryIsActive, value);
    }

    public string ProductName
    {
        get => _productName;
        set => SetProperty(ref _productName, value);
    }

    public string ProductBarcode
    {
        get => _productBarcode;
        set => SetProperty(ref _productBarcode, value);
    }

    public string ProductInternalCode
    {
        get => _productInternalCode;
        set => SetProperty(ref _productInternalCode, value);
    }

    public ProductTypeOptionDto? SelectedProductType
    {
        get => _selectedProductType;
        set => SetProperty(ref _selectedProductType, value);
    }

    public CategoryOptionDto? SelectedCategory
    {
        get => _selectedCategory;
        set
        {
            if (!SetProperty(ref _selectedCategory, value))
            {
                return;
            }

            RaiseSelectedCategoryUnitChanges();
        }
    }

    public string ProductPurchasePrice
    {
        get => _productPurchasePrice;
        set => SetProperty(ref _productPurchasePrice, value);
    }

    public string ProductSalePrice
    {
        get => _productSalePrice;
        set => SetProperty(ref _productSalePrice, value);
    }

    public decimal ProductStockQuantity
    {
        get => _productStockQuantity;
        set => SetProperty(ref _productStockQuantity, value);
    }

    public int ProductPackageCount
    {
        get => _productPackageCount;
        set
        {
            if (SetProperty(ref _productPackageCount, value))
            {
                OnPropertyChanged(nameof(ComputedProductStockQuantityText));
            }
        }
    }

    public int ProductUnitsPerPackage
    {
        get => _productUnitsPerPackage;
        set
        {
            if (SetProperty(ref _productUnitsPerPackage, value))
            {
                OnPropertyChanged(nameof(ComputedProductStockQuantityText));
            }
        }
    }

    public int ProductLowStockThreshold
    {
        get => _productLowStockThreshold;
        set => SetProperty(ref _productLowStockThreshold, value);
    }

    public InventoryProductDto? SelectedInventoryProduct
    {
        get => _selectedInventoryProduct;
        set
        {
            if (!SetProperty(ref _selectedInventoryProduct, value))
            {
                return;
            }

            if (value is not null)
            {
                NewStockQuantity = value.StockQuantity.ToString(CultureInfo.InvariantCulture);
            }

            _ = LoadStockMovementsAsync();
        }
    }

    public string InventorySearchText
    {
        get => _inventorySearchText;
        set => SetProperty(ref _inventorySearchText, value);
    }

    public string InventoryMessage
    {
        get => _inventoryMessage;
        set => SetProperty(ref _inventoryMessage, value);
    }

    public string NewStockQuantity
    {
        get => _newStockQuantity;
        set => SetProperty(ref _newStockQuantity, value);
    }

    public string StockAdjustmentReason
    {
        get => _stockAdjustmentReason;
        set => SetProperty(ref _stockAdjustmentReason, value);
    }

    public int LowStockCount
    {
        get => _lowStockCount;
        set
        {
            if (!SetProperty(ref _lowStockCount, value))
            {
                return;
            }

            OnPropertyChanged(nameof(LowStockBadgeText));
            OnPropertyChanged(nameof(LowStockBadgeVisibility));
            OnPropertyChanged(nameof(LowStockEmptyVisibility));
        }
    }

    public string LowStockBadgeText => LowStockCount == 0 ? "لا توجد تنبيهات" : $"تنبيهات منخفضة: {LowStockCount}";
    public Visibility LowStockBadgeVisibility => LowStockCount > 0 ? Visibility.Visible : Visibility.Collapsed;
    public Visibility LowStockEmptyVisibility => LowStockCount == 0 ? Visibility.Visible : Visibility.Collapsed;

    public bool IsNotificationsPopupOpen
    {
        get => _isNotificationsPopupOpen;
        set => SetProperty(ref _isNotificationsPopupOpen, value);
    }

    public string DiscountAmount
    {
        get => _discountAmount;
        set
        {
            if (!SetProperty(ref _discountAmount, value))
            {
                return;
            }

            RaiseCartTotalsChanged();
        }
    }

    public IReadOnlyList<string> DiscountTypes { get; } = new[] { DiscountTypeAmount, DiscountTypePercentage };

    public string SelectedDiscountType
    {
        get => _selectedDiscountType;
        set
        {
            if (!SetProperty(ref _selectedDiscountType, value))
            {
                return;
            }

            OnPropertyChanged(nameof(DiscountValueLabel));
            RaiseCartTotalsChanged();
        }
    }

    public string DiscountValueLabel => SelectedDiscountType == DiscountTypePercentage ? "نسبة الخصم" : "قيمة الخصم";

    public string DiscountPreviewText => CartDiscountAmount > 0 ? $"قيمة الخصم: {CartDiscountAmount:0.00} ج.م" : string.Empty;

    public int BarcodeScanCooldownMilliseconds
    {
        get => _barcodeScanCooldownMilliseconds;
        set => SetProperty(ref _barcodeScanCooldownMilliseconds, Math.Clamp(value, 300, 700));
    }

    public string PrintPages
    {
        get => _printPages;
        set => SetProperty(ref _printPages, value);
    }

    public string PrintCopies
    {
        get => _printCopies;
        set => SetProperty(ref _printCopies, value);
    }

    public string PrintPricePerPage
    {
        get => _printPricePerPage;
        set => SetProperty(ref _printPricePerPage, value);
    }

    public string PrintServiceName
    {
        get => _printServiceName;
        set => SetProperty(ref _printServiceName, value);
    }

    public string PrintingServiceSearchText
    {
        get => _printingServiceSearchText;
        set => SetProperty(ref _printingServiceSearchText, value);
    }

    public bool ShowInactivePrintingServices
    {
        get => _showInactivePrintingServices;
        set => SetProperty(ref _showInactivePrintingServices, value);
    }

    public PrintingServiceTemplateListItemDto? SelectedPrintingServiceTemplate
    {
        get => _selectedPrintingServiceTemplate;
        set
        {
            if (SetProperty(ref _selectedPrintingServiceTemplate, value))
            {
                OnPropertyChanged(nameof(CanTogglePrintingServiceTemplateActive));
            }
        }
    }

    public PrintingServiceTemplateListItemDto? SelectedCashierPrintingTemplate
    {
        get => _selectedCashierPrintingTemplate;
        set => SetProperty(ref _selectedCashierPrintingTemplate, value);
    }

    public string PrintingServiceName
    {
        get => _printingServiceName;
        set => SetProperty(ref _printingServiceName, value);
    }

    public PrintingServiceTypeOptionDto? SelectedPrintingServiceType
    {
        get => _selectedPrintingServiceType;
        set => SetProperty(ref _selectedPrintingServiceType, value);
    }

    public string PrintingServiceUnitName
    {
        get => _printingServiceUnitName;
        set => SetProperty(ref _printingServiceUnitName, value);
    }

    public string PrintingServiceSellingPricePerUnit
    {
        get => _printingServiceSellingPricePerUnit;
        set => SetProperty(ref _printingServiceSellingPricePerUnit, value);
    }

    public bool PrintingServiceUsesPaper
    {
        get => _printingServiceUsesPaper;
        set => SetProperty(ref _printingServiceUsesPaper, value);
    }

    public string PrintingServicePaperConsumptionPerUnit
    {
        get => _printingServicePaperConsumptionPerUnit;
        set => SetProperty(ref _printingServicePaperConsumptionPerUnit, value);
    }

    public bool PrintingServiceUsesInk
    {
        get => _printingServiceUsesInk;
        set => SetProperty(ref _printingServiceUsesInk, value);
    }

    public InkCostModeOptionDto? SelectedPrintingServiceInkCostMode
    {
        get => _selectedPrintingServiceInkCostMode;
        set => SetProperty(ref _selectedPrintingServiceInkCostMode, value);
    }

    public string PrintingServiceEstimatedInkCostPerUnit
    {
        get => _printingServiceEstimatedInkCostPerUnit;
        set => SetProperty(ref _printingServiceEstimatedInkCostPerUnit, value);
    }

    public bool PrintingServiceShowInCashier
    {
        get => _printingServiceShowInCashier;
        set => SetProperty(ref _printingServiceShowInCashier, value);
    }

    public bool PrintingServiceIsActive
    {
        get => _printingServiceIsActive;
        set => SetProperty(ref _printingServiceIsActive, value);
    }

    public string PrintingServiceShortcutKey
    {
        get => _printingServiceShortcutKey;
        set => SetProperty(ref _printingServiceShortcutKey, value);
    }

    public string PrintingServiceNotes
    {
        get => _printingServiceNotes;
        set => SetProperty(ref _printingServiceNotes, value);
    }

    public string PrintingServiceMessage
    {
        get => _printingServiceMessage;
        set => SetProperty(ref _printingServiceMessage, value);
    }

    public PrintingMaterialProductOptionDto? SelectedPrintingMaterialProduct
    {
        get => _selectedPrintingMaterialProduct;
        set => SetProperty(ref _selectedPrintingMaterialProduct, value);
    }

    public string PrintingMaterialQuantityPerUnit
    {
        get => _printingMaterialQuantityPerUnit;
        set => SetProperty(ref _printingMaterialQuantityPerUnit, value);
    }

    public string PrintingMaterialNotes
    {
        get => _printingMaterialNotes;
        set => SetProperty(ref _printingMaterialNotes, value);
    }

    public PrintingMaterialConsumptionViewModel? SelectedPrintingTemplateMaterial
    {
        get => _selectedPrintingTemplateMaterial;
        set => SetProperty(ref _selectedPrintingTemplateMaterial, value);
    }

    public PrintingMaterialDto? SelectedManagedPrintingMaterial
    {
        get => _selectedManagedPrintingMaterial;
        set
        {
            if (SetProperty(ref _selectedManagedPrintingMaterial, value))
            {
                OnPropertyChanged(nameof(CanToggleManagedPrintingMaterialActive));
            }
        }
    }

    public string PrintingMaterialsSearchText
    {
        get => _printingMaterialsSearchText;
        set => SetProperty(ref _printingMaterialsSearchText, value);
    }

    public bool ShowInactivePrintingMaterials
    {
        get => _showInactivePrintingMaterials;
        set => SetProperty(ref _showInactivePrintingMaterials, value);
    }

    public string ManagedPrintingMaterialName
    {
        get => _managedPrintingMaterialName;
        set => SetProperty(ref _managedPrintingMaterialName, value);
    }

    public string ManagedPrintingMaterialBarcode
    {
        get => _managedPrintingMaterialBarcode;
        set => SetProperty(ref _managedPrintingMaterialBarcode, value);
    }

    public string ManagedPrintingMaterialInternalCode
    {
        get => _managedPrintingMaterialInternalCode;
        set => SetProperty(ref _managedPrintingMaterialInternalCode, value);
    }

    public CategoryOptionDto? SelectedManagedPrintingMaterialCategory
    {
        get => _selectedManagedPrintingMaterialCategory;
        set
        {
            if (SetProperty(ref _selectedManagedPrintingMaterialCategory, value))
            {
                OnPropertyChanged(nameof(SelectedManagedPrintingMaterialUnitText));
            }
        }
    }

    public string ManagedPrintingMaterialPurchasePrice
    {
        get => _managedPrintingMaterialPurchasePrice;
        set => SetProperty(ref _managedPrintingMaterialPurchasePrice, value);
    }

    public decimal ManagedPrintingMaterialStockQuantity
    {
        get => _managedPrintingMaterialStockQuantity;
        set => SetProperty(ref _managedPrintingMaterialStockQuantity, value);
    }

    public int ManagedPrintingMaterialLowStockThreshold
    {
        get => _managedPrintingMaterialLowStockThreshold;
        set => SetProperty(ref _managedPrintingMaterialLowStockThreshold, value);
    }

    public string ManagedPrintingMaterialMessage
    {
        get => _managedPrintingMaterialMessage;
        set => SetProperty(ref _managedPrintingMaterialMessage, value);
    }

    public string CashierPrintingServiceQuantity
    {
        get => _cashierPrintingServiceQuantity;
        set => SetProperty(ref _cashierPrintingServiceQuantity, value);
    }

    public string ReturnInvoiceNumber
    {
        get => _returnInvoiceNumber;
        set => SetProperty(ref _returnInvoiceNumber, value);
    }

    public string ReturnMessage
    {
        get => _returnMessage;
        set => SetProperty(ref _returnMessage, value);
    }

    public string ReturnReason
    {
        get => _returnReason;
        set => SetProperty(ref _returnReason, value);
    }

    public string ReturnInvoiceSummary
    {
        get => _returnInvoiceSummary;
        set => SetProperty(ref _returnInvoiceSummary, value);
    }

    public ReturnInvoiceMatchDto? SelectedPossibleReturnInvoice
    {
        get => _selectedPossibleReturnInvoice;
        set => SetProperty(ref _selectedPossibleReturnInvoice, value);
    }

    public string ShiftSummaryText
    {
        get => _shiftSummaryText;
        set => SetProperty(ref _shiftSummaryText, value);
    }

    public string ClosingCash
    {
        get => _closingCash;
        set => SetProperty(ref _closingCash, value);
    }

    public string CloseShiftMessage
    {
        get => _closeShiftMessage;
        set => SetProperty(ref _closeShiftMessage, value);
    }

    public DateTime? ReportFromDateValue
    {
        get => _reportFromDateValue;
        set => SetProperty(ref _reportFromDateValue, value);
    }

    public DateTime? ReportToDateValue
    {
        get => _reportToDateValue;
        set => SetProperty(ref _reportToDateValue, value);
    }

    public string ReportSummaryText
    {
        get => _reportSummaryText;
        set => SetProperty(ref _reportSummaryText, value);
    }

    public string ReportMessage
    {
        get => _reportMessage;
        set => SetProperty(ref _reportMessage, value);
    }

    public DateTime? InvoiceHistoryFromDateValue
    {
        get => _invoiceHistoryFromDateValue;
        set => SetProperty(ref _invoiceHistoryFromDateValue, value);
    }

    public DateTime? InvoiceHistoryToDateValue
    {
        get => _invoiceHistoryToDateValue;
        set => SetProperty(ref _invoiceHistoryToDateValue, value);
    }

    public string InvoiceHistorySearchText
    {
        get => _invoiceHistorySearchText;
        set => SetProperty(ref _invoiceHistorySearchText, value);
    }

    public string InvoiceHistoryMessage
    {
        get => _invoiceHistoryMessage;
        set => SetProperty(ref _invoiceHistoryMessage, value);
    }

    public string InvoiceHistorySummary
    {
        get => _invoiceHistorySummary;
        set => SetProperty(ref _invoiceHistorySummary, value);
    }

    public InvoiceHistoryListItemDto? SelectedInvoiceHistoryItem
    {
        get => _selectedInvoiceHistoryItem;
        set
        {
            if (!SetProperty(ref _selectedInvoiceHistoryItem, value))
            {
                return;
            }

            _ = LoadSelectedInvoiceHistoryDetailsAsync();
        }
    }

    public string SelectedReceiptPrinterName
    {
        get => _selectedReceiptPrinterName;
        set
        {
            if (!SetProperty(ref _selectedReceiptPrinterName, value))
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(SelectedLabelPrinterName))
            {
                SelectedLabelPrinterName = value;
            }

            UpdatePrinterSettingsSummary();
        }
    }

    public string SelectedLabelPrinterName
    {
        get => _selectedLabelPrinterName;
        set
        {
            if (SetProperty(ref _selectedLabelPrinterName, value))
            {
                UpdatePrinterSettingsSummary();
            }
        }
    }

    public string PrinterSettingsMessage
    {
        get => _printerSettingsMessage;
        set => SetProperty(ref _printerSettingsMessage, value);
    }

    public string PrinterSettingsSummary
    {
        get => _printerSettingsSummary;
        set => SetProperty(ref _printerSettingsSummary, value);
    }

    public string ReceiptStoreName
    {
        get => _receiptStoreName;
        set => SetProperty(ref _receiptStoreName, value);
    }

    public string ReceiptTitle
    {
        get => _receiptTitle;
        set => SetProperty(ref _receiptTitle, value);
    }

    public string ReceiptFooter
    {
        get => _receiptFooter;
        set => SetProperty(ref _receiptFooter, value);
    }

    public string ReceiptPaperWidth
    {
        get => _receiptPaperWidth;
        set
        {
            if (SetProperty(ref _receiptPaperWidth, value))
            {
                UpdatePrinterSettingsSummary();
            }
        }
    }

    public string BarcodeLabelSize
    {
        get => _barcodeLabelSize;
        set
        {
            if (SetProperty(ref _barcodeLabelSize, value))
            {
                UpdatePrinterSettingsSummary();
            }
        }
    }

    public string BarcodePrinterProfile
    {
        get => _barcodePrinterProfile;
        set
        {
            if (SetProperty(ref _barcodePrinterProfile, value))
            {
                UpdatePrinterSettingsSummary();
            }
        }
    }

    public string BarcodeLabelGapMm
    {
        get => _barcodeLabelGapMm;
        set => SetProperty(ref _barcodeLabelGapMm, value);
    }

    public string BarcodeHorizontalOffsetMm
    {
        get => _barcodeHorizontalOffsetMm;
        set => SetProperty(ref _barcodeHorizontalOffsetMm, value);
    }

    public string BarcodeVerticalOffsetMm
    {
        get => _barcodeVerticalOffsetMm;
        set => SetProperty(ref _barcodeVerticalOffsetMm, value);
    }

    public string BarcodeLabelQuantity
    {
        get => _barcodeLabelQuantity;
        set => SetProperty(ref _barcodeLabelQuantity, value);
    }

    public string SystemStoreName
    {
        get => _systemStoreName;
        set => SetProperty(ref _systemStoreName, value);
    }

    public string SystemCurrency
    {
        get => _systemCurrency;
        set => SetProperty(ref _systemCurrency, value);
    }

    public int SystemLowStockThreshold
    {
        get => _systemLowStockThreshold;
        set => SetProperty(ref _systemLowStockThreshold, value);
    }

    public bool SystemAllowNegativeStock
    {
        get => _systemAllowNegativeStock;
        set => SetProperty(ref _systemAllowNegativeStock, value);
    }

    public bool SystemDiscountsEnabled
    {
        get => _systemDiscountsEnabled;
        set
        {
            if (!SetProperty(ref _systemDiscountsEnabled, value))
            {
                return;
            }

            OnPropertyChanged(nameof(DiscountVisibility));
            if (!value)
            {
                DiscountAmount = string.Empty;
                SelectedDiscountType = DiscountTypeAmount;
            }
        }
    }

    public string SystemSettingsMessage
    {
        get => _systemSettingsMessage;
        set => SetProperty(ref _systemSettingsMessage, value);
    }

    public BackupFileDto? SelectedBackupFile
    {
        get => _selectedBackupFile;
        set => SetProperty(ref _selectedBackupFile, value);
    }

    public string BackupMessage
    {
        get => _backupMessage;
        set => SetProperty(ref _backupMessage, value);
    }

    public string BackupStorageSummary
    {
        get => _backupStorageSummary;
        set => SetProperty(ref _backupStorageSummary, value);
    }

    public DateTime? AuditFromDateValue
    {
        get => _auditFromDateValue;
        set => SetProperty(ref _auditFromDateValue, value);
    }

    public DateTime? AuditToDateValue
    {
        get => _auditToDateValue;
        set => SetProperty(ref _auditToDateValue, value);
    }

    public string AuditSearchText
    {
        get => _auditSearchText;
        set => SetProperty(ref _auditSearchText, value);
    }

    public string AuditMessage
    {
        get => _auditMessage;
        set => SetProperty(ref _auditMessage, value);
    }

    public AuditUserFilterOptionDto? SelectedAuditUserFilter
    {
        get => _selectedAuditUserFilter;
        set => SetProperty(ref _selectedAuditUserFilter, value);
    }

    public AuditActionFilterOptionDto? SelectedAuditActionFilter
    {
        get => _selectedAuditActionFilter;
        set => SetProperty(ref _selectedAuditActionFilter, value);
    }

    public AuditLogEntryDto? SelectedAuditLog
    {
        get => _selectedAuditLog;
        set
        {
            if (!SetProperty(ref _selectedAuditLog, value))
            {
                return;
            }

            _ = LoadSelectedAuditDetailsAsync();
        }
    }

    public string AuditDetailTitle
    {
        get => _auditDetailTitle;
        set => SetProperty(ref _auditDetailTitle, value);
    }

    public string AuditDetailSummary
    {
        get => _auditDetailSummary;
        set => SetProperty(ref _auditDetailSummary, value);
    }

    public string AuditDetailLinesMessage
    {
        get => _auditDetailLinesMessage;
        set => SetProperty(ref _auditDetailLinesMessage, value);
    }

    public string AuditDetailTimelineMessage
    {
        get => _auditDetailTimelineMessage;
        set => SetProperty(ref _auditDetailTimelineMessage, value);
    }

    public UserDetailsDto? SelectedUser
    {
        get => _selectedUser;
        set => SetProperty(ref _selectedUser, value);
    }

    public RoleOptionDto? SelectedUserRole
    {
        get => _selectedUserRole;
        set
        {
            if (!SetProperty(ref _selectedUserRole, value) || _suppressPermissionReload)
            {
                return;
            }

            _ = LoadUserPermissionsAsync(null);
        }
    }

    public string UserFullName
    {
        get => _userFullName;
        set => SetProperty(ref _userFullName, value);
    }

    public string UserUsername
    {
        get => _userUsername;
        set => SetProperty(ref _userUsername, value);
    }

    public string UserPassword
    {
        get => _userPassword;
        set => SetProperty(ref _userPassword, value);
    }

    public bool UserIsActive
    {
        get => _userIsActive;
        set => SetProperty(ref _userIsActive, value);
    }

    public string UserMessage
    {
        get => _userMessage;
        set => SetProperty(ref _userMessage, value);
    }

    public string PermissionMessage
    {
        get => _permissionMessage;
        set => SetProperty(ref _permissionMessage, value);
    }

    public string SessionText
    {
        get => _sessionText;
        set => SetProperty(ref _sessionText, value);
    }

    public string ActiveWorkspace => _activeWorkspace;

    public bool IsQuickNavigationOpen
    {
        get => _isQuickNavigationOpen;
        set => SetProperty(ref _isQuickNavigationOpen, value);
    }

    public string QuickNavigationSearchText
    {
        get => _quickNavigationSearchText;
        set
        {
            if (SetProperty(ref _quickNavigationSearchText, value))
            {
                RefreshQuickNavigationResults();
            }
        }
    }

    public QuickNavigationItemViewModel? SelectedQuickNavigationItem
    {
        get => _selectedQuickNavigationItem;
        set => SetProperty(ref _selectedQuickNavigationItem, value);
    }

    public ObservableCollection<ProductLookupDto> Products { get; } = new();
    public ObservableCollection<ProductDetailsDto> ManagedProducts { get; } = new();
    public ObservableCollection<ProductTypeOptionDto> ProductTypes { get; } = new(ProductTypeLabels.SellableOptions);
    public ObservableCollection<CategoryDetailsDto> ManagedCategories { get; } = new();
    public ObservableCollection<MeasurementUnitOptionDto> CategoryMeasurementUnits { get; } = new(MeasurementUnitLabels.Options);
    public ObservableCollection<InventoryProductDto> InventoryProducts { get; } = new();
    public ObservableCollection<StockMovementDto> StockMovements { get; } = new();
    public ObservableCollection<LowStockNotificationDto> LowStockNotifications { get; } = new();
    public ObservableCollection<CategoryOptionDto> Categories { get; } = new();
    public ObservableCollection<CartLineViewModel> CartLines { get; } = new();
    public ObservableCollection<SuspendedInvoiceSummaryDto> SuspendedInvoices { get; } = new();
    public ObservableCollection<PrintingServiceTemplateListItemDto> PrintingServiceTemplates { get; } = new();
    public ObservableCollection<PrintingServiceTemplateListItemDto> CashierPrintingTemplates { get; } = new();
    public ObservableCollection<PrintingServiceTypeOptionDto> PrintingServiceTypes { get; } = new(PrintingServiceTypeLabels.Options);
    public ObservableCollection<InkCostModeOptionDto> InkCostModes { get; } = new(InkCostModeLabels.Options);
    public ObservableCollection<PrintingMaterialProductOptionDto> PrintingMaterialProducts { get; } = new();
    public ObservableCollection<PrintingMaterialConsumptionViewModel> PrintingTemplateMaterials { get; } = new();
    public ObservableCollection<PrintingMaterialDto> ManagedPrintingMaterials { get; } = new();
    public ObservableCollection<ReturnItemViewModel> ReturnItems { get; } = new();
    public ObservableCollection<ReturnInvoiceMatchDto> PossibleReturnInvoices { get; } = new();
    public ObservableCollection<TopSellingProductDto> TopSellingProducts { get; } = new();
    public ObservableCollection<InvoiceHistoryListItemDto> InvoiceHistoryItems { get; } = new();
    public ObservableCollection<InvoiceHistoryLineDto> InvoiceHistoryLines { get; } = new();
    public ObservableCollection<InvoiceHistoryReturnDto> InvoiceHistoryReturns { get; } = new();
    public ObservableCollection<string> InstalledPrinters { get; } = new();
    public ObservableCollection<string> ReceiptPaperWidths { get; } = new(["80mm", "58mm"]);
    public ObservableCollection<string> BarcodeLabelSizes { get; } = new([
        "38x50 mm",
        "40x55 mm",
        "50x25 mm",
        "60x40 mm",
        "38x25 mm",
        "38x25 mm - 2 stacked"
    ]);
    public ObservableCollection<string> BarcodePrinterProfiles { get; } = new([
        "Auto",
        "WindowsDriver",
        "TSPL",
        "ZPL"
    ]);
    public ObservableCollection<BackupFileDto> BackupFiles { get; } = new();
    public ObservableCollection<AuditLogEntryDto> AuditLogs { get; } = new();
    public ObservableCollection<AuditUserFilterOptionDto> AuditUserFilters { get; } = new();
    public ObservableCollection<AuditActionFilterOptionDto> AuditActionFilters { get; } = new();
    public ObservableCollection<AuditDetailFieldDto> AuditDetailFields { get; } = new();
    public ObservableCollection<AuditDetailLineDto> AuditDetailLines { get; } = new();
    public ObservableCollection<AuditTimelineEntryDto> AuditDetailTimeline { get; } = new();
    public ObservableCollection<UserDetailsDto> Users { get; } = new();
    public ObservableCollection<RoleOptionDto> UserRoles { get; } = new();
    public ObservableCollection<PermissionItemViewModel> UserPermissions { get; } = new();
    public ObservableCollection<QuickNavigationItemViewModel> QuickNavigationResults { get; } = new();
    public ICommand LoginCommand { get; }
    public ICommand ChangeRequiredPasswordCommand { get; }
    public ICommand OpenShiftCommand { get; }
    public ICommand SearchCommand { get; }
    public ICommand SearchOrAddProductCommand { get; }
    public ICommand AddSelectedProductCommand { get; }
    public ICommand RemoveSelectedCartLineCommand { get; }
    public ICommand HoldInvoiceCommand { get; }
    public ICommand ResumeSuspendedInvoiceCommand { get; }
    public ICommand CancelSuspendedInvoiceCommand { get; }
    public ICommand RefreshSuspendedInvoicesCommand { get; }
    public ICommand ToggleNotificationsCommand { get; }
    public ICommand AddPrintServiceCommand { get; }
    public ICommand AddPrintingTemplateToCartCommand { get; }
    public ICommand CompleteSaleCommand { get; }
    public ICommand CompleteSaleAndPrintCommand { get; }
    public ICommand PrintLastReceiptCommand { get; }
    public ICommand ShowPosCommand { get; }
    public ICommand ShowProductsCommand { get; }
    public ICommand ShowBarcodeCommand { get; }
    public ICommand ShowCategoriesCommand { get; }
    public ICommand ShowPrintingMaterialsCommand { get; }
    public ICommand ShowProductionCommand { get; }
    public ICommand SearchPrintingMaterialsCommand { get; }
    public ICommand NewPrintingMaterialCommand { get; }
    public ICommand EditSelectedPrintingMaterialCommand { get; }
    public ICommand SavePrintingMaterialCommand { get; }
    public ICommand TogglePrintingMaterialActiveCommand { get; }
    public ICommand SearchManagedProductsCommand { get; }
    public ICommand SaveCategoryCommand { get; }
    public ICommand NewCategoryCommand { get; }
    public ICommand EditSelectedCategoryCommand { get; }
    public ICommand ToggleCategoryActiveCommand { get; }
    public ICommand NewProductCommand { get; }
    public ICommand EditSelectedProductCommand { get; }
    public ICommand SaveProductCommand { get; }
    public ICommand ToggleProductActiveCommand { get; }
    public ICommand PrintSelectedProductBarcodeCommand { get; }
    public ICommand ImportProductsCommand { get; }
    public ICommand ExportProductsCommand { get; }
    public ICommand ShowPrintingServicesCommand { get; }
    public ICommand SearchPrintingServicesCommand { get; }
    public ICommand NewPrintingServiceTemplateCommand { get; }
    public ICommand EditSelectedPrintingServiceTemplateCommand { get; }
    public ICommand SavePrintingServiceTemplateCommand { get; }
    public ICommand TogglePrintingServiceTemplateActiveCommand { get; }
    public ICommand LoadPrintingMaterialProductsCommand { get; }
    public ICommand AddPrintingMaterialCommand { get; }
    public ICommand RemovePrintingMaterialCommand { get; }
    public ICommand ShowInventoryCommand { get; }
    public ICommand SearchInventoryCommand { get; }
    public ICommand AdjustStockCommand { get; }
    public ICommand ShowShiftCommand { get; }
    public ICommand CloseShiftCommand { get; }
    public ICommand ShowReportsCommand { get; }
    public ICommand ShowProfitReportsCommand { get; }
    public ICommand LoadReportsCommand { get; }
    public ICommand ReportTodayCommand { get; }
    public ICommand ReportWeekCommand { get; }
    public ICommand ReportMonthCommand { get; }
    public ICommand ShowInvoiceHistoryCommand { get; }
    public ICommand LoadInvoiceHistoryCommand { get; }
    public ICommand InvoiceHistoryTodayCommand { get; }
    public ICommand InvoiceHistoryWeekCommand { get; }
    public ICommand InvoiceHistoryMonthCommand { get; }
    public ICommand PrintSelectedInvoiceHistoryCommand { get; }
    public ICommand ShowPrinterSettingsCommand { get; }
    public ICommand SavePrinterSettingsCommand { get; }
    public ICommand TestReceiptPrinterCommand { get; }
    public ICommand TestBarcodeLabelPrinterCommand { get; }
    public ICommand RefreshPrintersCommand { get; }
    public ICommand ShowSystemSettingsCommand { get; }
    public ICommand SaveSystemSettingsCommand { get; }
    public ICommand ShowBackupCommand { get; }
    public ICommand CreateBackupCommand { get; }
    public ICommand RestoreBackupCommand { get; }
    public ICommand ImportBackupCommand { get; }
    public ICommand ExportSelectedBackupCommand { get; }
    public ICommand DeleteSelectedBackupCommand { get; }
    public ICommand ExportLogsCommand { get; }
    public ICommand ShowAuditLogsCommand { get; }
    public ICommand LoadAuditLogsCommand { get; }
    public ICommand ShowUsersCommand { get; }
    public ICommand NewUserCommand { get; }
    public ICommand EditSelectedUserCommand { get; }
    public ICommand SaveUserCommand { get; }
    public ICommand ToggleUserActiveCommand { get; }
    public ICommand ShowReturnsCommand { get; }
    public ICommand FindInvoiceForReturnCommand { get; }
    public ICommand SelectPossibleReturnInvoiceCommand { get; }
    public ICommand PrintReturnInvoiceCommand { get; }
    public ICommand ReturnAllItemsCommand { get; }
    public ICommand SaveReturnCommand { get; }
    public ICommand RefreshCurrentWorkspaceCommand { get; }
    public ICommand OpenQuickNavigationCommand { get; }
    public ICommand CloseQuickNavigationCommand { get; }
    public ICommand NavigateToSelectedQuickNavigationCommand { get; }
    public Visibility LoginVisibility => _isAuthenticated ? Visibility.Collapsed : Visibility.Visible;
    public Visibility ChangePasswordVisibility => _isAuthenticated && _mustChangePassword ? Visibility.Visible : Visibility.Collapsed;
    public Visibility NavigationVisibility => CanUseApplication ? Visibility.Visible : Visibility.Collapsed;
    public bool CanOpenPos => HasPermission(PermissionCodes.CanUsePOS);
    public bool CanOpenProducts => HasPermission(PermissionCodes.CanEditProduct);
    public bool CanOpenBarcode => HasPermission(PermissionCodes.CanEditProduct);
    public bool CanOpenCategories => HasPermission(PermissionCodes.CanManageSettings);
    public bool CanOpenPrintingMaterials => HasPermission(PermissionCodes.CanManageStock);
    public bool CanOpenProduction => HasPermission(PermissionCodes.CanManageStock) || HasPermission(PermissionCodes.CanEditProduct);
    public bool CanOpenPrintingServices => HasPermission(PermissionCodes.CanManageSettings);
    public bool CanOpenInventory => HasPermission(PermissionCodes.CanManageStock);
    public bool CanOpenReturns => HasPermission(PermissionCodes.CanReturnInvoice);
    public bool CanOpenShift => HasPermission(PermissionCodes.CanCloseShift);
    public bool CanOpenReports => HasPermission(PermissionCodes.CanViewReports);
    public bool CanOpenProfitReports => HasPermission(PermissionCodes.CanViewReports);
    public bool CanOpenInvoiceHistory => HasPermission(PermissionCodes.CanViewReports);
    public bool CanOpenPrinterSettings => HasPermission(PermissionCodes.CanChangePrinterSettings);
    public bool CanOpenSystemSettings => HasPermission(PermissionCodes.CanManageSettings);
    public bool CanOpenBackup => HasPermission(PermissionCodes.CanBackupRestore);
    public bool CanOpenAuditLogs => HasPermission(PermissionCodes.CanViewAuditLogs);
    public bool CanOpenUsers => HasPermission(PermissionCodes.CanManageUsers);
    public bool CanEditProductPrice => HasPermission(PermissionCodes.CanEditPrice);
    public bool CanEditProductStock => HasPermission(PermissionCodes.CanManageStock);
    public bool CanToggleManagedProductActive =>
        SelectedManagedProduct is not null &&
        (SelectedManagedProduct.IsActive
            ? HasPermission(PermissionCodes.CanDeleteProduct)
            : HasPermission(PermissionCodes.CanEditProduct));
    public bool CanToggleManagedCategoryActive =>
        SelectedManagedCategory is not null && HasPermission(PermissionCodes.CanManageSettings);
    public bool CanTogglePrintingServiceTemplateActive =>
        SelectedPrintingServiceTemplate is not null && HasPermission(PermissionCodes.CanManageSettings);
    public bool CanToggleManagedPrintingMaterialActive =>
        SelectedManagedPrintingMaterial is not null && HasPermission(PermissionCodes.CanManageStock);
    public string SelectedCategoryUnitText => SelectedCategory is null
        ? "اختر التصنيف أولاً"
        : $"وحدة القياس: {SelectedCategory.MeasurementUnitText}";
    public string SelectedManagedPrintingMaterialUnitText => SelectedManagedPrintingMaterialCategory is null
        ? "اختر التصنيف لتحديد وحدة القياس"
        : $"وحدة القياس: {SelectedManagedPrintingMaterialCategory.MeasurementUnitText}";
    public string ProductStockQuantityLabel => SelectedCategory?.MeasurementUnit switch
    {
        MeasurementUnit.Meter or MeasurementUnit.Kilogram or MeasurementUnit.Liter => "الكمية الموجودة",
        _ => "عدد المنتج"
    };
    public string ProductPurchasePriceLabel => SelectedCategory?.MeasurementUnit switch
    {
        MeasurementUnit.Meter => "سعر الشراء للمتر",
        MeasurementUnit.Kilogram => "سعر الشراء للكيلو",
        MeasurementUnit.Liter => "سعر الشراء للتر",
        MeasurementUnit.Carton => "سعر الشراء للكرتونة",
        MeasurementUnit.Box => "سعر الشراء للعلبة",
        _ => "سعر الشراء"
    };
    public string ProductSalePriceLabel => SelectedCategory?.MeasurementUnit switch
    {
        MeasurementUnit.Meter => "سعر البيع للمتر",
        MeasurementUnit.Kilogram => "سعر البيع للكيلو",
        MeasurementUnit.Liter => "سعر البيع للتر",
        MeasurementUnit.Carton or MeasurementUnit.Box => "سعر البيع للقطعة",
        _ => "سعر البيع"
    };
    public Visibility DirectQuantityVisibility => IsPackageBasedUnit(SelectedCategory?.MeasurementUnit) ? Visibility.Collapsed : Visibility.Visible;
    public Visibility PackageQuantityVisibility => IsPackageBasedUnit(SelectedCategory?.MeasurementUnit) ? Visibility.Visible : Visibility.Collapsed;
    public string PackageCountLabel => SelectedCategory?.MeasurementUnit == MeasurementUnit.Carton ? "عدد الكراتين" : "عدد العلب";
    public string UnitsPerPackageLabel => SelectedCategory?.MeasurementUnit == MeasurementUnit.Carton ? "عدد القطع داخل كل كرتونة" : "عدد القطع داخل كل علبة";
    public string ComputedProductStockQuantityText => IsPackageBasedUnit(SelectedCategory?.MeasurementUnit)
        ? $"إجمالي القطع المحسوب: {Math.Max(0, ProductPackageCount) * Math.Max(0, ProductUnitsPerPackage)}"
        : string.Empty;
    public Visibility OpenShiftVisibility => CanUseApplication && HasPermission(PermissionCodes.CanUsePOS) && !_shiftId.HasValue && _activeWorkspace == "POS" ? Visibility.Visible : Visibility.Collapsed;
    public Visibility PosVisibility => CanUseApplication && HasPermission(PermissionCodes.CanUsePOS) && _shiftId.HasValue && _activeWorkspace == "POS" ? Visibility.Visible : Visibility.Collapsed;
    public Visibility ProductManagementVisibility => CanUseApplication && HasPermission(PermissionCodes.CanEditProduct) && _activeWorkspace == "Products" ? Visibility.Visible : Visibility.Collapsed;
    public Visibility BarcodeVisibility => CanUseApplication && CanOpenBarcode && _activeWorkspace == "Barcode" ? Visibility.Visible : Visibility.Collapsed;
    public Visibility CategoryManagementVisibility => CanUseApplication && HasPermission(PermissionCodes.CanManageSettings) && _activeWorkspace == "Categories" ? Visibility.Visible : Visibility.Collapsed;
    public Visibility PrintingMaterialsVisibility => CanUseApplication && CanOpenPrintingMaterials && _activeWorkspace == "PrintingMaterials" ? Visibility.Visible : Visibility.Collapsed;
    public Visibility ProductionVisibility => CanUseApplication && CanOpenProduction && _activeWorkspace == "Production" ? Visibility.Visible : Visibility.Collapsed;
    public Visibility PrintingServicesVisibility => CanUseApplication && HasPermission(PermissionCodes.CanManageSettings) && _activeWorkspace == "PrintingServices" ? Visibility.Visible : Visibility.Collapsed;
    public Visibility InventoryVisibility => CanUseApplication && HasPermission(PermissionCodes.CanManageStock) && _activeWorkspace == "Inventory" ? Visibility.Visible : Visibility.Collapsed;
    public Visibility ReturnsVisibility => CanUseApplication && HasPermission(PermissionCodes.CanReturnInvoice) && _activeWorkspace == "Returns" ? Visibility.Visible : Visibility.Collapsed;
    public Visibility ShiftVisibility => CanUseApplication && HasPermission(PermissionCodes.CanCloseShift) && _activeWorkspace == "Shift" ? Visibility.Visible : Visibility.Collapsed;
    public Visibility ReportsVisibility => CanUseApplication && HasPermission(PermissionCodes.CanViewReports) && _activeWorkspace == "Reports" ? Visibility.Visible : Visibility.Collapsed;
    public Visibility ProfitReportsVisibility => CanUseApplication && HasPermission(PermissionCodes.CanViewReports) && _activeWorkspace == "ProfitReports" ? Visibility.Visible : Visibility.Collapsed;
    public Visibility InvoiceHistoryVisibility => CanUseApplication && HasPermission(PermissionCodes.CanViewReports) && _activeWorkspace == "InvoiceHistory" ? Visibility.Visible : Visibility.Collapsed;
    public Visibility PrinterSettingsVisibility => CanUseApplication && HasPermission(PermissionCodes.CanChangePrinterSettings) && _activeWorkspace == "PrinterSettings" ? Visibility.Visible : Visibility.Collapsed;
    public Visibility SystemSettingsVisibility => CanUseApplication && HasPermission(PermissionCodes.CanManageSettings) && _activeWorkspace == "SystemSettings" ? Visibility.Visible : Visibility.Collapsed;
    public Visibility BackupVisibility => CanUseApplication && HasPermission(PermissionCodes.CanBackupRestore) && _activeWorkspace == "Backup" ? Visibility.Visible : Visibility.Collapsed;
    public Visibility AuditLogsVisibility => CanUseApplication && HasPermission(PermissionCodes.CanViewAuditLogs) && _activeWorkspace == "AuditLogs" ? Visibility.Visible : Visibility.Collapsed;
    public Visibility UsersVisibility => CanUseApplication && HasPermission(PermissionCodes.CanManageUsers) && _activeWorkspace == "Users" ? Visibility.Visible : Visibility.Collapsed;
    public Visibility PossibleReturnInvoicesVisibility => PossibleReturnInvoices.Count > 1 ? Visibility.Visible : Visibility.Collapsed;
    public Visibility DiscountVisibility => SystemDiscountsEnabled && HasPermission(PermissionCodes.CanApplyDiscount) ? Visibility.Visible : Visibility.Collapsed;
    public decimal CartSubtotal => CartLines.Sum(x => x.Total);
    public decimal CartDiscountAmount => CalculateCurrentDiscountAmount();
    public decimal CartTotal => Math.Max(0, CartSubtotal - CartDiscountAmount);
    public string CartTotalText => $"{CartTotal:0.00} ج.م";
    public decimal ReturnTotal => ReturnItems.Sum(x => x.ReturnTotal);
    public string ReturnTotalText => $"إجمالي المرتجع: {ReturnTotal:0.00} ج.م";

    private bool HasPermission(string permissionCode) => _currentPermissions.Contains(permissionCode);
    private bool CanUseApplication => _isAuthenticated && !_mustChangePassword;

    private static bool IsPackageBasedUnit(MeasurementUnit? unit) =>
        unit is MeasurementUnit.Carton or MeasurementUnit.Box;

    private void RaiseSelectedCategoryUnitChanges()
    {
        OnPropertyChanged(nameof(SelectedCategoryUnitText));
        OnPropertyChanged(nameof(ProductStockQuantityLabel));
        OnPropertyChanged(nameof(ProductPurchasePriceLabel));
        OnPropertyChanged(nameof(ProductSalePriceLabel));
        OnPropertyChanged(nameof(DirectQuantityVisibility));
        OnPropertyChanged(nameof(PackageQuantityVisibility));
        OnPropertyChanged(nameof(PackageCountLabel));
        OnPropertyChanged(nameof(UnitsPerPackageLabel));
        OnPropertyChanged(nameof(ComputedProductStockQuantityText));
    }

    private void SelectDefaultWorkspace()
    {
        _activeWorkspace =
            HasPermission(PermissionCodes.CanUsePOS) ? "POS" :
            HasPermission(PermissionCodes.CanEditProduct) ? "Products" :
            HasPermission(PermissionCodes.CanManageSettings) ? "PrintingServices" :
            HasPermission(PermissionCodes.CanManageStock) ? "Inventory" :
            HasPermission(PermissionCodes.CanReturnInvoice) ? "Returns" :
            HasPermission(PermissionCodes.CanCloseShift) ? "Shift" :
            HasPermission(PermissionCodes.CanViewReports) ? "Reports" :
            HasPermission(PermissionCodes.CanChangePrinterSettings) ? "PrinterSettings" :
            HasPermission(PermissionCodes.CanManageSettings) ? "SystemSettings" :
            HasPermission(PermissionCodes.CanBackupRestore) ? "Backup" :
            HasPermission(PermissionCodes.CanViewAuditLogs) ? "AuditLogs" :
            HasPermission(PermissionCodes.CanManageUsers) ? "Users" :
            string.Empty;
    }

    private void RaisePermissionVisibilityChanges()
    {
        OnPropertyChanged(nameof(CanOpenPos));
        OnPropertyChanged(nameof(CanOpenProducts));
        OnPropertyChanged(nameof(CanOpenBarcode));
        OnPropertyChanged(nameof(CanOpenCategories));
        OnPropertyChanged(nameof(CanOpenPrintingMaterials));
        OnPropertyChanged(nameof(CanOpenProduction));
        OnPropertyChanged(nameof(CanOpenPrintingServices));
        OnPropertyChanged(nameof(CanOpenInventory));
        OnPropertyChanged(nameof(CanOpenReturns));
        OnPropertyChanged(nameof(CanOpenShift));
        OnPropertyChanged(nameof(CanOpenReports));
        OnPropertyChanged(nameof(CanOpenProfitReports));
        OnPropertyChanged(nameof(CanOpenInvoiceHistory));
        OnPropertyChanged(nameof(CanOpenPrinterSettings));
        OnPropertyChanged(nameof(CanOpenSystemSettings));
        OnPropertyChanged(nameof(CanOpenBackup));
        OnPropertyChanged(nameof(CanOpenAuditLogs));
        OnPropertyChanged(nameof(CanOpenUsers));
        OnPropertyChanged(nameof(CanEditProductPrice));
        OnPropertyChanged(nameof(CanEditProductStock));
        OnPropertyChanged(nameof(CanToggleManagedProductActive));
        OnPropertyChanged(nameof(CanToggleManagedCategoryActive));
        OnPropertyChanged(nameof(CanTogglePrintingServiceTemplateActive));
        OnPropertyChanged(nameof(CanToggleManagedPrintingMaterialActive));
        OnPropertyChanged(nameof(DiscountVisibility));
    }

    private void RefreshWorkspaceAfterPermissionChange()
    {
        var canStay =
            _activeWorkspace == "POS" && HasPermission(PermissionCodes.CanUsePOS) ||
            _activeWorkspace == "Products" && HasPermission(PermissionCodes.CanEditProduct) ||
            _activeWorkspace == "Barcode" && CanOpenBarcode ||
            _activeWorkspace == "Categories" && HasPermission(PermissionCodes.CanManageSettings) ||
            _activeWorkspace == "PrintingMaterials" && CanOpenPrintingMaterials ||
            _activeWorkspace == "Production" && CanOpenProduction ||
            _activeWorkspace == "PrintingServices" && HasPermission(PermissionCodes.CanManageSettings) ||
            _activeWorkspace == "Inventory" && HasPermission(PermissionCodes.CanManageStock) ||
            _activeWorkspace == "Returns" && HasPermission(PermissionCodes.CanReturnInvoice) ||
            _activeWorkspace == "Shift" && HasPermission(PermissionCodes.CanCloseShift) ||
            _activeWorkspace == "Reports" && HasPermission(PermissionCodes.CanViewReports) ||
            _activeWorkspace == "ProfitReports" && HasPermission(PermissionCodes.CanViewReports) ||
            _activeWorkspace == "InvoiceHistory" && HasPermission(PermissionCodes.CanViewReports) ||
            _activeWorkspace == "PrinterSettings" && HasPermission(PermissionCodes.CanChangePrinterSettings) ||
            _activeWorkspace == "SystemSettings" && HasPermission(PermissionCodes.CanManageSettings) ||
            _activeWorkspace == "Backup" && HasPermission(PermissionCodes.CanBackupRestore) ||
            _activeWorkspace == "AuditLogs" && HasPermission(PermissionCodes.CanViewAuditLogs) ||
            _activeWorkspace == "Users" && HasPermission(PermissionCodes.CanManageUsers);

        if (!canStay)
        {
            SelectDefaultWorkspace();
        }

        OnPropertyChanged(nameof(OpenShiftVisibility));
        OnPropertyChanged(nameof(PosVisibility));
        OnPropertyChanged(nameof(ProductManagementVisibility));
        OnPropertyChanged(nameof(BarcodeVisibility));
        OnPropertyChanged(nameof(CategoryManagementVisibility));
        OnPropertyChanged(nameof(PrintingMaterialsVisibility));
        OnPropertyChanged(nameof(ProductionVisibility));
        OnPropertyChanged(nameof(PrintingServicesVisibility));
        OnPropertyChanged(nameof(InventoryVisibility));
        OnPropertyChanged(nameof(ReturnsVisibility));
        OnPropertyChanged(nameof(ShiftVisibility));
        OnPropertyChanged(nameof(ReportsVisibility));
        OnPropertyChanged(nameof(ProfitReportsVisibility));
        OnPropertyChanged(nameof(InvoiceHistoryVisibility));
        OnPropertyChanged(nameof(PrinterSettingsVisibility));
        OnPropertyChanged(nameof(SystemSettingsVisibility));
        OnPropertyChanged(nameof(BackupVisibility));
        OnPropertyChanged(nameof(AuditLogsVisibility));
        OnPropertyChanged(nameof(UsersVisibility));
        OnPropertyChanged(nameof(ActiveWorkspace));
        RefreshQuickNavigationResults();
        RaisePermissionVisibilityChanges();
    }

    private void SetActiveWorkspace(string workspace)
    {
        _activeWorkspace = workspace;
        RaiseWorkspaceVisibilityChanges();
    }

    private void RaiseWorkspaceVisibilityChanges()
    {
        OnPropertyChanged(nameof(ActiveWorkspace));
        OnPropertyChanged(nameof(ChangePasswordVisibility));
        OnPropertyChanged(nameof(OpenShiftVisibility));
        OnPropertyChanged(nameof(PosVisibility));
        OnPropertyChanged(nameof(ProductManagementVisibility));
        OnPropertyChanged(nameof(BarcodeVisibility));
        OnPropertyChanged(nameof(CategoryManagementVisibility));
        OnPropertyChanged(nameof(PrintingMaterialsVisibility));
        OnPropertyChanged(nameof(ProductionVisibility));
        OnPropertyChanged(nameof(PrintingServicesVisibility));
        OnPropertyChanged(nameof(InventoryVisibility));
        OnPropertyChanged(nameof(ReturnsVisibility));
        OnPropertyChanged(nameof(ShiftVisibility));
        OnPropertyChanged(nameof(ReportsVisibility));
        OnPropertyChanged(nameof(ProfitReportsVisibility));
        OnPropertyChanged(nameof(InvoiceHistoryVisibility));
        OnPropertyChanged(nameof(PrinterSettingsVisibility));
        OnPropertyChanged(nameof(SystemSettingsVisibility));
        OnPropertyChanged(nameof(BackupVisibility));
        OnPropertyChanged(nameof(AuditLogsVisibility));
        OnPropertyChanged(nameof(UsersVisibility));
        RefreshQuickNavigationResults();
    }

    private async Task NavigateToWorkspaceAsync(string workspace)
    {
        switch (workspace)
        {
            case "POS":
                await ShowPosAsync();
                break;
            case "Products":
                await ShowProductsAsync();
                break;
            case "Barcode":
                await ShowBarcodeAsync();
                break;
            case "Categories":
                await ShowCategoriesAsync();
                break;
            case "PrintingMaterials":
                await ShowPrintingMaterialsAsync();
                break;
            case "Production":
                await ShowProductionAsync();
                break;
            case "PrintingServices":
                await ShowPrintingServicesAsync();
                break;
            case "Inventory":
                await ShowInventoryAsync();
                break;
            case "Returns":
                await ShowReturnsAsync();
                break;
            case "Shift":
                await ShowShiftAsync();
                break;
            case "Reports":
                await ShowReportsAsync();
                break;
            case "ProfitReports":
                await ShowProfitReportsAsync();
                break;
            case "InvoiceHistory":
                await ShowInvoiceHistoryAsync();
                break;
            case "PrinterSettings":
                await ShowPrinterSettingsAsync();
                break;
            case "SystemSettings":
                await ShowSystemSettingsAsync();
                break;
            case "Backup":
                await ShowBackupAsync();
                break;
            case "AuditLogs":
                await ShowAuditLogsAsync();
                break;
            case "Users":
                await ShowUsersAsync();
                break;
        }
    }

    private async Task RefreshCurrentWorkspaceAsync()
    {
        if (!CanUseApplication)
        {
            return;
        }

        await NavigateToWorkspaceAsync(_activeWorkspace);
    }

    private Task OpenQuickNavigationAsync()
    {
        if (!CanUseApplication)
        {
            return Task.CompletedTask;
        }

        QuickNavigationSearchText = string.Empty;
        RefreshQuickNavigationResults();
        IsQuickNavigationOpen = true;
        return Task.CompletedTask;
    }

    private Task CloseQuickNavigationAsync()
    {
        IsQuickNavigationOpen = false;
        return Task.CompletedTask;
    }

    private async Task NavigateToSelectedQuickNavigationAsync()
    {
        if (SelectedQuickNavigationItem is null || !SelectedQuickNavigationItem.IsEnabled)
        {
            return;
        }

        var workspace = SelectedQuickNavigationItem.Workspace;
        IsQuickNavigationOpen = false;
        QuickNavigationSearchText = string.Empty;
        await NavigateToWorkspaceAsync(workspace);
    }

    private void RefreshQuickNavigationResults()
    {
        var normalizedSearch = ArabicTextNormalizer.NormalizeForSearch(QuickNavigationSearchText);
        var matches = CreateQuickNavigationItems()
            .Where(x => x.IsEnabled)
            .Where(x =>
                string.IsNullOrWhiteSpace(normalizedSearch) ||
                ArabicTextNormalizer.NormalizeForSearch($"{x.Title} {x.Keywords}").Contains(normalizedSearch))
            .Take(12)
            .ToArray();

        QuickNavigationResults.Clear();
        foreach (var item in matches)
        {
            QuickNavigationResults.Add(item);
        }

        SelectedQuickNavigationItem = QuickNavigationResults.FirstOrDefault();
    }

    private IReadOnlyList<QuickNavigationItemViewModel> CreateQuickNavigationItems() =>
    [
        new("الكاشير", "POS", "بيع فاتورة باركود", CanOpenPos),
        new("الفواتير", "InvoiceHistory", "مراجعة فواتير طباعة فاتورة", CanOpenInvoiceHistory),
        new("المرتجعات", "Returns", "ارجاع مردود", CanOpenReturns),
        new("المنتجات", "Products", "باركود اصناف اسعار", CanOpenProducts),
        new("الباركود (قيد التطوير)", "Barcode", "طباعة ليبل ملصقات قيد التطوير", CanOpenBarcode),
        new("التصنيفات", "Categories", "وحدات قياس فئات", CanOpenCategories),
        new("المخزون والتنبيهات", "Inventory", "كمية تنبيه منخفض", CanOpenInventory),
        new("خدمات الطباعة", "PrintingServices", "تصوير سكان تغليف تجليد", CanOpenPrintingServices),
        new("خامات الطباعة", "PrintingMaterials", "ورق غلاف سلك رول خامات تشغيل", CanOpenPrintingMaterials),
        new("الإنتاج (قيد التطوير)", "Production", "مطبوعات مذكرة كتاب ملزمة قيد التطوير", CanOpenProduction),
        new("تقارير المبيعات", "Reports", "مبيعات يوم اسبوع شهر", CanOpenReports),
        new("تقارير الأرباح (قيد التطوير)", "ProfitReports", "ارباح تكلفة صافي ربح قيد التطوير", CanOpenProfitReports),
        new("الشيفت", "Shift", "فتح غلق وردية", CanOpenShift),
        new("المستخدمون والصلاحيات", "Users", "ادارة مستخدم صلاحية", CanOpenUsers),
        new("سجل التدقيق", "AuditLogs", "احداث عمليات", CanOpenAuditLogs),
        new("إعدادات النظام", "SystemSettings", "مكتبة خصومات مخزون", CanOpenSystemSettings),
        new("إعدادات الطباعة", "PrinterSettings", "ريسيت باركود طابعة", CanOpenPrinterSettings),
        new("النسخ الاحتياطي", "Backup", "باك اب استعادة تصدير", CanOpenBackup)
    ];

    private async Task LoginAsync()
    {
        LoginMessage = string.Empty;
        var result = await _authService.LoginAsync(Username, Password);

        if (!result.Succeeded || result.Value is null)
        {
            LoginMessage = result.Message;
            return;
        }

        _isAuthenticated = true;
        _mustChangePassword = result.Value.MustChangePassword;
        _cashierId = result.Value.UserId;
        _currentPermissions = result.Value.Permissions.ToHashSet(StringComparer.Ordinal);
        SessionText = $"مرحباً، {result.Value.FullName}";

        if (_mustChangePassword)
        {
            PasswordChangeMessage = "يجب تغيير كلمة المرور الافتراضية قبل استخدام النظام.";
            SessionText = "يجب تغيير كلمة المرور";
            RaiseAuthenticationVisibilityChanges();
            return;
        }

        await CompletePostLoginSetupAsync(result.Value.UserId);
        RaiseAuthenticationVisibilityChanges();
    }

    private async Task CompletePostLoginSetupAsync(int userId)
    {
        SelectDefaultWorkspace();
        _shiftId = await _shiftService.GetOpenShiftIdAsync(userId);
        await LoadSystemSettingsAsync();
        if (_shiftId.HasValue && HasPermission(PermissionCodes.CanUsePOS))
        {
            await SearchProductsAsync();
            await LoadSuspendedInvoicesAsync();
            await LoadCashierPrintingTemplatesAsync();
        }
        await RefreshNotificationsAsync();
    }

    private async Task ChangeRequiredPasswordAsync()
    {
        PasswordChangeMessage = string.Empty;
        if (!_cashierId.HasValue)
        {
            PasswordChangeMessage = "يجب تسجيل الدخول أولاً.";
            return;
        }

        if (NewPassword != ConfirmPassword)
        {
            PasswordChangeMessage = "تأكيد كلمة المرور غير مطابق.";
            return;
        }

        var result = await _authService.ChangeRequiredPasswordAsync(_cashierId.Value, NewPassword);
        PasswordChangeMessage = result.Message;
        if (!result.Succeeded)
        {
            return;
        }

        _mustChangePassword = false;
        NewPassword = string.Empty;
        ConfirmPassword = string.Empty;
        Password = string.Empty;
        SessionText = "تم تغيير كلمة المرور بنجاح";
        await CompletePostLoginSetupAsync(_cashierId.Value);
        RaiseAuthenticationVisibilityChanges();
    }

    private void RaiseAuthenticationVisibilityChanges()
    {
        OnPropertyChanged(nameof(LoginVisibility));
        OnPropertyChanged(nameof(ChangePasswordVisibility));
        OnPropertyChanged(nameof(NavigationVisibility));
        OnPropertyChanged(nameof(OpenShiftVisibility));
        OnPropertyChanged(nameof(PosVisibility));
        OnPropertyChanged(nameof(ProductManagementVisibility));
        OnPropertyChanged(nameof(BarcodeVisibility));
        OnPropertyChanged(nameof(CategoryManagementVisibility));
        OnPropertyChanged(nameof(PrintingMaterialsVisibility));
        OnPropertyChanged(nameof(ProductionVisibility));
        OnPropertyChanged(nameof(PrintingServicesVisibility));
        OnPropertyChanged(nameof(InventoryVisibility));
        OnPropertyChanged(nameof(ReturnsVisibility));
        OnPropertyChanged(nameof(ShiftVisibility));
        OnPropertyChanged(nameof(ReportsVisibility));
        OnPropertyChanged(nameof(ProfitReportsVisibility));
        OnPropertyChanged(nameof(InvoiceHistoryVisibility));
        OnPropertyChanged(nameof(PrinterSettingsVisibility));
        OnPropertyChanged(nameof(SystemSettingsVisibility));
        OnPropertyChanged(nameof(BackupVisibility));
        OnPropertyChanged(nameof(AuditLogsVisibility));
        OnPropertyChanged(nameof(UsersVisibility));
        OnPropertyChanged(nameof(ActiveWorkspace));
        RefreshQuickNavigationResults();
        RaisePermissionVisibilityChanges();
    }

    private async Task OpenShiftAsync()
    {
        if (!_cashierId.HasValue)
        {
            ShiftMessage = "يجب تسجيل الدخول أولاً.";
            return;
        }

        if (!TryParseMoney(OpeningCash, out var openingCash))
        {
            ShiftMessage = "رصيد بداية الشيفت غير صحيح. يمكنك كتابة المبلغ بهذا الشكل 500 أو 500.5 أو 500,5.";
            return;
        }

        Result<int> result;
        try
        {
            result = await _shiftService.OpenShiftAsync(_cashierId.Value, openingCash);
        }
        catch (UnauthorizedAccessException ex)
        {
            ShiftMessage = ex.Message;
            return;
        }

        if (!result.Succeeded || result.Value == 0)
        {
            ShiftMessage = result.Message;
            return;
        }

        _shiftId = result.Value;
        ShiftMessage = string.Empty;
        if (HasPermission(PermissionCodes.CanUsePOS))
        {
            await SearchProductsAsync();
            await LoadSuspendedInvoicesAsync();
            await LoadCashierPrintingTemplatesAsync();
        }
        OnPropertyChanged(nameof(OpenShiftVisibility));
        OnPropertyChanged(nameof(PosVisibility));
        OnPropertyChanged(nameof(InventoryVisibility));
        OnPropertyChanged(nameof(ReturnsVisibility));
        OnPropertyChanged(nameof(ShiftVisibility));
        OnPropertyChanged(nameof(ReportsVisibility));
        OnPropertyChanged(nameof(InvoiceHistoryVisibility));
        OnPropertyChanged(nameof(PrinterSettingsVisibility));
        OnPropertyChanged(nameof(SystemSettingsVisibility));
        OnPropertyChanged(nameof(BackupVisibility));
        OnPropertyChanged(nameof(AuditLogsVisibility));
        OnPropertyChanged(nameof(UsersVisibility));
    }

    private async Task ShowPosAsync()
    {
        SetActiveWorkspace("POS");
        await LoadSystemSettingsAsync();
        if (_shiftId.HasValue)
        {
            await SearchProductsAsync();
            await LoadSuspendedInvoicesAsync();
            await LoadCashierPrintingTemplatesAsync();
        }

        OnPropertyChanged(nameof(PosVisibility));
        OnPropertyChanged(nameof(ProductManagementVisibility));
        OnPropertyChanged(nameof(InventoryVisibility));
        OnPropertyChanged(nameof(ReturnsVisibility));
        OnPropertyChanged(nameof(OpenShiftVisibility));
        OnPropertyChanged(nameof(ShiftVisibility));
        OnPropertyChanged(nameof(ReportsVisibility));
        OnPropertyChanged(nameof(PrinterSettingsVisibility));
        OnPropertyChanged(nameof(SystemSettingsVisibility));
        OnPropertyChanged(nameof(BackupVisibility));
        OnPropertyChanged(nameof(AuditLogsVisibility));
        OnPropertyChanged(nameof(UsersVisibility));
    }

    private async Task ShowProductsAsync()
    {
        SetActiveWorkspace("Products");
        await LoadCategoriesAsync();
        await LoadManagedProductsAsync();
        OnPropertyChanged(nameof(PosVisibility));
        OnPropertyChanged(nameof(ProductManagementVisibility));
        OnPropertyChanged(nameof(InventoryVisibility));
        OnPropertyChanged(nameof(ReturnsVisibility));
        OnPropertyChanged(nameof(OpenShiftVisibility));
        OnPropertyChanged(nameof(ShiftVisibility));
        OnPropertyChanged(nameof(ReportsVisibility));
        OnPropertyChanged(nameof(PrinterSettingsVisibility));
        OnPropertyChanged(nameof(SystemSettingsVisibility));
        OnPropertyChanged(nameof(BackupVisibility));
        OnPropertyChanged(nameof(AuditLogsVisibility));
        OnPropertyChanged(nameof(UsersVisibility));
    }

    private Task ShowBarcodeAsync()
    {
        SetActiveWorkspace("Barcode");
        return Task.CompletedTask;
    }

    private async Task ShowCategoriesAsync()
    {
        SetActiveWorkspace("Categories");
        await LoadManagedCategoriesAsync();
        await LoadCategoriesAsync();
    }

    private async Task ShowPrintingMaterialsAsync()
    {
        SetActiveWorkspace("PrintingMaterials");
        await LoadCategoriesAsync();
        await LoadManagedPrintingMaterialsAsync();
    }

    private Task ShowProductionAsync()
    {
        SetActiveWorkspace("Production");
        return Task.CompletedTask;
    }

    private async Task ShowPrintingServicesAsync()
    {
        SetActiveWorkspace("PrintingServices");
        await LoadPrintingServicesAsync();
        await LoadPrintingMaterialProductsAsync();
    }

    private async Task ShowInventoryAsync()
    {
        SetActiveWorkspace("Inventory");
        await LoadInventoryAsync();
        OnPropertyChanged(nameof(PosVisibility));
        OnPropertyChanged(nameof(ProductManagementVisibility));
        OnPropertyChanged(nameof(InventoryVisibility));
        OnPropertyChanged(nameof(ReturnsVisibility));
        OnPropertyChanged(nameof(OpenShiftVisibility));
        OnPropertyChanged(nameof(ShiftVisibility));
        OnPropertyChanged(nameof(ReportsVisibility));
        OnPropertyChanged(nameof(PrinterSettingsVisibility));
        OnPropertyChanged(nameof(SystemSettingsVisibility));
        OnPropertyChanged(nameof(BackupVisibility));
        OnPropertyChanged(nameof(AuditLogsVisibility));
        OnPropertyChanged(nameof(UsersVisibility));
    }

    private async Task ShowShiftAsync()
    {
        SetActiveWorkspace("Shift");
        await LoadShiftSummaryAsync();
        OnPropertyChanged(nameof(PosVisibility));
        OnPropertyChanged(nameof(ProductManagementVisibility));
        OnPropertyChanged(nameof(InventoryVisibility));
        OnPropertyChanged(nameof(ReturnsVisibility));
        OnPropertyChanged(nameof(OpenShiftVisibility));
        OnPropertyChanged(nameof(ShiftVisibility));
        OnPropertyChanged(nameof(ReportsVisibility));
        OnPropertyChanged(nameof(PrinterSettingsVisibility));
        OnPropertyChanged(nameof(SystemSettingsVisibility));
        OnPropertyChanged(nameof(BackupVisibility));
        OnPropertyChanged(nameof(AuditLogsVisibility));
        OnPropertyChanged(nameof(UsersVisibility));
    }

    private async Task ShowReportsAsync()
    {
        SetActiveWorkspace("Reports");
        await LoadReportsAsync();
        OnPropertyChanged(nameof(PosVisibility));
        OnPropertyChanged(nameof(ProductManagementVisibility));
        OnPropertyChanged(nameof(InventoryVisibility));
        OnPropertyChanged(nameof(ReturnsVisibility));
        OnPropertyChanged(nameof(OpenShiftVisibility));
        OnPropertyChanged(nameof(ShiftVisibility));
        OnPropertyChanged(nameof(ReportsVisibility));
        OnPropertyChanged(nameof(PrinterSettingsVisibility));
        OnPropertyChanged(nameof(SystemSettingsVisibility));
        OnPropertyChanged(nameof(BackupVisibility));
        OnPropertyChanged(nameof(AuditLogsVisibility));
        OnPropertyChanged(nameof(UsersVisibility));
    }

    private Task ShowProfitReportsAsync()
    {
        SetActiveWorkspace("ProfitReports");
        return Task.CompletedTask;
    }

    private async Task ShowInvoiceHistoryAsync()
    {
        SetActiveWorkspace("InvoiceHistory");
        await LoadInvoiceHistoryAsync();
    }

    private async Task ShowPrinterSettingsAsync()
    {
        SetActiveWorkspace("PrinterSettings");
        await LoadPrinterSettingsAsync();
        OnPropertyChanged(nameof(PosVisibility));
        OnPropertyChanged(nameof(ProductManagementVisibility));
        OnPropertyChanged(nameof(InventoryVisibility));
        OnPropertyChanged(nameof(ReturnsVisibility));
        OnPropertyChanged(nameof(OpenShiftVisibility));
        OnPropertyChanged(nameof(ShiftVisibility));
        OnPropertyChanged(nameof(ReportsVisibility));
        OnPropertyChanged(nameof(PrinterSettingsVisibility));
        OnPropertyChanged(nameof(SystemSettingsVisibility));
        OnPropertyChanged(nameof(BackupVisibility));
        OnPropertyChanged(nameof(AuditLogsVisibility));
        OnPropertyChanged(nameof(UsersVisibility));
        RaisePermissionVisibilityChanges();
    }

    private async Task ShowSystemSettingsAsync()
    {
        SetActiveWorkspace("SystemSettings");
        await LoadSystemSettingsAsync();
        OnPropertyChanged(nameof(PosVisibility));
        OnPropertyChanged(nameof(ProductManagementVisibility));
        OnPropertyChanged(nameof(InventoryVisibility));
        OnPropertyChanged(nameof(ReturnsVisibility));
        OnPropertyChanged(nameof(OpenShiftVisibility));
        OnPropertyChanged(nameof(ShiftVisibility));
        OnPropertyChanged(nameof(ReportsVisibility));
        OnPropertyChanged(nameof(PrinterSettingsVisibility));
        OnPropertyChanged(nameof(SystemSettingsVisibility));
        OnPropertyChanged(nameof(BackupVisibility));
        OnPropertyChanged(nameof(AuditLogsVisibility));
        OnPropertyChanged(nameof(UsersVisibility));
    }

    private async Task ShowBackupAsync()
    {
        SetActiveWorkspace("Backup");
        await LoadBackupsAsync();
        OnPropertyChanged(nameof(PosVisibility));
        OnPropertyChanged(nameof(ProductManagementVisibility));
        OnPropertyChanged(nameof(InventoryVisibility));
        OnPropertyChanged(nameof(ReturnsVisibility));
        OnPropertyChanged(nameof(OpenShiftVisibility));
        OnPropertyChanged(nameof(ShiftVisibility));
        OnPropertyChanged(nameof(ReportsVisibility));
        OnPropertyChanged(nameof(PrinterSettingsVisibility));
        OnPropertyChanged(nameof(SystemSettingsVisibility));
        OnPropertyChanged(nameof(BackupVisibility));
        OnPropertyChanged(nameof(AuditLogsVisibility));
        OnPropertyChanged(nameof(UsersVisibility));
    }

    private async Task ShowAuditLogsAsync()
    {
        SetActiveWorkspace("AuditLogs");
        await LoadAuditFilterOptionsAsync();
        await LoadAuditLogsAsync();
        OnPropertyChanged(nameof(PosVisibility));
        OnPropertyChanged(nameof(ProductManagementVisibility));
        OnPropertyChanged(nameof(InventoryVisibility));
        OnPropertyChanged(nameof(ReturnsVisibility));
        OnPropertyChanged(nameof(OpenShiftVisibility));
        OnPropertyChanged(nameof(ShiftVisibility));
        OnPropertyChanged(nameof(ReportsVisibility));
        OnPropertyChanged(nameof(PrinterSettingsVisibility));
        OnPropertyChanged(nameof(SystemSettingsVisibility));
        OnPropertyChanged(nameof(BackupVisibility));
        OnPropertyChanged(nameof(AuditLogsVisibility));
        OnPropertyChanged(nameof(UsersVisibility));
    }

    private async Task ShowUsersAsync()
    {
        SetActiveWorkspace("Users");
        await LoadUsersAsync();
        OnPropertyChanged(nameof(PosVisibility));
        OnPropertyChanged(nameof(ProductManagementVisibility));
        OnPropertyChanged(nameof(InventoryVisibility));
        OnPropertyChanged(nameof(ReturnsVisibility));
        OnPropertyChanged(nameof(OpenShiftVisibility));
        OnPropertyChanged(nameof(ShiftVisibility));
        OnPropertyChanged(nameof(ReportsVisibility));
        OnPropertyChanged(nameof(PrinterSettingsVisibility));
        OnPropertyChanged(nameof(SystemSettingsVisibility));
        OnPropertyChanged(nameof(BackupVisibility));
        OnPropertyChanged(nameof(AuditLogsVisibility));
        OnPropertyChanged(nameof(UsersVisibility));
    }

    private Task ShowReturnsAsync()
    {
        SetActiveWorkspace("Returns");
        OnPropertyChanged(nameof(PosVisibility));
        OnPropertyChanged(nameof(ProductManagementVisibility));
        OnPropertyChanged(nameof(InventoryVisibility));
        OnPropertyChanged(nameof(ReturnsVisibility));
        OnPropertyChanged(nameof(OpenShiftVisibility));
        OnPropertyChanged(nameof(ShiftVisibility));
        OnPropertyChanged(nameof(ReportsVisibility));
        OnPropertyChanged(nameof(PrinterSettingsVisibility));
        OnPropertyChanged(nameof(SystemSettingsVisibility));
        OnPropertyChanged(nameof(BackupVisibility));
        OnPropertyChanged(nameof(AuditLogsVisibility));
        OnPropertyChanged(nameof(UsersVisibility));
        return Task.CompletedTask;
    }

    private async Task SearchProductsAsync()
    {
        try
        {
            Products.Clear();
            var results = await _productLookupService.SearchAsync(SearchText);
            foreach (var product in results)
            {
                Products.Add(product);
            }
        }
        catch (UnauthorizedAccessException ex)
        {
            PosMessage = ex.Message;
        }
    }

    private Task ToggleNotificationsAsync()
    {
        IsNotificationsPopupOpen = !IsNotificationsPopupOpen;
        return Task.CompletedTask;
    }

    private async Task SearchOrAddProductAsync()
    {
        PosMessage = string.Empty;

        try
        {
            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                var barcode = SearchText.Trim();
                var barcodeProduct = await _productLookupService.FindByBarcodeAsync(barcode);
                if (barcodeProduct is not null)
                {
                    if (IsDuplicateBarcodeScan(barcode))
                    {
                        SearchText = string.Empty;
                        return;
                    }

                    MarkBarcodeScanAccepted(barcode);
                    AddProductToCart(barcodeProduct);
                    SearchText = string.Empty;
                    return;
                }
            }

            await SearchProductsAsync();

            if (Products.Count == 1)
            {
                AddProductToCart(Products[0]);
                SearchText = string.Empty;
            }
            else if (Products.Count == 0)
            {
                PosMessage = "لم يتم العثور على منتج بهذا البحث أو الباركود.";
            }
        }
        catch (UnauthorizedAccessException ex)
        {
            PosMessage = ex.Message;
        }
    }

    private bool IsDuplicateBarcodeScan(string barcode)
    {
        var now = DateTimeOffset.UtcNow;
        return string.Equals(_lastAcceptedBarcode, barcode, StringComparison.Ordinal) &&
               now - _lastAcceptedBarcodeAt < TimeSpan.FromMilliseconds(BarcodeScanCooldownMilliseconds);
    }

    private void MarkBarcodeScanAccepted(string barcode)
    {
        _lastAcceptedBarcode = barcode;
        _lastAcceptedBarcodeAt = DateTimeOffset.UtcNow;
    }

    private async Task LoadCategoriesAsync()
    {
        try
        {
            var selectedCategoryId = SelectedCategory?.Id;
            Categories.Clear();
            var categories = await _productManagementService.GetCategoriesAsync();
            foreach (var category in categories)
            {
                Categories.Add(category);
            }

            SelectedCategory = selectedCategoryId.HasValue
                ? Categories.FirstOrDefault(x => x.Id == selectedCategoryId.Value) ?? Categories.FirstOrDefault()
                : Categories.FirstOrDefault();
        }
        catch (UnauthorizedAccessException ex)
        {
            ProductMessage = ex.Message;
        }
    }

    private async Task LoadManagedProductsAsync()
    {
        try
        {
            ManagedProducts.Clear();
            var products = await _productManagementService.SearchProductsAsync(ManagedProductsSearchText, ShowInactiveProducts);
            foreach (var product in products)
            {
                ManagedProducts.Add(product);
            }
        }
        catch (UnauthorizedAccessException ex)
        {
            ProductMessage = ex.Message;
        }
    }

    private async Task LoadManagedCategoriesAsync()
    {
        try
        {
            var selectedId = SelectedManagedCategory?.Id;
            ManagedCategories.Clear();
            var categories = await _categoryManagementService.GetCategoriesAsync(includeInactive: true);
            foreach (var category in categories)
            {
                ManagedCategories.Add(category);
            }

            SelectedManagedCategory = selectedId.HasValue
                ? ManagedCategories.FirstOrDefault(x => x.Id == selectedId.Value)
                : ManagedCategories.FirstOrDefault();
        }
        catch (UnauthorizedAccessException ex)
        {
            CategoryMessage = ex.Message;
        }
    }

    private async Task SaveCategoryAsync()
    {
        if (!_cashierId.HasValue)
        {
            CategoryMessage = "يجب تسجيل الدخول أولاً.";
            return;
        }

        try
        {
            var result = await _categoryManagementService.SaveCategoryAsync(
                new UpsertCategoryRequest(
                    _editingCategoryId,
                    CategoryName,
                    SelectedCategoryMeasurementUnit?.Value ?? MeasurementUnit.Piece,
                    CategoryIsActive),
                _cashierId.Value);

            CategoryMessage = result.Message;
            if (result.Succeeded && result.Value is not null)
            {
                _editingCategoryId = null;
                CategoryName = string.Empty;
                SelectedCategoryMeasurementUnit = CategoryMeasurementUnits.FirstOrDefault(x => x.Value == MeasurementUnit.Piece);
                CategoryIsActive = true;
                await LoadManagedCategoriesAsync();
                await LoadCategoriesAsync();
                SelectedManagedCategory = ManagedCategories.FirstOrDefault(x => x.Id == result.Value.Id);
                SelectedCategory = Categories.FirstOrDefault(x => x.Id == result.Value.Id);
            }
        }
        catch (UnauthorizedAccessException ex)
        {
            CategoryMessage = ex.Message;
        }
    }

    private Task NewCategoryAsync()
    {
        _editingCategoryId = null;
        CategoryName = string.Empty;
        SelectedCategoryMeasurementUnit = CategoryMeasurementUnits.FirstOrDefault(x => x.Value == MeasurementUnit.Piece);
        CategoryIsActive = true;
        CategoryMessage = "اكتب اسم التصنيف ثم احفظه.";
        return Task.CompletedTask;
    }

    private Task EditSelectedCategoryAsync()
    {
        if (SelectedManagedCategory is null)
        {
            CategoryMessage = "اختر تصنيفاً للتعديل.";
            return Task.CompletedTask;
        }

        _editingCategoryId = SelectedManagedCategory.Id;
        CategoryName = SelectedManagedCategory.Name;
        SelectedCategoryMeasurementUnit = CategoryMeasurementUnits.FirstOrDefault(x => x.Value == SelectedManagedCategory.MeasurementUnit)
            ?? CategoryMeasurementUnits.FirstOrDefault();
        CategoryIsActive = SelectedManagedCategory.IsActive;
        CategoryMessage = $"جاري تعديل التصنيف: {SelectedManagedCategory.Name}";
        return Task.CompletedTask;
    }

    private async Task ToggleCategoryActiveAsync()
    {
        if (!_cashierId.HasValue)
        {
            CategoryMessage = "يجب تسجيل الدخول أولاً.";
            return;
        }

        if (SelectedManagedCategory is null)
        {
            CategoryMessage = "اختر تصنيفاً أولاً.";
            return;
        }

        try
        {
            var result = await _categoryManagementService.SetCategoryActiveAsync(
                SelectedManagedCategory.Id,
                !SelectedManagedCategory.IsActive,
                _cashierId.Value);

            CategoryMessage = result.Message;
            if (result.Succeeded)
            {
                await LoadManagedCategoriesAsync();
                await LoadCategoriesAsync();
            }
        }
        catch (UnauthorizedAccessException ex)
        {
            CategoryMessage = ex.Message;
        }
    }

    private Task NewProductAsync()
    {
        _editingProductId = null;
        ProductName = string.Empty;
        ProductBarcode = string.Empty;
        ProductInternalCode = string.Empty;
        SelectedProductType = ProductTypes.FirstOrDefault();
        ProductPurchasePrice = string.Empty;
        ProductSalePrice = string.Empty;
        ProductStockQuantity = 0;
        ProductPackageCount = 0;
        ProductUnitsPerPackage = 0;
        ProductLowStockThreshold = 5;
        SelectedCategory = Categories.FirstOrDefault();
        ProductMessage = "اترك الباركود والكود الداخلي فارغين لإنشائهما تلقائياً.";
        return Task.CompletedTask;
    }

    private Task EditSelectedProductAsync()
    {
        if (SelectedManagedProduct is null)
        {
            ProductMessage = "اختر منتجاً للتعديل.";
            return Task.CompletedTask;
        }

        _editingProductId = SelectedManagedProduct.Id;
        ProductName = SelectedManagedProduct.Name;
        ProductBarcode = SelectedManagedProduct.Barcode;
        ProductInternalCode = SelectedManagedProduct.InternalCode;
        SelectedProductType = ProductTypes.FirstOrDefault(x => x.Value == SelectedManagedProduct.ProductType)
            ?? ProductTypes.FirstOrDefault();
        ProductPurchasePrice = SelectedManagedProduct.PurchasePrice.ToString("0.##", CultureInfo.InvariantCulture);
        ProductSalePrice = SelectedManagedProduct.SalePrice.ToString("0.##", CultureInfo.InvariantCulture);
        ProductStockQuantity = SelectedManagedProduct.StockQuantity;
        ProductPackageCount = SelectedManagedProduct.PackageCount;
        ProductUnitsPerPackage = SelectedManagedProduct.UnitsPerPackage;
        ProductLowStockThreshold = SelectedManagedProduct.LowStockThreshold;
        SelectedCategory = Categories.FirstOrDefault(x => x.Id == SelectedManagedProduct.CategoryId);
        if (IsPackageBasedUnit(SelectedCategory?.MeasurementUnit) && ProductPackageCount == 0 && ProductStockQuantity > 0)
        {
            ProductPackageCount = (int)Math.Ceiling(ProductStockQuantity);
            ProductUnitsPerPackage = 1;
        }

        ProductMessage = $"جاري تعديل المنتج: {SelectedManagedProduct.Name}";
        return Task.CompletedTask;
    }

    private async Task SaveProductAsync()
    {
        if (!_cashierId.HasValue)
        {
            ProductMessage = "يجب تسجيل الدخول أولاً.";
            return;
        }

        if (!TryParseMoney(ProductPurchasePrice, out var purchasePrice))
        {
            ProductMessage = "سعر الشراء غير صحيح. يمكنك كتابة السعر بهذا الشكل 8.5 أو 8,5.";
            return;
        }

        if (!TryParseMoney(ProductSalePrice, out var salePrice))
        {
            ProductMessage = "سعر البيع غير صحيح. يمكنك كتابة السعر بهذا الشكل 8.5 أو 8,5.";
            return;
        }

        var isPackageUnit = IsPackageBasedUnit(SelectedCategory?.MeasurementUnit);
        var stockQuantity = isPackageUnit
            ? ProductPackageCount * ProductUnitsPerPackage
            : ProductStockQuantity;

        if (isPackageUnit && ProductPackageCount > 0 && ProductUnitsPerPackage <= 0)
        {
            ProductMessage = "اكتب عدد القطع داخل كل علبة أو كرتونة.";
            return;
        }

        var request = new UpsertProductRequest(
            _editingProductId,
            ProductName,
            ProductBarcode,
            null,
            SelectedProductType?.Value ?? ProductType.NormalProduct,
            SelectedCategory?.Id,
            purchasePrice,
            salePrice,
            stockQuantity,
            isPackageUnit ? ProductPackageCount : 0,
            isPackageUnit ? ProductUnitsPerPackage : 0,
            ProductLowStockThreshold);

        try
        {
            var result = await _productManagementService.SaveProductAsync(request, _cashierId.Value);
            ProductMessage = result.Message;

            if (result.Succeeded)
            {
                await LoadManagedProductsAsync();
                await RefreshNotificationsAsync();
                await NewProductAsync();
            }
        }
        catch (UnauthorizedAccessException ex)
        {
            ProductMessage = ex.Message;
        }
    }

    private async Task ToggleProductActiveAsync()
    {
        if (!_cashierId.HasValue)
        {
            ProductMessage = "يجب تسجيل الدخول أولاً.";
            return;
        }

        if (SelectedManagedProduct is null)
        {
            ProductMessage = "اختر منتجاً أولاً.";
            return;
        }

        try
        {
            var result = await _productManagementService.SetProductActiveAsync(
                SelectedManagedProduct.Id,
                !SelectedManagedProduct.IsActive,
                _cashierId.Value);

            ProductMessage = result.Message;
            if (result.Succeeded)
            {
                await LoadManagedProductsAsync();
                if (HasPermission(PermissionCodes.CanUsePOS))
                {
                    await SearchProductsAsync();
                }
                await RefreshNotificationsAsync();
            }
        }
        catch (UnauthorizedAccessException ex)
        {
            ProductMessage = ex.Message;
        }
    }

    private async Task PrintSelectedProductBarcodeAsync()
    {
        if (SelectedManagedProduct is null)
        {
            ProductMessage = "اختر منتجاً من القائمة أولاً.";
            return;
        }

        if (!int.TryParse(BarcodeLabelQuantity, out var quantity) || quantity <= 0)
        {
            ProductMessage = "عدد الليبلات غير صحيح. اكتب رقم أكبر من صفر.";
            return;
        }

        if (quantity > 500)
        {
            ProductMessage = "لا يمكن طباعة أكثر من 500 ليبل في مرة واحدة.";
            return;
        }

        var settings = await _printerSettingsService.GetSettingsAsync();
        if (string.IsNullOrWhiteSpace(settings.LabelPrinterName))
        {
            ProductMessage = "حدد طابعة الباركود من إعدادات الطباعة أولاً.";
            return;
        }

        var printed = _receiptPrinter.PrintBarcodeLabels(
            new BarcodeLabelPrintRequest(
                SelectedManagedProduct.Name,
                SelectedManagedProduct.Barcode,
                SelectedManagedProduct.SalePrice,
                quantity),
            settings);

        ProductMessage = printed
            ? $"تم إرسال {quantity} ليبل باركود للمنتج {SelectedManagedProduct.Name}."
            : "تعذر إرسال ليبل الباركود للطابعة. راجع إعدادات طابعة الباركود.";
    }

    private async Task ImportProductsAsync()
    {
        if (!_cashierId.HasValue)
        {
            ProductMessage = "يجب تسجيل الدخول أولاً.";
            return;
        }

        var dialog = new OpenFileDialog
        {
            Title = "اختر ملف المنتجات",
            Filter = "Excel files (*.xlsx;*.xls;*.xlsm;*.xlsb;*.csv)|*.xlsx;*.xls;*.xlsm;*.xlsb;*.csv|All files (*.*)|*.*",
            CheckFileExists = true,
            Multiselect = false
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            ProductMessage = "جاري فحص واستيراد ملف المنتجات...";
            var result = await _productImportExportService.ImportProductsAsync(dialog.FileName, _cashierId.Value);
            ProductMessage = result.Message;

            if (result.Succeeded)
            {
                await LoadCategoriesAsync();
                ManagedProductsSearchText = string.Empty;
                await LoadManagedProductsAsync();
                await RefreshNotificationsAsync();
                if (HasPermission(PermissionCodes.CanUsePOS))
                {
                    await SearchProductsAsync();
                }
            }
        }
        catch (UnauthorizedAccessException ex)
        {
            ProductMessage = ex.Message;
        }
        catch (Exception)
        {
            ProductMessage = "تعذر استيراد ملف المنتجات. استخدم ملف Excel يحتوي على الأعمدة: الاسم، التصنيف، سعر الشراء، سعر البيع، المخزون.";
        }
    }

    private async Task ExportProductsAsync()
    {
        if (!_cashierId.HasValue)
        {
            ProductMessage = "يجب تسجيل الدخول أولاً.";
            return;
        }

        var dialog = new SaveFileDialog
        {
            Title = "اختر مكان تصدير المنتجات",
            Filter = "Excel Workbook (*.xlsx)|*.xlsx",
            FileName = $"qashira-products-{DateTime.Now:yyyyMMdd-HHmm}.xlsx",
            AddExtension = true,
            DefaultExt = ".xlsx",
            OverwritePrompt = true
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            var result = await _productImportExportService.ExportProductsAsync(dialog.FileName, ShowInactiveProducts, _cashierId.Value);
            ProductMessage = result.Message;
        }
        catch (UnauthorizedAccessException ex)
        {
            ProductMessage = ex.Message;
        }
        catch (Exception)
        {
            ProductMessage = "تعذر تصدير ملف المنتجات. تأكد من اختيار مكان حفظ متاح.";
        }
    }

    private async Task LoadInventoryAsync()
    {
        try
        {
            var selectedProductId = SelectedInventoryProduct?.Id;
            InventoryProducts.Clear();
            var products = await _inventoryService.SearchProductsAsync(InventorySearchText);
            foreach (var product in products)
            {
                InventoryProducts.Add(product);
            }

            SelectedInventoryProduct = selectedProductId.HasValue
                ? InventoryProducts.FirstOrDefault(x => x.Id == selectedProductId.Value) ?? InventoryProducts.FirstOrDefault()
                : InventoryProducts.FirstOrDefault();

            await LoadStockMovementsAsync();
            await RefreshNotificationsAsync();
        }
        catch (UnauthorizedAccessException ex)
        {
            InventoryMessage = ex.Message;
        }
    }

    private async Task LoadStockMovementsAsync()
    {
        try
        {
            StockMovements.Clear();
            var productId = SelectedInventoryProduct?.Id;
            var movements = await _inventoryService.GetRecentMovementsAsync(productId);
            foreach (var movement in movements)
            {
                StockMovements.Add(movement);
            }
        }
        catch (UnauthorizedAccessException ex)
        {
            InventoryMessage = ex.Message;
        }
    }

    private async Task AdjustStockAsync()
    {
        if (!_cashierId.HasValue)
        {
            InventoryMessage = "يجب تسجيل الدخول أولاً.";
            return;
        }

        if (SelectedInventoryProduct is null)
        {
            InventoryMessage = "اختر منتجاً لتعديل مخزونه.";
            return;
        }

        if (!TryParseNonNegativeDecimal(NewStockQuantity, out var newStockQuantity))
        {
            InventoryMessage = "اكتب كمية مخزون صحيحة بالأرقام فقط، ولا تقل عن صفر.";
            return;
        }

        var reason = string.IsNullOrWhiteSpace(StockAdjustmentReason)
            ? "تعديل يدوي من شاشة المخزون"
            : StockAdjustmentReason;

        try
        {
            var result = await _inventoryService.AdjustStockAsync(
                SelectedInventoryProduct.Id,
                newStockQuantity,
                reason,
                _cashierId.Value);

            InventoryMessage = result.Message;
            if (result.Succeeded)
            {
                StockAdjustmentReason = string.Empty;
                await LoadInventoryAsync();
                if (HasPermission(PermissionCodes.CanUsePOS))
                {
                    await SearchProductsAsync();
                }
            }
        }
        catch (UnauthorizedAccessException ex)
        {
            InventoryMessage = ex.Message;
        }
    }

    private async Task LoadReportsAsync()
    {
        ReportMessage = string.Empty;
        ReportSummaryText = string.Empty;
        TopSellingProducts.Clear();

        if (!ReportFromDateValue.HasValue || !ReportToDateValue.HasValue)
        {
            ReportMessage = "اختر تاريخ البداية والنهاية من التقويم.";
            return;
        }

        var fromDate = ReportFromDateValue.Value;
        var toDate = ReportToDateValue.Value;
        var from = new DateTimeOffset(fromDate.Date, TimeZoneInfo.Local.GetUtcOffset(fromDate.Date));
        var toDateExclusive = toDate.Date.AddDays(1);
        var to = new DateTimeOffset(toDateExclusive, TimeZoneInfo.Local.GetUtcOffset(toDateExclusive));

        Result<SalesReportDto> result;
        try
        {
            result = await _reportService.GetSalesReportAsync(new SalesReportRequest(from, to));
        }
        catch (UnauthorizedAccessException ex)
        {
            ReportMessage = ex.Message;
            return;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to load sales report from {FromDate} to {ToDate}.", ReportFromDateValue, ReportToDateValue);
            ReportMessage = "تعذر تحميل التقرير. تم تسجيل التفاصيل في ملف السجل.";
            return;
        }

        if (!result.Succeeded || result.Value is null)
        {
            ReportMessage = result.Message;
            return;
        }

        var report = result.Value;
        ReportSummaryText =
            $"عدد الفواتير: {report.InvoiceCount}\n" +
            $"عدد عمليات المرتجع: {report.ReturnCount}\n" +
            $"عدد القطع المرتجعة: {report.ReturnedItemQuantity}\n" +
            $"إجمالي المبيعات: {report.GrossSales:0.00} ج.م\n" +
            $"مبيعات المنتجات: {report.ProductSales:0.00} ج.م\n" +
            $"خدمات الطباعة والتصوير: {report.PrintingServiceSales:0.00} ج.م\n" +
            $"الخصومات: {report.Discounts:0.00} ج.م\n" +
            $"المرتجعات: {report.Returns:0.00} ج.م\n" +
            $"صافي المبيعات: {report.NetSales:0.00} ج.م\n" +
            $"متوسط الفاتورة: {report.AverageInvoice:0.00} ج.م";

        foreach (var product in report.TopProducts)
        {
            TopSellingProducts.Add(product);
        }

        if (report.InvoiceCount == 0 && report.ReturnCount == 0)
        {
            ReportMessage = "لا توجد عمليات في الفترة المحددة.";
        }
        else if (report.InvoiceCount == 0)
        {
            ReportMessage = "لا توجد فواتير بيع في الفترة المحددة، لكن توجد مرتجعات.";
        }
        else if (TopSellingProducts.Count == 0)
        {
            ReportMessage = "لا توجد مبيعات منتجات في الفترة المحددة.";
        }
    }

    private async Task LoadTodayReportAsync()
    {
        var today = DateTime.Today;
        ReportFromDateValue = today;
        ReportToDateValue = today;
        await LoadReportsAsync();
    }

    private async Task LoadCurrentWeekReportAsync()
    {
        var today = DateTime.Today;
        var daysSinceSaturday = ((int)today.DayOfWeek - (int)DayOfWeek.Saturday + 7) % 7;
        ReportFromDateValue = today.AddDays(-daysSinceSaturday);
        ReportToDateValue = today;
        await LoadReportsAsync();
    }

    private async Task LoadCurrentMonthReportAsync()
    {
        var today = DateTime.Today;
        ReportFromDateValue = new DateTime(today.Year, today.Month, 1);
        ReportToDateValue = today;
        await LoadReportsAsync();
    }

    private async Task LoadInvoiceHistoryAsync()
    {
        InvoiceHistoryMessage = string.Empty;
        InvoiceHistorySummary = string.Empty;
        InvoiceHistoryItems.Clear();
        InvoiceHistoryLines.Clear();
        InvoiceHistoryReturns.Clear();
        SelectedInvoiceHistoryItem = null;

        if (!InvoiceHistoryFromDateValue.HasValue || !InvoiceHistoryToDateValue.HasValue)
        {
            InvoiceHistoryMessage = "اختر تاريخ البداية والنهاية من التقويم.";
            return;
        }

        var (from, to) = BuildInclusiveDateRange(InvoiceHistoryFromDateValue.Value, InvoiceHistoryToDateValue.Value);

        Result<IReadOnlyList<InvoiceHistoryListItemDto>> result;
        try
        {
            result = await _invoiceHistoryService.SearchAsync(new InvoiceHistorySearchRequest(
                from,
                to,
                InvoiceHistorySearchText));
        }
        catch (UnauthorizedAccessException ex)
        {
            InvoiceHistoryMessage = ex.Message;
            return;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to load invoice history from {FromDate} to {ToDate}.", InvoiceHistoryFromDateValue, InvoiceHistoryToDateValue);
            InvoiceHistoryMessage = "تعذر تحميل الفواتير. تم تسجيل التفاصيل في ملف السجل.";
            return;
        }

        if (!result.Succeeded || result.Value is null)
        {
            InvoiceHistoryMessage = result.Message;
            return;
        }

        foreach (var invoice in result.Value)
        {
            InvoiceHistoryItems.Add(invoice);
        }

        InvoiceHistoryMessage = InvoiceHistoryItems.Count == 0
            ? "لا توجد فواتير مطابقة للفترة أو البحث."
            : $"تم العثور على {InvoiceHistoryItems.Count} فاتورة.";
    }

    private async Task LoadSelectedInvoiceHistoryDetailsAsync()
    {
        InvoiceHistorySummary = string.Empty;
        InvoiceHistoryLines.Clear();
        InvoiceHistoryReturns.Clear();

        if (SelectedInvoiceHistoryItem is null)
        {
            return;
        }

        Result<InvoiceHistoryDetailsDto> result;
        try
        {
            result = await _invoiceHistoryService.GetDetailsAsync(SelectedInvoiceHistoryItem.InvoiceId);
        }
        catch (UnauthorizedAccessException ex)
        {
            InvoiceHistoryMessage = ex.Message;
            return;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to load invoice history details for invoice {InvoiceId}.", SelectedInvoiceHistoryItem.InvoiceId);
            InvoiceHistoryMessage = "تعذر تحميل تفاصيل الفاتورة. تم تسجيل التفاصيل في ملف السجل.";
            return;
        }

        if (!result.Succeeded || result.Value is null)
        {
            InvoiceHistoryMessage = result.Message;
            return;
        }

        var details = result.Value;
        InvoiceHistorySummary =
            $"الفاتورة: {details.InvoiceNumber}\n" +
            $"التاريخ: {details.CreatedAt:yyyy-MM-dd HH:mm}\n" +
            $"الكاشير: {details.CashierName}\n" +
            $"الإجمالي قبل الخصم: {details.TotalAmount:0.00} ج.م\n" +
            $"الخصم: {details.DiscountAmount:0.00} ج.م\n" +
            $"المرتجعات: {details.ReturnedAmount:0.00} ج.م\n" +
            $"الصافي الحالي: {details.NetAmount:0.00} ج.م";

        foreach (var line in details.Lines)
        {
            InvoiceHistoryLines.Add(line);
        }

        foreach (var returnRow in details.Returns)
        {
            InvoiceHistoryReturns.Add(returnRow);
        }
    }

    private async Task LoadTodayInvoiceHistoryAsync()
    {
        var today = DateTime.Today;
        InvoiceHistoryFromDateValue = today;
        InvoiceHistoryToDateValue = today;
        await LoadInvoiceHistoryAsync();
    }

    private async Task LoadCurrentWeekInvoiceHistoryAsync()
    {
        var today = DateTime.Today;
        var daysSinceSaturday = ((int)today.DayOfWeek - (int)DayOfWeek.Saturday + 7) % 7;
        InvoiceHistoryFromDateValue = today.AddDays(-daysSinceSaturday);
        InvoiceHistoryToDateValue = today;
        await LoadInvoiceHistoryAsync();
    }

    private async Task LoadCurrentMonthInvoiceHistoryAsync()
    {
        var today = DateTime.Today;
        InvoiceHistoryFromDateValue = new DateTime(today.Year, today.Month, 1);
        InvoiceHistoryToDateValue = today;
        await LoadInvoiceHistoryAsync();
    }

    private async Task LoadPrinterSettingsAsync()
    {
        PrinterSettingsMessage = string.Empty;
        InstalledPrinters.Clear();

        foreach (var printer in _receiptPrinter.GetInstalledPrinters())
        {
            InstalledPrinters.Add(printer);
        }

        var settings = await _printerSettingsService.GetSettingsAsync();
        var defaultPrinterName = _receiptPrinter.GetDefaultPrinterName();
        var receiptPrinterName = ResolveInstalledPrinter(settings.ReceiptPrinterName, defaultPrinterName);
        var labelPrinterName = ResolveInstalledPrinter(settings.LabelPrinterName, receiptPrinterName);

        SelectedReceiptPrinterName = receiptPrinterName;
        SelectedLabelPrinterName = labelPrinterName;
        ReceiptStoreName = settings.StoreName;
        ReceiptTitle = settings.ReceiptTitle;
        ReceiptFooter = settings.ReceiptFooter;
        ReceiptPaperWidth = settings.ReceiptPaperWidth;
        BarcodeLabelSize = BarcodeLabelSizes.Contains(settings.BarcodeLabelSize)
            ? settings.BarcodeLabelSize
            : BarcodeLabelSizes[0];
        BarcodePrinterProfile = BarcodePrinterProfiles.Contains(settings.BarcodePrinterProfile)
            ? settings.BarcodePrinterProfile
            : BarcodePrinterProfiles[0];
        BarcodeLabelGapMm = settings.BarcodeLabelGapMm.ToString("0.##", CultureInfo.InvariantCulture);
        BarcodeHorizontalOffsetMm = settings.BarcodeHorizontalOffsetMm.ToString("0.##", CultureInfo.InvariantCulture);
        BarcodeVerticalOffsetMm = settings.BarcodeVerticalOffsetMm.ToString("0.##", CultureInfo.InvariantCulture);

        if (InstalledPrinters.Count == 0)
        {
            PrinterSettingsMessage = "لم يتم العثور على طابعات مثبتة على Windows.";
            UpdatePrinterSettingsSummary();
            return;
        }

        if (!string.IsNullOrWhiteSpace(settings.ReceiptPrinterName) &&
            !InstalledPrinters.Contains(settings.ReceiptPrinterName))
        {
            PrinterSettingsMessage = "الطابعة المحفوظة غير موجودة حالياً على Windows. تم اختيار الطابعة الافتراضية مؤقتاً.";
        }

        UpdatePrinterSettingsSummary();
    }

    private async Task SavePrinterSettingsAsync()
    {
        if (!_cashierId.HasValue)
        {
            PrinterSettingsMessage = "يجب تسجيل الدخول أولاً.";
            return;
        }

        if (!TryParseMoney(BarcodeLabelGapMm, out var barcodeGap) || barcodeGap < 0 || barcodeGap > 10)
        {
            PrinterSettingsMessage = "فاصل الليبل يجب أن يكون رقمًا بين 0 و 10 مم.";
            return;
        }

        if (!TryParseMoney(BarcodeHorizontalOffsetMm, out var barcodeHorizontalOffset) || barcodeHorizontalOffset < -15 || barcodeHorizontalOffset > 15)
        {
            PrinterSettingsMessage = "معايرة الباركود يجب أن تكون رقمًا بين -15 و 15 مم. السالب يحرك الطباعة لليسار والموجب لليمين.";
            return;
        }

        if (!TryParseMoney(BarcodeVerticalOffsetMm, out var barcodeVerticalOffset) || barcodeVerticalOffset < -15 || barcodeVerticalOffset > 15)
        {
            PrinterSettingsMessage = "المعايرة الرأسية يجب أن تكون رقمًا بين -15 و 15 مم.";
            return;
        }

        try
        {
            var result = await _printerSettingsService.SaveSettingsAsync(
                new PrinterSettingsDto(
                    SelectedReceiptPrinterName,
                    SelectedLabelPrinterName,
                    ReceiptStoreName,
                    ReceiptTitle,
                    ReceiptFooter,
                    ReceiptPaperWidth,
                    BarcodeLabelSize,
                    BarcodePrinterProfile,
                    (double)barcodeGap,
                    (double)barcodeHorizontalOffset,
                    (double)barcodeVerticalOffset),
                _cashierId.Value);
            PrinterSettingsMessage = result.Message;
            UpdatePrinterSettingsSummary();
        }
        catch (UnauthorizedAccessException ex)
        {
            PrinterSettingsMessage = ex.Message;
        }
    }

    private Task TestReceiptPrinterAsync()
    {
        if (string.IsNullOrWhiteSpace(SelectedReceiptPrinterName))
        {
            PrinterSettingsMessage = "اختر طابعة الإيصالات أولاً.";
            return Task.CompletedTask;
        }

        var printed = _receiptPrinter.PrintTestPage(new PrinterSettingsDto(
            SelectedReceiptPrinterName,
            SelectedLabelPrinterName,
            ReceiptStoreName,
            ReceiptTitle,
            ReceiptFooter,
            ReceiptPaperWidth,
            BarcodeLabelSize,
            BarcodePrinterProfile,
            TryParseMoney(BarcodeLabelGapMm, out var receiptGap) ? (double)receiptGap : 2,
            0,
            0));
        PrinterSettingsMessage = printed
            ? "تم إرسال اختبار طباعة الإيصال."
            : "تعذر الوصول إلى الطابعة المحددة.";
        return Task.CompletedTask;
    }

    private Task TestBarcodeLabelPrinterAsync()
    {
        if (string.IsNullOrWhiteSpace(SelectedLabelPrinterName))
        {
            PrinterSettingsMessage = "اختر طابعة الباركود أولاً.";
            return Task.CompletedTask;
        }

        var printed = _receiptPrinter.PrintTestBarcodeLabel(new PrinterSettingsDto(
            SelectedReceiptPrinterName,
            SelectedLabelPrinterName,
            ReceiptStoreName,
            ReceiptTitle,
            ReceiptFooter,
            ReceiptPaperWidth,
            BarcodeLabelSize,
            BarcodePrinterProfile,
            TryParseMoney(BarcodeLabelGapMm, out var gap) ? (double)gap : 2,
            TryParseMoney(BarcodeHorizontalOffsetMm, out var horizontalOffset) ? (double)horizontalOffset : 0,
            TryParseMoney(BarcodeVerticalOffsetMm, out var verticalOffset) ? (double)verticalOffset : 0));
        PrinterSettingsMessage = printed
            ? "تم إرسال اختبار طباعة الباركود."
            : "تعذر الوصول إلى طابعة الباركود المحددة.";
        return Task.CompletedTask;
    }

    private string ResolveInstalledPrinter(string savedPrinterName, string? fallbackPrinterName)
    {
        if (!string.IsNullOrWhiteSpace(savedPrinterName) && InstalledPrinters.Contains(savedPrinterName))
        {
            return savedPrinterName;
        }

        if (!string.IsNullOrWhiteSpace(fallbackPrinterName) && InstalledPrinters.Contains(fallbackPrinterName))
        {
            return fallbackPrinterName;
        }

        return InstalledPrinters.FirstOrDefault() ?? string.Empty;
    }

    private void UpdatePrinterSettingsSummary()
    {
        var receiptPrinter = string.IsNullOrWhiteSpace(SelectedReceiptPrinterName)
            ? "غير محددة"
            : SelectedReceiptPrinterName;
        var labelPrinter = string.IsNullOrWhiteSpace(SelectedLabelPrinterName)
            ? "غير محددة"
            : SelectedLabelPrinterName;
        var receiptBodyWidth = ReceiptPaperWidth is "57mm" or "58mm" ? "54mm" : "76mm";

        PrinterSettingsSummary =
            $"الإيصال: {ReceiptPaperWidth} / مساحة طباعة {receiptBodyWidth} على {receiptPrinter}\n" +
            $"الباركود: {BarcodeLabelSize} / ملف {BarcodePrinterProfile} على {labelPrinter}";
    }

    private async Task LoadSystemSettingsAsync()
    {
        SystemSettingsMessage = string.Empty;
        var settings = await _systemSettingsService.GetSettingsAsync();

        SystemStoreName = settings.StoreName;
        SystemCurrency = settings.Currency;
        SystemLowStockThreshold = settings.DefaultLowStockThreshold;
        SystemAllowNegativeStock = settings.AllowNegativeStock;
        SystemDiscountsEnabled = settings.DiscountsEnabled;
    }

    private async Task SaveSystemSettingsAsync()
    {
        if (!_cashierId.HasValue)
        {
            SystemSettingsMessage = "يجب تسجيل الدخول أولاً.";
            return;
        }

        try
        {
            var result = await _systemSettingsService.SaveSettingsAsync(
                new SystemSettingsDto(
                    SystemStoreName,
                    SystemCurrency,
                    SystemLowStockThreshold,
                    SystemAllowNegativeStock,
                    SystemDiscountsEnabled),
                _cashierId.Value);

            SystemSettingsMessage = result.Message;
            OnPropertyChanged(nameof(DiscountVisibility));
        }
        catch (UnauthorizedAccessException ex)
        {
            SystemSettingsMessage = ex.Message;
        }
    }

    private async Task LoadBackupsAsync()
    {
        BackupMessage = string.Empty;
        BackupFiles.Clear();

        try
        {
            var backups = await _backupService.GetBackupsAsync();
            foreach (var backup in backups)
            {
                BackupFiles.Add(backup);
            }

            BackupStorageSummary = $"عدد النسخ: {BackupFiles.Count} - المساحة المستخدمة: {FormatFileSize(BackupFiles.Sum(x => x.SizeBytes))}";
            SelectedBackupFile = BackupFiles.FirstOrDefault();
            if (BackupFiles.Count == 0)
            {
                BackupMessage = "لا توجد نسخ احتياطية محفوظة حتى الآن.";
            }
        }
        catch (UnauthorizedAccessException ex)
        {
            BackupMessage = ex.Message;
        }
    }

    private async Task CreateBackupAsync()
    {
        if (!_cashierId.HasValue)
        {
            BackupMessage = "يجب تسجيل الدخول أولاً.";
            return;
        }

        try
        {
            var result = await _backupService.CreateBackupAsync(_cashierId.Value);
            BackupMessage = result.Message;

            if (result.Succeeded)
            {
                await LoadBackupsAsync();
            }

            if (result.Value is not null)
            {
                SelectedBackupFile = BackupFiles.FirstOrDefault(x => x.FullPath == result.Value.BackupPath);
                BackupMessage = result.Message;
            }
        }
        catch (UnauthorizedAccessException ex)
        {
            BackupMessage = ex.Message;
        }
    }

    private async Task RestoreBackupAsync()
    {
        if (!_cashierId.HasValue)
        {
            BackupMessage = "يجب تسجيل الدخول أولاً.";
            return;
        }

        if (SelectedBackupFile is null)
        {
            BackupMessage = "اختر نسخة احتياطية من القائمة أولاً.";
            return;
        }

        try
        {
            var result = await _backupService.RestoreBackupAsync(SelectedBackupFile.FullPath, _cashierId.Value);
            BackupMessage = result.Message;
            if (result.Succeeded)
            {
                await LoadBackupsAsync();
            }
        }
        catch (UnauthorizedAccessException ex)
        {
            BackupMessage = ex.Message;
        }
    }

    private async Task ImportBackupAsync()
    {
        if (!_cashierId.HasValue)
        {
            BackupMessage = "يجب تسجيل الدخول أولاً.";
            return;
        }

        var dialog = new OpenFileDialog
        {
            Title = "اختر ملف النسخة الاحتياطية",
            Filter = "SQLite backup (*.db)|*.db|All files (*.*)|*.*",
            CheckFileExists = true,
            Multiselect = false
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            var result = await _backupService.ImportBackupAsync(dialog.FileName, _cashierId.Value);
            BackupMessage = result.Message;

            if (result.Succeeded)
            {
                await LoadBackupsAsync();
                if (result.Value is not null)
                {
                    SelectedBackupFile = BackupFiles.FirstOrDefault(x => x.FullPath == result.Value.BackupPath);
                    BackupMessage = result.Message;
                }
            }
        }
        catch (UnauthorizedAccessException ex)
        {
            BackupMessage = ex.Message;
        }
    }

    private async Task ExportSelectedBackupAsync()
    {
        if (!_cashierId.HasValue)
        {
            BackupMessage = "يجب تسجيل الدخول أولاً.";
            return;
        }

        if (SelectedBackupFile is null)
        {
            BackupMessage = "اختر نسخة احتياطية من القائمة أولاً.";
            return;
        }

        var dialog = new SaveFileDialog
        {
            Title = "اختر مكان تصدير النسخة الاحتياطية",
            Filter = "SQLite backup (*.db)|*.db",
            FileName = SelectedBackupFile.FileName,
            AddExtension = true,
            DefaultExt = ".db",
            OverwritePrompt = true
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            var result = await _backupService.ExportBackupAsync(SelectedBackupFile.FullPath, dialog.FileName, _cashierId.Value);
            BackupMessage = result.Message;
        }
        catch (UnauthorizedAccessException ex)
        {
            BackupMessage = ex.Message;
        }
    }

    private async Task DeleteSelectedBackupAsync()
    {
        if (!_cashierId.HasValue)
        {
            BackupMessage = "يجب تسجيل الدخول أولاً.";
            return;
        }

        if (SelectedBackupFile is null)
        {
            BackupMessage = "اختر نسخة احتياطية من القائمة أولاً.";
            return;
        }

        var confirm = MessageBox.Show(
            $"هل تريد حذف النسخة الاحتياطية المحددة؟\n{SelectedBackupFile.FileName}",
            "تأكيد حذف النسخة الاحتياطية",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning,
            MessageBoxResult.No,
            MessageBoxOptions.RightAlign | MessageBoxOptions.RtlReading);

        if (confirm != MessageBoxResult.Yes)
        {
            return;
        }

        try
        {
            var result = await _backupService.DeleteBackupAsync(SelectedBackupFile.FullPath, _cashierId.Value);
            BackupMessage = result.Message;
            if (result.Succeeded)
            {
                await LoadBackupsAsync();
            }
        }
        catch (UnauthorizedAccessException ex)
        {
            BackupMessage = ex.Message;
        }
    }

    private async Task ExportLogsAsync()
    {
        if (!_cashierId.HasValue)
        {
            BackupMessage = "يجب تسجيل الدخول أولاً.";
            return;
        }

        try
        {
            var result = await _logExportService.ExportLogsAsync(_cashierId.Value);
            BackupMessage = result.Message;
        }
        catch (UnauthorizedAccessException ex)
        {
            BackupMessage = ex.Message;
        }
    }

    private async Task LoadAuditFilterOptionsAsync()
    {
        AuditUserFilters.Clear();
        AuditActionFilters.Clear();
        SelectedAuditUserFilter = null;
        SelectedAuditActionFilter = null;

        if (!_cashierId.HasValue)
        {
            return;
        }

        var result = await _auditLogQueryService.GetFilterOptionsAsync(_cashierId.Value);
        if (!result.Succeeded || result.Value is null)
        {
            AuditMessage = result.Message;
            return;
        }

        foreach (var user in result.Value.Users)
        {
            AuditUserFilters.Add(user);
        }

        foreach (var action in result.Value.Actions)
        {
            AuditActionFilters.Add(action);
        }

        SelectedAuditUserFilter = AuditUserFilters.FirstOrDefault();
        SelectedAuditActionFilter = AuditActionFilters.FirstOrDefault();
    }

    private async Task LoadAuditLogsAsync()
    {
        AuditMessage = string.Empty;
        AuditLogs.Clear();
        SelectedAuditLog = null;
        ClearAuditDetails();

        if (!_cashierId.HasValue)
        {
            AuditMessage = "يجب تسجيل الدخول أولاً.";
            return;
        }

        if (!AuditFromDateValue.HasValue || !AuditToDateValue.HasValue)
        {
            AuditMessage = "اختر تاريخ البداية والنهاية من التقويم.";
            return;
        }

        var fromDate = AuditFromDateValue.Value.Date;
        var toDateEnd = AuditToDateValue.Value.Date.AddDays(1).AddTicks(-1);
        var from = new DateTimeOffset(fromDate, TimeZoneInfo.Local.GetUtcOffset(fromDate));
        var to = new DateTimeOffset(toDateEnd, TimeZoneInfo.Local.GetUtcOffset(toDateEnd));

        var result = await _auditLogQueryService.SearchAsync(
            new AuditLogSearchRequest(
                from,
                to,
                AuditSearchText,
                SelectedAuditUserFilter?.UserId,
                SelectedAuditActionFilter?.Action),
            _cashierId.Value);

        if (!result.Succeeded || result.Value is null)
        {
            AuditMessage = result.Message;
            return;
        }

        foreach (var log in result.Value)
        {
            AuditLogs.Add(log);
        }

        SelectedAuditLog = AuditLogs.FirstOrDefault();
        AuditMessage = AuditLogs.Count == 0
            ? "لا توجد عمليات مسجلة في هذه الفترة."
            : $"تم تحميل {AuditLogs.Count} عملية.";
    }

    private async Task LoadSelectedAuditDetailsAsync()
    {
        ClearAuditDetails();

        if (!_cashierId.HasValue || SelectedAuditLog is null)
        {
            return;
        }

        try
        {
            var result = await _auditLogQueryService.GetDetailsAsync(SelectedAuditLog.Id, _cashierId.Value);
            if (!result.Succeeded || result.Value is null)
            {
                AuditDetailTitle = "تفاصيل العملية";
                AuditDetailSummary = "لا توجد تفاصيل إضافية متاحة لهذه العملية.";
                AuditDetailLinesMessage = "لا توجد بنود مسجلة لهذه العملية.";
                AuditDetailTimelineMessage = "لا توجد عمليات مرتبطة بهذه العملية.";
                return;
            }

            AuditDetailTitle = result.Value.Title;
            AuditDetailSummary = result.Value.Summary;
            foreach (var field in result.Value.Fields)
            {
                AuditDetailFields.Add(field);
            }

            foreach (var line in result.Value.Lines)
            {
                AuditDetailLines.Add(line);
            }

            AuditDetailLinesMessage = AuditDetailLines.Count == 0
                ? "لا توجد بنود مسجلة لهذه العملية."
                : string.Empty;

            foreach (var entry in result.Value.Timeline)
            {
                AuditDetailTimeline.Add(entry);
            }

            AuditDetailTimelineMessage = AuditDetailTimeline.Count == 0
                ? "لا توجد عمليات مرتبطة بهذه العملية."
                : string.Empty;
        }
        catch
        {
            AuditDetailTitle = "تفاصيل العملية";
            AuditDetailSummary = "لا توجد تفاصيل إضافية متاحة لهذه العملية.";
            AuditDetailLinesMessage = "لا توجد بنود مسجلة لهذه العملية.";
            AuditDetailTimelineMessage = "لا توجد عمليات مرتبطة بهذه العملية.";
        }
    }

    private void ClearAuditDetails()
    {
        AuditDetailTitle = string.Empty;
        AuditDetailSummary = string.Empty;
        AuditDetailLinesMessage = string.Empty;
        AuditDetailTimelineMessage = string.Empty;
        AuditDetailFields.Clear();
        AuditDetailLines.Clear();
        AuditDetailTimeline.Clear();
    }

    private async Task LoadUsersAsync()
    {
        UserMessage = string.Empty;
        Users.Clear();
        UserRoles.Clear();
        UserPermissions.Clear();

        try
        {
            foreach (var role in await _userManagementService.GetRolesAsync())
            {
                UserRoles.Add(role);
            }

            foreach (var user in await _userManagementService.GetUsersAsync())
            {
                Users.Add(user);
            }

            _suppressPermissionReload = true;
            SelectedUserRole ??= UserRoles.FirstOrDefault();
            _suppressPermissionReload = false;
            await LoadUserPermissionsAsync(_editingUserId);
        }
        catch (UnauthorizedAccessException ex)
        {
            _suppressPermissionReload = false;
            UserMessage = ex.Message;
        }
    }

    private Task NewUserAsync()
    {
        _editingUserId = null;
        SelectedUser = null;
        UserFullName = string.Empty;
        UserUsername = string.Empty;
        UserPassword = string.Empty;
        UserIsActive = true;
        _suppressPermissionReload = true;
        SelectedUserRole = UserRoles.FirstOrDefault();
        _suppressPermissionReload = false;
        _ = LoadUserPermissionsAsync(null);
        UserMessage = "اكتب بيانات المستخدم الجديد. كلمة المرور مطلوبة عند الإنشاء.";
        return Task.CompletedTask;
    }

    private Task EditSelectedUserAsync()
    {
        if (SelectedUser is null)
        {
            UserMessage = "اختر مستخدماً للتعديل.";
            return Task.CompletedTask;
        }

        _editingUserId = SelectedUser.Id;
        UserFullName = SelectedUser.FullName;
        UserUsername = SelectedUser.Username;
        UserPassword = string.Empty;
        UserIsActive = SelectedUser.IsActive;
        _suppressPermissionReload = true;
        SelectedUserRole = UserRoles.FirstOrDefault(x => x.Id == SelectedUser.RoleId);
        _suppressPermissionReload = false;
        _ = LoadUserPermissionsAsync(_editingUserId);
        UserMessage = "اترك كلمة المرور فارغة إذا كنت لا تريد تغييرها.";
        return Task.CompletedTask;
    }

    private async Task SaveUserAsync()
    {
        if (!_cashierId.HasValue)
        {
            UserMessage = "يجب تسجيل الدخول أولاً.";
            return;
        }

        if (SelectedUserRole is null)
        {
            UserMessage = "اختر دور المستخدم.";
            return;
        }

        try
        {
            var result = await _userManagementService.SaveUserAsync(
                new UpsertUserRequest(
                    _editingUserId,
                    UserFullName,
                    UserUsername,
                    UserPassword,
                    SelectedUserRole.Id,
                    UserIsActive,
                    UserPermissions.Where(x => x.IsGranted).Select(x => x.Code).ToArray()),
                _cashierId.Value);

            UserMessage = result.Message;
            if (result.Succeeded)
            {
                if (_editingUserId == _cashierId)
                {
                    _currentPermissions = UserPermissions
                        .Where(x => x.IsGranted)
                        .Select(x => x.Code)
                        .ToHashSet(StringComparer.Ordinal);
                    _currentUserSession.SignIn(
                        _cashierId.Value,
                        UserUsername,
                        UserFullName,
                        _currentPermissions);
                    RaisePermissionVisibilityChanges();
                    RefreshWorkspaceAfterPermissionChange();
                }

                await LoadUsersAsync();
                _editingUserId = result.Value?.Id;
                SelectedUser = Users.FirstOrDefault(x => x.Id == _editingUserId);
                UserPassword = string.Empty;
            }
        }
        catch (UnauthorizedAccessException ex)
        {
            UserMessage = ex.Message;
        }
    }

    private async Task ToggleUserActiveAsync()
    {
        if (!_cashierId.HasValue)
        {
            UserMessage = "يجب تسجيل الدخول أولاً.";
            return;
        }

        if (SelectedUser is null)
        {
            UserMessage = "اختر مستخدماً أولاً.";
            return;
        }

        try
        {
            var result = await _userManagementService.SetUserActiveAsync(
                SelectedUser.Id,
                !SelectedUser.IsActive,
                _cashierId.Value);

            UserMessage = result.Message;
            await LoadUsersAsync();
        }
        catch (UnauthorizedAccessException ex)
        {
            UserMessage = ex.Message;
        }
    }

    private async Task LoadUserPermissionsAsync(int? userId)
    {
        PermissionMessage = string.Empty;
        UserPermissions.Clear();

        if (SelectedUserRole is null)
        {
            PermissionMessage = "اختر الدور أولاً.";
            return;
        }

        try
        {
            var permissions = await _userManagementService.GetUserPermissionsAsync(userId, SelectedUserRole.Id);
            foreach (var permission in permissions)
            {
                UserPermissions.Add(new PermissionItemViewModel(permission));
            }

            PermissionMessage = "حدد الصلاحيات المسموحة لهذا المستخدم ثم اضغط حفظ المستخدم.";
        }
        catch (UnauthorizedAccessException ex)
        {
            PermissionMessage = ex.Message;
        }
    }

    private async Task LoadShiftSummaryAsync()
    {
        CloseShiftMessage = string.Empty;

        if (!_cashierId.HasValue)
        {
            ShiftSummaryText = "يجب تسجيل الدخول أولاً.";
            return;
        }

        Result<ShiftSummaryDto> result;
        try
        {
            result = await _shiftService.GetOpenShiftSummaryAsync(_cashierId.Value);
        }
        catch (UnauthorizedAccessException ex)
        {
            ShiftSummaryText = ex.Message;
            return;
        }

        if (!result.Succeeded || result.Value is null)
        {
            ShiftSummaryText = result.Message;
            return;
        }

        var summary = result.Value;
        ShiftSummaryText =
            $"افتتاح الشيفت: {summary.OpeningCash:0.00} ج.م\n" +
            $"مبيعات نقدية: {summary.CashSales:0.00} ج.م\n" +
            $"مرتجعات: {summary.ReturnsAmount:0.00} ج.م\n" +
            $"عدد الفواتير: {summary.InvoiceCount}\n" +
            $"المتوقع في الدرج: {summary.ExpectedCash:0.00} ج.م";
    }

    private async Task CloseShiftAsync()
    {
        if (!_cashierId.HasValue)
        {
            CloseShiftMessage = "يجب تسجيل الدخول أولاً.";
            return;
        }

        if (!TryParseMoney(ClosingCash, out var closingCash))
        {
            CloseShiftMessage = "مبلغ إغلاق الشيفت غير صحيح. يمكنك كتابة المبلغ بهذا الشكل 500 أو 500.5 أو 500,5.";
            return;
        }

        Result<CloseShiftResultDto> result;
        try
        {
            result = await _shiftService.CloseShiftAsync(_cashierId.Value, closingCash);
        }
        catch (UnauthorizedAccessException ex)
        {
            CloseShiftMessage = ex.Message;
            return;
        }

        CloseShiftMessage = result.Message;
        if (result.Succeeded && result.Value is not null)
        {
            await EndSessionAfterShiftClosedAsync(result.Value);
        }
    }

    private async Task EndSessionAfterShiftClosedAsync(CloseShiftResultDto result)
    {
        await _authService.LogoutAsync();

        _isAuthenticated = false;
        _cashierId = null;
        _shiftId = null;
        _currentPermissions.Clear();
        SetActiveWorkspace("POS");
        Username = string.Empty;
        Password = string.Empty;
        NewPassword = string.Empty;
        ConfirmPassword = string.Empty;
        ClosingCash = string.Empty;
        OpeningCash = string.Empty;
        DiscountAmount = string.Empty;
        SelectedDiscountType = DiscountTypeAmount;
        PosMessage = string.Empty;
        ShiftMessage = string.Empty;
        CloseShiftMessage = string.Empty;
        ShiftSummaryText = string.Empty;
        SessionText = "لم يتم تسجيل الدخول";
        LoginMessage =
            $"تم إغلاق الشيفت بنجاح. المتوقع {result.ExpectedCash:0.00} ج.م، الفعلي {result.ClosingCash:0.00} ج.م، الفرق {result.Difference:0.00} ج.م. سجل الدخول مرة أخرى لبدء شيفت جديد.";

        CartLines.Clear();
        SuspendedInvoices.Clear();
        Products.Clear();
        _selectedProducts.Clear();
        SelectedProduct = null;
        SelectedCartLine = null;
        SelectedSuspendedInvoice = null;
        _lastInvoiceId = null;

        OnPropertyChanged(nameof(LoginVisibility));
        OnPropertyChanged(nameof(NavigationVisibility));
        OnPropertyChanged(nameof(OpenShiftVisibility));
        OnPropertyChanged(nameof(PosVisibility));
        OnPropertyChanged(nameof(ProductManagementVisibility));
        OnPropertyChanged(nameof(PrintingServicesVisibility));
        OnPropertyChanged(nameof(InventoryVisibility));
        OnPropertyChanged(nameof(ReturnsVisibility));
        OnPropertyChanged(nameof(ShiftVisibility));
        OnPropertyChanged(nameof(ReportsVisibility));
        OnPropertyChanged(nameof(InvoiceHistoryVisibility));
        OnPropertyChanged(nameof(PrinterSettingsVisibility));
        OnPropertyChanged(nameof(SystemSettingsVisibility));
        OnPropertyChanged(nameof(BackupVisibility));
        OnPropertyChanged(nameof(AuditLogsVisibility));
        OnPropertyChanged(nameof(UsersVisibility));
        RaisePermissionVisibilityChanges();
        OnPropertyChanged(nameof(CartTotal));
        OnPropertyChanged(nameof(CartTotalText));
    }

    private async Task FindInvoiceForReturnAsync()
    {
        ReturnMessage = string.Empty;
        ReturnItems.Clear();
        PossibleReturnInvoices.Clear();
        SelectedPossibleReturnInvoice = null;
        OnPropertyChanged(nameof(PossibleReturnInvoicesVisibility));
        OnPropertyChanged(nameof(ReturnTotal));
        OnPropertyChanged(nameof(ReturnTotalText));
        _returnInvoiceId = null;
        ReturnInvoiceSummary = string.Empty;

        Result<IReadOnlyList<ReturnInvoiceMatchDto>> matchesResult;
        try
        {
            matchesResult = await _returnService.SearchInvoicesAsync(ReturnInvoiceNumber);
        }
        catch (UnauthorizedAccessException ex)
        {
            ReturnMessage = ex.Message;
            return;
        }

        if (!matchesResult.Succeeded || matchesResult.Value is null)
        {
            ReturnMessage = matchesResult.Message;
            return;
        }

        if (matchesResult.Value.Count == 0)
        {
            ReturnMessage = "لم يتم العثور على الفاتورة.";
            return;
        }

        if (matchesResult.Value.Count > 1)
        {
            foreach (var match in matchesResult.Value)
            {
                PossibleReturnInvoices.Add(match);
            }

            SelectedPossibleReturnInvoice = PossibleReturnInvoices.FirstOrDefault();
            ReturnMessage = "اختر الفاتورة المطلوبة من النتائج المحتملة.";
            OnPropertyChanged(nameof(PossibleReturnInvoicesVisibility));
            return;
        }

        await LoadInvoiceForReturnAsync(matchesResult.Value[0].InvoiceNumber);
    }

    private async Task SelectPossibleReturnInvoiceAsync()
    {
        if (SelectedPossibleReturnInvoice is null)
        {
            ReturnMessage = "اختر فاتورة من النتائج المحتملة.";
            return;
        }

        ReturnInvoiceNumber = SelectedPossibleReturnInvoice.InvoiceNumber;
        await LoadInvoiceForReturnAsync(SelectedPossibleReturnInvoice.InvoiceNumber);
    }

    private async Task LoadInvoiceForReturnAsync(string invoiceNumber)
    {
        ReturnItems.Clear();
        PossibleReturnInvoices.Clear();
        SelectedPossibleReturnInvoice = null;
        OnPropertyChanged(nameof(PossibleReturnInvoicesVisibility));

        Result<InvoiceForReturnDto> result;
        try
        {
            result = await _returnService.FindInvoiceAsync(invoiceNumber);
        }
        catch (UnauthorizedAccessException ex)
        {
            ReturnMessage = ex.Message;
            return;
        }

        if (!result.Succeeded || result.Value is null)
        {
            ReturnMessage = result.Message;
            return;
        }

        _returnInvoiceId = result.Value.InvoiceId;
        ReturnInvoiceSummary = $"فاتورة {result.Value.InvoiceNumber} - الإجمالي {result.Value.NetAmount:0.00} ج.م";

        foreach (var item in result.Value.Items)
        {
            ReturnItems.Add(new ReturnItemViewModel(item));
        }

        OnPropertyChanged(nameof(ReturnTotal));
        OnPropertyChanged(nameof(ReturnTotalText));
    }

    private Task ReturnAllItemsAsync()
    {
        if (ReturnItems.Count == 0)
        {
            ReturnMessage = "ابحث عن الفاتورة أولاً.";
            return Task.CompletedTask;
        }

        foreach (var item in ReturnItems)
        {
            item.ReturnQuantity = item.ReturnableQuantity;
        }

        ReturnMessage = string.Empty;
        return Task.CompletedTask;
    }

    private async Task SaveReturnAsync()
    {
        if (!_cashierId.HasValue || !_shiftId.HasValue)
        {
            ReturnMessage = "لا يوجد شيفت مفتوح. افتح الشيفت قبل حفظ المرتجع.";
            return;
        }

        if (!_returnInvoiceId.HasValue)
        {
            ReturnMessage = "ابحث عن الفاتورة أولاً.";
            return;
        }

        var lines = ReturnItems
            .Where(x => x.ReturnQuantity > 0)
            .Select(x => new ReturnLineRequest(x.InvoiceItemId, x.ReturnQuantity))
            .ToArray();

        Result<ReturnResultDto> result;
        try
        {
            result = await _returnService.CreateReturnAsync(new CreateReturnRequest(
                _returnInvoiceId.Value,
                _cashierId.Value,
                _shiftId.Value,
                ReturnReason,
                lines));
        }
        catch (UnauthorizedAccessException ex)
        {
            ReturnMessage = ex.Message;
            return;
        }

        ReturnMessage = result.Message;
        if (result.Succeeded)
        {
            ReturnReason = string.Empty;
            await LoadInvoiceForReturnAsync(ReturnInvoiceNumber);
            if (HasPermission(PermissionCodes.CanUsePOS))
            {
                await SearchProductsAsync();
            }
            await RefreshNotificationsAsync();
        }
    }

    private Task AddSelectedProductAsync()
    {
        var productsToAdd = _selectedProducts.Count > 0
            ? _selectedProducts
            : SelectedProduct is null
                ? []
                : [SelectedProduct];

        if (productsToAdd.Count == 0)
        {
            PosMessage = "اختر منتجاً واحداً أو أكثر من نتائج البحث.";
            return Task.CompletedTask;
        }

        foreach (var product in productsToAdd)
        {
            AddProductToCart(product);
        }

        return Task.CompletedTask;
    }

    public void SetSelectedProducts(IEnumerable<ProductLookupDto> products)
    {
        _selectedProducts.Clear();
        _selectedProducts.AddRange(products);
    }

    public void AddProductFromDoubleClick(ProductLookupDto product)
    {
        AddProductToCart(product);
    }

    private Task RemoveSelectedCartLineAsync()
    {
        if (SelectedCartLine is null)
        {
            PosMessage = "اختر صنفاً من الفاتورة لحذفه.";
            return Task.CompletedTask;
        }

        CartLines.Remove(SelectedCartLine);
        SelectedCartLine = null;
        PosMessage = string.Empty;
        return Task.CompletedTask;
    }

    private async Task LoadSuspendedInvoicesAsync()
    {
        SuspendedInvoices.Clear();
        SelectedSuspendedInvoice = null;

        if (!_cashierId.HasValue || !_shiftId.HasValue || !HasPermission(PermissionCodes.CanUsePOS))
        {
            return;
        }

        var result = await _posService.GetSuspendedInvoicesAsync(_cashierId.Value, _shiftId.Value);
        if (!result.Succeeded || result.Value is null)
        {
            PosMessage = result.Message;
            return;
        }

        foreach (var invoice in result.Value)
        {
            SuspendedInvoices.Add(invoice);
        }

        SelectedSuspendedInvoice = SuspendedInvoices.FirstOrDefault();
    }

    private async Task HoldInvoiceAsync()
    {
        if (!_cashierId.HasValue || !_shiftId.HasValue)
        {
            PosMessage = "لا يوجد شيفت مفتوح.";
            return;
        }

        if (CartLines.Count == 0)
        {
            PosMessage = "أضف صنفاً واحداً على الأقل قبل تعليق الفاتورة.";
            return;
        }

        if (!TryBuildDiscountAmount(out var discount, out var discountError))
        {
            PosMessage = discountError ?? "قيمة الخصم غير صحيحة.";
            return;
        }

        var result = await _posService.SuspendInvoiceAsync(new SuspendInvoiceRequest(
            _cashierId.Value,
            _shiftId.Value,
            discount,
            BuildSaleLinesFromCart()));

        if (!result.Succeeded || result.Value is null)
        {
            PosMessage = result.Message;
            return;
        }

        CartLines.Clear();
        DiscountAmount = string.Empty;
        SelectedDiscountType = DiscountTypeAmount;
        SelectedCartLine = null;
        _selectedProducts.Clear();
        await LoadSuspendedInvoicesAsync();
        OnPropertyChanged(nameof(CartTotal));
        OnPropertyChanged(nameof(CartTotalText));
        PosMessage = $"تم تعليق الفاتورة {result.Value.HoldNumber} بقيمة {result.Value.TotalAmount:0.00} ج.م.";
    }

    private async Task ResumeSuspendedInvoiceAsync()
    {
        if (!_cashierId.HasValue || !_shiftId.HasValue)
        {
            PosMessage = "لا يوجد شيفت مفتوح.";
            return;
        }

        if (SelectedSuspendedInvoice is null)
        {
            PosMessage = "اختر فاتورة معلقة لاسترجاعها.";
            return;
        }

        if (CartLines.Count > 0)
        {
            PosMessage = "الفاتورة الحالية بها أصناف. احفظها أو علّقها أولاً قبل استرجاع فاتورة أخرى.";
            return;
        }

        var result = await _posService.ResumeSuspendedInvoiceAsync(SelectedSuspendedInvoice.Id, _cashierId.Value, _shiftId.Value);
        if (!result.Succeeded || result.Value is null)
        {
            PosMessage = result.Message;
            await LoadSuspendedInvoicesAsync();
            return;
        }

        CartLines.Clear();
        foreach (var line in result.Value.Lines)
        {
            CartLines.Add(new CartLineViewModel(
                line.ProductId,
                line.ItemName,
                line.Barcode,
                line.UnitPrice,
                line.Quantity,
                line.PrintingServiceTemplateId));
        }

        SelectedDiscountType = DiscountTypeAmount;
        DiscountAmount = result.Value.DiscountAmount > 0 ? result.Value.DiscountAmount.ToString("0.##", CultureInfo.InvariantCulture) : string.Empty;
        await LoadSuspendedInvoicesAsync();
        OnPropertyChanged(nameof(CartTotal));
        OnPropertyChanged(nameof(CartTotalText));
        PosMessage = $"تم استرجاع الفاتورة المعلقة {result.Value.HoldNumber}.";
    }

    private async Task CancelSuspendedInvoiceAsync()
    {
        if (!_cashierId.HasValue || !_shiftId.HasValue)
        {
            PosMessage = "لا يوجد شيفت مفتوح.";
            return;
        }

        if (SelectedSuspendedInvoice is null)
        {
            PosMessage = "اختر فاتورة معلقة لإلغائها.";
            return;
        }

        var result = await _posService.CancelSuspendedInvoiceAsync(SelectedSuspendedInvoice.Id, _cashierId.Value, _shiftId.Value);
        PosMessage = result.Message;
        await LoadSuspendedInvoicesAsync();
    }

    private Task AddPrintServiceAsync()
    {
        if (!int.TryParse(PrintPages, out var pages) || pages <= 0)
        {
            PosMessage = "عدد الصفحات غير صحيح.";
            return Task.CompletedTask;
        }

        if (!int.TryParse(PrintCopies, out var copies) || copies <= 0)
        {
            PosMessage = "عدد النسخ غير صحيح.";
            return Task.CompletedTask;
        }

        if (!TryParseMoney(PrintPricePerPage, out var pricePerPage) || pricePerPage <= 0)
        {
            PosMessage = "سعر الصفحة غير صحيح.";
            return Task.CompletedTask;
        }

        var quantity = pages * copies;
        var name = string.IsNullOrWhiteSpace(PrintServiceName)
            ? "خدمة طباعة"
            : PrintServiceName.Trim();

        CartLines.Add(new CartLineViewModel(null, $"{name} - {pages} صفحة × {copies} نسخة", string.Empty, pricePerPage, quantity));
        PosMessage = string.Empty;
        return Task.CompletedTask;
    }

    private async Task AddPrintingTemplateToCartAsync()
    {
        if (SelectedCashierPrintingTemplate is null)
        {
            PosMessage = "اختر خدمة طباعة أولاً.";
            return;
        }

        if (!TryParseMoney(CashierPrintingServiceQuantity, out var quantity) || quantity <= 0)
        {
            PosMessage = "كمية خدمة الطباعة غير صحيحة.";
            return;
        }

        CartLines.Add(new CartLineViewModel(
            null,
            SelectedCashierPrintingTemplate.ServiceName,
            string.Empty,
            SelectedCashierPrintingTemplate.SellingPricePerUnit,
            quantity,
            SelectedCashierPrintingTemplate.Id,
            SelectedCashierPrintingTemplate.UnitName));

        CashierPrintingServiceQuantity = "1";
        PosMessage = string.Empty;
        await Task.CompletedTask;
    }

    private async Task LoadCashierPrintingTemplatesAsync()
    {
        CashierPrintingTemplates.Clear();
        SelectedCashierPrintingTemplate = null;

        if (!HasPermission(PermissionCodes.CanUsePOS))
        {
            return;
        }

        var templates = await _printingServiceTemplateService.GetCashierTemplatesAsync();
        foreach (var template in templates)
        {
            CashierPrintingTemplates.Add(template);
        }

        SelectedCashierPrintingTemplate = CashierPrintingTemplates.FirstOrDefault();
    }

    private async Task LoadManagedPrintingMaterialsAsync()
    {
        ManagedPrintingMaterials.Clear();
        SelectedManagedPrintingMaterial = null;

        try
        {
            var materials = await _printingMaterialService.SearchAsync(
                PrintingMaterialsSearchText,
                ShowInactivePrintingMaterials);

            foreach (var material in materials)
            {
                ManagedPrintingMaterials.Add(material);
            }

            SelectedManagedPrintingMaterial = ManagedPrintingMaterials.FirstOrDefault();
            ManagedPrintingMaterialMessage = materials.Count == 0 ? "لا توجد خامات طباعة مطابقة." : string.Empty;
        }
        catch (UnauthorizedAccessException ex)
        {
            ManagedPrintingMaterialMessage = ex.Message;
        }
    }

    private Task NewPrintingMaterialAsync()
    {
        _editingPrintingMaterialId = null;
        ManagedPrintingMaterialName = string.Empty;
        ManagedPrintingMaterialBarcode = string.Empty;
        ManagedPrintingMaterialInternalCode = string.Empty;
        ManagedPrintingMaterialPurchasePrice = string.Empty;
        ManagedPrintingMaterialStockQuantity = 0;
        ManagedPrintingMaterialLowStockThreshold = _systemLowStockThreshold > 0 ? _systemLowStockThreshold : 5;
        SelectedManagedPrintingMaterialCategory = Categories.FirstOrDefault();
        ManagedPrintingMaterialMessage = "الخامة لا تظهر في الكاشير، وتستخدم فقط لاستهلاك خدمات الطباعة.";
        return Task.CompletedTask;
    }

    private Task EditSelectedPrintingMaterialAsync()
    {
        if (SelectedManagedPrintingMaterial is null)
        {
            ManagedPrintingMaterialMessage = "اختر خامة طباعة للتعديل.";
            return Task.CompletedTask;
        }

        _editingPrintingMaterialId = SelectedManagedPrintingMaterial.Id;
        ManagedPrintingMaterialName = SelectedManagedPrintingMaterial.Name;
        ManagedPrintingMaterialBarcode = SelectedManagedPrintingMaterial.Barcode;
        ManagedPrintingMaterialInternalCode = SelectedManagedPrintingMaterial.InternalCode;
        ManagedPrintingMaterialPurchasePrice = SelectedManagedPrintingMaterial.PurchasePrice.ToString("0.##", CultureInfo.InvariantCulture);
        ManagedPrintingMaterialStockQuantity = SelectedManagedPrintingMaterial.StockQuantity;
        ManagedPrintingMaterialLowStockThreshold = SelectedManagedPrintingMaterial.LowStockThreshold;
        SelectedManagedPrintingMaterialCategory = Categories.FirstOrDefault(x => x.Id == SelectedManagedPrintingMaterial.CategoryId);
        ManagedPrintingMaterialMessage = $"جاري تعديل خامة الطباعة: {SelectedManagedPrintingMaterial.Name}";
        return Task.CompletedTask;
    }

    private async Task SavePrintingMaterialAsync()
    {
        if (!_cashierId.HasValue)
        {
            ManagedPrintingMaterialMessage = "سجّل الدخول أولاً.";
            return;
        }

        if (!TryParseMoney(ManagedPrintingMaterialPurchasePrice, out var purchasePrice))
        {
            ManagedPrintingMaterialMessage = "سعر الشراء غير صحيح.";
            return;
        }

        try
        {
            var result = await _printingMaterialService.SaveAsync(
                new UpsertPrintingMaterialRequest(
                    _editingPrintingMaterialId,
                    ManagedPrintingMaterialName,
                    ManagedPrintingMaterialBarcode,
                    SelectedManagedPrintingMaterialCategory?.Id,
                    purchasePrice,
                    ManagedPrintingMaterialStockQuantity,
                    ManagedPrintingMaterialLowStockThreshold),
                _cashierId.Value);

            ManagedPrintingMaterialMessage = result.Message;
            if (!result.Succeeded)
            {
                return;
            }

            await LoadManagedPrintingMaterialsAsync();
            await LoadPrintingMaterialProductsAsync();
            await RefreshNotificationsAsync();
            await NewPrintingMaterialAsync();
        }
        catch (UnauthorizedAccessException ex)
        {
            ManagedPrintingMaterialMessage = ex.Message;
        }
    }

    private async Task TogglePrintingMaterialActiveAsync()
    {
        if (!_cashierId.HasValue)
        {
            ManagedPrintingMaterialMessage = "سجّل الدخول أولاً.";
            return;
        }

        if (SelectedManagedPrintingMaterial is null)
        {
            ManagedPrintingMaterialMessage = "اختر خامة طباعة أولاً.";
            return;
        }

        try
        {
            var result = await _printingMaterialService.SetActiveAsync(
                SelectedManagedPrintingMaterial.Id,
                !SelectedManagedPrintingMaterial.IsActive,
                _cashierId.Value);

            ManagedPrintingMaterialMessage = result.Message;
            await LoadManagedPrintingMaterialsAsync();
            await LoadPrintingMaterialProductsAsync();
            await RefreshNotificationsAsync();
        }
        catch (UnauthorizedAccessException ex)
        {
            ManagedPrintingMaterialMessage = ex.Message;
        }
    }

    private async Task LoadPrintingServicesAsync()
    {
        PrintingServiceTemplates.Clear();
        SelectedPrintingServiceTemplate = null;

        try
        {
            var templates = await _printingServiceTemplateService.SearchAsync(PrintingServiceSearchText, ShowInactivePrintingServices);
            foreach (var template in templates)
            {
                PrintingServiceTemplates.Add(template);
            }

            SelectedPrintingServiceTemplate = PrintingServiceTemplates.FirstOrDefault();
            PrintingServiceMessage = templates.Count == 0 ? "لا توجد خدمات طباعة مطابقة." : string.Empty;
        }
        catch (UnauthorizedAccessException ex)
        {
            PrintingServiceMessage = ex.Message;
        }
    }

    private async Task LoadPrintingMaterialProductsAsync()
    {
        PrintingMaterialProducts.Clear();
        try
        {
            var products = await _printingServiceTemplateService.GetMaterialProductsAsync();
            foreach (var product in products)
            {
                PrintingMaterialProducts.Add(product);
            }

            SelectedPrintingMaterialProduct = PrintingMaterialProducts.FirstOrDefault();
        }
        catch (UnauthorizedAccessException ex)
        {
            PrintingServiceMessage = ex.Message;
        }
    }

    private Task NewPrintingServiceTemplateAsync()
    {
        _editingPrintingServiceTemplateId = null;
        SelectedPrintingServiceTemplate = null;
        PrintingServiceName = string.Empty;
        SelectedPrintingServiceType = PrintingServiceTypes.LastOrDefault();
        PrintingServiceUnitName = "صفحة";
        PrintingServiceSellingPricePerUnit = string.Empty;
        PrintingServiceUsesPaper = false;
        PrintingServicePaperConsumptionPerUnit = "1";
        PrintingServiceUsesInk = false;
        SelectedPrintingServiceInkCostMode = InkCostModes.FirstOrDefault();
        PrintingServiceEstimatedInkCostPerUnit = string.Empty;
        PrintingServiceShowInCashier = true;
        PrintingServiceIsActive = true;
        PrintingServiceShortcutKey = string.Empty;
        PrintingServiceNotes = string.Empty;
        PrintingTemplateMaterials.Clear();
        PrintingMaterialQuantityPerUnit = "1";
        PrintingMaterialNotes = string.Empty;
        PrintingServiceMessage = string.Empty;
        return Task.CompletedTask;
    }

    private async Task EditSelectedPrintingServiceTemplateAsync()
    {
        if (SelectedPrintingServiceTemplate is null)
        {
            PrintingServiceMessage = "اختر خدمة طباعة من القائمة.";
            return;
        }

        var details = await _printingServiceTemplateService.GetAsync(SelectedPrintingServiceTemplate.Id);
        if (details is null)
        {
            PrintingServiceMessage = "لم يتم العثور على خدمة الطباعة.";
            return;
        }

        _editingPrintingServiceTemplateId = details.Id;
        PrintingServiceName = details.ServiceName;
        SelectedPrintingServiceType = PrintingServiceTypes.FirstOrDefault(x => x.Value == details.ServiceType);
        PrintingServiceUnitName = details.UnitName;
        PrintingServiceSellingPricePerUnit = details.SellingPricePerUnit.ToString("0.##", CultureInfo.InvariantCulture);
        PrintingServiceUsesPaper = details.UsesPaper;
        PrintingServicePaperConsumptionPerUnit = details.PaperConsumptionPerUnit.ToString("0.###", CultureInfo.InvariantCulture);
        PrintingServiceUsesInk = details.UsesInk;
        SelectedPrintingServiceInkCostMode = InkCostModes.FirstOrDefault(x => x.Value == details.InkCostMode);
        PrintingServiceEstimatedInkCostPerUnit = details.EstimatedInkCostPerUnit.ToString("0.##", CultureInfo.InvariantCulture);
        PrintingServiceShowInCashier = details.ShowInCashier;
        PrintingServiceIsActive = details.IsActive;
        PrintingServiceShortcutKey = details.ShortcutKey ?? string.Empty;
        PrintingServiceNotes = details.Notes ?? string.Empty;

        PrintingTemplateMaterials.Clear();
        foreach (var material in details.Materials)
        {
            PrintingTemplateMaterials.Add(new PrintingMaterialConsumptionViewModel(
                material.ProductId,
                material.ProductName,
                material.Barcode,
                material.CurrentStockQuantity,
                material.PurchasePrice,
                material.QuantityPerUnit,
                material.Notes));
        }

        SelectedPrintingTemplateMaterial = PrintingTemplateMaterials.FirstOrDefault();
        PrintingServiceMessage = string.Empty;
    }

    private async Task SavePrintingServiceTemplateAsync()
    {
        if (!_cashierId.HasValue)
        {
            PrintingServiceMessage = "سجّل الدخول أولاً.";
            return;
        }

        if (!TryParseMoney(PrintingServiceSellingPricePerUnit, out var price) || price < 0)
        {
            PrintingServiceMessage = "سعر البيع للوحدة غير صحيح.";
            return;
        }

        if (!TryParseNonNegativeDecimal(PrintingServicePaperConsumptionPerUnit, out var paperConsumption))
        {
            paperConsumption = 1m;
        }

        if (!TryParseMoney(PrintingServiceEstimatedInkCostPerUnit, out var inkCost) || inkCost < 0)
        {
            inkCost = 0m;
        }

        var materials = PrintingTemplateMaterials
            .Select(x => new PrintingMaterialConsumptionUpsertDto(x.ProductId, x.QuantityPerUnit, x.Notes))
            .ToArray();

        var result = await _printingServiceTemplateService.SaveAsync(
            new UpsertPrintingServiceTemplateRequest(
                _editingPrintingServiceTemplateId,
                PrintingServiceName,
                SelectedPrintingServiceType?.Value ?? PrintingServiceType.Other,
                PrintingServiceUnitName,
                price,
                materials.Length > 0,
                paperConsumption,
                PrintingServiceUsesInk,
                PrintingServiceUsesInk ? SelectedPrintingServiceInkCostMode?.Value ?? InkCostMode.None : InkCostMode.None,
                PrintingServiceUsesInk ? inkCost : 0m,
                PrintingServiceShowInCashier,
                PrintingServiceIsActive,
                PrintingServiceShortcutKey,
                PrintingServiceNotes,
                materials),
            _cashierId.Value);

        PrintingServiceMessage = result.Message;
        if (!result.Succeeded || result.Value is null)
        {
            return;
        }

        _editingPrintingServiceTemplateId = result.Value.Id;
        await LoadPrintingServicesAsync();
        await LoadCashierPrintingTemplatesAsync();
    }

    private async Task TogglePrintingServiceTemplateActiveAsync()
    {
        if (!_cashierId.HasValue)
        {
            PrintingServiceMessage = "سجّل الدخول أولاً.";
            return;
        }

        if (SelectedPrintingServiceTemplate is null)
        {
            PrintingServiceMessage = "اختر خدمة طباعة من القائمة.";
            return;
        }

        var result = await _printingServiceTemplateService.ToggleActiveAsync(SelectedPrintingServiceTemplate.Id, _cashierId.Value);
        PrintingServiceMessage = result.Message;
        await LoadPrintingServicesAsync();
        await LoadCashierPrintingTemplatesAsync();
    }

    private Task AddPrintingMaterialAsync()
    {
        if (SelectedPrintingMaterialProduct is null)
        {
            PrintingServiceMessage = "اختر خامة من المخزون.";
            return Task.CompletedTask;
        }

        if (!TryParseNonNegativeDecimal(PrintingMaterialQuantityPerUnit, out var quantity) || quantity <= 0)
        {
            PrintingServiceMessage = "كمية الاستهلاك لكل وحدة غير صحيحة.";
            return Task.CompletedTask;
        }

        if (PrintingTemplateMaterials.Any(x => x.ProductId == SelectedPrintingMaterialProduct.Id))
        {
            PrintingServiceMessage = "هذه الخامة مضافة بالفعل.";
            return Task.CompletedTask;
        }

        PrintingTemplateMaterials.Add(new PrintingMaterialConsumptionViewModel(
            SelectedPrintingMaterialProduct.Id,
            SelectedPrintingMaterialProduct.Name,
            SelectedPrintingMaterialProduct.Barcode,
            SelectedPrintingMaterialProduct.StockQuantity,
            SelectedPrintingMaterialProduct.PurchasePrice,
            quantity,
            PrintingMaterialNotes));

        PrintingMaterialQuantityPerUnit = "1";
        PrintingMaterialNotes = string.Empty;
        PrintingServiceMessage = string.Empty;
        return Task.CompletedTask;
    }

    private Task RemovePrintingMaterialAsync()
    {
        if (SelectedPrintingTemplateMaterial is null)
        {
            PrintingServiceMessage = "اختر خامة لحذفها.";
            return Task.CompletedTask;
        }

        PrintingTemplateMaterials.Remove(SelectedPrintingTemplateMaterial);
        SelectedPrintingTemplateMaterial = PrintingTemplateMaterials.FirstOrDefault();
        PrintingServiceMessage = string.Empty;
        return Task.CompletedTask;
    }

    private void AddProductToCart(ProductLookupDto product)
    {
        var existing = CartLines.SingleOrDefault(x => x.ProductId == product.Id);
        if (existing is null)
        {
            CartLines.Add(new CartLineViewModel(
                product.Id,
                product.Name,
                product.Barcode,
                product.SalePrice));
        }
        else
        {
            existing.Quantity++;
        }

        PosMessage = string.Empty;
        OnPropertyChanged(nameof(CartTotal));
        OnPropertyChanged(nameof(CartTotalText));
    }

    private async Task CompleteSaleAsync()
    {
        await CompleteSaleCoreAsync(printAfterSave: false);
    }

    private async Task CompleteSaleAndPrintAsync()
    {
        await CompleteSaleCoreAsync(printAfterSave: true);
    }

    private async Task CompleteSaleCoreAsync(bool printAfterSave)
    {
        if (!_cashierId.HasValue || !_shiftId.HasValue)
        {
            PosMessage = "لا يوجد شيفت مفتوح.";
            return;
        }

        if (CartLines.Count == 0)
        {
            PosMessage = "أضف صنفاً واحداً على الأقل إلى الفاتورة.";
            return;
        }

        if (!TryBuildDiscountAmount(out var discount, out var discountError))
        {
            PosMessage = discountError ?? "قيمة الخصم غير صحيحة.";
            return;
        }

        var request = new CompleteSaleRequest(
            _cashierId.Value,
            _shiftId.Value,
            discount,
            PaymentMethod.Cash,
            BuildSaleLinesFromCart());

        var result = await _posService.CompleteSaleAsync(request);
        if (!result.Succeeded || result.Value is null)
        {
            PosMessage = result.Message;
            return;
        }

        CartLines.Clear();
        DiscountAmount = string.Empty;
        SelectedDiscountType = DiscountTypeAmount;
        _lastInvoiceId = result.Value.InvoiceId;
        OnPropertyChanged(nameof(CartTotal));
        OnPropertyChanged(nameof(CartTotalText));
        PosMessage = $"تم حفظ الفاتورة {result.Value.InvoiceNumber} بقيمة {result.Value.NetAmount:0.00} ج.م.";
        await SearchProductsAsync();
        await RefreshNotificationsAsync();

        if (printAfterSave)
        {
            await PrintReceiptByIdAsync(result.Value.InvoiceId, message => PosMessage = message);
        }
    }

    private bool TryBuildDiscountAmount(out decimal discount, out string? error)
    {
        discount = 0;
        error = null;

        if (!SystemDiscountsEnabled || string.IsNullOrWhiteSpace(DiscountAmount))
        {
            return true;
        }

        if (!TryParseMoney(DiscountAmount, out var enteredValue) || enteredValue < 0)
        {
            error = "قيمة الخصم غير صحيحة.";
            return false;
        }

        var subtotal = CartSubtotal;
        if (subtotal <= 0)
        {
            error = "لا يمكن تطبيق خصم على فاتورة فارغة.";
            return false;
        }

        if (SelectedDiscountType == DiscountTypePercentage)
        {
            if (enteredValue > 100)
            {
                error = "نسبة الخصم لا يمكن أن تزيد عن 100%.";
                return false;
            }

            discount = Math.Round(subtotal * enteredValue / 100m, 2, MidpointRounding.AwayFromZero);
        }
        else
        {
            discount = enteredValue;
        }

        if (discount > subtotal)
        {
            error = "قيمة الخصم أكبر من إجمالي الفاتورة.";
            return false;
        }

        return true;
    }

    private decimal CalculateCurrentDiscountAmount()
    {
        if (!SystemDiscountsEnabled || string.IsNullOrWhiteSpace(DiscountAmount) || !TryParseMoney(DiscountAmount, out var enteredValue) || enteredValue < 0)
        {
            return 0;
        }

        var subtotal = CartSubtotal;
        if (subtotal <= 0)
        {
            return 0;
        }

        var discount = SelectedDiscountType == DiscountTypePercentage
            ? enteredValue > 100
                ? 0
                : Math.Round(subtotal * enteredValue / 100m, 2, MidpointRounding.AwayFromZero)
            : enteredValue;

        return Math.Min(discount, subtotal);
    }

    private SaleLineRequest[] BuildSaleLinesFromCart() =>
        CartLines.Select(x => new SaleLineRequest(
            x.ProductId,
            x.Name,
            x.Quantity,
            x.UnitPrice,
            x.ProductId.HasValue ? ItemType.Product : ItemType.PrintingService,
            x.PrintingServiceTemplateId)).ToArray();

    private async Task PrintLastReceiptAsync()
    {
        if (!_lastInvoiceId.HasValue)
        {
            PosMessage = "لا توجد فاتورة محفوظة للطباعة.";
            return;
        }

        await PrintReceiptByIdAsync(_lastInvoiceId.Value, message => PosMessage = message);
    }

    private async Task PrintReturnInvoiceAsync()
    {
        if (!_returnInvoiceId.HasValue)
        {
            ReturnMessage = "ابحث عن الفاتورة أولاً قبل الطباعة.";
            return;
        }

        await PrintReceiptByIdAsync(_returnInvoiceId.Value, message => ReturnMessage = message);
    }

    private async Task PrintSelectedInvoiceHistoryAsync()
    {
        if (SelectedInvoiceHistoryItem is null)
        {
            InvoiceHistoryMessage = "اختر فاتورة من القائمة أولاً.";
            return;
        }

        await PrintReceiptByIdAsync(SelectedInvoiceHistoryItem.InvoiceId, message => InvoiceHistoryMessage = message);
    }

    private async Task PrintReceiptByIdAsync(int invoiceId, Action<string> setMessage)
    {
        try
        {
            var result = await _receiptService.GetReceiptAsync(invoiceId);
            if (!result.Succeeded || result.Value is null)
            {
                setMessage(result.Message);
                return;
            }

            var settings = await _printerSettingsService.GetSettingsAsync();
            var printed = _receiptPrinter.Print(result.Value, settings);
            setMessage(printed
                ? $"تم إرسال الفاتورة {result.Value.InvoiceNumber} للطباعة."
                : "لم يتم اختيار طابعة الإيصالات أو تعذر الوصول إليها. راجع إعدادات الطباعة.");
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Receipt printing failed for invoice {InvoiceId}.", invoiceId);
            setMessage("تعذر طباعة الفاتورة. تم تسجيل التفاصيل في ملف السجل.");
        }
    }

    private async Task RefreshNotificationsAsync()
    {
        LowStockNotifications.Clear();
        var notifications = await _notificationService.GetActiveLowStockAsync();
        foreach (var notification in notifications)
        {
            LowStockNotifications.Add(notification);
        }

        LowStockCount = notifications.Count;
        if (LowStockCount == 0)
        {
            IsNotificationsPopupOpen = false;
        }
    }

    private static (DateTimeOffset From, DateTimeOffset To) BuildInclusiveDateRange(DateTime fromDate, DateTime toDate)
    {
        var fromLocalDate = fromDate.Date;
        var toExclusiveDate = toDate.Date.AddDays(1);
        return (
            new DateTimeOffset(fromLocalDate, TimeZoneInfo.Local.GetUtcOffset(fromLocalDate)),
            new DateTimeOffset(toExclusiveDate, TimeZoneInfo.Local.GetUtcOffset(toExclusiveDate)));
    }

    private void RaiseCartTotalsChanged()
    {
        OnPropertyChanged(nameof(CartSubtotal));
        OnPropertyChanged(nameof(CartDiscountAmount));
        OnPropertyChanged(nameof(DiscountPreviewText));
        OnPropertyChanged(nameof(CartTotal));
        OnPropertyChanged(nameof(CartTotalText));
    }

    private void CartLines_OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems is not null)
        {
            foreach (CartLineViewModel item in e.OldItems)
            {
                item.PropertyChanged -= CartLine_OnPropertyChanged;
            }
        }

        if (e.NewItems is not null)
        {
            foreach (CartLineViewModel item in e.NewItems)
            {
                item.PropertyChanged += CartLine_OnPropertyChanged;
            }
        }

        RaiseCartTotalsChanged();
    }

    private void CartLine_OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(CartLineViewModel.Quantity) or nameof(CartLineViewModel.Total))
        {
            RaiseCartTotalsChanged();
        }
    }

    private void ReturnItems_OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems is not null)
        {
            foreach (ReturnItemViewModel item in e.OldItems)
            {
                item.PropertyChanged -= ReturnItem_OnPropertyChanged;
            }
        }

        if (e.NewItems is not null)
        {
            foreach (ReturnItemViewModel item in e.NewItems)
            {
                item.PropertyChanged += ReturnItem_OnPropertyChanged;
            }
        }

        OnPropertyChanged(nameof(ReturnTotal));
        OnPropertyChanged(nameof(ReturnTotalText));
    }

    private void ReturnItem_OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ReturnItemViewModel.ReturnQuantity) or nameof(ReturnItemViewModel.ReturnTotal))
        {
            OnPropertyChanged(nameof(ReturnTotal));
            OnPropertyChanged(nameof(ReturnTotalText));
        }
    }

    private static string FormatFileSize(long bytes)
    {
        if (bytes < 1024)
        {
            return $"{bytes} بايت";
        }

        var kb = bytes / 1024d;
        if (kb < 1024)
        {
            return $"{kb:0.0} ك.ب";
        }

        return $"{kb / 1024d:0.0} م.ب";
    }

    private static bool TryParseMoney(string value, out decimal amount)
    {
        return decimal.TryParse(
            NormalizeDecimalText(value),
            NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign,
            CultureInfo.InvariantCulture,
            out amount);
    }

    private static bool TryParseNonNegativeInt(string value, out int number)
    {
        return int.TryParse(
            NormalizeNumericText(value),
            NumberStyles.None,
            CultureInfo.InvariantCulture,
            out number) && number >= 0;
    }

    private static bool TryParseNonNegativeDecimal(string value, out decimal number)
    {
        return TryParseMoney(value, out number) && number >= 0;
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
            .ToArray());
    }

    private static bool TryParseDate(string value, out DateTime date)
    {
        return DateTime.TryParseExact(
            (value ?? string.Empty).Trim(),
            "yyyy-MM-dd",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out date);
    }
}

public sealed record QuickNavigationItemViewModel(
    string Title,
    string Workspace,
    string Keywords,
    bool IsEnabled);

