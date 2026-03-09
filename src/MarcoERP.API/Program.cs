using MarcoERP.Application.Interfaces.Sync;
using MarcoERP.Persistence.Services.Sync;
using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using FluentValidation;
using MarcoERP.API.Middleware;
using MarcoERP.API.Services;
using MarcoERP.Application.Common;
using MarcoERP.Application.DTOs.Accounting;
using MarcoERP.Application.DTOs.Inventory;
using MarcoERP.Application.DTOs.Purchases;
using MarcoERP.Application.DTOs.Sales;
using MarcoERP.Application.DTOs.Security;
using MarcoERP.Application.DTOs.Settings;
using MarcoERP.Application.DTOs.Treasury;
using MarcoERP.Application.Interfaces;
using MarcoERP.Application.Interfaces.Accounting;
using MarcoERP.Application.Interfaces.Inventory;
using MarcoERP.Application.Interfaces.Purchases;
using MarcoERP.Application.Interfaces.Sales;
using MarcoERP.Application.Interfaces.Security;
using MarcoERP.Application.Interfaces.Settings;
using MarcoERP.Application.Interfaces.Treasury;
using MarcoERP.Application.Services.Accounting;
using MarcoERP.Application.Services.Common;
using MarcoERP.Application.Services.Inventory;
using MarcoERP.Application.Services.Purchases;
using MarcoERP.Application.Services.Sales;
using MarcoERP.Application.Services.Security;
using MarcoERP.Application.Services.Settings;
using MarcoERP.Application.Services.Treasury;
using MarcoERP.Application.Validators.Accounting;
using MarcoERP.Application.Validators.Inventory;
using MarcoERP.Application.Validators.Purchases;
using MarcoERP.Application.Validators.Sales;
using MarcoERP.Application.Validators.Security;
using MarcoERP.Application.Validators.Settings;
using MarcoERP.Application.Validators.Treasury;
using MarcoERP.Domain.Interfaces;
using MarcoERP.Domain.Interfaces.Inventory;
using MarcoERP.Domain.Interfaces.Purchases;
using MarcoERP.Domain.Interfaces.Sales;
using MarcoERP.Domain.Interfaces.Security;
using MarcoERP.Domain.Interfaces.Settings;
using MarcoERP.Domain.Interfaces.Treasury;
using MarcoERP.Domain.Entities.Accounting.Policies;
using MarcoERP.Infrastructure.Security;
using MarcoERP.Infrastructure.Services;
using MarcoERP.Persistence;
using MarcoERP.Persistence.Interceptors;
using MarcoERP.Persistence.Repositories;
using MarcoERP.Persistence.Repositories.Inventory;
using MarcoERP.Persistence.Repositories.Purchases;
using MarcoERP.Persistence.Repositories.Sales;
using MarcoERP.Persistence.Repositories.Security;
using MarcoERP.Persistence.Repositories.Settings;
using MarcoERP.Persistence.Repositories.Treasury;
using MarcoERP.Persistence.Services;
using MarcoERP.Persistence.Services.Reports;
using MarcoERP.Application.Interfaces.Reports;
using MarcoERP.Application.Services.Printing;
using MarcoERP.Application.Interfaces.Printing;
using MarcoERP.Application.Services.Reports;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var renderPort = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrWhiteSpace(renderPort)
    && string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ASPNETCORE_URLS")))
{
    Environment.SetEnvironmentVariable("ASPNETCORE_URLS", $"http://0.0.0.0:{renderPort}");
}

var builder = WebApplication.CreateBuilder(args);
var configuration = builder.Configuration;

// ═══════════════════════════════════════════════════════
// JWT Configuration
// ═══════════════════════════════════════════════════════
builder.Services.Configure<JwtSettings>(configuration.GetSection("Jwt"));
builder.Services.AddSingleton<JwtTokenService>();
builder.Services.AddScoped<RefreshTokenStore>();

