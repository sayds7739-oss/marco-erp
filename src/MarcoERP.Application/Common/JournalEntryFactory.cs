using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MarcoERP.Domain.Entities.Accounting;
using MarcoERP.Domain.Entities.Accounting.Policies;
using MarcoERP.Domain.Enums;
using MarcoERP.Domain.Interfaces;

namespace MarcoERP.Application.Common
{
    /// <summary>
    /// Specification for a single journal entry debit/credit line.
    /// </summary>
    public sealed record JournalLineSpec(
        int AccountId,
        decimal Debit,
        decimal Credit,
        string Description,
        int? CostCenterId = null,
        int? WarehouseId = null);

    /// <summary>
    /// Centralises the Create → AddLines → NextNumber → Post → Save pipeline
    /// that was duplicated across 13 posting services.
    /// </summary>
    public sealed class JournalEntryFactory
    {
        private readonly IJournalEntryRepository _journalRepo;
        private readonly IJournalNumberGenerator _journalNumberGen;

        public JournalEntryFactory(
            IJournalEntryRepository journalRepo,
            IJournalNumberGenerator journalNumberGen)
        {
            _journalRepo = journalRepo ?? throw new ArgumentNullException(nameof(journalRepo));
            _journalNumberGen = journalNumberGen ?? throw new ArgumentNullException(nameof(journalNumberGen));
        }

        /// <summary>
        /// Creates a journal entry, adds the given lines, generates a sequential number,
        /// posts it, and persists it to the repository — all in one call.
        /// </summary>
        /// <param name="journalDate">The date of the journal entry.</param>
        /// <param name="description">Arabic description for the journal header.</param>
        /// <param name="sourceType">The originating document type.</param>
        /// <param name="fiscalYearId">Active fiscal year Id.</param>
        /// <param name="fiscalPeriodId">Active fiscal period Id.</param>
        /// <param name="lines">Debit/credit line specifications.</param>
        /// <param name="username">The user performing the posting.</param>
        /// <param name="now">Current UTC timestamp.</param>
        /// <param name="referenceNumber">Optional reference number (invoice#, voucher#, etc.).</param>
        /// <param name="sourceId">Optional source entity Id.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>The persisted, posted <see cref="JournalEntry"/>.</returns>
        public async Task<JournalEntry> CreateAndPostAsync(
            DateTime journalDate,
            string description,
            SourceType sourceType,
            int fiscalYearId,
            int fiscalPeriodId,
            IEnumerable<JournalLineSpec> lines,
            string username,
            DateTime now,
            string referenceNumber = null,
            int? sourceId = null,
            CancellationToken ct = default)
        {
            var journal = JournalEntry.CreateDraft(
                journalDate,
                description,
                sourceType,
                fiscalYearId,
                fiscalPeriodId,
                referenceNumber,
                sourceId: sourceId);

            foreach (var line in lines)
            {
                journal.AddLine(
                    line.AccountId,
                    line.Debit,
                    line.Credit,
                    now,
                    line.Description,
                    line.CostCenterId,
                    line.WarehouseId);
            }

            var journalNumber = await _journalNumberGen.NextNumberAsync(fiscalYearId, ct);
            journal.Post(journalNumber, username, now);

            await _journalRepo.AddAsync(journal, ct);
            return journal;
        }
    }
}
