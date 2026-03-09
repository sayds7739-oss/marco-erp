using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MarcoERP.Domain.Entities.Purchases;
using MarcoERP.Domain.Enums;

namespace MarcoERP.Domain.Interfaces.Purchases
{
    /// <summary>
    /// Repository contract for PurchaseReturn aggregate.
    /// </summary>
    public interface IPurchaseReturnRepository : IRepository<PurchaseReturn>
    {
        /// <summary>Gets return with all lines eagerly loaded (no tracking — read-only).</summary>
        Task<PurchaseReturn> GetWithLinesAsync(int id, CancellationToken cancellationToken = default);

        /// <summary>Gets return with all lines eagerly loaded WITH change tracking (for updates).</summary>
        Task<PurchaseReturn> GetWithLinesTrackedAsync(int id, CancellationToken cancellationToken = default);

        /// <summary>Gets return by number.</summary>
        Task<PurchaseReturn> GetByNumberAsync(string returnNumber, CancellationToken cancellationToken = default);

        /// <summary>Checks if a return number already exists.</summary>
        Task<bool> NumberExistsAsync(string returnNumber, CancellationToken cancellationToken = default);

        /// <summary>Gets returns filtered by status.</summary>
        Task<IReadOnlyList<PurchaseReturn>> GetByStatusAsync(InvoiceStatus status, CancellationToken cancellationToken = default);

        /// <summary>Gets returns for a specific supplier.</summary>
        Task<IReadOnlyList<PurchaseReturn>> GetBySupplierAsync(int supplierId, CancellationToken cancellationToken = default);

        /// <summary>Gets returns referencing a specific original invoice.</summary>
        Task<IReadOnlyList<PurchaseReturn>> GetByOriginalInvoiceAsync(int invoiceId, CancellationToken cancellationToken = default);

        /// <summary>Generates the next return number (PR-YYYYMM-####).</summary>
        Task<string> GetNextNumberAsync(CancellationToken cancellationToken = default);
    }
}