var jwtSettings = configuration.GetSection("Jwt").Get<JwtSettings>()!;

// SECURITY: Prefer JWT secret from environment variable; fall back to config for dev
var jwtSecretKey = Environment.GetEnvironmentVariable("MARCOERP_JWT_SECRET")
    ?? jwtSettings.SecretKey;
if (string.IsNullOrWhiteSpace(jwtSecretKey) || jwtSecretKey.Length < 32)
    throw new InvalidOperationException(
        "JWT SecretKey must be at least 32 characters. Set MARCOERP_JWT_SECRET environment variable for production.");

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtSettings.Issuer,
        ValidAudience = jwtSettings.Audience,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecretKey)),
        ClockSkew = TimeSpan.FromMinutes(1)
    };
});
builder.Services.AddAuthorization();

// ═══════════════════════════════════════════════════════
// Swagger
// ═══════════════════════════════════════════════════════
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "MarcoERP API",
        Version = "v1",
        Description = "MarcoERP Mobile API - REST endpoints for the Flutter mobile app"
    });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter your JWT token"
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });
});

// ═══════════════════════════════════════════════════════
// CORS (restrict to known origins)
// ═══════════════════════════════════════════════════════
var allowedOrigins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? new[] { "http://localhost:3000", "https://localhost:3000" };
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins(allowedOrigins)
              .WithMethods("GET", "POST", "PUT", "DELETE", "OPTIONS")
              .WithHeaders("Authorization", "Content-Type", "Accept", "X-Requested-With", "X-Idempotency-Key")
              .AllowCredentials();
    });
});

// ═══════════════════════════════════════════════════════
// Controllers
// ═══════════════════════════════════════════════════════
builder.Services.AddControllers(options =>
    {
        // Auto-validate request DTOs via FluentValidation
        options.Filters.Add<MarcoERP.API.Middleware.FluentValidationFilter>();
    })
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
    });

// ═══════════════════════════════════════════════════════
// Rate Limiting (brute-force protection for auth endpoints)
// ═══════════════════════════════════════════════════════
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = 429;
    options.AddFixedWindowLimiter("auth", limiterOptions =>
    {
        limiterOptions.PermitLimit = 10;
        limiterOptions.Window = TimeSpan.FromMinutes(1);
        limiterOptions.QueueLimit = 0;
    });
    // Global rate limiter for data endpoints — prevents abuse
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 120,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));
});

builder.Services.AddHttpContextAccessor();
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

// ═══════════════════════════════════════════════════════
// Persistence Layer
// ═══════════════════════════════════════════════════════
var dbProvider = Environment.GetEnvironmentVariable("DB_PROVIDER")
    ?? configuration.GetValue<string>("Database:Provider")
    ?? "SqlServer";
var connectionStringName = dbProvider.Equals("PostgreSQL", StringComparison.OrdinalIgnoreCase)
    ? "PostgreSQLConnection"
    : "DefaultConnection";
var connectionString = Environment.GetEnvironmentVariable("DB_CONNECTION")
    ?? configuration.GetConnectionString(connectionStringName);

builder.Services.AddScoped<AuditSaveChangesInterceptor>();
builder.Services.AddSingleton<HardDeleteProtectionInterceptor>();
builder.Services.AddSingleton<SyncVersionInterceptor>();

builder.Services.AddDbContext<MarcoDbContext>((serviceProvider, options) =>
{
    if (dbProvider.Equals("PostgreSQL", StringComparison.OrdinalIgnoreCase))
    {
        options.UseNpgsql(connectionString, npgsqlOptions =>
        {
            npgsqlOptions.MigrationsAssembly(typeof(MarcoDbContext).Assembly.FullName);
        });
    }
    else
    {
        options.UseSqlServer(connectionString, sqlOptions =>
        {
            sqlOptions.MigrationsAssembly(typeof(MarcoDbContext).Assembly.FullName);
        });
    }

    var auditInterceptor = serviceProvider.GetRequiredService<AuditSaveChangesInterceptor>();
    var hardDeleteGuard = serviceProvider.GetRequiredService<HardDeleteProtectionInterceptor>();
    var syncVersionInterceptor = serviceProvider.GetRequiredService<SyncVersionInterceptor>();
    options.AddInterceptors(auditInterceptor, hardDeleteGuard, syncVersionInterceptor);
});

