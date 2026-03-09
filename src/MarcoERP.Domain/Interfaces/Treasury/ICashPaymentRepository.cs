using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MarcoERP.Domain.Entities.Treasury;
using MarcoERP.Domain.Enums;

namespace MarcoERP.Domain.Interfaces.Treasury
{
    /// <summary>
    /// Repository contract for CashPayment entity.
    /// </summary>
    public interface ICashPaymentRepository : IRepository<CashPayment>
    {
        /// <summary>Gets a cash payment by ID with navigation properties.</summary>
        Task<CashPayment> GetWithDetailsAsync(int id, CancellationToken ct = default);

        /// <summary>Gets a cash payment by ID with navigation properties and change tracking enabled.</summary>
        Task<CashPayment> GetWithDetailsTrackedAsync(int id, CancellationToken ct = default);

        /// <summary>Gets a cash payment by its payment number.</summary>
        Task<CashPayment> GetByNumberAsync(string paymentNumber, CancellationToken ct = default);

        /// <summary>Checks if a payment number already exists.</summary>
        Task<bool> NumberExistsAsync(string paymentNumber, CancellationToken ct = default);

        /// <summary>Gets cash payments by status.</summary>
        Task<IReadOnlyList<CashPayment>> GetByStatusAsync(InvoiceStatus status, CancellationToken ct = default);

        /// <summary>Gets cash payments by cashbox.</summary>
        Task<IReadOnlyList<CashPayment>> GetByCashboxAsync(int cashboxId, CancellationToken ct = default);

        /// <summary>Gets cash payments by supplier.</summary>
        Task<IReadOnlyList<CashPayment>> GetBySupplierAsync(int supplierId, CancellationToken ct = default);

        /// <summary>Gets the next auto-generated payment number (CP-YYYYMM-####).</summary>
        Task<string> GetNextNumberAsync(CancellationToken ct = default);
    }
}
