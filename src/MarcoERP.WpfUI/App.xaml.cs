using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using MaterialDesignThemes.Wpf;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;
using MarcoERP.Persistence;
using MarcoERP.Persistence.Interceptors;
using MarcoERP.Persistence.Repositories;
using MarcoERP.Persistence.Seeds;
using MarcoERP.Persistence.Services;
using MarcoERP.Domain.Interfaces;
using MarcoERP.Domain.Entities.Accounting.Policies;
using MarcoERP.Domain.Enums;
using MarcoERP.Application.Interfaces;
using MarcoERP.Application.Common;
using MarcoERP.Application.Interfaces.Accounting;
using MarcoERP.Application.Services.Accounting;
using MarcoERP.Infrastructure.Services;
using MarcoERP.Infrastructure.Security;
using FluentValidation;
using MarcoERP.Application.DTOs.Accounting;
using MarcoERP.Application.DTOs.Inventory;
using MarcoERP.Application.Validators.Accounting;
using MarcoERP.Application.Validators.Inventory;
using MarcoERP.Application.Interfaces.Inventory;
using MarcoERP.Application.Services.Inventory;
using MarcoERP.Domain.Interfaces.Inventory;
using MarcoERP.Persistence.Repositories.Inventory;
using MarcoERP.Domain.Interfaces.Sales;
using MarcoERP.Domain.Interfaces.Purchases;
using MarcoERP.Persistence.Repositories.Sales;
using MarcoERP.Persistence.Repositories.Purchases;
using MarcoERP.Application.DTOs.Sales;
using MarcoERP.Application.DTOs.Purchases;
using MarcoERP.Application.Validators.Sales;
using MarcoERP.Application.Validators.Purchases;
using MarcoERP.Application.Interfaces.Sales;
using MarcoERP.Application.Interfaces.Purchases;
using MarcoERP.Application.Services.Sales;
using MarcoERP.Application.Services.Purchases;
using MarcoERP.Application.Services.Common;
using MarcoERP.Domain.Interfaces.Treasury;
using MarcoERP.Persistence.Repositories.Treasury;
using MarcoERP.Application.DTOs.Treasury;
using MarcoERP.Application.Validators.Treasury;
using MarcoERP.Application.Interfaces.Treasury;
using MarcoERP.Application.Services.Treasury;
using MarcoERP.Application.Interfaces.Reports;
using MarcoERP.Application.Interfaces.SmartEntry;
using MarcoERP.Application.Interfaces.Search;
using MarcoERP.Application.Interfaces.Security;
using MarcoERP.Application.Interfaces.Settings;
using MarcoERP.Application.Interfaces.Printing;
using MarcoERP.Application.Services.Printing;
using MarcoERP.Application.Services.Security;
using MarcoERP.Application.Services.Settings;
using MarcoERP.Application.DTOs.Security;
using MarcoERP.Application.DTOs.Settings;
using MarcoERP.Application.Validators.Security;
using MarcoERP.Application.Validators.Settings;
using MarcoERP.Domain.Interfaces.Security;
using MarcoERP.Domain.Interfaces.Settings;
using MarcoERP.Persistence.Repositories.Security;
using MarcoERP.Persistence.Repositories.Settings;
using MarcoERP.Persistence.Services.Reports;
using MarcoERP.Application.Services.Reports;
using MarcoERP.Persistence.Services.SmartEntry;
using MarcoERP.Persistence.Services.Search;
using MarcoERP.Persistence.Services.Settings;
using MarcoERP.WpfUI.ViewModels.Sales;
using MarcoERP.WpfUI.Views.Sales;
using Microsoft.Extensions.Logging;
using Serilog;
using MarcoERP.WpfUI.Navigation;
using MarcoERP.WpfUI.Services;
using MarcoERP.WpfUI.Views.Shell;
using MarcoERP.WpfUI.ViewModels;
using MarcoERP.WpfUI.Views;
using MarcoERP.WpfUI.ViewModels.Accounting;
using MarcoERP.WpfUI.ViewModels.Inventory;
using MarcoERP.WpfUI.ViewModels.Purchases;
using MarcoERP.WpfUI.ViewModels.Treasury;
using MarcoERP.WpfUI.ViewModels.Reports;
using MarcoERP.WpfUI.ViewModels.Settings;
using MarcoERP.WpfUI.ViewModels.Shell;
using MarcoERP.WpfUI.Views.Accounting;
using MarcoERP.WpfUI.Views.Inventory;
using MarcoERP.WpfUI.Views.Purchases;
using MarcoERP.WpfUI.Views.Treasury;
using MarcoERP.WpfUI.Views.Reports;
using MarcoERP.WpfUI.Views.Settings;
using MarcoERP.WpfUI.ViewModels.Common;
using MarcoERP.WpfUI.Views.Common;
using MarcoERP.Application.Reporting.Interfaces;
using MarcoERP.WpfUI.Reporting;

namespace MarcoERP.WpfUI
{
    /// <summary>
    /// Application entry point — Composition Root for Dependency Injection.
    /// </summary>
    public partial class App : System.Windows.Application
    {
        /// <summary>Phase 5: Code version for integrity checks.</summary>
        public static string CurrentAppVersion => "1.1.0";

        private IServiceProvider _serviceProvider;
        private IConfiguration _configuration;
        private IBackgroundJobService _backgroundJobService;

        // ── Phase 6: Migration Guard state ──
        private int _pendingMigrationCount;

        /// <summary>Global access to the DI container (WPF single-window pattern).</summary>
        public static IServiceProvider Services { get; private set; }

        /// <summary>
        /// Returns true when AppSettings:VatModel is "Inclusive".
        /// Governance: ACCOUNTING_PRINCIPLES VAT-03.
        /// </summary>
        public static bool IsVatInclusive
        {
            get
            {
                var config = Services?.GetService(typeof(IConfiguration)) as IConfiguration;
                var model = config?["AppSettings:VatModel"];
                return string.Equals(model, "Inclusive", StringComparison.OrdinalIgnoreCase);
            }
        }

        public App()
        {
            // Handle any unhandled exceptions
            DispatcherUnhandledException += App_DispatcherUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
        }

        private static void App_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            MessageBox.Show(
                $"خطأ غير معالج:\n\n{e.Exception.Message}",
                "خطأ في التطبيق",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            e.Handled = true;
        }

        private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            var ex = e.ExceptionObject as Exception;
            MessageBox.Show(
                $"خطأ حرج:\n\n{ex?.Message}",
                "خطأ حرج",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }

        protected override async void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Global: Catch unobserved Task exceptions and log them without crashing the app
            TaskScheduler.UnobservedTaskException += (sender, args) =>
            {
                System.Diagnostics.Debug.WriteLine($"[UnobservedTask] {args.Exception}");
                args.SetObserved();
            };

            AppDomain.CurrentDomain.UnhandledException += (sender, args) =>
            {
                System.Diagnostics.Debug.WriteLine($"[UnhandledException] {args.ExceptionObject}");
            };