// ═══════════════════════════════════════════════════════
// Domain Repositories
// ═══════════════════════════════════════════════════════
builder.Services.AddScoped<IAccountRepository, AccountRepository>();
builder.Services.AddScoped<IJournalEntryRepository, JournalEntryRepository>();
builder.Services.AddScoped<IFiscalYearRepository, FiscalYearRepository>();
builder.Services.AddScoped<IAuditLogRepository, AuditLogRepository>();
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();

// Inventory Repositories
builder.Services.AddScoped<ICategoryRepository, CategoryRepository>();
builder.Services.AddScoped<IUnitRepository, UnitRepository>();
builder.Services.AddScoped<IProductRepository, ProductRepository>();
builder.Services.AddScoped<IWarehouseRepository, WarehouseRepository>();
builder.Services.AddScoped<IWarehouseProductRepository, WarehouseProductRepository>();
builder.Services.AddScoped<IInventoryMovementRepository, InventoryMovementRepository>();

// Sales & Purchases Repositories
builder.Services.AddScoped<ICustomerRepository, CustomerRepository>();
builder.Services.AddScoped<ISupplierRepository, SupplierRepository>();
builder.Services.AddScoped<ISalesRepresentativeRepository, SalesRepresentativeRepository>();
builder.Services.AddScoped<IPurchaseInvoiceRepository, PurchaseInvoiceRepository>();
builder.Services.AddScoped<IPurchaseReturnRepository, PurchaseReturnRepository>();
builder.Services.AddScoped<ISalesInvoiceRepository, SalesInvoiceRepository>();
builder.Services.AddScoped<ISalesReturnRepository, SalesReturnRepository>();
builder.Services.AddScoped<IPosSessionRepository, PosSessionRepository>();
builder.Services.AddScoped<IPosPaymentRepository, PosPaymentRepository>();
builder.Services.AddScoped<IPriceListRepository, PriceListRepository>();
builder.Services.AddScoped<IInventoryAdjustmentRepository, InventoryAdjustmentRepository>();
builder.Services.AddScoped<ISalesQuotationRepository, SalesQuotationRepository>();
builder.Services.AddScoped<IPurchaseQuotationRepository, PurchaseQuotationRepository>();
builder.Services.AddScoped<IOpeningBalanceRepository, OpeningBalanceRepository>();

// Treasury Repositories
builder.Services.AddScoped<ICashboxRepository, CashboxRepository>();
builder.Services.AddScoped<IBankAccountRepository, BankAccountRepository>();
builder.Services.AddScoped<IBankReconciliationRepository, BankReconciliationRepository>();
builder.Services.AddScoped<ICashReceiptRepository, CashReceiptRepository>();
builder.Services.AddScoped<ICashPaymentRepository, CashPaymentRepository>();
builder.Services.AddScoped<ICashTransferRepository, CashTransferRepository>();

// Security & Settings Repositories
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IRoleRepository, RoleRepository>();
builder.Services.AddScoped<ISystemSettingRepository, SystemSettingRepository>();
builder.Services.AddScoped<IFeatureRepository, FeatureRepository>();
builder.Services.AddScoped<IProfileRepository, ProfileRepository>();
builder.Services.AddScoped<IVersionRepository, VersionRepository>();

