using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MarcoERP.Domain.Entities.Sales;
using MarcoERP.Domain.Enums;

namespace MarcoERP.Domain.Interfaces.Sales
{
    /// <summary>
    /// Repository contract for SalesReturn aggregate.
    /// </summary>
    public interface ISalesReturnRepository : IRepository<SalesReturn>
    {
        /// <summary>Gets return with all lines eagerly loaded (no tracking — read-only).</summary>
        Task<SalesReturn> GetWithLinesAsync(int id, CancellationToken cancellationToken = default);

        /// <summary>Gets return with all lines eagerly loaded WITH change tracking (for updates).</summary>
        Task<SalesReturn> GetWithLinesTrackedAsync(int id, CancellationToken cancellationToken = default);

        /// <summary>Gets return by number.</summary>
        Task<SalesReturn> GetByNumberAsync(string returnNumber, CancellationToken cancellationToken = default);

        /// <summary>Checks if a return number already exists.</summary>
        Task<bool> NumberExistsAsync(string returnNumber, CancellationToken cancellationToken = default);

        /// <summary>Gets returns filtered by status.</summary>
        Task<IReadOnlyList<SalesReturn>> GetByStatusAsync(InvoiceStatus status, CancellationToken cancellationToken = default);

        /// <summary>Gets returns for a specific customer.</summary>
        Task<IReadOnlyList<SalesReturn>> GetByCustomerAsync(int customerId, CancellationToken cancellationToken = default);

        /// <summary>Gets returns referencing a specific original invoice.</summary>
        Task<IReadOnlyList<SalesReturn>> GetByOriginalInvoiceAsync(int invoiceId, CancellationToken cancellationToken = default);

        /// <summary>Generates the next return number (SR-YYYYMM-####).</summary>
        Task<string> GetNextNumberAsync(CancellationToken cancellationToken = default);
    }
}
