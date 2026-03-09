using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MarcoERP.Application.Common;
using MarcoERP.Application.DTOs.Accounting;
using MarcoERP.Domain.Enums;

namespace MarcoERP.Application.Interfaces.Accounting
{
    /// <summary>
    /// Application service for journal entry management.
    /// Implements the 15-step posting workflow (Section 3.5).
    /// Application-layer validates JE-INV-07 through JE-INV-10.
    /// </summary>
    public interface IJournalEntryService
    {
        // ── Queries ─────────────────────────────────────────────

        /// <summary>Gets a journal entry by ID with all lines.</summary>
        Task<ServiceResult<JournalEntryDto>> GetByIdAsync(int id, CancellationToken cancellationToken = default);

        /// <summary>Gets journal entries for a fiscal period.</summary>
        Task<ServiceResult<IReadOnlyList<JournalEntryDto>>> GetByPeriodAsync(int fiscalPeriodId, CancellationToken cancellationToken = default);

        /// <summary>Gets journal entries by status.</summary>
        Task<ServiceResult<IReadOnlyList<JournalEntryDto>>> GetByStatusAsync(JournalEntryStatus status, CancellationToken cancellationToken = default);

        /// <summary>Gets draft journal entries for a fiscal year.</summary>
        Task<ServiceResult<IReadOnlyList<JournalEntryDto>>> GetDraftsByYearAsync(int fiscalYearId, CancellationToken cancellationToken = default);

        /// <summary>Gets journal entries within a date range.</summary>
        Task<ServiceResult<IReadOnlyList<JournalEntryDto>>> GetByDateRangeAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);

        // ── Commands ────────────────────────────────────────────

        /// <summary>
        /// Creates a new draft journal entry.
        /// Resolves fiscal year/period from JournalDate.
        /// Validates all accounts are postable (JE-INV-06).
        /// </summary>
        [RequiresPermission(PermissionKeys.JournalCreate)]
        Task<ServiceResult<JournalEntryDto>> CreateDraftAsync(CreateJournalEntryDto dto, CancellationToken cancellationToken = default);

        /// <summary>
        /// Posts a draft journal entry (15-step workflow).
        /// Validates JE-INV-07 through JE-INV-10 (period open, year active, date in range).
        /// Generates sequential JournalNumber.
        /// </summary>
        [RequiresPermission(PermissionKeys.JournalPost)]
        Task<ServiceResult<PostJournalResultDto>> PostAsync(int journalEntryId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Reverses a posted journal entry.
        /// Creates a new reversal entry with swapped debit/credit.
        /// Both entries are persisted atomically.
        /// </summary>
        [RequiresPermission(PermissionKeys.JournalReverse)]
        Task<ServiceResult<PostJournalResultDto>> ReverseAsync(ReverseJournalEntryDto dto, CancellationToken cancellationToken = default);

        /// <summary>
        /// Soft-deletes a draft journal entry.
        /// Only drafts can be deleted.
        /// </summary>
        [RequiresPermission(PermissionKeys.JournalCreate)]
        Task<ServiceResult> DeleteDraftAsync(int journalEntryId, CancellationToken cancellationToken = default);
    }
}
