using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MarcoERP.Application.Common;
using MarcoERP.Application.DTOs.Accounting;
using MarcoERP.Application.Interfaces;
using MarcoERP.Application.Interfaces.Accounting;
using MarcoERP.Application.Interfaces.Settings;
using MarcoERP.Domain.Entities.Accounting;
using MarcoERP.Domain.Entities.Accounting.Policies;
using MarcoERP.Domain.Entities.Inventory;
using MarcoERP.Domain.Enums;
using MarcoERP.Domain.Exceptions;
using MarcoERP.Domain.Exceptions.Accounting;
using MarcoERP.Domain.Interfaces;
using MarcoERP.Domain.Interfaces.Inventory;
using MarcoERP.Domain.Interfaces.Sales;
using MarcoERP.Domain.Interfaces.Purchases;
using MarcoERP.Domain.Interfaces.Treasury;
using FluentValidation;
using Microsoft.Extensions.Logging;

namespace MarcoERP.Application.Services.Accounting
{
    /// <summary>
    /// خدمة الأرصدة الافتتاحية — تدير دورة حياة مستند الأرصدة الافتتاحية.
    /// الإنشاء → التعديل → الترحيل → (حذف مسودة اختياري).
    /// عند الترحيل:
    ///   1. ينشئ قيد يومية تلقائي بنوع SourceType.Opening
    ///   2. يحدّث أرصدة العملاء (PreviousBalance)
    ///   3. يحدّث أرصدة الموردين (PreviousBalance)
    ///   4. يحدّث أرصدة المخزون (WarehouseProduct.SetOpeningBalance + InventoryMovement)
    ///   5. يحدّث أرصدة الصناديق (Cashbox.Balance)
    ///   6. لا يعدّل رصيد البنك (لأن BankAccount لا يحتوي على Balance property)
    /// </summary>
    [Module(SystemModule.Accounting)]
    public sealed class OpeningBalanceService : IOpeningBalanceService
    {
        // ── Dependencies ────────────────────────────────────────
        private readonly IOpeningBalanceRepository _openingBalanceRepo;
        private readonly IFiscalYearRepository _fiscalYearRepo;
        private readonly IAccountRepository _accountRepo;
        private readonly IJournalEntryRepository _journalRepo;
        private readonly IJournalNumberGenerator _journalNumberGen;
        private readonly ICustomerRepository _customerRepo;
        private readonly ISupplierRepository _supplierRepo;
        private readonly IWarehouseProductRepository _whProductRepo;
        private readonly IInventoryMovementRepository _movementRepo;
        private readonly IWarehouseRepository _warehouseRepo;
        private readonly IProductRepository _productRepo;
        private readonly ICashboxRepository _cashboxRepo;
        private readonly IBankAccountRepository _bankAccountRepo;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ICurrentUserService _currentUser;
        private readonly IDateTimeProvider _dateTime;
        private readonly IAuditLogger _auditLogger;
        private readonly IValidator<CreateOpeningBalanceDto> _createValidator;
        private readonly IValidator<UpdateOpeningBalanceDto> _updateValidator;
        private readonly ILogger<OpeningBalanceService> _logger;
        private readonly IFeatureService _featureService;

        public OpeningBalanceService(
            IOpeningBalanceRepository openingBalanceRepo,
            IFiscalYearRepository fiscalYearRepo,
            IAccountRepository accountRepo,
            IJournalEntryRepository journalRepo,
            IJournalNumberGenerator journalNumberGen,
            ICustomerRepository customerRepo,
            ISupplierRepository supplierRepo,
            IWarehouseProductRepository whProductRepo,
            IInventoryMovementRepository movementRepo,
            IWarehouseRepository warehouseRepo,
            IProductRepository productRepo,
            ICashboxRepository cashboxRepo,
            IBankAccountRepository bankAccountRepo,
            IUnitOfWork unitOfWork,
            ICurrentUserService currentUser,
            IDateTimeProvider dateTime,
            IAuditLogger auditLogger,
            IValidator<CreateOpeningBalanceDto> createValidator,
            IValidator<UpdateOpeningBalanceDto> updateValidator,
            ILogger<OpeningBalanceService> logger = null,
            IFeatureService featureService = null)
        {
            _openingBalanceRepo = openingBalanceRepo ?? throw new ArgumentNullException(nameof(openingBalanceRepo));
            _fiscalYearRepo = fiscalYearRepo ?? throw new ArgumentNullException(nameof(fiscalYearRepo));
            _accountRepo = accountRepo ?? throw new ArgumentNullException(nameof(accountRepo));
            _journalRepo = journalRepo ?? throw new ArgumentNullException(nameof(journalRepo));
            _journalNumberGen = journalNumberGen ?? throw new ArgumentNullException(nameof(journalNumberGen));
            _customerRepo = customerRepo ?? throw new ArgumentNullException(nameof(customerRepo));
            _supplierRepo = supplierRepo ?? throw new ArgumentNullException(nameof(supplierRepo));
            _whProductRepo = whProductRepo ?? throw new ArgumentNullException(nameof(whProductRepo));
            _movementRepo = movementRepo ?? throw new ArgumentNullException(nameof(movementRepo));
            _warehouseRepo = warehouseRepo ?? throw new ArgumentNullException(nameof(warehouseRepo));
            _productRepo = productRepo ?? throw new ArgumentNullException(nameof(productRepo));
            _cashboxRepo = cashboxRepo ?? throw new ArgumentNullException(nameof(cashboxRepo));
            _bankAccountRepo = bankAccountRepo ?? throw new ArgumentNullException(nameof(bankAccountRepo));
            _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
            _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
            _dateTime = dateTime ?? throw new ArgumentNullException(nameof(dateTime));
            _auditLogger = auditLogger ?? throw new ArgumentNullException(nameof(auditLogger));
            _createValidator = createValidator ?? throw new ArgumentNullException(nameof(createValidator));
            _updateValidator = updateValidator ?? throw new ArgumentNullException(nameof(updateValidator));
            _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<OpeningBalanceService>.Instance;
            _featureService = featureService;
        }

