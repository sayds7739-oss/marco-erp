using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MarcoERP.Application.Common;
using MarcoERP.Application.DTOs.Settings;
using MarcoERP.Application.Interfaces;
using MarcoERP.Application.Interfaces.Settings;
using MarcoERP.Domain.Interfaces;

namespace MarcoERP.Persistence.Services.Settings
{
    /// <summary>
    /// Implementation of IRecycleBinService for viewing and restoring soft-deleted records.
    /// </summary>
    public sealed class RecycleBinService : IRecycleBinService
    {
        private readonly MarcoDbContext _db;
        private readonly ICurrentUserService _currentUser;
        private readonly IDateTimeProvider _dateTimeProvider;

        private static readonly IReadOnlyList<(string Key, string ArabicName)> SupportedTypes = new List<(string, string)>
        {
            ("Product", "الأصناف"),
            ("Customer", "العملاء"),
            ("Supplier", "الموردين"),
            ("Account", "الحسابات"),
            ("SalesInvoice", "فواتير البيع"),
            ("PurchaseInvoice", "فواتير الشراء"),
            ("SalesReturn", "مرتجعات البيع"),
            ("PurchaseReturn", "مرتجعات الشراء"),
            ("SalesQuotation", "عروض الأسعار"),
            ("PurchaseQuotation", "طلبات الشراء"),
            ("PriceList", "قوائم الأسعار"),
            ("SalesRepresentative", "مندوبي المبيعات"),
        };

        public RecycleBinService(
            MarcoDbContext db,
            ICurrentUserService currentUser,
            IDateTimeProvider dateTimeProvider)
        {
            _db = db ?? throw new ArgumentNullException(nameof(db));
            _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
            _dateTimeProvider = dateTimeProvider ?? throw new ArgumentNullException(nameof(dateTimeProvider));
        }

        public IReadOnlyList<(string Key, string ArabicName)> GetSupportedEntityTypes() => SupportedTypes;

