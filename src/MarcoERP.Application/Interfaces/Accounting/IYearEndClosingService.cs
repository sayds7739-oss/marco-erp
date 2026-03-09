using System.Threading;
using System.Threading.Tasks;
using MarcoERP.Application.Common;

namespace MarcoERP.Application.Interfaces.Accounting
{
    /// <summary>
    /// Application service contract for year-end closing operations.
    /// Implements the closing entry logic: Revenue/COGS/Expense → Retained Earnings.
    /// </summary>
    public interface IYearEndClosingService
    {
        /// <summary>
        /// Generates the closing journal entry for the specified fiscal year.
        /// Transfers all temporary account balances to Retained Earnings (3121).
        /// </summary>
        [RequiresPermission(PermissionKeys.FiscalYearManage)]
        Task<ServiceResult> GenerateClosingEntryAsync(int fiscalYearId, CancellationToken ct = default);
    }
}
