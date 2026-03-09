using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MarcoERP.Domain.Entities.Treasury;
using MarcoERP.Domain.Enums;

namespace MarcoERP.Domain.Interfaces.Treasury
{
    /// <summary>
    /// Repository contract for CashTransfer entity.
    /// </summary>
    public interface ICashTransferRepository : IRepository<CashTransfer>
    {
        /// <summary>Gets a cash transfer by ID with navigation properties.</summary>
        Task<CashTransfer> GetWithDetailsAsync(int id, CancellationToken ct = default);

        /// <summary>Gets a cash transfer by ID with tracking enabled for mutation operations.</summary>
        Task<CashTransfer> GetWithDetailsTrackedAsync(int id, CancellationToken ct = default);

        /// <summary>Gets a cash transfer by its transfer number.</summary>
        Task<CashTransfer> GetByNumberAsync(string transferNumber, CancellationToken ct = default);

        /// <summary>Checks if a transfer number already exists.</summary>
        Task<bool> NumberExistsAsync(string transferNumber, CancellationToken ct = default);

        /// <summary>Gets cash transfers by status.</summary>
        Task<IReadOnlyList<CashTransfer>> GetByStatusAsync(InvoiceStatus status, CancellationToken ct = default);

        /// <summary>Gets cash transfers involving a specific cashbox (source or target).</summary>
        Task<IReadOnlyList<CashTransfer>> GetByCashboxAsync(int cashboxId, CancellationToken ct = default);

        /// <summary>Gets the next auto-generated transfer number (CT-YYYYMM-####).</summary>
        Task<string> GetNextNumberAsync(CancellationToken ct = default);
    }
}