        // ════════════════════════════════════════════════════════
        //  Queries
        // ════════════════════════════════════════════════════════

        public async Task<ServiceResult<IReadOnlyList<OpeningBalanceListDto>>> GetAllAsync(CancellationToken ct = default)
        {
            var entities = await _openingBalanceRepo.GetAllAsync(ct);

            var dtos = entities.Select(e => new OpeningBalanceListDto
            {
                Id = e.Id,
                FiscalYearId = e.FiscalYearId,
                FiscalYear = e.FiscalYear?.Year ?? 0,
                BalanceDate = e.BalanceDate,
                Status = e.Status,
                StatusText = GetStatusText(e.Status),
                TotalDebit = e.TotalDebit,
                TotalCredit = e.TotalCredit,
                Difference = e.TotalDebit - e.TotalCredit,
                LineCount = e.Lines?.Count ?? 0,
                PostedBy = e.PostedBy,
                PostedAt = e.PostedAt
            }).ToList();

            return ServiceResult<IReadOnlyList<OpeningBalanceListDto>>.Success(dtos);
        }

        public async Task<ServiceResult<OpeningBalanceDto>> GetByIdAsync(int id, CancellationToken ct = default)
        {
            var entity = await _openingBalanceRepo.GetWithLinesAsync(id, ct);
            if (entity == null)
                return ServiceResult<OpeningBalanceDto>.Failure("الأرصدة الافتتاحية غير موجودة.");

            var dto = await MapToDetailDtoAsync(entity, ct);
            return ServiceResult<OpeningBalanceDto>.Success(dto);
        }

        public async Task<ServiceResult<OpeningBalanceDto>> GetByFiscalYearAsync(int fiscalYearId, CancellationToken ct = default)
        {
            var entity = await _openingBalanceRepo.GetByFiscalYearWithLinesAsync(fiscalYearId, ct);
            if (entity == null)
                return ServiceResult<OpeningBalanceDto>.Failure("لا توجد أرصدة افتتاحية لهذه السنة المالية.");

            var dto = await MapToDetailDtoAsync(entity, ct);
            return ServiceResult<OpeningBalanceDto>.Success(dto);
        }

        // ════════════════════════════════════════════════════════
        //  Commands
        // ════════════════════════════════════════════════════════