            // Global: Select all text in TextBox on focus (improves data entry for prices/amounts)
            EventManager.RegisterClassHandler(typeof(System.Windows.Controls.TextBox),
                System.Windows.Controls.TextBox.GotKeyboardFocusEvent,
                new KeyboardFocusChangedEventHandler((s, _) =>
                {
                    if (s is System.Windows.Controls.TextBox tb)
                        tb.SelectAll();
                }));
            EventManager.RegisterClassHandler(typeof(System.Windows.Controls.TextBox),
                System.Windows.Controls.TextBox.PreviewMouseLeftButtonDownEvent,
                new MouseButtonEventHandler((s, me) =>
                {
                    if (s is System.Windows.Controls.TextBox tb && !tb.IsKeyboardFocusWithin)
                    {
                        me.Handled = true;
                        tb.Focus();
                    }
                }));

            try
            {
                // Build configuration
                _configuration = new ConfigurationBuilder()
                    .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
                    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                    .Build();

                // Build DI container
                var services = new ServiceCollection();
                ConfigureServices(services);
                _serviceProvider = services.BuildServiceProvider();
                Services = _serviceProvider;

                var databaseReady = await InitializeDatabaseAsync();

                if (databaseReady)
                {
                    // Start background jobs
                    _backgroundJobService = _serviceProvider.GetRequiredService<IBackgroundJobService>();
                    _backgroundJobService.StartAll();
                }

                var loginWindow = _serviceProvider.GetRequiredService<LoginWindow>();
                MainWindow = loginWindow;
                loginWindow.Show();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Startup Error] {ex}");
                var userMessage = ex is InvalidOperationException
                    ? ex.Message
                    : "حدث خطأ في تهيئة التطبيق. يرجى مراجعة سجلات النظام.";
                MessageBox.Show(
                    $"فشل تهيئة التطبيق:\n\n{userMessage}",
                    "خطأ في التهيئة",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                Environment.Exit(1);
            }
        }

