using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MarcoERP.Domain.Entities.Treasury;
using MarcoERP.Domain.Enums;

namespace MarcoERP.Domain.Interfaces.Treasury
{
    /// <summary>
    /// Repository contract for CashReceipt entity.
    /// </summary>
    public interface ICashReceiptRepository : IRepository<CashReceipt>
    {
        /// <summary>Gets a cash receipt by ID with navigation properties.</summary>
        Task<CashReceipt> GetWithDetailsAsync(int id, CancellationToken ct = default);

        /// <summary>Gets a cash receipt by ID with navigation properties and change tracking enabled.</summary>
        Task<CashReceipt> GetWithDetailsTrackedAsync(int id, CancellationToken ct = default);

        /// <summary>Gets a cash receipt by its receipt number.</summary>
        Task<CashReceipt> GetByNumberAsync(string receiptNumber, CancellationToken ct = default);

        /// <summary>Checks if a receipt number already exists.</summary>
        Task<bool> NumberExistsAsync(string receiptNumber, CancellationToken ct = default);

        /// <summary>Gets cash receipts by status.</summary>
        Task<IReadOnlyList<CashReceipt>> GetByStatusAsync(InvoiceStatus status, CancellationToken ct = default);

        /// <summary>Gets cash receipts by cashbox.</summary>
        Task<IReadOnlyList<CashReceipt>> GetByCashboxAsync(int cashboxId, CancellationToken ct = default);

        /// <summary>Gets cash receipts by customer.</summary>
        Task<IReadOnlyList<CashReceipt>> GetByCustomerAsync(int customerId, CancellationToken ct = default);

        /// <summary>Gets the next auto-generated receipt number (CR-YYYYMM-####).</summary>
        Task<string> GetNextNumberAsync(CancellationToken ct = default);
    }
}