        public async Task<ServiceResult<OpeningBalanceDto>> CreateAsync(
            CreateOpeningBalanceDto dto, CancellationToken ct = default)
        {
            _logger.LogInformation("Operation={Operation} Entity={Entity} EntityId={EntityId}",
                "CreateAsync", "OpeningBalance", 0);

            // Feature Guard
            if (_featureService != null)
            {
                var guard = await FeatureGuard.CheckAsync<OpeningBalanceDto>(
                    _featureService, FeatureKeys.Accounting, ct);
                if (guard != null) return guard;
            }

            // DTO validation
            var validationResult = await _createValidator.ValidateAsync(dto, ct);
            if (!validationResult.IsValid)
                return ServiceResult<OpeningBalanceDto>.Failure(
                    validationResult.Errors.Select(e => e.ErrorMessage));

            // Check fiscal year exists
            var fiscalYear = await _fiscalYearRepo.GetWithPeriodsAsync(dto.FiscalYearId, ct);
            if (fiscalYear == null)
                return ServiceResult<OpeningBalanceDto>.Failure("السنة المالية غير موجودة.");

            // Fiscal year must be Active or Setup (for initial setup)
            if (fiscalYear.Status == FiscalYearStatus.Closed)
                return ServiceResult<OpeningBalanceDto>.Failure(
                    "لا يمكن إنشاء أرصدة افتتاحية لسنة مالية مقفلة.");

            // One opening balance per fiscal year
            if (await _openingBalanceRepo.ExistsForFiscalYearAsync(dto.FiscalYearId, ct))
                return ServiceResult<OpeningBalanceDto>.Failure(
                    "توجد أرصدة افتتاحية مسبقاً لهذه السنة المالية. يمكنك تعديلها.");

            // Balance date must be within fiscal year
            if (!fiscalYear.ContainsDate(dto.BalanceDate))
                return ServiceResult<OpeningBalanceDto>.Failure(
                    $"تاريخ الأرصدة الافتتاحية يجب أن يكون ضمن السنة المالية ({fiscalYear.StartDate:yyyy-MM-dd} - {fiscalYear.EndDate:yyyy-MM-dd}).");

            try
            {
                var entity = new OpeningBalance(dto.FiscalYearId, dto.BalanceDate, dto.Notes);

                // Add initial lines if provided
                if (dto.Lines != null && dto.Lines.Count > 0)
                {
                    var lineErrors = await AddLinesToEntityAsync(entity, dto.Lines, ct);
                    if (lineErrors.Count > 0)
                        return ServiceResult<OpeningBalanceDto>.Failure(lineErrors);
                }

                await _unitOfWork.ExecuteInTransactionAsync(async () =>
                {
                    await _openingBalanceRepo.AddAsync(entity, ct);
                    await _unitOfWork.SaveChangesAsync(ct);

                    await _auditLogger.LogAsync(
                        "OpeningBalance", entity.Id, "Created",
                        _currentUser.Username,
                        $"أرصدة افتتاحية للسنة المالية {fiscalYear.Year} — {entity.Lines.Count} بند.",
                        ct);
                    await _unitOfWork.SaveChangesAsync(ct);
                }, IsolationLevel.ReadCommitted, ct);

                var result = await _openingBalanceRepo.GetWithLinesAsync(entity.Id, ct);
                var resultDto = await MapToDetailDtoAsync(result, ct);
                return ServiceResult<OpeningBalanceDto>.Success(resultDto);
            }
            catch (OpeningBalanceDomainException ex)
            {
                return ServiceResult<OpeningBalanceDto>.Failure(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "CreateAsync failed for OpeningBalance.");
                return ServiceResult<OpeningBalanceDto>.Failure(
                    ErrorSanitizer.SanitizeGeneric(ex, "إنشاء الأرصدة الافتتاحية"));
            }
        }

        public async Task<ServiceResult<OpeningBalanceDto>> UpdateAsync(
            UpdateOpeningBalanceDto dto, CancellationToken ct = default)
        {
            _logger.LogInformation("Operation={Operation} Entity={Entity} EntityId={EntityId}",
                "UpdateAsync", "OpeningBalance", dto.Id);

            // DTO validation
            var validationResult = await _updateValidator.ValidateAsync(dto, ct);
            if (!validationResult.IsValid)
                return ServiceResult<OpeningBalanceDto>.Failure(
                    validationResult.Errors.Select(e => e.ErrorMessage));

            var entity = await _openingBalanceRepo.GetWithLinesTrackedAsync(dto.Id, ct);
            if (entity == null)
                return ServiceResult<OpeningBalanceDto>.Failure("الأرصدة الافتتاحية غير موجودة.");

            if (entity.Status != OpeningBalanceStatus.Draft)
                return ServiceResult<OpeningBalanceDto>.Failure("لا يمكن تعديل أرصدة افتتاحية مرحّلة.");

            // Validate balance date within fiscal year
            var fiscalYear = await _fiscalYearRepo.GetByIdAsync(entity.FiscalYearId, ct);
            if (fiscalYear != null && !fiscalYear.ContainsDate(dto.BalanceDate))
                return ServiceResult<OpeningBalanceDto>.Failure(
                    $"تاريخ الأرصدة الافتتاحية يجب أن يكون ضمن السنة المالية.");

            try
            {
                entity.UpdateDraft(dto.BalanceDate, dto.Notes);

                // Clear existing lines and re-add
                var existingLineIds = entity.Lines.Select(l => l.Id).ToList();
                foreach (var lineId in existingLineIds)
                    entity.RemoveLine(lineId);

                // Add new lines
                var lineErrors = await AddLinesToEntityAsync(entity, dto.Lines, ct);
                if (lineErrors.Count > 0)
                    return ServiceResult<OpeningBalanceDto>.Failure(lineErrors);

                await _unitOfWork.ExecuteInTransactionAsync(async () =>
                {
                    _openingBalanceRepo.Update(entity);
                    await _unitOfWork.SaveChangesAsync(ct);

                    await _auditLogger.LogAsync(
                        "OpeningBalance", entity.Id, "Updated",
                        _currentUser.Username,
                        $"تعديل الأرصدة الافتتاحية — {entity.Lines.Count} بند.",
                        ct);
                    await _unitOfWork.SaveChangesAsync(ct);
                }, IsolationLevel.ReadCommitted, ct);

                var result = await _openingBalanceRepo.GetWithLinesAsync(entity.Id, ct);
                var resultDto = await MapToDetailDtoAsync(result, ct);
                return ServiceResult<OpeningBalanceDto>.Success(resultDto);
            }
            catch (OpeningBalanceDomainException ex)
            {
                return ServiceResult<OpeningBalanceDto>.Failure(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "UpdateAsync failed for OpeningBalance.");
                return ServiceResult<OpeningBalanceDto>.Failure(
                    ErrorSanitizer.SanitizeGeneric(ex, "تعديل الأرصدة الافتتاحية"));
            }
        }

