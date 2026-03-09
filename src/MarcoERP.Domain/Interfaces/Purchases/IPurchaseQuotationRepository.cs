using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MarcoERP.Domain.Entities.Purchases;
using MarcoERP.Domain.Enums;

namespace MarcoERP.Domain.Interfaces.Purchases
{
    /// <summary>
    /// Repository contract for PurchaseQuotation aggregate.
    /// </summary>
    public interface IPurchaseQuotationRepository : IRepository<PurchaseQuotation>
    {
        /// <summary>Gets quotation with all lines eagerly loaded (no tracking — read-only).</summary>
        Task<PurchaseQuotation> GetWithLinesAsync(int id, CancellationToken cancellationToken = default);

        /// <summary>Gets quotation with all lines eagerly loaded WITH change tracking (for updates).</summary>
        Task<PurchaseQuotation> GetWithLinesTrackedAsync(int id, CancellationToken cancellationToken = default);

        /// <summary>Gets quotation by number.</summary>
        Task<PurchaseQuotation> GetByNumberAsync(string quotationNumber, CancellationToken cancellationToken = default);

        /// <summary>Checks if a quotation number already exists.</summary>
        Task<bool> NumberExistsAsync(string quotationNumber, CancellationToken cancellationToken = default);

        /// <summary>Gets quotations filtered by status.</summary>
        Task<IReadOnlyList<PurchaseQuotation>> GetByStatusAsync(QuotationStatus status, CancellationToken cancellationToken = default);

        /// <summary>Gets quotations for a specific supplier.</summary>
        Task<IReadOnlyList<PurchaseQuotation>> GetBySupplierAsync(int supplierId, CancellationToken cancellationToken = default);

        /// <summary>Generates the next quotation number (PQ-YYYYMM-####).</summary>
        Task<string> GetNextNumberAsync(CancellationToken cancellationToken = default);
    }
}