// ═══════════════════════════════════════════════════════
// Infrastructure Layer
// ═══════════════════════════════════════════════════════
builder.Services.AddScoped<ICurrentUserService, ApiCurrentUserService>();
builder.Services.AddSingleton<IDateTimeProvider, DateTimeProvider>();
builder.Services.AddSingleton<IPasswordHasher, PasswordHasher>();
builder.Services.AddSingleton<ICompanyContext, DefaultCompanyContext>();
builder.Services.AddSingleton<ILineCalculationService, LineCalculationService>();
builder.Services.AddScoped<IAuditLogger, AuditLogger>();
builder.Services.AddScoped<IJournalNumberGenerator, JournalNumberGenerator>();
builder.Services.AddScoped<JournalEntryFactory>();
builder.Services.AddScoped<StockManager>();
builder.Services.AddScoped<FiscalPeriodValidator>();
builder.Services.AddScoped<ICodeGenerator, CodeGenerator>();

// No-op implementations for WPF-specific services not needed in API
builder.Services.AddSingleton<IAlertService, ApiNoOpAlertService>();

// ═══════════════════════════════════════════════════════
// FluentValidation Validators
// ═══════════════════════════════════════════════════════
// Accounting
builder.Services.AddScoped<IValidator<CreateAccountDto>, CreateAccountDtoValidator>();
builder.Services.AddScoped<IValidator<UpdateAccountDto>, UpdateAccountDtoValidator>();
builder.Services.AddScoped<IValidator<CreateFiscalYearDto>, CreateFiscalYearDtoValidator>();
builder.Services.AddScoped<IValidator<CreateJournalEntryDto>, CreateJournalEntryDtoValidator>();
builder.Services.AddScoped<IValidator<ReverseJournalEntryDto>, ReverseJournalEntryDtoValidator>();

// Inventory
builder.Services.AddScoped<IValidator<CreateCategoryDto>, CreateCategoryDtoValidator>();
builder.Services.AddScoped<IValidator<UpdateCategoryDto>, UpdateCategoryDtoValidator>();
builder.Services.AddScoped<IValidator<CreateUnitDto>, CreateUnitDtoValidator>();
builder.Services.AddScoped<IValidator<UpdateUnitDto>, UpdateUnitDtoValidator>();
builder.Services.AddScoped<IValidator<CreateProductDto>, CreateProductDtoValidator>();
builder.Services.AddScoped<IValidator<UpdateProductDto>, UpdateProductDtoValidator>();
builder.Services.AddScoped<IValidator<CreateWarehouseDto>, CreateWarehouseDtoValidator>();
builder.Services.AddScoped<IValidator<UpdateWarehouseDto>, UpdateWarehouseDtoValidator>();
builder.Services.AddScoped<IValidator<CreateInventoryAdjustmentDto>, CreateInventoryAdjustmentDtoValidator>();
builder.Services.AddScoped<IValidator<UpdateInventoryAdjustmentDto>, UpdateInventoryAdjustmentDtoValidator>();
builder.Services.AddScoped<IValidator<BulkPriceUpdateRequestDto>, BulkPriceUpdateRequestDtoValidator>();