        public async Task<ServiceResult<OpeningBalanceDto>> PostAsync(int id, CancellationToken ct = default)
        {
            _logger.LogInformation("Operation={Operation} Entity={Entity} EntityId={EntityId}",
                "PostAsync", "OpeningBalance", id);

            var entity = await _openingBalanceRepo.GetWithLinesTrackedAsync(id, ct);
            if (entity == null)
                return ServiceResult<OpeningBalanceDto>.Failure("الأرصدة الافتتاحية غير موجودة.");

            if (entity.Status != OpeningBalanceStatus.Draft)
                return ServiceResult<OpeningBalanceDto>.Failure("الأرصدة الافتتاحية مرحّلة بالفعل.");

            if (!entity.Lines.Any())
                return ServiceResult<OpeningBalanceDto>.Failure("لا يمكن ترحيل أرصدة افتتاحية بدون بنود.");

            if (!entity.IsBalanced)
                return ServiceResult<OpeningBalanceDto>.Failure(
                    $"الأرصدة الافتتاحية غير متوازنة. الفرق: {entity.GetDifference():N4}. " +
                    "يجب أن يتساوى إجمالي المدين مع إجمالي الدائن.");

            // Validate fiscal year is Active
            var fiscalYear = await _fiscalYearRepo.GetWithPeriodsAsync(entity.FiscalYearId, ct);
            if (fiscalYear == null)
                return ServiceResult<OpeningBalanceDto>.Failure("السنة المالية غير موجودة.");
            if (fiscalYear.Status != FiscalYearStatus.Active)
                return ServiceResult<OpeningBalanceDto>.Failure(
                    "السنة المالية يجب أن تكون في حالة فعّالة للترحيل.");

            // Resolve period from balance date
            var period = fiscalYear.GetPeriod(entity.BalanceDate.Month);
            if (period == null)
                return ServiceResult<OpeningBalanceDto>.Failure("لا توجد فترة مالية للتاريخ المحدد.");
            if (!period.IsOpen)
                return ServiceResult<OpeningBalanceDto>.Failure(
                    $"الفترة المالية ({period.Year}-{period.Month:D2}) مقفلة.");

            // Validate all account IDs are postable
            var accountIds = entity.Lines.Select(l => l.AccountId).Distinct().ToList();
            var accountErrors = new List<string>();
            foreach (var accountId in accountIds)
            {
                var account = await _accountRepo.GetByIdAsync(accountId, ct);
                if (account == null)
                    accountErrors.Add($"الحساب رقم {accountId} غير موجود.");
                else if (!account.CanReceivePostings())
                    accountErrors.Add($"الحساب '{account.AccountCode} - {account.AccountNameAr}' لا يقبل القيود.");
            }
            if (accountErrors.Count > 0)
                return ServiceResult<OpeningBalanceDto>.Failure(accountErrors);

            try
            {
                var now = _dateTime.UtcNow;
                var username = _currentUser.Username ?? "System";

                await _unitOfWork.ExecuteInTransactionAsync(async () =>
                {
                    // ── Step 1: Create opening balance journal entry ──
                    var journal = JournalEntry.CreateOpeningBalanceDraft(
                        entity.BalanceDate,
                        $"أرصدة افتتاحية — السنة المالية {fiscalYear.Year}",
                        fiscalYear.Id,
                        period.Id);

                    // Aggregate lines by AccountId (combine debit/credit per account)
                    var aggregatedLines = entity.Lines
                        .GroupBy(l => l.AccountId)
                        .Select(g => new
                        {
                            AccountId = g.Key,
                            TotalDebit = g.Sum(l => l.DebitAmount),
                            TotalCredit = g.Sum(l => l.CreditAmount)
                        })
                        .Where(l => l.TotalDebit != 0 || l.TotalCredit != 0)
                        .ToList();

                    foreach (var jLine in aggregatedLines)
                    {
                        // Net debit/credit to avoid both being > 0 (which crashes JournalEntryLine.Create)
                        var net = jLine.TotalDebit - jLine.TotalCredit;
                        var debit = net >= 0 ? net : 0m;
                        var credit = net < 0 ? Math.Abs(net) : 0m;

                        journal.AddLine(
                            jLine.AccountId,
                            debit,
                            credit,
                            now,
                            "رصيد افتتاحي");
                    }

                    // Generate number and post
                    var journalNumber = await _journalNumberGen.NextNumberAsync(fiscalYear.Id, ct);
                    journal.Post(journalNumber, username, now);

                    await _journalRepo.AddAsync(journal, ct);
                    await _unitOfWork.SaveChangesAsync(ct);

                    // Mark accounts as having postings
                    foreach (var accId in accountIds)
                    {
                        var account = await _accountRepo.GetByIdAsync(accId, ct);
                        if (account != null && !account.HasPostings)
                        {
                            account.MarkAsUsed();
                            _accountRepo.Update(account);
                        }
                    }

                    // ── Step 2: Update subsidiary ledgers ──

                    // Customer balances
                    var customerLines = entity.Lines
                        .Where(l => l.LineType == OpeningBalanceLineType.Customer && l.CustomerId.HasValue)
                        .ToList();
                    foreach (var cl in customerLines)
                    {
                        var customer = await _customerRepo.GetByIdAsync(cl.CustomerId.Value, ct);
                        if (customer != null)
                        {
                            var balance = cl.DebitAmount > 0 ? cl.DebitAmount : -cl.CreditAmount;
                            customer.AdjustPreviousBalance(balance);
                            _customerRepo.Update(customer);
                        }
                    }

                    // Supplier balances
                    var supplierLines = entity.Lines
                        .Where(l => l.LineType == OpeningBalanceLineType.Supplier && l.SupplierId.HasValue)
                        .ToList();
                    foreach (var sl in supplierLines)
                    {
                        var supplier = await _supplierRepo.GetByIdAsync(sl.SupplierId.Value, ct);
                        if (supplier != null)
                        {
                            var balance = sl.CreditAmount > 0 ? sl.CreditAmount : -sl.DebitAmount;
                            supplier.AdjustPreviousBalance(balance);
                            _supplierRepo.Update(supplier);
                        }
                    }

                    // Inventory balances
                    var inventoryLines = entity.Lines
                        .Where(l => l.LineType == OpeningBalanceLineType.Inventory
                                    && l.ProductId.HasValue
                                    && l.WarehouseId.HasValue)
                        .ToList();
                    foreach (var il in inventoryLines)
                    {
                        var whProduct = await _whProductRepo.GetOrCreateAsync(
                            il.WarehouseId.Value, il.ProductId.Value, ct);
                        whProduct.SetOpeningBalance(il.Quantity);
                        _whProductRepo.Update(whProduct);

                        // Create inventory movement
                        var product = await _productRepo.GetByIdAsync(il.ProductId.Value, ct);
                        var movement = new InventoryMovement(
                            il.ProductId.Value,
                            il.WarehouseId.Value,
                            product?.BaseUnitId ?? 1,
                            MovementType.OpeningBalance,
                            il.Quantity,
                            il.Quantity, // base quantity = quantity (base unit)
                            il.UnitCost,
                            il.DebitAmount, // total cost
                            entity.BalanceDate,
                            $"OB-{fiscalYear.Year}",
                            SourceType.Opening,
                            sourceId: entity.Id,
                            notes: "رصيد افتتاحي");
                        movement.SetBalanceAfter(whProduct.Quantity);
                        await _movementRepo.AddAsync(movement, ct);

                        // OB-01: Update product weighted average cost from opening balance
                        if (product != null && il.UnitCost > 0)
                        {
                            product.SetWeightedAverageCost(il.UnitCost);
                            _productRepo.Update(product);
                        }
                    }

                    // Cashbox balances
                    var cashboxLines = entity.Lines
                        .Where(l => l.LineType == OpeningBalanceLineType.Cashbox && l.CashboxId.HasValue)
                        .ToList();
                    foreach (var cbl in cashboxLines)
                    {
                        var cashbox = await _cashboxRepo.GetByIdAsync(cbl.CashboxId.Value, ct);
                        if (cashbox != null)
                        {
                            cashbox.IncreaseBalance(cbl.DebitAmount);
                            _cashboxRepo.Update(cashbox);
                        }
                    }

                    // Bank accounts — no Balance property to update, the GL posting is sufficient.

                    // ── Step 3: Post the opening balance document ──
                    entity.Post(journal.Id, username, now);
                    _openingBalanceRepo.Update(entity);

                    await _unitOfWork.SaveChangesAsync(ct);

                    // Audit log
                    await _auditLogger.LogAsync(
                        "OpeningBalance", entity.Id, "Posted",
                        username,
                        $"ترحيل الأرصدة الافتتاحية — {entity.Lines.Count} بند. " +
                        $"قيد يومية: {journal.JournalNumber}. " +
                        $"المدين: {entity.TotalDebit:N4}، الدائن: {entity.TotalCredit:N4}.",
                        ct);
                    await _unitOfWork.SaveChangesAsync(ct);

                }, IsolationLevel.Serializable, ct);

                var result = await _openingBalanceRepo.GetWithLinesAsync(id, ct);
                var resultDto = await MapToDetailDtoAsync(result, ct);
                return ServiceResult<OpeningBalanceDto>.Success(resultDto);
            }
            catch (OpeningBalanceDomainException ex)
            {
                return ServiceResult<OpeningBalanceDto>.Failure(ex.Message);
            }
            catch (ConcurrencyConflictException)
            {
                return ServiceResult<OpeningBalanceDto>.Failure(
                    "تعذر ترحيل الأرصدة الافتتاحية بسبب تعارض تزامن. الرجاء إعادة المحاولة.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "PostAsync failed for OpeningBalance Id={Id}.", id);
                return ServiceResult<OpeningBalanceDto>.Failure(
                    ErrorSanitizer.SanitizeGeneric(ex, "ترحيل الأرصدة الافتتاحية"));
            }
        }

        public async Task<ServiceResult> DeleteDraftAsync(int id, CancellationToken ct = default)
        {
            _logger.LogInformation("Operation={Operation} Entity={Entity} EntityId={EntityId}",
                "DeleteDraftAsync", "OpeningBalance", id);

            var entity = await _openingBalanceRepo.GetByIdAsync(id, ct);
            if (entity == null)
                return ServiceResult.Failure("الأرصدة الافتتاحية غير موجودة.");

            if (entity.Status != OpeningBalanceStatus.Draft)
                return ServiceResult.Failure("لا يمكن حذف أرصدة افتتاحية مرحّلة.");

            try
            {
                await _unitOfWork.ExecuteInTransactionAsync(async () =>
                {
                    _openingBalanceRepo.Remove(entity);
                    await _unitOfWork.SaveChangesAsync(ct);

                    await _auditLogger.LogAsync(
                        "OpeningBalance", id, "DraftDeleted",
                        _currentUser.Username,
                        "حذف مسودة الأرصدة الافتتاحية.",
                        ct);
                    await _unitOfWork.SaveChangesAsync(ct);
                }, IsolationLevel.ReadCommitted, ct);

                return ServiceResult.Success();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "DeleteDraftAsync failed for OpeningBalance Id={Id}.", id);
                return ServiceResult.Failure(
                    ErrorSanitizer.SanitizeGeneric(ex, "حذف الأرصدة الافتتاحية"));
            }
        }