        private async Task<bool> InitializeDatabaseAsync()
        {
            var applyMigrations = _configuration.GetValue<bool>("Database:ApplyMigrationsOnStartup");
            var seedData = _configuration.GetValue<bool>("Database:SeedOnStartup");

            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<MarcoDbContext>();

            try
            {
                var canConnect = await dbContext.Database.CanConnectAsync();
                if (!canConnect)
                {
                    MessageBox.Show(
                        "لا يمكن الاتصال بقاعدة البيانات.\nجاري إنشاء قاعدة البيانات...",
                        "معلومات الاتصال",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }

                if (applyMigrations)
                {
                    // ── Phase 6: Startup Migration Guard ─────────────
                    var pendingMigrations = (await dbContext.Database
                        .GetPendingMigrationsAsync()).ToList();

                    _pendingMigrationCount = pendingMigrations.Count;
                    // ── End Phase 6 Guard ────────────────────────────

                    // Apply migrations normally (controlled or direct)
                    await dbContext.Database.MigrateAsync();
                }
                else
                {
                    await dbContext.Database.EnsureCreatedAsync();
                }

                if (seedData)
                {
                    await SystemAccountSeed.SeedAsync(dbContext);
                    await CompanySeed.SeedAsync(dbContext);
                    await UnitSeed.SeedAsync(dbContext);

                    var hasUsers = await dbContext.Users.AnyAsync();

                    // Governance: CFG-01, DPR-03 — Never store passwords in source control.
                    // Priority: Environment variable > appsettings (which should be empty in production).
                    var adminSeedPassword = Environment.GetEnvironmentVariable("MARCOERP_ADMIN_PASSWORD")
                        ?? _configuration["Security:AdminSeedPassword"];

                    if (!hasUsers && string.IsNullOrWhiteSpace(adminSeedPassword))
                        throw new InvalidOperationException("كلمة مرور المسؤول مطلوبة عند تفعيل SeedOnStartup.");

                    var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
                    var adminPasswordHash = string.IsNullOrWhiteSpace(adminSeedPassword)
                        ? string.Empty
                        : passwordHasher.HashPassword(adminSeedPassword);

                    await SecuritySeed.SeedAsync(dbContext, adminPasswordHash);
                    await SystemSettingSeed.SeedAsync(dbContext);
                    await FeatureSeed.SeedAsync(dbContext);
                    await ProfileSeed.SeedAsync(dbContext);
                    await VersionSeed.SeedAsync(dbContext);
                }

                await ShowStartupIntegrityWarningsAsync(dbContext);

                return true;
            }
            catch (Exception dbEx)
            {
                var errorDetails = $@"❌ فشل الاتصال بقاعدة البيانات

🔴 تفاصيل الخطأ:
━━━━━━━━━━━━━━━━━━━━━━━━
{dbEx.Message}

💡 تأكد من:
1. تشغيل SQL Server (.\\SQL2022)
2. صلاحيات Windows Authentication
3. اسم الـ Instance صحيح";

                MessageBox.Show(
                    errorDetails,
                    "خطأ في الاتصال بقاعدة البيانات",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return false;
            }
        }

        private static async Task ShowStartupIntegrityWarningsAsync(MarcoDbContext dbContext)
        {
            var warnings = new System.Collections.Generic.List<string>();

            var hasActiveFiscalYear = await dbContext.FiscalYears
                .AsNoTracking()
                .AnyAsync(fy => fy.Status == FiscalYearStatus.Active);
            if (!hasActiveFiscalYear)
                warnings.Add("لا توجد سنة مالية نشطة.");

            var defaultCashboxSetting = await dbContext.SystemSettings
                .FirstOrDefaultAsync(s => s.SettingKey == "DefaultCashboxId");

            var hasValidDefaultCashbox = false;
            if (defaultCashboxSetting != null &&
                int.TryParse(defaultCashboxSetting.SettingValue, out var defaultCashboxId))
            {
                hasValidDefaultCashbox = await dbContext.Cashboxes
                    .AsNoTracking()
                    .AnyAsync(c => c.Id == defaultCashboxId && c.IsActive);
            }

            if (!hasValidDefaultCashbox)
            {
                var fallbackCashbox = await dbContext.Cashboxes
                    .Where(c => c.IsActive)
                    .OrderByDescending(c => c.IsDefault)
                    .ThenBy(c => c.Id)
                    .FirstOrDefaultAsync();

                if (fallbackCashbox != null)
                {
                    if (defaultCashboxSetting == null)
                    {
                        defaultCashboxSetting = new MarcoERP.Domain.Entities.Settings.SystemSetting(
                            "DefaultCashboxId",
                            fallbackCashbox.Id.ToString(),
                            "الصندوق الافتراضي",
                            "حسابات افتراضية",
                            "int");
                        await dbContext.SystemSettings.AddAsync(defaultCashboxSetting);
                    }
                    else
                    {
                        defaultCashboxSetting.UpdateValue(fallbackCashbox.Id.ToString());
                    }

                    await dbContext.SaveChangesAsync();
                }
                else
                {
                    warnings.Add("لا توجد خزنة فعّالة لضبط الصندوق الافتراضي (DefaultCashboxId).");
                }
            }

            var draftInClosedPeriodsCount = await (
                from journal in dbContext.JournalEntries.AsNoTracking()
                join period in dbContext.FiscalPeriods.AsNoTracking() on journal.FiscalPeriodId equals period.Id
                where journal.Status == JournalEntryStatus.Draft && period.Status != PeriodStatus.Open
                select journal.Id
            ).CountAsync();

            if (draftInClosedPeriodsCount > 0)
                warnings.Add($"يوجد {draftInClosedPeriodsCount} قيود مسودة ضمن فترات مالية مغلقة.");

            if (warnings.Count == 0)
                return;

            MessageBox.Show(
                "⚠️ تحذيرات سلامة بدء التشغيل:\n\n- " + string.Join("\n- ", warnings),
                "تحذيرات سلامة البيانات",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }

        protected override void OnExit(ExitEventArgs e)
        {
            _backgroundJobService?.StopAll();
            _backgroundJobService?.Dispose();
            (_serviceProvider as IDisposable)?.Dispose();
            base.OnExit(e);
        }

        private void ConfigureServices(IServiceCollection services)
        {
            // Configuration
            services.AddSingleton(_configuration);

            // ─── Persistence Layer ───
            var dbProvider = Environment.GetEnvironmentVariable("DB_PROVIDER")
                ?? _configuration.GetValue<string>("Database:Provider")
                ?? "SqlServer";
            var connectionStringName = dbProvider.Equals("PostgreSQL", StringComparison.OrdinalIgnoreCase)
                ? "PostgreSQLConnection"
                : "DefaultConnection";
            var connectionString = Environment.GetEnvironmentVariable("DB_CONNECTION")
                ?? _configuration.GetConnectionString(connectionStringName);

            services.AddScoped<AuditSaveChangesInterceptor>();
            services.AddSingleton<HardDeleteProtectionInterceptor>();

            services.AddDbContext<MarcoDbContext>((serviceProvider, options) =>
            {
                var migrationsAssembly = typeof(MarcoDbContext).Assembly.FullName;

                if (dbProvider.Equals("PostgreSQL", StringComparison.OrdinalIgnoreCase))
                {
                    options.UseNpgsql(connectionString, npgsqlOptions =>
                    {
                        npgsqlOptions.MigrationsAssembly(migrationsAssembly);
                    });
                }
                else
                {
                    options.UseSqlServer(connectionString, sqlOptions =>
                    {
                        sqlOptions.MigrationsAssembly(migrationsAssembly);
                        // Note: EnableRetryOnFailure removed - incompatible with user-initiated transactions
                    });
                }

                // Register interceptors
                var auditInterceptor = serviceProvider.GetRequiredService<AuditSaveChangesInterceptor>();
                var hardDeleteGuard = serviceProvider.GetRequiredService<HardDeleteProtectionInterceptor>();
                options.AddInterceptors(auditInterceptor, hardDeleteGuard);
            });

            // ─── Domain Repositories ───
            services.AddScoped<IAccountRepository, AccountRepository>();
            services.AddScoped<IJournalEntryRepository, JournalEntryRepository>();
            services.AddScoped<IFiscalYearRepository, FiscalYearRepository>();
            services.AddScoped<IAuditLogRepository, AuditLogRepository>();
            services.AddScoped<IUnitOfWork, UnitOfWork>();

            // ─── Inventory Repositories ───
            services.AddScoped<ICategoryRepository, CategoryRepository>();
            services.AddScoped<IUnitRepository, UnitRepository>();
            services.AddScoped<IProductRepository, ProductRepository>();
            services.AddScoped<IWarehouseRepository, WarehouseRepository>();
            services.AddScoped<IWarehouseProductRepository, WarehouseProductRepository>();
            services.AddScoped<IInventoryMovementRepository, InventoryMovementRepository>();

            // ─── Sales & Purchases Repositories ───
            services.AddScoped<ICustomerRepository, CustomerRepository>();
            services.AddScoped<ISupplierRepository, SupplierRepository>();
            services.AddScoped<ISalesRepresentativeRepository, SalesRepresentativeRepository>();
            services.AddScoped<IPurchaseInvoiceRepository, PurchaseInvoiceRepository>();
            services.AddScoped<IPurchaseReturnRepository, PurchaseReturnRepository>();
            services.AddScoped<ISalesInvoiceRepository, SalesInvoiceRepository>();
            services.AddScoped<ISalesReturnRepository, SalesReturnRepository>();
            services.AddScoped<IPosSessionRepository, PosSessionRepository>();

            // ─── Attachments ───
            services.AddScoped<IAttachmentRepository, AttachmentRepository>();
            services.AddScoped<IAttachmentService, Application.Services.AttachmentService>();
            services.AddScoped<IPosPaymentRepository, PosPaymentRepository>();
            services.AddScoped<IPriceListRepository, PriceListRepository>();
            services.AddScoped<IInventoryAdjustmentRepository, InventoryAdjustmentRepository>();
            services.AddScoped<ISalesQuotationRepository, SalesQuotationRepository>();
            services.AddScoped<IPurchaseQuotationRepository, PurchaseQuotationRepository>();
            services.AddScoped<IOpeningBalanceRepository, OpeningBalanceRepository>();

            // ─── Treasury Repositories ───
            services.AddScoped<ICashboxRepository, CashboxRepository>();
            services.AddScoped<IBankAccountRepository, BankAccountRepository>();
            services.AddScoped<IBankReconciliationRepository, BankReconciliationRepository>();
            services.AddScoped<ICashReceiptRepository, CashReceiptRepository>();
            services.AddScoped<ICashPaymentRepository, CashPaymentRepository>();
            services.AddScoped<ICashTransferRepository, CashTransferRepository>();

            // ─── Security & Settings Repositories ───
            services.AddScoped<IUserRepository, UserRepository>();
            services.AddScoped<IRoleRepository, RoleRepository>();
            services.AddScoped<ISystemSettingRepository, SystemSettingRepository>();
            services.AddScoped<IFeatureRepository, FeatureRepository>();
            services.AddScoped<IProfileRepository, ProfileRepository>();

            // ─── Infrastructure Layer ───
            services.AddSingleton<IDateTimeProvider, DateTimeProvider>();
            services.AddSingleton<IAlertService, AlertService>();
            services.AddSingleton<IDialogService, DialogService>();
            services.AddSingleton<IActivityTracker, ActivityTracker>();
            services.AddSingleton<IBackgroundJobService, BackgroundJobService>();
            services.AddLogging(builder =>
            {
                builder.ClearProviders();
                var logger = new Serilog.LoggerConfiguration()
                    .MinimumLevel.Information()
                    .MinimumLevel.Override("Microsoft.EntityFrameworkCore", Serilog.Events.LogEventLevel.Warning)
                    .Enrich.FromLogContext()
                    .WriteTo.File(
                        System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs", "marcoerp-.log"),
                        rollingInterval: Serilog.RollingInterval.Day,
                        retainedFileCountLimit: 30,
                        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {SourceContext} {Message:lj}{NewLine}{Exception}")
                    .WriteTo.Debug()
                    .CreateLogger();
                builder.AddSerilog(logger, dispose: true);
            });
            services.AddSingleton<ICurrentUserService, CurrentUserService>();
            services.AddSingleton<ICompanyContext, DefaultCompanyContext>();
            services.AddScoped<IAuditLogger, AuditLogger>();
            services.AddScoped<IJournalNumberGenerator, JournalNumberGenerator>();
            services.AddScoped<JournalEntryFactory>();
            services.AddScoped<StockManager>();
            services.AddScoped<FiscalPeriodValidator>();
            services.AddScoped<ICodeGenerator, CodeGenerator>();
            services.AddSingleton<IPasswordHasher, PasswordHasher>();
            services.AddSingleton<IReceiptPrinterService, WindowsEscPosPrinterService>();
            services.AddScoped<IEmailService, MarcoERP.Infrastructure.Services.EmailService>();
            services.AddSingleton<INotificationService, MarcoERP.Application.Services.NotificationService>();

            // ─── FluentValidation Validators ───
            services.AddScoped<IValidator<CreateAccountDto>, CreateAccountDtoValidator>();
            services.AddScoped<IValidator<UpdateAccountDto>, UpdateAccountDtoValidator>();
            services.AddScoped<IValidator<CreateFiscalYearDto>, CreateFiscalYearDtoValidator>();
            services.AddScoped<IValidator<CreateJournalEntryDto>, CreateJournalEntryDtoValidator>();
            services.AddScoped<IValidator<ReverseJournalEntryDto>, ReverseJournalEntryDtoValidator>();

            // ─── Inventory Validators ───
            services.AddScoped<IValidator<CreateCategoryDto>, CreateCategoryDtoValidator>();
            services.AddScoped<IValidator<UpdateCategoryDto>, UpdateCategoryDtoValidator>();
            services.AddScoped<IValidator<CreateUnitDto>, CreateUnitDtoValidator>();
            services.AddScoped<IValidator<UpdateUnitDto>, UpdateUnitDtoValidator>();
            services.AddScoped<IValidator<CreateProductDto>, CreateProductDtoValidator>();
            services.AddScoped<IValidator<UpdateProductDto>, UpdateProductDtoValidator>();
            services.AddScoped<IValidator<CreateWarehouseDto>, CreateWarehouseDtoValidator>();
            services.AddScoped<IValidator<UpdateWarehouseDto>, UpdateWarehouseDtoValidator>();
            services.AddScoped<IValidator<CreateInventoryAdjustmentDto>, CreateInventoryAdjustmentDtoValidator>();
            services.AddScoped<IValidator<UpdateInventoryAdjustmentDto>, UpdateInventoryAdjustmentDtoValidator>();
            services.AddScoped<IValidator<BulkPriceUpdateRequestDto>, BulkPriceUpdateRequestDtoValidator>();

            // ─── Sales & Purchases Validators ───
            services.AddScoped<IValidator<CreateCustomerDto>, CreateCustomerDtoValidator>();
            services.AddScoped<IValidator<UpdateCustomerDto>, UpdateCustomerDtoValidator>();
            services.AddScoped<IValidator<CreateSupplierDto>, CreateSupplierDtoValidator>();
            services.AddScoped<IValidator<UpdateSupplierDto>, UpdateSupplierDtoValidator>();
            services.AddScoped<IValidator<CreatePurchaseInvoiceDto>, CreatePurchaseInvoiceDtoValidator>();
            services.AddScoped<IValidator<UpdatePurchaseInvoiceDto>, UpdatePurchaseInvoiceDtoValidator>();
            services.AddScoped<IValidator<CreatePurchaseReturnDto>, CreatePurchaseReturnDtoValidator>();
            services.AddScoped<IValidator<UpdatePurchaseReturnDto>, UpdatePurchaseReturnDtoValidator>();
            services.AddScoped<IValidator<CreateSalesInvoiceDto>, CreateSalesInvoiceDtoValidator>();
            services.AddScoped<IValidator<UpdateSalesInvoiceDto>, UpdateSalesInvoiceDtoValidator>();
            services.AddScoped<IValidator<CreateSalesReturnDto>, CreateSalesReturnDtoValidator>();
            services.AddScoped<IValidator<UpdateSalesReturnDto>, UpdateSalesReturnDtoValidator>();
            services.AddScoped<IValidator<CreateSalesRepresentativeDto>, CreateSalesRepresentativeDtoValidator>();
            services.AddScoped<IValidator<UpdateSalesRepresentativeDto>, UpdateSalesRepresentativeDtoValidator>();
            services.AddScoped<IValidator<CreateSalesQuotationDto>, CreateSalesQuotationDtoValidator>();
            services.AddScoped<IValidator<UpdateSalesQuotationDto>, UpdateSalesQuotationDtoValidator>();
            services.AddScoped<IValidator<CreatePriceListDto>, CreatePriceListDtoValidator>();
            services.AddScoped<IValidator<UpdatePriceListDto>, UpdatePriceListDtoValidator>();
            services.AddScoped<IValidator<CreatePurchaseQuotationDto>, CreatePurchaseQuotationDtoValidator>();
            services.AddScoped<IValidator<UpdatePurchaseQuotationDto>, UpdatePurchaseQuotationDtoValidator>();

            // ─── POS Validators ───
            services.AddScoped<IValidator<OpenPosSessionDto>, OpenPosSessionDtoValidator>();
            services.AddScoped<IValidator<ClosePosSessionDto>, ClosePosSessionDtoValidator>();
            services.AddScoped<IValidator<CompletePoseSaleDto>, CompletePosSaleDtoValidator>();

            // ─── Treasury Validators ───
            services.AddScoped<IValidator<CreateCashboxDto>, CreateCashboxDtoValidator>();
            services.AddScoped<IValidator<UpdateCashboxDto>, UpdateCashboxDtoValidator>();
            services.AddScoped<IValidator<CreateBankAccountDto>, CreateBankAccountDtoValidator>();
            services.AddScoped<IValidator<UpdateBankAccountDto>, UpdateBankAccountDtoValidator>();
            services.AddScoped<IValidator<CreateBankReconciliationDto>, CreateBankReconciliationDtoValidator>();
            services.AddScoped<IValidator<CreateBankReconciliationItemDto>, CreateBankReconciliationItemDtoValidator>();
            services.AddScoped<IValidator<UpdateBankReconciliationDto>, UpdateBankReconciliationDtoValidator>();
            services.AddScoped<IValidator<CreateCashReceiptDto>, CreateCashReceiptDtoValidator>();
            services.AddScoped<IValidator<UpdateCashReceiptDto>, UpdateCashReceiptDtoValidator>();
            services.AddScoped<IValidator<CreateCashPaymentDto>, CreateCashPaymentDtoValidator>();
            services.AddScoped<IValidator<UpdateCashPaymentDto>, UpdateCashPaymentDtoValidator>();
            services.AddScoped<IValidator<CreateCashTransferDto>, CreateCashTransferDtoValidator>();
            services.AddScoped<IValidator<UpdateCashTransferDto>, UpdateCashTransferDtoValidator>();

            // ─── Security & Settings Validators ───
            services.AddScoped<IValidator<CreateRoleDto>, CreateRoleDtoValidator>();
            services.AddScoped<IValidator<UpdateRoleDto>, UpdateRoleDtoValidator>();
            services.AddScoped<IValidator<CreateUserDto>, CreateUserDtoValidator>();
            services.AddScoped<IValidator<UpdateUserDto>, UpdateUserDtoValidator>();
            services.AddScoped<IValidator<ResetPasswordDto>, ResetPasswordDtoValidator>();
            services.AddScoped<IValidator<ChangePasswordDto>, ChangePasswordDtoValidator>();
            services.AddScoped<IValidator<LoginDto>, LoginDtoValidator>();
            services.AddScoped<IValidator<UpdateSystemSettingDto>, UpdateSystemSettingDtoValidator>();
            services.AddScoped<IValidator<ToggleFeatureDto>, ToggleFeatureDtoValidator>();

            // ─── Opening Balance Validators ───
            services.AddScoped<IValidator<CreateOpeningBalanceDto>, CreateOpeningBalanceDtoValidator>();
            services.AddScoped<IValidator<UpdateOpeningBalanceDto>, UpdateOpeningBalanceDtoValidator>();

            // ─── Application Layer (wrapped with AuthorizationProxy — Phase 4) ───
            services.AddAuthorizedService<IAccountService, AccountService>();
            services.AddAuthorizedService<IJournalEntryService, JournalEntryService>();
            services.AddAuthorizedService<IYearEndClosingService, YearEndClosingService>();
            services.AddAuthorizedService<IFiscalYearService, FiscalYearService>();
            services.AddAuthorizedService<IOpeningBalanceService, OpeningBalanceService>();

            // ─── Inventory Services ───
            services.AddAuthorizedService<ICategoryService, CategoryService>();
            services.AddAuthorizedService<IUnitService, UnitService>();
            services.AddAuthorizedService<IProductService, ProductService>();
            services.AddAuthorizedService<IWarehouseService, WarehouseService>();
            services.AddAuthorizedService<IBulkPriceUpdateService, BulkPriceUpdateService>();

            // ─── Sales & Purchases Services ───
            services.AddAuthorizedService<ICustomerService, CustomerService>();
            services.AddAuthorizedService<ICustomerImportService, Application.Services.Sales.CustomerImportService>();
            services.AddAuthorizedService<ISupplierService, SupplierService>();
            services.AddAuthorizedService<ISupplierImportService, Application.Services.Purchases.SupplierImportService>();
            services.AddAuthorizedService<ISalesRepresentativeService, SalesRepresentativeService>();
            services.AddAuthorizedService<IPurchaseInvoiceService, PurchaseInvoiceService>();
            services.AddScoped<PurchaseInvoiceRepositories>();
            services.AddScoped<PurchaseInvoiceServices>();
            services.AddScoped<PurchaseInvoiceValidators>();
            services.AddAuthorizedService<IPurchaseReturnService, PurchaseReturnService>();
            services.AddScoped<PurchaseReturnRepositories>();
            services.AddScoped<PurchaseReturnServices>();
            services.AddScoped<PurchaseReturnValidators>();
            services.AddAuthorizedService<ISalesInvoiceService, SalesInvoiceService>();
            services.AddScoped<SalesInvoiceRepositories>();
            services.AddScoped<SalesInvoiceServices>();
            services.AddScoped<SalesInvoiceValidators>();
            services.AddAuthorizedService<ISalesReturnService, SalesReturnService>();
            services.AddScoped<SalesReturnRepositories>();
            services.AddScoped<SalesReturnServices>();
            services.AddScoped<SalesReturnValidators>();
            services.AddAuthorizedService<IPosService, PosService>();
            services.AddScoped<PosSalesRepositories>();
            services.AddScoped<PosInventoryRepositories>();
            services.AddScoped<PosAccountingRepositories>();
            services.AddScoped<PosRepositories>();
            services.AddScoped<PosServices>();
            services.AddScoped<PosValidators>();
            services.AddAuthorizedService<IPriceListService, PriceListService>();
            services.AddAuthorizedService<IInventoryAdjustmentService, InventoryAdjustmentService>();
            services.AddAuthorizedService<ISalesQuotationService, SalesQuotationService>();
            services.AddAuthorizedService<IPurchaseQuotationService, PurchaseQuotationService>();

            // ─── Treasury Services ───
            services.AddAuthorizedService<ICashboxService, CashboxService>();
            services.AddAuthorizedService<IBankAccountService, BankAccountService>();
            services.AddAuthorizedService<IBankReconciliationService, BankReconciliationService>();
            services.AddAuthorizedService<ICashReceiptService, CashReceiptService>();
            services.AddScoped<CashReceiptRepositories>();
            services.AddScoped<CashReceiptServices>();
            services.AddScoped<CashReceiptValidators>();
            services.AddAuthorizedService<ICashPaymentService, CashPaymentService>();
            services.AddScoped<CashPaymentRepositories>();
            services.AddScoped<CashPaymentServices>();
            services.AddScoped<CashPaymentValidators>();
            services.AddAuthorizedService<ICashTransferService, CashTransferService>();
            services.AddScoped<CashTransferRepositories>();
            services.AddScoped<CashTransferServices>();
            services.AddScoped<CashTransferValidators>();

            services.AddScoped<ITreasuryInvoicePaymentQueryService, TreasuryInvoicePaymentQueryService>();

            // ─── Security & Settings Services ───
            services.AddScoped<IAuthenticationService, AuthenticationService>();
            services.AddAuthorizedService<IUserService, UserService>();
            services.AddAuthorizedService<IRoleService, RoleService>();
            services.AddAuthorizedService<ISystemSettingsService, SystemSettingsService>();
            services.AddAuthorizedService<IDataPurgeService, DataPurgeService>();
            services.AddAuthorizedService<IFeatureService, FeatureService>();
            services.AddAuthorizedService<IProfileService, ProfileService>();
            services.AddScoped<IImpactAnalyzerService, ImpactAnalyzerService>();
            services.AddScoped<IVersionRepository, VersionRepository>();
            services.AddAuthorizedService<IVersionService, VersionService>();
            // Phase 8D: Module Dependency Inspector (reflection-based, report-only)
            services.AddSingleton<IModuleDependencyInspector>(sp =>
                new MarcoERP.Persistence.Services.Settings.ModuleDependencyInspector(
                    typeof(MarcoERP.Application.Common.ModuleAttribute).Assembly,
                    typeof(MarcoERP.Persistence.MarcoDbContext).Assembly));
            services.AddScoped<IIntegrityCheckService>(sp =>
                new MarcoERP.Persistence.Services.Settings.GovernanceIntegrityCheckService(
                    sp.GetRequiredService<MarcoDbContext>(),
                    () => CurrentAppVersion,
                    sp.GetRequiredService<IModuleDependencyInspector>()));
            services.AddAuthorizedService<IBackupService, BackupService>();
            services.AddScoped<IDatabaseBackupService, DatabaseBackupService>();
            services.AddAuthorizedService<IMigrationExecutionService, MigrationExecutionService>();
            services.AddScoped<IGovernanceAuditService, GovernanceAuditService>();
            services.AddAuthorizedService<IAuditLogService, AuditLogService>();
            services.AddAuthorizedService<IIntegrityService, IntegrityService>();
            services.AddAuthorizedService<IRecycleBinService, RecycleBinService>();

            // ─── Reports Service ───
            services.AddAuthorizedService<IReportService, ReportService>();
            services.AddAuthorizedService<IReportExportService, FastReportExportService>();

            // ─── Interactive Reporting Framework ───
            services.AddSingleton<IDrillDownResolver, DrillDownResolver>();
            services.AddSingleton<DrillDownEngine>();

            // ─── Product Import ───
            services.AddAuthorizedService<IProductImportService, MarcoERP.Application.Services.Inventory.ProductImportService>();

            // ─── Common Calculations ───
            services.AddSingleton<ILineCalculationService, LineCalculationService>();

            // ─── Smart Entry (read-only queries) ───
            services.AddScoped<ISmartEntryQueryService, SmartEntryQueryService>();

            // ─── Global Search (Ctrl+K) ───
            services.AddScoped<IGlobalSearchQueryService, GlobalSearchQueryService>();

            // ─── Navigation & Window Services ───
            services.AddSingleton<IWindowService, WindowService>();
            services.AddSingleton<IQuickTreasuryDialogService, QuickTreasuryDialogService>();
            services.AddScoped<IInvoiceTreasuryIntegrationService, InvoiceTreasuryIntegrationService>();
            services.AddSingleton<IInvoicePdfPreviewService, InvoicePdfPreviewService>();

            // ─── Print Center ───
            services.AddScoped<IPrintProfileProvider, PrintProfileProvider>();
            services.AddScoped<IDocumentHtmlBuilder, DocumentHtmlBuilder>();

            services.AddSingleton<IViewRegistry>(sp =>
            {
                var registry = new ViewRegistry();

                // Brush shortcuts for icon colors
                var acctBrush = new SolidColorBrush(Color.FromRgb(165, 214, 167));
                var invBrush = new SolidColorBrush(Color.FromRgb(255, 224, 130));
                var salesBrush = new SolidColorBrush(Color.FromRgb(239, 154, 154));
                var purchBrush = new SolidColorBrush(Color.FromRgb(206, 147, 216));
                var treasBrush = new SolidColorBrush(Color.FromRgb(128, 203, 196));
                var reportBrush = new SolidColorBrush(Color.FromRgb(176, 190, 197));
                var settBrush = new SolidColorBrush(Color.FromRgb(176, 190, 197));

                registry.Register<DashboardView, DashboardViewModel>("Dashboard", "لوحة التحكم", PackIconKind.ViewDashboard, new SolidColorBrush(Color.FromRgb(144, 202, 249)));

                // Accounting
                registry.Register<ChartOfAccountsView, ChartOfAccountsViewModel>("ChartOfAccounts", "شجرة الحسابات", PackIconKind.FileTree, acctBrush);
                registry.Register<JournalEntryView, JournalEntryViewModel>("JournalEntries", "القيود اليومية", PackIconKind.BookOpenPageVariant, acctBrush);
                registry.Register<FiscalPeriodView, FiscalPeriodViewModel>("FiscalPeriods", "الفترات المالية", PackIconKind.CalendarMonth, acctBrush);
                registry.Register<OpeningBalanceWizardView, OpeningBalanceWizardViewModel>("OpeningBalance", "الأرصدة الافتتاحية", PackIconKind.ScaleBalance, acctBrush);

                // Inventory
                registry.Register<CategoryView, CategoryViewModel>("Categories", "التصنيفات", PackIconKind.Shape, invBrush);
                registry.Register<UnitView, UnitViewModel>("Units", "وحدات القياس", PackIconKind.Ruler, invBrush);
                registry.Register<ProductView, ProductViewModel>("Products", "الأصناف", PackIconKind.PackageVariant, invBrush);
                registry.Register<WarehouseView, WarehouseViewModel>("Warehouses", "المخازن", PackIconKind.Warehouse, invBrush);
                registry.Register<BulkPriceUpdateView, BulkPriceUpdateViewModel>("BulkPriceUpdate", "تحديث الأسعار الجماعي", PackIconKind.TagMultiple, invBrush);
                registry.Register<InventoryAdjustmentListView, InventoryAdjustmentListViewModel>("InventoryAdjustments", "تسويات المخزون", PackIconKind.ClipboardCheck, invBrush);
                registry.Register<InventoryAdjustmentDetailView, InventoryAdjustmentDetailViewModel>("InventoryAdjustmentDetail", "تسوية مخزون", PackIconKind.ClipboardCheck, invBrush);
                registry.Register<ProductImportView, ProductImportViewModel>("ProductImport", "استيراد الأصناف", PackIconKind.FileImport, invBrush);

                // Sales
                registry.Register<SalesInvoiceListView, SalesInvoiceListViewModel>("SalesInvoices", "فواتير البيع", PackIconKind.ReceiptTextOutline, salesBrush);
                registry.Register<SalesInvoiceDetailView, SalesInvoiceDetailViewModel>("SalesInvoiceDetail", "فاتورة بيع", PackIconKind.ReceiptTextOutline, salesBrush);
                registry.Register<SalesReturnListView, SalesReturnListViewModel>("SalesReturns", "مرتجعات البيع", PackIconKind.ReceiptTextMinus, salesBrush);
                registry.Register<SalesReturnDetailView, SalesReturnDetailViewModel>("SalesReturnDetail", "مرتجع بيع", PackIconKind.ReceiptTextMinus, salesBrush);
                registry.Register<CustomerView, CustomerViewModel>("Customers", "العملاء", PackIconKind.AccountGroup, salesBrush);
                registry.Register<CustomerImportView, CustomerImportViewModel>("CustomerImport", "استيراد العملاء", PackIconKind.FileImport, salesBrush);
                registry.Register<SalesRepresentativeView, SalesRepresentativeViewModel>("SalesRepresentatives", "مندوبي المبيعات", PackIconKind.BadgeAccount, salesBrush);
                registry.Register<PriceListView, PriceListViewModel>("PriceLists", "قوائم الأسعار", PackIconKind.CurrencyUsd, salesBrush);
                registry.Register<SalesQuotationListView, SalesQuotationListViewModel>("SalesQuotations", "عروض أسعار البيع", PackIconKind.FileDocumentEdit, salesBrush);
                registry.Register<SalesQuotationDetailView, SalesQuotationDetailViewModel>("SalesQuotationDetail", "عرض سعر بيع", PackIconKind.FileDocumentEdit, salesBrush);

                // Purchases
                registry.Register<PurchaseInvoiceListView, PurchaseInvoiceListViewModel>("PurchaseInvoices", "فواتير الشراء", PackIconKind.CartOutline, purchBrush);
                registry.Register<PurchaseInvoiceDetailView, PurchaseInvoiceDetailViewModel>("PurchaseInvoiceDetail", "فاتورة شراء", PackIconKind.CartOutline, purchBrush);
                registry.Register<PurchaseReturnListView, PurchaseReturnListViewModel>("PurchaseReturns", "مرتجعات الشراء", PackIconKind.CartMinus, purchBrush);
                registry.Register<PurchaseReturnDetailView, PurchaseReturnDetailViewModel>("PurchaseReturnDetail", "مرتجع شراء", PackIconKind.CartMinus, purchBrush);
                registry.Register<SupplierView, SupplierViewModel>("Suppliers", "الموردين", PackIconKind.TruckDelivery, purchBrush);
                registry.Register<SupplierImportView, SupplierImportViewModel>("SupplierImport", "استيراد الموردين", PackIconKind.FileImport, purchBrush);
                registry.Register<PurchaseQuotationListView, PurchaseQuotationListViewModel>("PurchaseQuotations", "طلبات الشراء", PackIconKind.ClipboardTextSearch, purchBrush);
                registry.Register<PurchaseQuotationDetailView, PurchaseQuotationDetailViewModel>("PurchaseQuotationDetail", "طلب شراء", PackIconKind.ClipboardTextSearch, purchBrush);

                // Treasury
                registry.Register<CashboxView, CashboxViewModel>("Cashboxes", "الخزن", PackIconKind.Safe, treasBrush);
                registry.Register<BankAccountView, BankAccountViewModel>("BankAccounts", "الحسابات البنكية", PackIconKind.Bank, treasBrush);
                registry.Register<BankReconciliationView, BankReconciliationViewModel>("BankReconciliation", "التسوية البنكية", PackIconKind.ScaleBalance, treasBrush);
                registry.Register<CashReceiptView, CashReceiptViewModel>("CashReceipts", "سندات القبض", PackIconKind.CashPlus, treasBrush);
                registry.Register<CashPaymentView, CashPaymentViewModel>("CashPayments", "سندات الصرف", PackIconKind.CashMinus, treasBrush);
                registry.Register<CashTransferView, CashTransferViewModel>("CashTransfers", "التحويلات", PackIconKind.SwapHorizontal, treasBrush);

                // Reports
                registry.Register<ReportHubView, ReportHubViewModel>("Reports", "التقارير", PackIconKind.ChartBar, reportBrush);
                registry.Register<TrialBalanceView, TrialBalanceViewModel>("TrialBalance", "ميزان المراجعة", PackIconKind.ChartBar, reportBrush);
                registry.Register<AccountStatementView, AccountStatementViewModel>("AccountStatement", "كشف حساب", PackIconKind.ChartBar, reportBrush);
                registry.Register<IncomeStatementView, IncomeStatementViewModel>("IncomeStatement", "قائمة الدخل", PackIconKind.ChartBar, reportBrush);
                registry.Register<BalanceSheetView, BalanceSheetViewModel>("BalanceSheet", "الميزانية العمومية", PackIconKind.ChartBar, reportBrush);
                registry.Register<SalesReportView, SalesReportViewModel>("SalesReport", "تقرير المبيعات", PackIconKind.ChartBar, reportBrush);
                registry.Register<PurchaseReportView, PurchaseReportViewModel>("PurchaseReport", "تقرير المشتريات", PackIconKind.ChartBar, reportBrush);
                registry.Register<ProfitReportView, ProfitReportViewModel>("ProfitReport", "تقرير الأرباح", PackIconKind.ChartBar, reportBrush);
                registry.Register<VatReportView, VatReportViewModel>("VatReport", "تقرير الضريبة", PackIconKind.ChartBar, reportBrush);
                registry.Register<InventoryReportView, InventoryReportViewModel>("InventoryReport", "تقرير المخزون", PackIconKind.ChartBar, reportBrush);
                registry.Register<StockCardView, StockCardViewModel>("StockCard", "بطاقة الصنف", PackIconKind.ChartBar, reportBrush);
                registry.Register<CashboxMovementView, CashboxMovementViewModel>("CashboxMovement", "حركة الخزنة", PackIconKind.ChartBar, reportBrush);
                registry.Register<AgingReportView, AgingReportViewModel>("AgingReport", "أعمار الديون", PackIconKind.ChartBar, reportBrush);

                // Settings
                registry.Register<FiscalYearView, FiscalYearViewModel>("FiscalYear", "السنة المالية", PackIconKind.Calendar, settBrush);
                registry.Register<SystemSettingsView, SystemSettingsViewModel>("SystemSettings", "إعدادات النظام", PackIconKind.Cog, settBrush);
                registry.Register<UserManagementView, UserManagementViewModel>("UserManagement", "إدارة المستخدمين", PackIconKind.AccountMultiple, settBrush);
                registry.Register<RoleManagementView, RoleManagementViewModel>("RoleManagement", "إدارة الأدوار", PackIconKind.ShieldAccount, settBrush);
                registry.Register<AuditLogView, AuditLogViewModel>("AuditLog", "سجل المراجعة", PackIconKind.ClipboardTextClock, settBrush);
                registry.Register<BackupSettingsView, BackupSettingsViewModel>("BackupSettings", "النسخ الاحتياطي", PackIconKind.DatabaseExport, settBrush);
                registry.Register<IntegrityCheckView, IntegrityCheckViewModel>("IntegrityCheck", "فحص السلامة", PackIconKind.ShieldCheck, settBrush);
                registry.Register<GovernanceConsoleView, GovernanceConsoleViewModel>("GovernanceConsole", "وحدة التحكم", PackIconKind.ShieldOutline, settBrush);
                registry.Register<GovernanceIntegrityView, GovernanceIntegrityViewModel>("GovernanceIntegrity", "فحص سلامة الحوكمة", PackIconKind.ShieldCheck, settBrush);
                registry.Register<MigrationCenterView, MigrationCenterViewModel>("MigrationCenter", "مركز التحديثات", PackIconKind.Update, settBrush);
                registry.Register<PrintCenterView, PrintCenterViewModel>("PrintCenter", "مركز الطباعة", PackIconKind.Printer, settBrush);
                registry.Register<RecycleBinView, RecycleBinViewModel>("RecycleBin", "سلة المحذوفات", PackIconKind.DeleteRestore, settBrush);

                return registry;
            });
            services.AddSingleton<TabNavigationService>();
            services.AddSingleton<INavigationService>(sp => sp.GetRequiredService<TabNavigationService>());

            // ─── WPF Views & ViewModels ───
            services.AddSingleton<MainWindowViewModel>();
            services.AddTransient<MainWindow>();
            services.AddTransient<LoginViewModel>();
            services.AddTransient<LoginWindow>();

            // ── Onboarding Wizard ──
            services.AddTransient<Views.Setup.OnboardingWizardWindow>();
            services.AddTransient<ViewModels.Setup.OnboardingWizardViewModel>();

            services.AddTransient<DashboardView>();
            services.AddTransient<DashboardViewModel>();

            services.AddTransient<ChartOfAccountsView>();
            services.AddTransient<ChartOfAccountsViewModel>();
            services.AddTransient<JournalEntryView>();
            services.AddTransient<JournalEntryViewModel>();
            services.AddTransient<FiscalPeriodView>();
            services.AddTransient<FiscalPeriodViewModel>();
            services.AddTransient<OpeningBalanceWizardView>();
            services.AddTransient<OpeningBalanceWizardViewModel>();

            services.AddTransient<CategoryView>();
            services.AddTransient<CategoryViewModel>();
            services.AddTransient<UnitView>();
            services.AddTransient<UnitViewModel>();
            services.AddTransient<ProductView>();
            services.AddTransient<ProductViewModel>();
            services.AddTransient<ProductImportView>();
            services.AddTransient<ProductImportViewModel>();
            services.AddTransient<WarehouseView>();
            services.AddTransient<WarehouseViewModel>();
            services.AddTransient<BulkPriceUpdateView>();
            services.AddTransient<BulkPriceUpdateViewModel>();
            services.AddTransient<InventoryAdjustmentListView>();
            services.AddTransient<InventoryAdjustmentListViewModel>();
            services.AddTransient<InventoryAdjustmentDetailView>();
            services.AddTransient<InventoryAdjustmentDetailViewModel>();

            services.AddTransient<SalesInvoiceView>();
            services.AddTransient<SalesInvoiceViewModel>();
            services.AddTransient<SalesInvoiceListView>();
            services.AddTransient<SalesInvoiceListViewModel>();
            services.AddTransient<SalesInvoiceDetailView>();
            services.AddTransient<SalesInvoiceDetailViewModel>();
            services.AddTransient<SalesReturnView>();
            services.AddTransient<SalesReturnViewModel>();
            services.AddTransient<SalesReturnListView>();
            services.AddTransient<SalesReturnListViewModel>();
            services.AddTransient<SalesReturnDetailView>();
            services.AddTransient<SalesReturnDetailViewModel>();
            services.AddTransient<CustomerView>();
            services.AddTransient<CustomerViewModel>();
            services.AddTransient<CustomerImportView>();
            services.AddTransient<CustomerImportViewModel>();
            services.AddTransient<SalesRepresentativeView>();
            services.AddTransient<SalesRepresentativeViewModel>();
            services.AddTransient<PriceListView>();
            services.AddTransient<PriceListViewModel>();
            services.AddTransient<PosWindow>();
            services.AddTransient<PosViewModel>();
            services.AddTransient<SalesQuotationListView>();
            services.AddTransient<SalesQuotationListViewModel>();
            services.AddTransient<SalesQuotationDetailView>();
            services.AddTransient<SalesQuotationDetailViewModel>();

            services.AddTransient<PurchaseInvoiceView>();
            services.AddTransient<PurchaseInvoiceViewModel>();
            services.AddTransient<PurchaseInvoiceListView>();
            services.AddTransient<PurchaseInvoiceListViewModel>();
            services.AddTransient<PurchaseInvoiceDetailView>();
            services.AddTransient<PurchaseInvoiceDetailViewModel>();
            services.AddTransient<PurchaseReturnView>();
            services.AddTransient<PurchaseReturnViewModel>();
            services.AddTransient<PurchaseReturnListView>();
            services.AddTransient<PurchaseReturnListViewModel>();
            services.AddTransient<PurchaseReturnDetailView>();
            services.AddTransient<PurchaseReturnDetailViewModel>();
            services.AddTransient<SupplierView>();
            services.AddTransient<SupplierViewModel>();
            services.AddTransient<SupplierImportView>();
            services.AddTransient<SupplierImportViewModel>();
            services.AddTransient<PurchaseQuotationListView>();
            services.AddTransient<PurchaseQuotationListViewModel>();
            services.AddTransient<PurchaseQuotationDetailView>();
            services.AddTransient<PurchaseQuotationDetailViewModel>();

            services.AddTransient<CashboxView>();
            services.AddTransient<CashboxViewModel>();
            services.AddTransient<BankAccountView>();
            services.AddTransient<BankAccountViewModel>();
            services.AddTransient<BankReconciliationView>();
            services.AddTransient<BankReconciliationViewModel>();
            services.AddTransient<CashReceiptView>();
            services.AddTransient<CashReceiptViewModel>();
            services.AddTransient<CashPaymentView>();
            services.AddTransient<CashPaymentViewModel>();
            services.AddTransient<CashTransferView>();
            services.AddTransient<CashTransferViewModel>();

            services.AddTransient<ReportHubView>();
            services.AddTransient<ReportHubViewModel>();
            services.AddTransient<TrialBalanceView>();
            services.AddTransient<TrialBalanceViewModel>();
            services.AddTransient<AccountStatementView>();
            services.AddTransient<AccountStatementViewModel>();
            services.AddTransient<IncomeStatementView>();
            services.AddTransient<IncomeStatementViewModel>();
            services.AddTransient<BalanceSheetView>();
            services.AddTransient<BalanceSheetViewModel>();
            services.AddTransient<SalesReportView>();
            services.AddTransient<SalesReportViewModel>();
            services.AddTransient<PurchaseReportView>();
            services.AddTransient<PurchaseReportViewModel>();
            services.AddTransient<ProfitReportView>();
            services.AddTransient<ProfitReportViewModel>();
            services.AddTransient<VatReportView>();
            services.AddTransient<VatReportViewModel>();
            services.AddTransient<InventoryReportView>();
            services.AddTransient<InventoryReportViewModel>();
            services.AddTransient<StockCardView>();
            services.AddTransient<StockCardViewModel>();
            services.AddTransient<CashboxMovementView>();
            services.AddTransient<CashboxMovementViewModel>();
            services.AddTransient<AgingReportView>();
            services.AddTransient<AgingReportViewModel>();

            services.AddTransient<FiscalYearView>();
            services.AddTransient<FiscalYearViewModel>();
            services.AddTransient<SystemSettingsView>();
            services.AddTransient<SystemSettingsViewModel>();
            services.AddTransient<GovernanceConsoleView>();
            services.AddTransient<GovernanceConsoleViewModel>();
            services.AddTransient<GovernanceIntegrityView>();
            services.AddTransient<GovernanceIntegrityViewModel>();
            services.AddTransient<MigrationCenterView>();
            services.AddTransient<MigrationCenterViewModel>();
            services.AddTransient<UserManagementView>();
            services.AddTransient<UserManagementViewModel>();
            services.AddTransient<RoleManagementView>();
            services.AddTransient<RoleManagementViewModel>();
            services.AddTransient<AuditLogView>();
            services.AddTransient<AuditLogViewModel>();
            services.AddTransient<BackupSettingsView>();
            services.AddTransient<BackupSettingsViewModel>();
            services.AddTransient<IntegrityCheckView>();
            services.AddTransient<IntegrityCheckViewModel>();
            services.AddTransient<PrintCenterView>();
            services.AddTransient<PrintCenterViewModel>();
            services.AddTransient<RecycleBinView>();
            services.AddTransient<RecycleBinViewModel>();

            services.AddTransient<QuickCashReceiptViewModel>();
            services.AddTransient<QuickCashReceiptWindow>();
            services.AddTransient<QuickCashPaymentViewModel>();
            services.AddTransient<QuickCashPaymentWindow>();

            // ─── Common Dialogs ───
            services.AddTransient<QuickTreasuryDialogViewModel>();
            services.AddTransient<QuickTreasuryDialog>();
            services.AddTransient<InvoicePdfPreviewDialogViewModel>();
            services.AddTransient<InvoicePdfPreviewDialog>();
        }

        /// <summary>
        /// Resolves a service from the DI container.
        /// </summary>
        public T GetService<T>() where T : class
        {
            return _serviceProvider.GetService<T>();
        }

        /// <summary>
        /// Resolves a required service from the DI container.
        /// </summary>
        public T GetRequiredService<T>() where T : class
        {
            return _serviceProvider.GetRequiredService<T>();
        }
    }
}