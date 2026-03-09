using System.Threading;
using System.Threading.Tasks;

namespace MarcoERP.Domain.Entities.Accounting.Policies
{
    /// <summary>
    /// Generates sequential journal numbers per fiscal year.
    /// Implemented in Infrastructure layer using DB SEQUENCE.
    /// </summary>
    public interface IJournalNumberGenerator
    {
        /// <summary>Returns the next sequential journal number for the given fiscal year.</summary>
        Task<string> NextNumberAsync(int fiscalYearId, CancellationToken cancellationToken = default);
    }
}
