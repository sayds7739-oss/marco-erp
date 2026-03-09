using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MarcoERP.Application.Common;
using MarcoERP.Application.DTOs.Settings;
using MarcoERP.Application.Interfaces;
using MarcoERP.Application.Interfaces.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MarcoERP.Persistence.Services.Settings
{
    /// <summary>
    /// CRITICAL SERVICE: Performs selective cleanup of transactional and master data.
    /// This service bypasses normal EF Core interceptors and should only be used
    /// during initial system setup or controlled data reset scenarios.
    ///
    /// Security Guards:
    /// 1. Blocked in Production Mode (IsProductionMode = true)
    /// 2. Requires SuperAdmin permission (enforced via AuthorizationProxy)
    /// 3. Full audit logging of all operations via ILogger
    /// </summary>
    public sealed class DataPurgeService : IDataPurgeService
    {
        private readonly MarcoDbContext _dbContext;
        private readonly ICurrentUserService _currentUserService;
        private readonly ILogger<DataPurgeService> _logger;

        public DataPurgeService(
            MarcoDbContext dbContext,
            ICurrentUserService currentUserService,
            ILogger<DataPurgeService> logger = null)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
            _logger = logger;
        }

        public async Task<ServiceResult<DataPurgeResultDto>> PurgeAsync(DataPurgeOptionsDto options, CancellationToken ct = default)
        {
            if (options == null)
                return ServiceResult<DataPurgeResultDto>.Failure("خيارات المسح غير صالحة.");

            // SECURITY GUARD 1: Block in Production Mode
            var isProductionMode = await IsProductionModeAsync(ct);
            if (isProductionMode)
            {
                _logger?.LogWarning(
                    "DataPurge blocked: Production mode is enabled. User={User}",
                    _currentUserService.Username);
                return ServiceResult<DataPurgeResultDto>.Failure(
                    "عملية مسح البيانات محظورة أثناء وضع الإنتاج. " +
                    "قم بتعطيل وضع الإنتاج من الإعدادات أولاً.");
            }

            // SECURITY GUARD 2: Log the operation attempt (via structured logging for audit trail)
            var username = _currentUserService.Username ?? "Unknown";
            _logger?.LogWarning(
                "[AUDIT] DataPurge initiated. User={User}, Time={Time}, KeepProducts={KeepProducts}, KeepCustomers={KeepCustomers}, KeepSuppliers={KeepSuppliers}",
                username, DateTime.UtcNow, options.KeepProducts, options.KeepCustomers, options.KeepSuppliers);

            var deletedItems = new List<DataPurgeItemResultDto>();

            try
            {
                await using var tx = await _dbContext.Database.BeginTransactionAsync(ct);

                async Task DeleteTableAsync(string tableName, string entityName)
                {
                    var sql = DbProviderHelper.DeleteFromTable(tableName);
                    var affected = await _dbContext.Database.ExecuteSqlRawAsync(sql, ct);
                    deletedItems.Add(new DataPurgeItemResultDto
                    {
                        EntityName = entityName,
                        DeletedRows = affected
                    });
                }

                var keepSalesRepsEffective = options.KeepSalesRepresentatives || options.KeepCustomers;
                var keepPriceListsEffective = options.KeepCustomers;

                await DeleteTableAsync("BankReconciliationItems", "بنود التسوية البنكية");
                await DeleteTableAsync("BankReconciliations", "التسويات البنكية");

                await DeleteTableAsync("PosPayments", "مدفوعات نقاط البيع");
                await DeleteTableAsync("CashReceipts", "سندات القبض");
                await DeleteTableAsync("CashPayments", "سندات الصرف");
                await DeleteTableAsync("PosSessions", "جلسات نقاط البيع");
                await DeleteTableAsync("CashTransfers", "تحويلات الخزنة");

                await DeleteTableAsync("SalesReturnLines", "بنود مرتجعات البيع");
                await DeleteTableAsync("SalesReturns", "مرتجعات البيع");
                await DeleteTableAsync("SalesQuotationLines", "بنود عروض أسعار البيع");
                await DeleteTableAsync("SalesQuotations", "عروض أسعار البيع");
                await DeleteTableAsync("SalesInvoiceLines", "بنود فواتير البيع");
                await DeleteTableAsync("SalesInvoices", "فواتير البيع");

                await DeleteTableAsync("PurchaseReturnLines", "بنود مرتجعات الشراء");
                await DeleteTableAsync("PurchaseReturns", "مرتجعات الشراء");
                await DeleteTableAsync("PurchaseQuotationLines", "بنود طلبات الشراء");
                await DeleteTableAsync("PurchaseQuotations", "طلبات الشراء");
                await DeleteTableAsync("PurchaseInvoiceLines", "بنود فواتير الشراء");
                await DeleteTableAsync("PurchaseInvoices", "فواتير الشراء");

                await DeleteTableAsync("InventoryMovements", "حركات المخزون");
                await DeleteTableAsync("InventoryAdjustmentLines", "بنود تسويات المخزون");
                await DeleteTableAsync("InventoryAdjustments", "تسويات المخزون");
                await DeleteTableAsync("WarehouseProducts", "أرصدة الأصناف بالمخازن");

                await DeleteTableAsync("PriceTiers", "شرائح الأسعار");
                if (!keepPriceListsEffective)
                    await DeleteTableAsync("PriceLists", "قوائم الأسعار");

                await DeleteTableAsync("JournalEntryLines", "بنود القيود اليومية");
                await DeleteTableAsync("JournalEntries", "القيود اليومية");

                if (!keepSalesRepsEffective)
                    await DeleteTableAsync("SalesRepresentatives", "مندوبي المبيعات");

                if (!options.KeepCustomers)
                    await DeleteTableAsync("Customers", "العملاء");

                if (!options.KeepSuppliers)
                    await DeleteTableAsync("Suppliers", "الموردين");

                if (!options.KeepProducts)
                {
                    await DeleteTableAsync("ProductUnits", "وحدات الأصناف");
                    await DeleteTableAsync("Products", "الأصناف");
                }

                await DeleteTableAsync("CodeSequences", "سلاسل الترقيم");
                await DeleteTableAsync("AuditLogs", "سجل المراجعة");
                await DeleteTableAsync("BackupHistory", "سجل النسخ الاحتياطي");

                await tx.CommitAsync(ct);

                var result = new DataPurgeResultDto
                {
                    ExecutedAtUtc = DateTime.UtcNow,
                    TotalDeletedRows = deletedItems.Sum(i => i.DeletedRows),
                    Items = deletedItems
                };

                // SECURITY GUARD 3: Log successful completion (structured logging for audit)
                _logger?.LogWarning(
                    "[AUDIT] DataPurge completed. User={User}, TotalRowsDeleted={TotalRows}, TablesAffected={Tables}",
                    username, result.TotalDeletedRows,
                    string.Join(", ", deletedItems.Select(i => $"{i.EntityName}({i.DeletedRows})")));

                return ServiceResult<DataPurgeResultDto>.Success(result);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "[AUDIT] DataPurge FAILED. User={User}, Error={Error}", username, ex.Message);

                var userMsg = ex.InnerException?.Message?.Contains("FOREIGN KEY", StringComparison.OrdinalIgnoreCase) == true
                    ? "لا يمكن حذف البيانات لوجود سجلات مرتبطة."
                    : "حدث خطأ أثناء حذف البيانات. يرجى المراجعة.";
                return ServiceResult<DataPurgeResultDto>.Failure(userMsg);
            }
        }

        private async Task<bool> IsProductionModeAsync(CancellationToken ct)
        {
            var setting = await _dbContext.SystemSettings
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.SettingKey == "IsProductionMode", ct);

            if (setting == null)
                return true; // Default to production mode for safety

            return string.Equals(setting.SettingValue, "true", StringComparison.OrdinalIgnoreCase);
        }
    }
}
