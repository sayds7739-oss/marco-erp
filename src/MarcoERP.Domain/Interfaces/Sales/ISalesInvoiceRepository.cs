using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MarcoERP.Domain.Entities.Sales;
using MarcoERP.Domain.Enums;

namespace MarcoERP.Domain.Interfaces.Sales
{
    /// <summary>
    /// Repository contract for SalesInvoice aggregate.
    /// </summary>
    public interface ISalesInvoiceRepository : IRepository<SalesInvoice>
    {
        /// <summary>Gets invoice with all lines eagerly loaded (no tracking — read-only).</summary>
        Task<SalesInvoice> GetWithLinesAsync(int id, CancellationToken cancellationToken = default);

        /// <summary>Gets invoice with all lines eagerly loaded WITH change tracking (for updates).</summary>
        Task<SalesInvoice> GetWithLinesTrackedAsync(int id, CancellationToken cancellationToken = default);

        /// <summary>Gets invoice by number.</summary>
        Task<SalesInvoice> GetByNumberAsync(string invoiceNumber, CancellationToken cancellationToken = default);

        /// <summary>Checks if an invoice number already exists.</summary>
        Task<bool> NumberExistsAsync(string invoiceNumber, CancellationToken cancellationToken = default);

        /// <summary>Gets invoices filtered by status.</summary>
        Task<IReadOnlyList<SalesInvoice>> GetByStatusAsync(InvoiceStatus status, CancellationToken cancellationToken = default);

        /// <summary>Gets invoices for a specific customer.</summary>
        Task<IReadOnlyList<SalesInvoice>> GetByCustomerAsync(int customerId, CancellationToken cancellationToken = default);

        /// <summary>Generates the next invoice number (SI-YYYYMM-####).</summary>
        Task<string> GetNextNumberAsync(CancellationToken cancellationToken = default);
    }
}