        // ════════════════════════════════════════════════════════
        //  Private Helpers
        // ════════════════════════════════════════════════════════

        /// <summary>Processes line DTOs and adds them to the domain entity.</summary>
        private async Task<List<string>> AddLinesToEntityAsync(
            OpeningBalance entity,
            List<CreateOpeningBalanceLineDto> lineDtos,
            CancellationToken ct)
        {
            var errors = new List<string>();

            foreach (var lineDto in lineDtos)
            {
                try
                {
                    switch (lineDto.LineType)
                    {
                        case OpeningBalanceLineType.Account:
                        {
                            if (!lineDto.AccountId.HasValue || lineDto.AccountId.Value <= 0)
                            {
                                errors.Add("الحساب مطلوب لبنود الحسابات العامة.");
                                continue;
                            }

                            var account = await _accountRepo.GetByIdAsync(lineDto.AccountId.Value, ct);
                            if (account == null)
                            {
                                errors.Add($"الحساب رقم {lineDto.AccountId} غير موجود.");
                                continue;
                            }

                            // Only balance sheet accounts for opening balance
                            if (!Account.IsBalanceSheetType(account.AccountType))
                            {
                                errors.Add($"الحساب '{account.AccountCode} - {account.AccountNameAr}' ليس حساب ميزانية. " +
                                           "الأرصدة الافتتاحية تقتصر على حسابات الأصول والخصوم وحقوق الملكية.");
                                continue;
                            }

                            entity.AddAccountLine(
                                lineDto.AccountId.Value,
                                lineDto.DebitAmount,
                                lineDto.CreditAmount,
                                lineDto.Notes);
                            break;
                        }

                        case OpeningBalanceLineType.Customer:
                        {
                            if (!lineDto.CustomerId.HasValue || lineDto.CustomerId.Value <= 0)
                            {
                                errors.Add("العميل مطلوب لبنود العملاء.");
                                continue;
                            }

                            var customer = await _customerRepo.GetByIdAsync(lineDto.CustomerId.Value, ct);
                            if (customer == null)
                            {
                                errors.Add($"العميل رقم {lineDto.CustomerId} غير موجود.");
                                continue;
                            }

                            var accountId = customer.AccountId
                                            ?? await ResolveControlAccountAsync("1121", ct);
                            if (accountId <= 0)
                            {
                                errors.Add("لم يتم تعيين حساب ذمم مدينة للعميل ولا يوجد حساب افتراضي (1121).");
                                continue;
                            }

                            entity.AddCustomerLine(
                                lineDto.CustomerId.Value,
                                accountId,
                                lineDto.Amount,
                                lineDto.Notes);
                            break;
                        }

                        case OpeningBalanceLineType.Supplier:
                        {
                            if (!lineDto.SupplierId.HasValue || lineDto.SupplierId.Value <= 0)
                            {
                                errors.Add("المورد مطلوب لبنود الموردين.");
                                continue;
                            }

                            var supplier = await _supplierRepo.GetByIdAsync(lineDto.SupplierId.Value, ct);
                            if (supplier == null)
                            {
                                errors.Add($"المورد رقم {lineDto.SupplierId} غير موجود.");
                                continue;
                            }

                            var accountId = supplier.AccountId
                                            ?? await ResolveControlAccountAsync("2111", ct);
                            if (accountId <= 0)
                            {
                                errors.Add("لم يتم تعيين حساب ذمم دائنة للمورد ولا يوجد حساب افتراضي (2111).");
                                continue;
                            }

                            entity.AddSupplierLine(
                                lineDto.SupplierId.Value,
                                accountId,
                                lineDto.Amount,
                                lineDto.Notes);
                            break;
                        }

                        case OpeningBalanceLineType.Inventory:
                        {
                            if (!lineDto.ProductId.HasValue || lineDto.ProductId.Value <= 0)
                            {
                                errors.Add("الصنف مطلوب لبنود المخزون.");
                                continue;
                            }
                            if (!lineDto.WarehouseId.HasValue || lineDto.WarehouseId.Value <= 0)
                            {
                                errors.Add("المخزن مطلوب لبنود المخزون.");
                                continue;
                            }

                            var product = await _productRepo.GetByIdAsync(lineDto.ProductId.Value, ct);
                            if (product == null)
                            {
                                errors.Add($"الصنف رقم {lineDto.ProductId} غير موجود.");
                                continue;
                            }

                            var warehouse = await _warehouseRepo.GetByIdAsync(lineDto.WarehouseId.Value, ct);
                            if (warehouse == null)
                            {
                                errors.Add($"المخزن رقم {lineDto.WarehouseId} غير موجود.");
                                continue;
                            }

                            var accountId = warehouse.AccountId
                                            ?? await ResolveControlAccountAsync("1131", ct);
                            if (accountId <= 0)
                            {
                                errors.Add("لم يتم تعيين حساب مخزون للمخزن ولا يوجد حساب افتراضي (1131).");
                                continue;
                            }

                            entity.AddInventoryLine(
                                lineDto.ProductId.Value,
                                lineDto.WarehouseId.Value,
                                accountId,
                                lineDto.Quantity,
                                lineDto.UnitCost,
                                lineDto.Notes);
                            break;
                        }

                        case OpeningBalanceLineType.Cashbox:
                        {
                            if (!lineDto.CashboxId.HasValue || lineDto.CashboxId.Value <= 0)
                            {
                                errors.Add("الصندوق مطلوب لبنود الصناديق.");
                                continue;
                            }

                            var cashbox = await _cashboxRepo.GetByIdAsync(lineDto.CashboxId.Value, ct);
                            if (cashbox == null)
                            {
                                errors.Add($"الصندوق رقم {lineDto.CashboxId} غير موجود.");
                                continue;
                            }

                            var accountId = cashbox.AccountId
                                            ?? await ResolveControlAccountAsync("1110", ct);
                            if (accountId <= 0)
                            {
                                errors.Add("لم يتم تعيين حساب نقدية للصندوق ولا يوجد حساب افتراضي (1110).");
                                continue;
                            }

                            entity.AddCashboxLine(
                                lineDto.CashboxId.Value,
                                accountId,
                                lineDto.Amount,
                                lineDto.Notes);
                            break;
                        }

                        case OpeningBalanceLineType.BankAccount:
                        {
                            if (!lineDto.BankAccountId.HasValue || lineDto.BankAccountId.Value <= 0)
                            {
                                errors.Add("الحساب البنكي مطلوب لبنود البنوك.");
                                continue;
                            }

                            var bankAccount = await _bankAccountRepo.GetByIdAsync(lineDto.BankAccountId.Value, ct);
                            if (bankAccount == null)
                            {
                                errors.Add($"الحساب البنكي رقم {lineDto.BankAccountId} غير موجود.");
                                continue;
                            }

                            var accountId = bankAccount.AccountId
                                            ?? await ResolveControlAccountAsync("1112", ct);
                            if (accountId <= 0)
                            {
                                errors.Add("لم يتم تعيين حساب بنك ولا يوجد حساب افتراضي (1112).");
                                continue;
                            }

                            entity.AddBankAccountLine(
                                lineDto.BankAccountId.Value,
                                accountId,
                                lineDto.Amount,
                                lineDto.Notes);
                            break;
                        }

                        default:
                            errors.Add($"نوع البند غير مدعوم: {lineDto.LineType}.");
                            break;
                    }
                }
                catch (OpeningBalanceDomainException ex)
                {
                    errors.Add(ex.Message);
                }
            }

            return errors;
        }

