using MarcoERP.Application.Interfaces.Settings;
using Microsoft.AspNetCore.Mvc;

namespace MarcoERP.API.Controllers.Settings
{
    /// <summary>
    /// API controller for data integrity checks.
    /// </summary>
    public class IntegrityController : ApiControllerBase
    {
        private readonly IIntegrityService _integrityService;

        public IntegrityController(IIntegrityService integrityService)
        {
            _integrityService = integrityService;
        }

        // ── Queries ─────────────────────────────────────────────

        /// <summary>Verifies total debits == total credits across all posted journal entries.</summary>
        [HttpGet("trial-balance")]
        public async Task<IActionResult> CheckTrialBalance(CancellationToken ct)
        {
            var result = await _integrityService.CheckTrialBalanceAsync(ct);
            return FromResult(result);
        }

        /// <summary>Verifies each posted journal entry has balanced DR/CR.</summary>
        [HttpGet("journal-balances")]
        public async Task<IActionResult> CheckJournalBalances(CancellationToken ct)
        {
            var result = await _integrityService.CheckJournalBalancesAsync(ct);
            return FromResult(result);
        }

        /// <summary>Verifies warehouse quantities match sum of inventory movements.</summary>
        [HttpGet("inventory-reconciliation")]
        public async Task<IActionResult> CheckInventoryReconciliation(CancellationToken ct)
        {
            var result = await _integrityService.CheckInventoryReconciliationAsync(ct);
            return FromResult(result);
        }

        /// <summary>Runs all 3 integrity checks and returns a combined report.</summary>
        [HttpGet("full")]
        public async Task<IActionResult> RunFullCheck(CancellationToken ct)
        {
            var result = await _integrityService.RunFullCheckAsync(ct);
            return FromResult(result);
        }
    }
}
