using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MarcoERP.Application.Common;
using MarcoERP.Application.DTOs.Sales;
using MarcoERP.Application.Mappers.Sales;
using Microsoft.Extensions.Logging;
using MarcoERP.Domain.Entities.Sales;
using MarcoERP.Domain.Enums;
using MarcoERP.Domain.Exceptions.Sales;

namespace MarcoERP.Application.Services.Sales
{
    public sealed partial class PosService
    {
        // ══════════════════════════════════════════════════════════
        //  SESSION MANAGEMENT
        // ══════════════════════════════════════════════════════════

        public async Task<ServiceResult<PosSessionDto>> OpenSessionAsync(OpenPosSessionDto dto, CancellationToken ct = default)
        {
            _logger.LogInformation("Operation={Operation} Entity={Entity} EntityId={EntityId}", "OpenSessionAsync", "PosSession", 0);

            var vr = await _openSessionValidator.ValidateAsync(dto, ct);
            if (!vr.IsValid)
                return ServiceResult<PosSessionDto>.Failure(string.Join(" | ", vr.Errors.Select(e => e.ErrorMessage)));

            var userId = _currentUser.UserId;
            if (userId <= 0)
                return ServiceResult<PosSessionDto>.Failure("لم يتم تحديد المستخدم الحالي.");

            // Check if user already has an open session
            if (await _sessionRepo.HasOpenSessionAsync(userId, ct))
                return ServiceResult<PosSessionDto>.Failure("لديك جلسة مفتوحة بالفعل. أغلقها أولاً.");

            var sessionNumber = await _sessionRepo.GetNextSessionNumberAsync(ct);

            var session = new PosSession(
                sessionNumber,
                userId,
                dto.CashboxId,
                dto.WarehouseId,
                dto.OpeningBalance,
                _dateTime.UtcNow);

            await _sessionRepo.AddAsync(session, ct);
            await _unitOfWork.SaveChangesAsync(ct);

            var saved = await _sessionRepo.GetByIdAsync(session.Id, ct);
            return ServiceResult<PosSessionDto>.Success(PosMapper.ToSessionDto(saved));
        }

        public async Task<ServiceResult<PosSessionDto>> GetCurrentSessionAsync(CancellationToken ct = default)
        {
            var userId = _currentUser.UserId;
            if (userId <= 0)
                return ServiceResult<PosSessionDto>.Failure("لم يتم تحديد المستخدم الحالي.");

            var session = await _sessionRepo.GetOpenSessionByUserAsync(userId, ct);
            if (session == null)
                return ServiceResult<PosSessionDto>.Failure("لا توجد جلسة مفتوحة.");

            return ServiceResult<PosSessionDto>.Success(PosMapper.ToSessionDto(session));
        }

        public async Task<ServiceResult<PosSessionDto>> CloseSessionAsync(ClosePosSessionDto dto, CancellationToken ct = default)
        {
            _logger.LogInformation("Operation={Operation} Entity={Entity} EntityId={EntityId}", "CloseSessionAsync", "PosSession", dto.SessionId);

            var vr = await _closeSessionValidator.ValidateAsync(dto, ct);
            if (!vr.IsValid)
                return ServiceResult<PosSessionDto>.Failure(string.Join(" | ", vr.Errors.Select(e => e.ErrorMessage)));

            var session = await _sessionRepo.GetByIdAsync(dto.SessionId, ct);
            if (session == null)
                return ServiceResult<PosSessionDto>.Failure("الجلسة غير موجودة.");

            if (!session.IsOpen)
                return ServiceResult<PosSessionDto>.Failure("الجلسة مغلقة بالفعل.");

            try
            {
                session.Close(dto.ActualClosingBalance, dto.Notes, _dateTime.UtcNow);

                // POS-03: Post Over/Short journal when variance is non-zero
                await PostOverShortJournalIfNeededAsync(session, ct);

                _sessionRepo.Update(session);
                await _unitOfWork.SaveChangesAsync(ct);

                return ServiceResult<PosSessionDto>.Success(PosMapper.ToSessionDto(session));
            }
            catch (SalesInvoiceDomainException ex)
            {
                return ServiceResult<PosSessionDto>.Failure(ex.Message);
            }
        }

