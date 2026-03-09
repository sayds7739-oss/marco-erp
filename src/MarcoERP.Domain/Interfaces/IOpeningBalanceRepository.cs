using System.Threading;
using System.Threading.Tasks;
using MarcoERP.Domain.Entities.Accounting;

namespace MarcoERP.Domain.Interfaces
{
    /// <summary>
    /// Repository interface for the OpeningBalance aggregate.
    /// </summary>
    public interface IOpeningBalanceRepository : IRepository<OpeningBalance>
    {
        /// <summary>Gets an opening balance with all its lines loaded (no tracking).</summary>
        Task<OpeningBalance> GetWithLinesAsync(int id, CancellationToken ct = default);

        /// <summary>Gets an opening balance with all its lines loaded WITH change tracking.</summary>
        Task<OpeningBalance> GetWithLinesTrackedAsync(int id, CancellationToken ct = default);

        /// <summary>Gets the opening balance for a given fiscal year (one per year).</summary>
        Task<OpeningBalance> GetByFiscalYearAsync(int fiscalYearId, CancellationToken ct = default);

        /// <summary>Gets the opening balance for a fiscal year with lines (no tracking).</summary>
        Task<OpeningBalance> GetByFiscalYearWithLinesAsync(int fiscalYearId, CancellationToken ct = default);

        /// <summary>Checks if an opening balance already exists for the given fiscal year.</summary>
        Task<bool> ExistsForFiscalYearAsync(int fiscalYearId, CancellationToken ct = default);
    }
}