// Sales & Purchases
builder.Services.AddScoped<IValidator<CreateCustomerDto>, CreateCustomerDtoValidator>();
builder.Services.AddScoped<IValidator<UpdateCustomerDto>, UpdateCustomerDtoValidator>();
builder.Services.AddScoped<IValidator<CreateSupplierDto>, CreateSupplierDtoValidator>();
builder.Services.AddScoped<IValidator<UpdateSupplierDto>, UpdateSupplierDtoValidator>();
builder.Services.AddScoped<IValidator<CreatePurchaseInvoiceDto>, CreatePurchaseInvoiceDtoValidator>();
builder.Services.AddScoped<IValidator<UpdatePurchaseInvoiceDto>, UpdatePurchaseInvoiceDtoValidator>();
builder.Services.AddScoped<IValidator<CreatePurchaseReturnDto>, CreatePurchaseReturnDtoValidator>();
builder.Services.AddScoped<IValidator<UpdatePurchaseReturnDto>, UpdatePurchaseReturnDtoValidator>();
builder.Services.AddScoped<IValidator<CreateSalesInvoiceDto>, CreateSalesInvoiceDtoValidator>();
builder.Services.AddScoped<IValidator<UpdateSalesInvoiceDto>, UpdateSalesInvoiceDtoValidator>();
builder.Services.AddScoped<IValidator<CreateSalesReturnDto>, CreateSalesReturnDtoValidator>();
builder.Services.AddScoped<IValidator<UpdateSalesReturnDto>, UpdateSalesReturnDtoValidator>();
builder.Services.AddScoped<IValidator<CreateSalesRepresentativeDto>, CreateSalesRepresentativeDtoValidator>();
builder.Services.AddScoped<IValidator<UpdateSalesRepresentativeDto>, UpdateSalesRepresentativeDtoValidator>();
builder.Services.AddScoped<IValidator<CreateSalesQuotationDto>, CreateSalesQuotationDtoValidator>();
builder.Services.AddScoped<IValidator<UpdateSalesQuotationDto>, UpdateSalesQuotationDtoValidator>();
builder.Services.AddScoped<IValidator<CreatePriceListDto>, CreatePriceListDtoValidator>();
builder.Services.AddScoped<IValidator<UpdatePriceListDto>, UpdatePriceListDtoValidator>();
builder.Services.AddScoped<IValidator<CreatePurchaseQuotationDto>, CreatePurchaseQuotationDtoValidator>();
builder.Services.AddScoped<IValidator<UpdatePurchaseQuotationDto>, UpdatePurchaseQuotationDtoValidator>();

// POS
builder.Services.AddScoped<IValidator<OpenPosSessionDto>, OpenPosSessionDtoValidator>();
builder.Services.AddScoped<IValidator<ClosePosSessionDto>, ClosePosSessionDtoValidator>();
builder.Services.AddScoped<IValidator<CompletePoseSaleDto>, CompletePosSaleDtoValidator>();

// Treasury
builder.Services.AddScoped<IValidator<CreateCashboxDto>, CreateCashboxDtoValidator>();
builder.Services.AddScoped<IValidator<UpdateCashboxDto>, UpdateCashboxDtoValidator>();
builder.Services.AddScoped<IValidator<CreateBankAccountDto>, CreateBankAccountDtoValidator>();
builder.Services.AddScoped<IValidator<UpdateBankAccountDto>, UpdateBankAccountDtoValidator>();
builder.Services.AddScoped<IValidator<CreateBankReconciliationDto>, CreateBankReconciliationDtoValidator>();
builder.Services.AddScoped<IValidator<CreateBankReconciliationItemDto>, CreateBankReconciliationItemDtoValidator>();
builder.Services.AddScoped<IValidator<UpdateBankReconciliationDto>, UpdateBankReconciliationDtoValidator>();
builder.Services.AddScoped<IValidator<CreateCashReceiptDto>, CreateCashReceiptDtoValidator>();
builder.Services.AddScoped<IValidator<UpdateCashReceiptDto>, UpdateCashReceiptDtoValidator>();
builder.Services.AddScoped<IValidator<CreateCashPaymentDto>, CreateCashPaymentDtoValidator>();
builder.Services.AddScoped<IValidator<UpdateCashPaymentDto>, UpdateCashPaymentDtoValidator>();
builder.Services.AddScoped<IValidator<CreateCashTransferDto>, CreateCashTransferDtoValidator>();
builder.Services.AddScoped<IValidator<UpdateCashTransferDto>, UpdateCashTransferDtoValidator>();