        public async Task<ServiceResult<IReadOnlyList<DeletedRecordDto>>> GetAllDeletedAsync(CancellationToken ct = default)
        {
            var results = new List<DeletedRecordDto>();

            // Products
            var deletedProducts = await _db.Products
                .IgnoreQueryFilters()
                .Where(p => p.IsDeleted)
                .Select(p => new DeletedRecordDto
                {
                    Id = p.Id,
                    EntityType = "Product",
                    EntityTypeArabic = "صنف",
                    DisplayName = p.NameAr,
                    Code = p.Code,
                    DeletedBy = p.DeletedBy,
                    DeletedAt = p.DeletedAt ?? DateTime.MinValue,
                    CanRestore = true
                })
                .ToListAsync(ct);
            results.AddRange(deletedProducts);

            // Customers
            var deletedCustomers = await _db.Customers
                .IgnoreQueryFilters()
                .Where(c => c.IsDeleted)
                .Select(c => new DeletedRecordDto
                {
                    Id = c.Id,
                    EntityType = "Customer",
                    EntityTypeArabic = "عميل",
                    DisplayName = c.NameAr,
                    Code = c.Code,
                    DeletedBy = c.DeletedBy,
                    DeletedAt = c.DeletedAt ?? DateTime.MinValue,
                    CanRestore = true
                })
                .ToListAsync(ct);
            results.AddRange(deletedCustomers);

            // Suppliers
            var deletedSuppliers = await _db.Suppliers
                .IgnoreQueryFilters()
                .Where(s => s.IsDeleted)
                .Select(s => new DeletedRecordDto
                {
                    Id = s.Id,
                    EntityType = "Supplier",
                    EntityTypeArabic = "مورد",
                    DisplayName = s.NameAr,
                    Code = s.Code,
                    DeletedBy = s.DeletedBy,
                    DeletedAt = s.DeletedAt ?? DateTime.MinValue,
                    CanRestore = true
                })
                .ToListAsync(ct);
            results.AddRange(deletedSuppliers);

            // SalesInvoices (only drafts can be restored)
            var deletedSalesInvoices = await _db.SalesInvoices
                .IgnoreQueryFilters()
                .Where(i => i.IsDeleted)
                .Select(i => new DeletedRecordDto
                {
                    Id = i.Id,
                    EntityType = "SalesInvoice",
                    EntityTypeArabic = "فاتورة بيع",
                    DisplayName = $"فاتورة بيع #{i.InvoiceNumber}",
                    Code = i.InvoiceNumber,
                    DeletedBy = i.DeletedBy,
                    DeletedAt = i.DeletedAt ?? DateTime.MinValue,
                    CanRestore = true
                })
                .ToListAsync(ct);
            results.AddRange(deletedSalesInvoices);

            // PurchaseInvoices
            var deletedPurchaseInvoices = await _db.PurchaseInvoices
                .IgnoreQueryFilters()
                .Where(i => i.IsDeleted)
                .Select(i => new DeletedRecordDto
                {
                    Id = i.Id,
                    EntityType = "PurchaseInvoice",
                    EntityTypeArabic = "فاتورة شراء",
                    DisplayName = $"فاتورة شراء #{i.InvoiceNumber}",
                    Code = i.InvoiceNumber,
                    DeletedBy = i.DeletedBy,
                    DeletedAt = i.DeletedAt ?? DateTime.MinValue,
                    CanRestore = true
                })
                .ToListAsync(ct);
            results.AddRange(deletedPurchaseInvoices);

            // PriceLists
            var deletedPriceLists = await _db.PriceLists
                .IgnoreQueryFilters()
                .Where(p => p.IsDeleted)
                .Select(p => new DeletedRecordDto
                {
                    Id = p.Id,
                    EntityType = "PriceList",
                    EntityTypeArabic = "قائمة أسعار",
                    DisplayName = p.NameAr,
                    Code = p.Code,
                    DeletedBy = p.DeletedBy,
                    DeletedAt = p.DeletedAt ?? DateTime.MinValue,
                    CanRestore = true
                })
                .ToListAsync(ct);
            results.AddRange(deletedPriceLists);

            // SalesRepresentatives
            var deletedReps = await _db.SalesRepresentatives
                .IgnoreQueryFilters()
                .Where(r => r.IsDeleted)
                .Select(r => new DeletedRecordDto
                {
                    Id = r.Id,
                    EntityType = "SalesRepresentative",
                    EntityTypeArabic = "مندوب مبيعات",
                    DisplayName = r.NameAr,
                    Code = r.Code,
                    DeletedBy = r.DeletedBy,
                    DeletedAt = r.DeletedAt ?? DateTime.MinValue,
                    CanRestore = true
                })
                .ToListAsync(ct);
            results.AddRange(deletedReps);

            return ServiceResult<IReadOnlyList<DeletedRecordDto>>.Success(
                results.OrderByDescending(r => r.DeletedAt).ToList());
        }

        public async Task<ServiceResult<IReadOnlyList<DeletedRecordDto>>> GetByEntityTypeAsync(string entityType, CancellationToken ct = default)
        {
            var all = await GetAllDeletedAsync(ct);
            if (!all.IsSuccess)
                return all;

            var filtered = all.Data.Where(r => r.EntityType == entityType).ToList();
            return ServiceResult<IReadOnlyList<DeletedRecordDto>>.Success(filtered);
        }