        /// <summary>Resolves a GL control account ID by code.</summary>
        private async Task<int> ResolveControlAccountAsync(string accountCode, CancellationToken ct)
        {
            var account = await _accountRepo.GetByCodeAsync(accountCode, ct);
            return account?.Id ?? 0;
        }

        /// <summary>Maps entity to detail DTO with subsidiary entity names.</summary>
        private async Task<OpeningBalanceDto> MapToDetailDtoAsync(
            OpeningBalance entity, CancellationToken ct)
        {
            var dto = new OpeningBalanceDto
            {
                Id = entity.Id,
                FiscalYearId = entity.FiscalYearId,
                FiscalYear = entity.FiscalYear?.Year ?? 0,
                BalanceDate = entity.BalanceDate,
                Status = entity.Status,
                StatusText = GetStatusText(entity.Status),
                JournalEntryId = entity.JournalEntryId,
                Notes = entity.Notes,
                PostedBy = entity.PostedBy,
                PostedAt = entity.PostedAt,
                TotalDebit = entity.TotalDebit,
                TotalCredit = entity.TotalCredit,
                Difference = entity.TotalDebit - entity.TotalCredit,
                IsBalanced = entity.IsBalanced,
                CreatedBy = entity.CreatedBy,
                CreatedAt = entity.CreatedAt,
                Lines = new List<OpeningBalanceLineDto>()
            };

            foreach (var line in entity.Lines)
            {
                var lineDto = new OpeningBalanceLineDto
                {
                    Id = line.Id,
                    LineType = line.LineType,
                    LineTypeText = GetLineTypeText(line.LineType),
                    AccountId = line.AccountId,
                    DebitAmount = line.DebitAmount,
                    CreditAmount = line.CreditAmount,
                    CustomerId = line.CustomerId,
                    SupplierId = line.SupplierId,
                    ProductId = line.ProductId,
                    WarehouseId = line.WarehouseId,
                    CashboxId = line.CashboxId,
                    BankAccountId = line.BankAccountId,
                    Quantity = line.Quantity,
                    UnitCost = line.UnitCost,
                    Notes = line.Notes
                };

                // Resolve account info
                var account = await _accountRepo.GetByIdAsync(line.AccountId, ct);
                if (account != null)
                {
                    lineDto.AccountCode = account.AccountCode;
                    lineDto.AccountName = account.AccountNameAr;
                }

                // Resolve subsidiary entity names
                if (line.CustomerId.HasValue)
                {
                    var customer = await _customerRepo.GetByIdAsync(line.CustomerId.Value, ct);
                    lineDto.CustomerName = customer?.NameAr;
                }
                if (line.SupplierId.HasValue)
                {
                    var supplier = await _supplierRepo.GetByIdAsync(line.SupplierId.Value, ct);
                    lineDto.SupplierName = supplier?.NameAr;
                }
                if (line.ProductId.HasValue)
                {
                    var product = await _productRepo.GetByIdAsync(line.ProductId.Value, ct);
                    lineDto.ProductName = product?.NameAr;
                }
                if (line.WarehouseId.HasValue)
                {
                    var warehouse = await _warehouseRepo.GetByIdAsync(line.WarehouseId.Value, ct);
                    lineDto.WarehouseName = warehouse?.NameAr;
                }
                if (line.CashboxId.HasValue)
                {
                    var cashbox = await _cashboxRepo.GetByIdAsync(line.CashboxId.Value, ct);
                    lineDto.CashboxName = cashbox?.NameAr;
                }
                if (line.BankAccountId.HasValue)
                {
                    var bankAccount = await _bankAccountRepo.GetByIdAsync(line.BankAccountId.Value, ct);
                    lineDto.BankAccountName = bankAccount?.NameAr;
                }

                dto.Lines.Add(lineDto);
            }

            return dto;
        }

        private static string GetStatusText(OpeningBalanceStatus status) => status switch
        {
            OpeningBalanceStatus.Draft => "مسودة",
            OpeningBalanceStatus.Posted => "مرحّلة",
            _ => status.ToString()
        };

        private static string GetLineTypeText(OpeningBalanceLineType type) => type switch
        {
            OpeningBalanceLineType.Account => "حساب عام",
            OpeningBalanceLineType.Customer => "عميل",
            OpeningBalanceLineType.Supplier => "مورد",
            OpeningBalanceLineType.Inventory => "مخزون",
            OpeningBalanceLineType.Cashbox => "صندوق",
            OpeningBalanceLineType.BankAccount => "حساب بنكي",
            _ => type.ToString()
        };
    }
}