// Security & Settings
builder.Services.AddScoped<IValidator<CreateRoleDto>, CreateRoleDtoValidator>();
builder.Services.AddScoped<IValidator<UpdateRoleDto>, UpdateRoleDtoValidator>();
builder.Services.AddScoped<IValidator<CreateUserDto>, CreateUserDtoValidator>();
builder.Services.AddScoped<IValidator<UpdateUserDto>, UpdateUserDtoValidator>();
builder.Services.AddScoped<IValidator<ResetPasswordDto>, ResetPasswordDtoValidator>();
builder.Services.AddScoped<IValidator<ChangePasswordDto>, ChangePasswordDtoValidator>();
builder.Services.AddScoped<IValidator<LoginDto>, LoginDtoValidator>();
builder.Services.AddScoped<IValidator<UpdateSystemSettingDto>, UpdateSystemSettingDtoValidator>();
builder.Services.AddScoped<IValidator<ToggleFeatureDto>, ToggleFeatureDtoValidator>();

// Opening Balance
builder.Services.AddScoped<IValidator<CreateOpeningBalanceDto>, CreateOpeningBalanceDtoValidator>();
builder.Services.AddScoped<IValidator<UpdateOpeningBalanceDto>, UpdateOpeningBalanceDtoValidator>();

// ═══════════════════════════════════════════════════════
// Application Services (wrapped with AuthorizationProxy)
// ═══════════════════════════════════════════════════════
// Accounting
builder.Services.AddAuthorizedService<IAccountService, AccountService>();
builder.Services.AddAuthorizedService<IJournalEntryService, JournalEntryService>();
builder.Services.AddAuthorizedService<IYearEndClosingService, YearEndClosingService>();
builder.Services.AddAuthorizedService<IFiscalYearService, FiscalYearService>();
builder.Services.AddAuthorizedService<IOpeningBalanceService, OpeningBalanceService>();

// Inventory
builder.Services.AddAuthorizedService<ICategoryService, CategoryService>();
builder.Services.AddAuthorizedService<IUnitService, UnitService>();
builder.Services.AddAuthorizedService<IProductService, ProductService>();
builder.Services.AddAuthorizedService<IWarehouseService, WarehouseService>();
builder.Services.AddAuthorizedService<IBulkPriceUpdateService, BulkPriceUpdateService>();

// Sales & Purchases
builder.Services.AddAuthorizedService<ICustomerService, CustomerService>();
builder.Services.AddAuthorizedService<ISupplierService, SupplierService>();
builder.Services.AddAuthorizedService<ISalesRepresentativeService, SalesRepresentativeService>();
builder.Services.AddAuthorizedService<IPurchaseInvoiceService, PurchaseInvoiceService>();
builder.Services.AddScoped<PurchaseInvoiceRepositories>();
builder.Services.AddScoped<PurchaseInvoiceServices>();
builder.Services.AddScoped<PurchaseInvoiceValidators>();
builder.Services.AddAuthorizedService<IPurchaseReturnService, PurchaseReturnService>();
builder.Services.AddScoped<PurchaseReturnRepositories>();
builder.Services.AddScoped<PurchaseReturnServices>();
builder.Services.AddScoped<PurchaseReturnValidators>();
builder.Services.AddAuthorizedService<ISalesInvoiceService, SalesInvoiceService>();
builder.Services.AddScoped<SalesInvoiceRepositories>();
builder.Services.AddScoped<SalesInvoiceServices>();
builder.Services.AddScoped<SalesInvoiceValidators>();
builder.Services.AddAuthorizedService<ISalesReturnService, SalesReturnService>();
builder.Services.AddScoped<SalesReturnRepositories>();
builder.Services.AddScoped<SalesReturnServices>();
builder.Services.AddScoped<SalesReturnValidators>();
builder.Services.AddAuthorizedService<IPosService, PosService>();
builder.Services.AddScoped<PosSalesRepositories>();
builder.Services.AddScoped<PosInventoryRepositories>();
builder.Services.AddScoped<PosAccountingRepositories>();
builder.Services.AddScoped<PosRepositories>();
builder.Services.AddScoped<PosServices>();
builder.Services.AddScoped<PosValidators>();
builder.Services.AddAuthorizedService<IPriceListService, PriceListService>();
builder.Services.AddAuthorizedService<IInventoryAdjustmentService, InventoryAdjustmentService>();
builder.Services.AddAuthorizedService<ISalesQuotationService, SalesQuotationService>();
builder.Services.AddAuthorizedService<IPurchaseQuotationService, PurchaseQuotationService>();

