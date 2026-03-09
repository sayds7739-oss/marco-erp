using MarcoERP.Application.DTOs.Accounting;
using MarcoERP.Application.Interfaces.Accounting;
using Microsoft.AspNetCore.Mvc;

namespace MarcoERP.API.Controllers.Accounting
{
    /// <summary>
    /// API controller for fiscal year and period management.
    /// </summary>
    public class FiscalYearsController : ApiControllerBase
    {
        private readonly IFiscalYearService _fiscalYearService;

        public FiscalYearsController(IFiscalYearService fiscalYearService)
        {
            _fiscalYearService = fiscalYearService;
        }

        // ── Queries ─────────────────────────────────────────────

        /// <summary>Gets all fiscal years.</summary>
        [HttpGet]
        public async Task<IActionResult> GetAll(CancellationToken ct)
        {
            var result = await _fiscalYearService.GetAllAsync(ct);
            return FromResult(result);
        }

        /// <summary>Gets a fiscal year by ID with all 12 periods.</summary>
        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id, CancellationToken ct)
        {
            var result = await _fiscalYearService.GetByIdAsync(id, ct);
            return FromResult(result);
        }

        /// <summary>Gets a fiscal year by calendar year number.</summary>
        [HttpGet("year/{year:int}")]
        public async Task<IActionResult> GetByYear(int year, CancellationToken ct)
        {
            var result = await _fiscalYearService.GetByYearAsync(year, ct);
            return FromResult(result);
        }

        /// <summary>Gets the currently active fiscal year.</summary>
        [HttpGet("active")]
        public async Task<IActionResult> GetActive(CancellationToken ct)
        {
            var result = await _fiscalYearService.GetActiveYearAsync(ct);
            return FromResult(result);
        }

        // ── Fiscal Year Commands ────────────────────────────────

        /// <summary>Creates a new fiscal year with 12 monthly periods.</summary>
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateFiscalYearDto dto, CancellationToken ct)
        {
            var result = await _fiscalYearService.CreateAsync(dto, ct);
            return FromResult(result);
        }

        /// <summary>Activates a fiscal year.</summary>
        [HttpPatch("{id:int}/activate")]
        public async Task<IActionResult> Activate(int id, CancellationToken ct)
        {
            var result = await _fiscalYearService.ActivateAsync(id, ct);
            return FromResult(result);
        }

        /// <summary>Closes a fiscal year (irreversible).</summary>
        [HttpPatch("{id:int}/close")]
        public async Task<IActionResult> Close(int id, CancellationToken ct)
        {
            var result = await _fiscalYearService.CloseAsync(id, ct);
            return FromResult(result);
        }

        // ── Period Commands ─────────────────────────────────────

        /// <summary>Locks a fiscal period.</summary>
        [HttpPatch("periods/{periodId:int}/lock")]
        public async Task<IActionResult> LockPeriod(int periodId, CancellationToken ct)
        {
            var result = await _fiscalYearService.LockPeriodAsync(periodId, ct);
            return FromResult(result);
        }

        /// <summary>Unlocks the most recent locked period (admin-only, requires reason).</summary>
        [HttpPatch("periods/{periodId:int}/unlock")]
        public async Task<IActionResult> UnlockPeriod(
            int periodId,
            [FromQuery] string reason,
            CancellationToken ct)
        {
            var result = await _fiscalYearService.UnlockPeriodAsync(periodId, reason, ct);
            return FromResult(result);
        }
    }
}
