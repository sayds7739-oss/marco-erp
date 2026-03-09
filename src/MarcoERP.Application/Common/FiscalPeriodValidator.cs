using System;
using System.Threading;
using System.Threading.Tasks;
using MarcoERP.Application.Interfaces;
using MarcoERP.Domain.Entities.Accounting;
using MarcoERP.Domain.Enums;
using MarcoERP.Domain.Interfaces;
using MarcoERP.Domain.Interfaces.Settings;

namespace MarcoERP.Application.Common
{
    // ── Shared context types (replace per-service PostingContext / CancelContext) ──

    /// <summary>
    /// Result of fiscal-period posting validation.
    /// Replaces PurchaseInvoicePostingContext, CashReceiptPostingContext, SalesReturnPostingContext, etc.
    /// </summary>
    public sealed class PostingContext
    {
        public FiscalYear FiscalYear { get; init; } = default!;
        public FiscalPeriod Period { get; init; } = default!;
        public DateTime Now { get; init; }
        public string Username { get; init; } = string.Empty;
    }

    /// <summary>
    /// Result of fiscal-period cancel/reversal validation.
    /// Replaces PurchaseInvoiceCancelContext, CashReceiptCancelContext, SalesReturnCancelContext, etc.
    /// </summary>
    public sealed class CancelContext
    {
        public FiscalYear FiscalYear { get; init; } = default!;
        public FiscalPeriod Period { get; init; } = default!;
        public DateTime Today { get; init; }
    }

    /// <summary>
    /// Centralises the fiscal-period validation logic that was duplicated
    /// across 7+ posting/cancel methods in Purchase / Sales / Treasury services.
    /// <para>
    /// <b>Posting path</b> (7 services):
    /// Production-mode backdating guard → active fiscal year → date-within-year → period open.
    /// </para>
    /// <para>
    /// <b>Cancel path</b> (6 services):
    /// Fiscal year by calendar year → year active → period open.
    /// </para>
    /// </summary>
    public sealed class FiscalPeriodValidator
    {
        private readonly IFiscalYearRepository _fiscalYearRepo;
        private readonly ISystemSettingRepository _systemSettingRepository;
        private readonly IDateTimeProvider _dateTime;
        private readonly ICurrentUserService _currentUser;

        public FiscalPeriodValidator(
            IFiscalYearRepository fiscalYearRepo,
            ISystemSettingRepository systemSettingRepository,
            IDateTimeProvider dateTime,
            ICurrentUserService currentUser)
        {
            _fiscalYearRepo = fiscalYearRepo ?? throw new ArgumentNullException(nameof(fiscalYearRepo));
            _systemSettingRepository = systemSettingRepository ?? throw new ArgumentNullException(nameof(systemSettingRepository));
            _dateTime = dateTime ?? throw new ArgumentNullException(nameof(dateTime));
            _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
        }

        /// <summary>
        /// Full posting validation: production-mode backdating guard →
        /// active fiscal year → date containment → period open.
        /// </summary>
        public async Task<PostingContext> ValidateForPostingAsync(
            DateTime documentDate, CancellationToken ct = default)
        {
            // Step 1: Production-mode backdating guard
            var isProductionMode = await ProductionHardening.IsProductionModeAsync(_systemSettingRepository, ct);
            if (isProductionMode && ProductionHardening.IsBackdated(documentDate, _dateTime.Today))
                throw new InvalidOperationException("وضع الإنتاج يمنع الترحيل بتاريخ سابق.");

            // Step 2: Active fiscal year lookup
            var fiscalYear = await _fiscalYearRepo.GetActiveYearAsync(ct);
            if (fiscalYear == null)
                throw new InvalidOperationException("لا توجد سنة مالية نشطة.");

            // Step 3: Date-within-year check
            if (!fiscalYear.ContainsDate(documentDate))
                throw new InvalidOperationException(
                    $"تاريخ المستند {documentDate:yyyy-MM-dd} لا يقع ضمن السنة المالية النشطة.");

            // Step 4: Load periods + resolve month
            var yearWithPeriods = await _fiscalYearRepo.GetWithPeriodsAsync(fiscalYear.Id, ct);
            var period = yearWithPeriods.GetPeriod(documentDate.Month);
            if (period == null)
                throw new InvalidOperationException("لا توجد فترة مالية للشهر المحدد.");

            // Step 5: Period-open guard
            if (!period.IsOpen)
                throw new InvalidOperationException(
                    $"الفترة المالية ({period.Year}-{period.Month:D2}) مقفلة. لا يمكن الترحيل.");

            return new PostingContext
            {
                FiscalYear = yearWithPeriods,
                Period = period,
                Now = _dateTime.UtcNow,
                Username = _currentUser.Username ?? "System"
            };
        }

        /// <summary>
        /// Simplified POS posting validation (no production-mode check).
        /// </summary>
        public async Task<PostingContext> ValidateForPosPostingAsync(
            DateTime today, CancellationToken ct = default)
        {
            var fiscalYear = await _fiscalYearRepo.GetActiveYearAsync(ct);
            if (fiscalYear == null)
                throw new InvalidOperationException("لا توجد سنة مالية نشطة.");

            // Date-within-year check (prevent POS sales posting into wrong fiscal year)
            if (!fiscalYear.ContainsDate(today))
                throw new InvalidOperationException(
                    $"تاريخ المستند {today:yyyy-MM-dd} لا يقع ضمن السنة المالية النشطة.");

            var yearWithPeriods = await _fiscalYearRepo.GetWithPeriodsAsync(fiscalYear.Id, ct);
            var period = yearWithPeriods.GetPeriod(today.Month);
            if (period == null || !period.IsOpen)
                throw new InvalidOperationException("الفترة المالية مقفلة — لا يمكن إجراء عمليات بيع.");

            return new PostingContext
            {
                FiscalYear = yearWithPeriods,
                Period = period,
                Now = _dateTime.UtcNow,
                Username = _currentUser.Username ?? "POS"
            };
        }

        /// <summary>
        /// Cancel/reversal validation: fiscal year by calendar year →
        /// year active status → period open.
        /// </summary>
        public async Task<CancelContext> ValidateForCancelAsync(
            DateTime documentDate, CancellationToken ct = default)
        {
            var fiscalYear = await _fiscalYearRepo.GetByYearAsync(documentDate.Year, ct);
            if (fiscalYear == null)
                throw new InvalidOperationException($"لا توجد سنة مالية للعام {documentDate.Year}.");

            fiscalYear = await _fiscalYearRepo.GetWithPeriodsAsync(fiscalYear.Id, ct);
            if (fiscalYear.Status != FiscalYearStatus.Active)
                throw new InvalidOperationException($"السنة المالية {fiscalYear.Year} ليست فعّالة.");

            var period = fiscalYear.GetPeriod(documentDate.Month);
            if (period == null || !period.IsOpen)
                throw new InvalidOperationException($"الفترة المالية لـ {documentDate:yyyy-MM} مُقفلة.");

            return new CancelContext
            {
                FiscalYear = fiscalYear,
                Period = period,
                Today = documentDate
            };
        }
    }
}