// Treasury
builder.Services.AddAuthorizedService<ICashboxService, CashboxService>();
builder.Services.AddAuthorizedService<IBankAccountService, BankAccountService>();
builder.Services.AddAuthorizedService<IBankReconciliationService, BankReconciliationService>();
builder.Services.AddAuthorizedService<ICashReceiptService, CashReceiptService>();
builder.Services.AddScoped<CashReceiptRepositories>();
builder.Services.AddScoped<CashReceiptServices>();
builder.Services.AddScoped<CashReceiptValidators>();
builder.Services.AddAuthorizedService<ICashPaymentService, CashPaymentService>();
builder.Services.AddScoped<CashPaymentRepositories>();
builder.Services.AddScoped<CashPaymentServices>();
builder.Services.AddScoped<CashPaymentValidators>();
builder.Services.AddAuthorizedService<ICashTransferService, CashTransferService>();
builder.Services.AddScoped<CashTransferRepositories>();
builder.Services.AddScoped<CashTransferServices>();
builder.Services.AddScoped<CashTransferValidators>();
builder.Services.AddScoped<ITreasuryInvoicePaymentQueryService, MarcoERP.Persistence.Services.TreasuryInvoicePaymentQueryService>();
builder.Services.AddScoped<MarcoERP.Application.Interfaces.SmartEntry.ISmartEntryQueryService, MarcoERP.Persistence.Services.SmartEntry.SmartEntryQueryService>();

// Security & Settings
builder.Services.AddScoped<IAuthenticationService, AuthenticationService>();
builder.Services.AddAuthorizedService<IUserService, UserService>();
builder.Services.AddAuthorizedService<IRoleService, RoleService>();
builder.Services.AddAuthorizedService<ISystemSettingsService, SystemSettingsService>();
builder.Services.AddAuthorizedService<IDataPurgeService, MarcoERP.Persistence.Services.Settings.DataPurgeService>();
builder.Services.AddAuthorizedService<IFeatureService, FeatureService>();
builder.Services.AddAuthorizedService<IProfileService, ProfileService>();
builder.Services.AddScoped<IImpactAnalyzerService, ImpactAnalyzerService>();
builder.Services.AddAuthorizedService<IVersionService, VersionService>();
builder.Services.AddAuthorizedService<IBackupService, BackupService>();
builder.Services.AddScoped<IDatabaseBackupService, MarcoERP.Persistence.Services.Settings.DatabaseBackupService>();
builder.Services.AddAuthorizedService<IMigrationExecutionService, MarcoERP.Persistence.Services.Settings.MigrationExecutionService>();
builder.Services.AddAuthorizedService<IAuditLogService, AuditLogService>();
builder.Services.AddAuthorizedService<IIntegrityService, IntegrityService>();
builder.Services.AddAuthorizedService<IRecycleBinService, MarcoERP.Persistence.Services.Settings.RecycleBinService>();

// Reports
builder.Services.AddAuthorizedService<IReportService, ReportService>();
builder.Services.AddAuthorizedService<IReportExportService, ReportExportService>();

// Sync Service (wrapped with AuthorizationProxy like all other services)
builder.Services.AddAuthorizedService<ISyncService, SyncService>();

// Product Import
builder.Services.AddAuthorizedService<IProductImportService, MarcoERP.Application.Services.Inventory.ProductImportService>();

// Printing (API doesn't need physical printing but some services may depend on it)
builder.Services.AddScoped<IPrintProfileProvider, PrintProfileProvider>();
builder.Services.AddScoped<IDocumentHtmlBuilder, DocumentHtmlBuilder>();
builder.Services.AddSingleton<IReceiptPrinterService, ApiNoOpReceiptPrinterService>();

