using MarcoERP.Application.DTOs.Accounting;
using MarcoERP.Application.Interfaces.Accounting;
using MarcoERP.Domain.Enums;
using Microsoft.AspNetCore.Mvc;

namespace MarcoERP.API.Controllers.Accounting
{
    /// <summary>
    /// API controller for journal entry management.
    /// </summary>
    public class JournalEntriesController : ApiControllerBase
    {
        private readonly IJournalEntryService _journalEntryService;

        public JournalEntriesController(IJournalEntryService journalEntryService)
        {
            _journalEntryService = journalEntryService;
        }

        // ── Queries ─────────────────────────────────────────────

        /// <summary>Gets a journal entry by ID with all lines.</summary>
        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id, CancellationToken ct)
        {
            var result = await _journalEntryService.GetByIdAsync(id, ct);
            return FromResult(result);
        }

        /// <summary>Gets journal entries for a fiscal period.</summary>
        [HttpGet("period/{periodId:int}")]
        public async Task<IActionResult> GetByPeriod(int periodId, CancellationToken ct)
        {
            var result = await _journalEntryService.GetByPeriodAsync(periodId, ct);
            return FromResult(result);
        }

        /// <summary>Gets journal entries by status.</summary>
        [HttpGet("status/{status}")]
        public async Task<IActionResult> GetByStatus(JournalEntryStatus status, CancellationToken ct)
        {
            var result = await _journalEntryService.GetByStatusAsync(status, ct);
            return FromResult(result);
        }

        /// <summary>Gets draft journal entries for a fiscal year.</summary>
        [HttpGet("year/{yearId:int}/drafts")]
        public async Task<IActionResult> GetDraftsByYear(int yearId, CancellationToken ct)
        {
            var result = await _journalEntryService.GetDraftsByYearAsync(yearId, ct);
            return FromResult(result);
        }

        /// <summary>Gets journal entries within a date range.</summary>
        [HttpGet("date-range")]
        public async Task<IActionResult> GetByDateRange(
            [FromQuery] DateTime start,
            [FromQuery] DateTime end,
            CancellationToken ct)
        {
            var result = await _journalEntryService.GetByDateRangeAsync(start, end, ct);
            return FromResult(result);
        }

        // ── Commands ────────────────────────────────────────────

        /// <summary>Creates a new draft journal entry.</summary>
        [HttpPost]
        public async Task<IActionResult> CreateDraft([FromBody] CreateJournalEntryDto dto, CancellationToken ct)
        {
            var result = await _journalEntryService.CreateDraftAsync(dto, ct);
            return FromResult(result);
        }

        /// <summary>Posts a draft journal entry.</summary>
        [HttpPatch("{id:int}/post")]
        public async Task<IActionResult> Post(int id, CancellationToken ct)
        {
            var result = await _journalEntryService.PostAsync(id, ct);
            return FromResult(result);
        }

        /// <summary>Reverses a posted journal entry.</summary>
        [HttpPost("{id:int}/reverse")]
        public async Task<IActionResult> Reverse(int id, [FromBody] ReverseJournalEntryDto dto, CancellationToken ct)
        {
            if (dto == null)
            {
                return BadRequest("بيانات عكس القيد مطلوبة.");
            }

            // The route id is canonical; never trust a conflicting body id.
            dto.JournalEntryId = id;

            var result = await _journalEntryService.ReverseAsync(dto, ct);
            return FromResult(result);
        }

        /// <summary>Soft-deletes a draft journal entry.</summary>
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id, CancellationToken ct)
        {
            var result = await _journalEntryService.DeleteDraftAsync(id, ct);
            return FromResult(result);
        }
    }
}
