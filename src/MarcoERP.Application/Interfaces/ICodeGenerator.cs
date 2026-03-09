using System.Threading;
using System.Threading.Tasks;

namespace MarcoERP.Application.Interfaces
{
    /// <summary>
    /// Generates sequential document codes using the CodeSequence table.
    /// ACG-01: Implemented in Infrastructure/Persistence layer.
    /// ACG-02: Called by Application layer during posting.
    /// ACG-03: Each document type has its own sequence.
    /// ACG-04: Codes are sequential and unique for posted documents.
    /// ACG-05: Codes reset at the start of each fiscal year.
    /// </summary>
    public interface ICodeGenerator
    {
        /// <summary>
        /// Returns the next sequential code for the given document type and fiscal year.
        /// Must be called within a Serializable transaction (SEQ-03).
        /// </summary>
        /// <param name="documentType">Document type prefix (e.g., "JV", "SI", "PI", "CR", "CP", "CT", "SR", "PR").</param>
        /// <param name="fiscalYearId">Fiscal year ID for sequence scoping.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Formatted code string (e.g., "SI-2026-00001").</returns>
        Task<string> NextCodeAsync(string documentType, int fiscalYearId,
            CancellationToken cancellationToken = default);
    }
}