        public async Task<ServiceResult> RestoreAsync(string entityType, int entityId, CancellationToken ct = default)
        {
            if (!_currentUser.IsAuthenticated)
                return ServiceResult.Failure("يجب تسجيل الدخول أولاً.");

            if (!_currentUser.HasPermission(PermissionKeys.RecycleBinRestore))
                return ServiceResult.Failure("لا تملك الصلاحية لاستعادة السجلات المحذوفة.");

            switch (entityType)
            {
                case "Product":
                    return await RestoreProductAsync(entityId, ct);
                case "Customer":
                    return await RestoreCustomerAsync(entityId, ct);
                case "Supplier":
                    return await RestoreSupplierAsync(entityId, ct);
                case "SalesInvoice":
                    return await RestoreSalesInvoiceAsync(entityId, ct);
                case "PurchaseInvoice":
                    return await RestorePurchaseInvoiceAsync(entityId, ct);
                case "PriceList":
                    return await RestorePriceListAsync(entityId, ct);
                case "SalesRepresentative":
                    return await RestoreSalesRepresentativeAsync(entityId, ct);
                default:
                    return ServiceResult.Failure($"نوع السجل '{entityType}' غير مدعوم للاستعادة.");
            }
        }

        private async Task<ServiceResult> RestoreProductAsync(int id, CancellationToken ct)
        {
            var entity = await _db.Products.IgnoreQueryFilters().FirstOrDefaultAsync(p => p.Id == id, ct);
            if (entity == null)
                return ServiceResult.Failure("السجل غير موجود.");

            entity.Restore();
            await _db.SaveChangesAsync(ct);
            return ServiceResult.Success();
        }

        private async Task<ServiceResult> RestoreCustomerAsync(int id, CancellationToken ct)
        {
            var entity = await _db.Customers.IgnoreQueryFilters().FirstOrDefaultAsync(c => c.Id == id, ct);
            if (entity == null)
                return ServiceResult.Failure("السجل غير موجود.");

            entity.Restore();
            await _db.SaveChangesAsync(ct);
            return ServiceResult.Success();
        }

        private async Task<ServiceResult> RestoreSupplierAsync(int id, CancellationToken ct)
        {
            var entity = await _db.Suppliers.IgnoreQueryFilters().FirstOrDefaultAsync(s => s.Id == id, ct);
            if (entity == null)
                return ServiceResult.Failure("السجل غير موجود.");

            entity.Restore();
            await _db.SaveChangesAsync(ct);
            return ServiceResult.Success();
        }

        private async Task<ServiceResult> RestoreSalesInvoiceAsync(int id, CancellationToken ct)
        {
            var entity = await _db.SalesInvoices.IgnoreQueryFilters().FirstOrDefaultAsync(i => i.Id == id, ct);
            if (entity == null)
                return ServiceResult.Failure("السجل غير موجود.");

            entity.Restore();
            await _db.SaveChangesAsync(ct);
            return ServiceResult.Success();
        }

        private async Task<ServiceResult> RestorePurchaseInvoiceAsync(int id, CancellationToken ct)
        {
            var entity = await _db.PurchaseInvoices.IgnoreQueryFilters().FirstOrDefaultAsync(i => i.Id == id, ct);
            if (entity == null)
                return ServiceResult.Failure("السجل غير موجود.");

            entity.Restore();
            await _db.SaveChangesAsync(ct);
            return ServiceResult.Success();
        }

        private async Task<ServiceResult> RestorePriceListAsync(int id, CancellationToken ct)
        {
            var entity = await _db.PriceLists.IgnoreQueryFilters().FirstOrDefaultAsync(p => p.Id == id, ct);
            if (entity == null)
                return ServiceResult.Failure("السجل غير موجود.");

            entity.Restore();
            await _db.SaveChangesAsync(ct);
            return ServiceResult.Success();
        }

        private async Task<ServiceResult> RestoreSalesRepresentativeAsync(int id, CancellationToken ct)
        {
            var entity = await _db.SalesRepresentatives.IgnoreQueryFilters().FirstOrDefaultAsync(r => r.Id == id, ct);
            if (entity == null)
                return ServiceResult.Failure("السجل غير موجود.");

            entity.Restore();
            await _db.SaveChangesAsync(ct);
            return ServiceResult.Success();
        }
    }
}
