using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MarcoERP.Domain.Entities.Purchases;
using MarcoERP.Domain.Enums;

namespace MarcoERP.Domain.Interfaces.Purchases
{
    /// <summary>
    /// Repository contract for PurchaseInvoice aggregate.
    /// </summary>
    public interface IPurchaseInvoiceRepository : IRepository<PurchaseInvoice>
    {
        /// <summary>Gets invoice with all lines eagerly loaded (no tracking — read-only).</summary>
        Task<PurchaseInvoice> GetWithLinesAsync(int id, CancellationToken cancellationToken = default);

        /// <summary>Gets invoice with all lines eagerly loaded WITH change tracking (for updates).</summary>
        Task<PurchaseInvoice> GetWithLinesTrackedAsync(int id, CancellationToken cancellationToken = default);

        /// <summary>Gets invoice by number.</summary>
        Task<PurchaseInvoice> GetByNumberAsync(string invoiceNumber, CancellationToken cancellationToken = default);

        /// <summary>Checks if an invoice number already exists.</summary>
        Task<bool> NumberExistsAsync(string invoiceNumber, CancellationToken cancellationToken = default);

        /// <summary>Gets invoices filtered by status.</summary>
        Task<IReadOnlyList<PurchaseInvoice>> GetByStatusAsync(InvoiceStatus status, CancellationToken cancellationToken = default);

        /// <summary>Gets invoices for a specific supplier.</summary>
        Task<IReadOnlyList<PurchaseInvoice>> GetBySupplierAsync(int supplierId, CancellationToken cancellationToken = default);

        /// <summary>Generates the next invoice number (PI-YYYYMM-####).</summary>
        Task<string> GetNextNumberAsync(CancellationToken cancellationToken = default);
    }
}