// Response compression (gzip) — critical for sync payloads over mobile networks
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    options.MimeTypes = Microsoft.AspNetCore.ResponseCompression.ResponseCompressionDefaults
        .MimeTypes.Concat(new[] { "application/json" });
});
builder.Services.Configure<Microsoft.AspNetCore.ResponseCompression.GzipCompressionProviderOptions>(
    options => options.Level = System.IO.Compression.CompressionLevel.Fastest);

// ═══════════════════════════════════════════════════════
// Build & Configure Pipeline
// ═══════════════════════════════════════════════════════
var app = builder.Build();

app.UseForwardedHeaders();

// Response compression — must be first to wrap all responses
app.UseResponseCompression();

// Global exception handling
app.UseMiddleware<ExceptionHandlingMiddleware>();

// Idempotency guard for sync push endpoint
app.UseMiddleware<IdempotencyMiddleware>();

// Swagger — Development only (security: hide API surface in production)
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "MarcoERP API v1");
        c.RoutePrefix = string.Empty; // Swagger at root
    });
}

// HTTPS redirection — enabled outside Development
if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
    app.UseHttpsRedirection();
}

// Security headers
app.Use(async (context, next) =>
{
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["X-Frame-Options"] = "DENY";
    context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    context.Response.Headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
    context.Response.Headers["Content-Security-Policy"] = "default-src 'none'; frame-ancestors 'none'";
    context.Response.Headers["X-XSS-Protection"] = "1; mode=block";
    context.Response.Headers["Strict-Transport-Security"] = "max-age=31536000; includeSubDomains";
    await next();
});

app.UseCors();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// Apply pending migrations on startup if configured
var applyMigrationsOnStartup = bool.TryParse(
        Environment.GetEnvironmentVariable("DB_APPLY_MIGRATIONS_ON_STARTUP"),
        out var applyMigrationsFromEnv)
    ? applyMigrationsFromEnv
    : configuration.GetValue<bool>("Database:ApplyMigrationsOnStartup");

if (applyMigrationsOnStartup)
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<MarcoDbContext>();
    await db.Database.MigrateAsync();
}

// ═══════════════════════════════════════════════════════
// Startup Logging for Mobile App Debugging
// ═══════════════════════════════════════════════════════
var logger = app.Services.GetRequiredService<ILogger<Program>>();
var urls = configuration.GetValue<string>("ASPNETCORE_URLS") ?? "http://0.0.0.0:5000";
logger.LogInformation("╔══════════════════════════════════════════════════════════════╗");
logger.LogInformation("║          MarcoERP API Started Successfully                   ║");
logger.LogInformation("╠══════════════════════════════════════════════════════════════╣");
logger.LogInformation("║  Listening on: {urls}", urls);
logger.LogInformation("║  Environment: {env}", app.Environment.EnvironmentName);
logger.LogInformation("║  Render Port: {port}", renderPort ?? "n/a");
logger.LogInformation("║  Swagger UI: http://0.0.0.0:5000/swagger");
logger.LogInformation("║  Health Check: http://0.0.0.0:5000/api/health");
logger.LogInformation("║  Mobile Login: http://YOUR-IP:5000/api/auth/login");
logger.LogInformation("╠══════════════════════════════════════════════════════════════╣");
logger.LogInformation("║  Network Access Instructions:                                ║");
logger.LogInformation("║  1. Find your PC IP: ipconfig (Windows) or ifconfig (Linux)  ║");
logger.LogInformation("║  2. Update mobile app: api_constants.dart                    ║");
logger.LogInformation("║  3. Ensure firewall allows port 5000                         ║");
logger.LogInformation("║  4. Test: http://YOUR-IP:5000/api/health                     ║");
logger.LogInformation("╚══════════════════════════════════════════════════════════════╝");

await app.RunAsync();