        public async Task<ServiceResult<PosSessionDto>> GetSessionByIdAsync(int id, CancellationToken ct = default)
        {
            var session = await _sessionRepo.GetWithPaymentsAsync(id, ct);
            if (session == null)
                return ServiceResult<PosSessionDto>.Failure("الجلسة غير موجودة.");

            return ServiceResult<PosSessionDto>.Success(PosMapper.ToSessionDto(session));
        }

        public async Task<ServiceResult<IReadOnlyList<PosSessionListDto>>> GetAllSessionsAsync(CancellationToken ct = default)
        {
            var sessions = await _sessionRepo.GetAllAsync(ct);
            return ServiceResult<IReadOnlyList<PosSessionListDto>>.Success(
                sessions.Select(PosMapper.ToSessionListDto).ToList());
        }

        // ══════════════════════════════════════════════════════════
        //  OVER / SHORT JOURNAL POSTING (POS-03)
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Creates a journal entry for the cash over/short variance when closing a POS session.
        /// Over (Variance &gt; 0): DR Cashbox Account / CR CashOverShort (income).
        /// Short (Variance &lt; 0): DR CashOverShort (expense) / CR Cashbox Account.
        /// Non-fatal: logs and continues if accounts are missing or fiscal period is closed.
        /// </summary>
        private async Task PostOverShortJournalIfNeededAsync(PosSession session, CancellationToken ct)
        {
            if (session.Variance == 0)
                return;

            try
            {
                var today = _dateTime.Today;
                var journalContext = await _fiscalValidator.ValidateForPosPostingAsync(today, ct);

                var cashAccount = await _accountRepo.GetByCodeAsync(CashAccountCode, ct);
                var overShortAccount = await _accountRepo.GetByCodeAsync(CashOverShortAccountCode, ct);

                if (cashAccount == null || overShortAccount == null)
                {
                    _logger.LogWarning(
                        "POS-03: Cannot post Over/Short journal for session {SessionNumber} — " +
                        "system accounts not found (Cash={CashFound}, OverShort={OverShortFound}).",
                        session.SessionNumber,
                        cashAccount != null,
                        overShortAccount != null);
                    return;
                }

                var absVariance = Math.Abs(session.Variance);
                var lines = new List<JournalLineSpec>();

                if (session.Variance > 0)
                {
                    // OVER: actual cash > expected → surplus
                    // DR Cashbox (increase asset)  /  CR CashOverShort (income)
                    lines.Add(new JournalLineSpec(cashAccount.Id, absVariance, 0,
                        $"فائض صندوق — جلسة {session.SessionNumber}"));
                    lines.Add(new JournalLineSpec(overShortAccount.Id, 0, absVariance,
                        $"فروقات الصندوق — جلسة {session.SessionNumber}"));
                }
                else
                {
                    // SHORT: actual cash < expected → deficit
                    // DR CashOverShort (expense)  /  CR Cashbox (decrease asset)
                    lines.Add(new JournalLineSpec(overShortAccount.Id, absVariance, 0,
                        $"عجز صندوق — جلسة {session.SessionNumber}"));
                    lines.Add(new JournalLineSpec(cashAccount.Id, 0, absVariance,
                        $"فروقات الصندوق — جلسة {session.SessionNumber}"));
                }

                await _journalFactory.CreateAndPostAsync(
                    today,
                    $"فروقات صندوق — إقفال جلسة {session.SessionNumber}",
                    SourceType.PosSession,
                    journalContext.FiscalYear.Id,
                    journalContext.Period.Id,
                    lines,
                    journalContext.Username,
                    journalContext.Now,
                    referenceNumber: session.SessionNumber,
                    sourceId: session.Id,
                    ct: ct);

                _logger.LogInformation(
                    "POS-03: Over/Short journal posted for session {SessionNumber}, Variance={Variance:N4}.",
                    session.SessionNumber,
                    session.Variance);
            }
            catch (Exception ex)
            {
                // Non-fatal: session close must succeed even if over/short posting fails
                // (e.g. fiscal period closed, accounts missing, etc.)
                _logger.LogWarning(ex,
                    "POS-03: Failed to post Over/Short journal for session {SessionNumber}. " +
                    "Variance={Variance:N4}. Session close will proceed without journal.",
                    session.SessionNumber,
                    session.Variance);
            }
        }
    }
}
