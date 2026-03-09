using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MarcoERP.Application.Common;
using MarcoERP.Application.DTOs.Inventory;
using MarcoERP.Application.Interfaces;
using MarcoERP.Application.Interfaces.Inventory;
using MarcoERP.Domain.Entities.Accounting;
using MarcoERP.Domain.Entities.Accounting.Policies;
using MarcoERP.Domain.Entities.Inventory;
using MarcoERP.Domain.Enums;
using MarcoERP.Domain.Exceptions;
using MarcoERP.Domain.Exceptions.Inventory;
using MarcoERP.Application.Interfaces.Settings;
using MarcoERP.Domain.Interfaces;
using MarcoERP.Domain.Interfaces.Inventory;
using MarcoERP.Domain.Interfaces.Settings;
using Microsoft.Extensions.Logging;

namespace MarcoERP.Application.Services.Inventory
{
    /// <summary>
    /// Implements inventory adjustment lifecycle: Create → Edit → Post → (Cancel).
    /// On Post: creates inventory movements, updates stock, generates adjustment journal.
    /// Surplus: DR Inventory / CR Adjustment (income)
    /// Shortage: DR Adjustment (expense) / CR Inventory
    /// </summary>
    [Module(SystemModule.Inventory)]
    public sealed class InventoryAdjustmentService : IInventoryAdjustmentService
    {
        private readonly IInventoryAdjustmentRepository _adjustmentRepo;
        private readonly IProductRepository _productRepo;
        private readonly IWarehouseProductRepository _whProductRepo;
        private readonly IInventoryMovementRepository _movementRepo;
        private readonly IJournalEntryRepository _journalRepo;
        private readonly IAccountRepository _accountRepo;
        private readonly IFiscalYearRepository _fiscalYearRepo;
        private readonly IJournalNumberGenerator _journalNumberGen;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ICurrentUserService _currentUser;
        private readonly IDateTimeProvider _dateTime;
        private readonly ISystemSettingRepository _systemSettingRepository;
        private readonly ILogger<InventoryAdjustmentService> _logger;
        private readonly JournalEntryFactory _journalFactory;
        private readonly IFeatureService _featureService;

        // GL Account codes
        private const string InventoryAccountCode = "1131";
        private const string AdjustmentExpenseAccountCode = "5112"; // عجز وتالف مخزني
        private const string AdjustmentIncomeAccountCode = "4112"; // فائض مخزني

        public InventoryAdjustmentService(
            IInventoryAdjustmentRepository adjustmentRepo,
            IProductRepository productRepo,
            IWarehouseProductRepository whProductRepo,
            IInventoryMovementRepository movementRepo,
            IJournalEntryRepository journalRepo,
            IAccountRepository accountRepo,
            IFiscalYearRepository fiscalYearRepo,
            IJournalNumberGenerator journalNumberGen,
            IUnitOfWork unitOfWork,
            ICurrentUserService currentUser,
            IDateTimeProvider dateTime,
            ISystemSettingRepository systemSettingRepository,
            JournalEntryFactory journalFactory,
            ILogger<InventoryAdjustmentService> logger,
            IFeatureService featureService = null)
        {
            _adjustmentRepo = adjustmentRepo ?? throw new ArgumentNullException(nameof(adjustmentRepo));
            _productRepo = productRepo ?? throw new ArgumentNullException(nameof(productRepo));
            _whProductRepo = whProductRepo ?? throw new ArgumentNullException(nameof(whProductRepo));
            _movementRepo = movementRepo ?? throw new ArgumentNullException(nameof(movementRepo));
            _journalRepo = journalRepo ?? throw new ArgumentNullException(nameof(journalRepo));
            _accountRepo = accountRepo ?? throw new ArgumentNullException(nameof(accountRepo));
            _fiscalYearRepo = fiscalYearRepo ?? throw new ArgumentNullException(nameof(fiscalYearRepo));
            _journalNumberGen = journalNumberGen ?? throw new ArgumentNullException(nameof(journalNumberGen));
            _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
            _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
            _dateTime = dateTime ?? throw new ArgumentNullException(nameof(dateTime));
            _systemSettingRepository = systemSettingRepository ?? throw new ArgumentNullException(nameof(systemSettingRepository));
            _journalFactory = journalFactory ?? throw new ArgumentNullException(nameof(journalFactory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _featureService = featureService;
        }

        public async Task<ServiceResult<IReadOnlyList<InventoryAdjustmentListDto>>> GetAllAsync(CancellationToken ct = default)
        {
            var entities = await _adjustmentRepo.GetAllAsync(ct);
            var dtos = entities.Select(e => new InventoryAdjustmentListDto
            {
                Id = e.Id,
                AdjustmentNumber = e.AdjustmentNumber,
                AdjustmentDate = e.AdjustmentDate,
                WarehouseId = e.WarehouseId,
                WarehouseName = e.Warehouse?.NameAr,
                Reason = e.Reason,
                Status = e.Status.ToString(),
                TotalCostDifference = e.TotalCostDifference,
                LineCount = e.Lines?.Count ?? 0
            }).ToList();

            return ServiceResult<IReadOnlyList<InventoryAdjustmentListDto>>.Success(dtos);
        }

        public async Task<ServiceResult<InventoryAdjustmentDto>> GetByIdAsync(int id, CancellationToken ct = default)
        {
            var entity = await _adjustmentRepo.GetWithLinesAsync(id, ct);
            if (entity == null)
                return ServiceResult<InventoryAdjustmentDto>.Failure("التسوية غير موجودة.");

            return ServiceResult<InventoryAdjustmentDto>.Success(MapToDto(entity));
        }

        public async Task<ServiceResult<string>> GetNextNumberAsync(CancellationToken ct = default)
        {
            var next = await _adjustmentRepo.GetNextNumberAsync(ct);
            return ServiceResult<string>.Success(next);
        }

        public async Task<ServiceResult<InventoryAdjustmentDto>> CreateAsync(
            CreateInventoryAdjustmentDto dto, CancellationToken ct = default)
        {
            // Feature Guard — block operation if Inventory module is disabled
            if (_featureService != null)
            {
                var guard = await FeatureGuard.CheckAsync<InventoryAdjustmentDto>(_featureService, FeatureKeys.Inventory, ct);
                if (guard != null) return guard;
            }

            _logger.LogInformation(
                "CreateAsync started for {Entity} operation {Operation}.",
                nameof(InventoryAdjustment),
                "Create");

            if (dto.WarehouseId <= 0)
                return ServiceResult<InventoryAdjustmentDto>.Failure("يجب اختيار المخزن.");

            if (dto.Lines == null || dto.Lines.Count == 0)
                return ServiceResult<InventoryAdjustmentDto>.Failure("لا يمكن حفظ تسوية بدون بنود.");

            if (dto.Lines.Any(l => l.ProductId <= 0 || l.UnitId <= 0 || l.ActualQuantity < 0))
                return ServiceResult<InventoryAdjustmentDto>.Failure("يوجد بنود غير مكتملة (صنف أو وحدة أو كمية غير صحيحة).");

            try
            {
                var number = await _adjustmentRepo.GetNextNumberAsync(ct);
                var adjustment = new InventoryAdjustment(
                    number, dto.AdjustmentDate, dto.WarehouseId, dto.Reason, dto.Notes);

                foreach (var lineDto in dto.Lines)
                {
                    var product = await _productRepo.GetByIdWithUnitsAsync(lineDto.ProductId, ct);
                    if (product == null)
                        return ServiceResult<InventoryAdjustmentDto>.Failure($"الصنف برقم {lineDto.ProductId} غير موجود.");

                    var productUnit = product.ProductUnits.FirstOrDefault(pu => pu.UnitId == lineDto.UnitId);
                    decimal conversionFactor = productUnit?.ConversionFactor ?? 1m;

                    // Get current system quantity
                    var whProduct = await _whProductRepo.GetAsync(dto.WarehouseId, lineDto.ProductId, ct);
                    decimal systemQty = whProduct?.Quantity ?? 0;

                    // Convert actual qty to base unit for comparison
                    decimal actualInBaseUnit = lineDto.ActualQuantity * conversionFactor;

                    adjustment.AddLine(
                        lineDto.ProductId,
                        lineDto.UnitId,
                        systemQty,
                        actualInBaseUnit,
                        1m, // already in base unit
                        product.WeightedAverageCost);
                }

                await _adjustmentRepo.AddAsync(adjustment, ct);
                await _unitOfWork.SaveChangesAsync(ct);

                var saved = await _adjustmentRepo.GetWithLinesAsync(adjustment.Id, ct);
                return ServiceResult<InventoryAdjustmentDto>.Success(MapToDto(saved));
            }
            catch (InventoryDomainException ex)
            {
                return ServiceResult<InventoryAdjustmentDto>.Failure(ex.Message);
            }
            catch (ConcurrencyConflictException)
            {
                return ServiceResult<InventoryAdjustmentDto>.Failure("تعذر حفظ التسوية بسبب تعارض تزامن. الرجاء إعادة المحاولة.");
            }
            catch (DuplicateRecordException)
            {
                return ServiceResult<InventoryAdjustmentDto>.Failure("رقم التسوية مستخدم بالفعل. الرجاء إعادة المحاولة.");
            }
        }

        public async Task<ServiceResult<InventoryAdjustmentDto>> UpdateAsync(
            UpdateInventoryAdjustmentDto dto, CancellationToken ct = default)
        {
            _logger.LogInformation("Operation={Operation} Entity={Entity} EntityId={EntityId}", "UpdateAsync", "InventoryAdjustment", dto.Id);

            if (dto.WarehouseId <= 0)
                return ServiceResult<InventoryAdjustmentDto>.Failure("يجب اختيار المخزن.");

            if (dto.Lines == null || dto.Lines.Count == 0)
                return ServiceResult<InventoryAdjustmentDto>.Failure("لا يمكن حفظ تسوية بدون بنود.");

            if (dto.Lines.Any(l => l.ProductId <= 0 || l.UnitId <= 0 || l.ActualQuantity < 0))
                return ServiceResult<InventoryAdjustmentDto>.Failure("يوجد بنود غير مكتملة (صنف أو وحدة أو كمية غير صحيحة).");

            var adjustment = await _adjustmentRepo.GetWithLinesTrackedAsync(dto.Id, ct);
            if (adjustment == null)
                return ServiceResult<InventoryAdjustmentDto>.Failure("التسوية غير موجودة.");

            if (adjustment.Status != InvoiceStatus.Draft)
                return ServiceResult<InventoryAdjustmentDto>.Failure("لا يمكن تعديل تسوية مرحّلة أو ملغاة.");

            try
            {
                var newLines = new List<InventoryAdjustmentLine>();
                foreach (var lineDto in dto.Lines)
                {
                    var product = await _productRepo.GetByIdWithUnitsAsync(lineDto.ProductId, ct);
                    if (product == null)
                        return ServiceResult<InventoryAdjustmentDto>.Failure($"الصنف برقم {lineDto.ProductId} غير موجود.");

                    var productUnit = product.ProductUnits.FirstOrDefault(pu => pu.UnitId == lineDto.UnitId);
                    decimal conversionFactor = productUnit?.ConversionFactor ?? 1m;
                    var whProduct = await _whProductRepo.GetAsync(dto.WarehouseId, lineDto.ProductId, ct);
                    decimal systemQty = whProduct?.Quantity ?? 0;
                    decimal actualInBaseUnit = lineDto.ActualQuantity * conversionFactor;

                    newLines.Add(new InventoryAdjustmentLine(
                        lineDto.ProductId, lineDto.UnitId, systemQty, actualInBaseUnit,
                        1m, product.WeightedAverageCost,
                        lineDto.Id));
                }

                adjustment.ReplaceLines(newLines);
                // Entity is already tracked — no need for _adjustmentRepo.Update(adjustment)
                await _unitOfWork.SaveChangesAsync(ct);

                var saved = await _adjustmentRepo.GetWithLinesAsync(adjustment.Id, ct);
                return ServiceResult<InventoryAdjustmentDto>.Success(MapToDto(saved));
            }
            catch (InventoryDomainException ex)
            {
                return ServiceResult<InventoryAdjustmentDto>.Failure(ex.Message);
            }
        }

        public async Task<ServiceResult<InventoryAdjustmentDto>> PostAsync(int id, CancellationToken ct = default)
        {
            _logger.LogInformation(
                "PostAsync started for {Entity}Id {EntityId} operation {Operation}.",
                nameof(InventoryAdjustment),
                id,
                "Post");

            var adjustment = await _adjustmentRepo.GetWithLinesAsync(id, ct);
            if (adjustment == null)
                return ServiceResult<InventoryAdjustmentDto>.Failure("التسوية غير موجودة.");

            if (adjustment.Status != InvoiceStatus.Draft)
                return ServiceResult<InventoryAdjustmentDto>.Failure("لا يمكن ترحيل تسوية مرحّلة بالفعل أو ملغاة.");

            if (!adjustment.Lines.Any())
                return ServiceResult<InventoryAdjustmentDto>.Failure("لا يمكن ترحيل تسوية بدون بنود.");

            InventoryAdjustment saved = null;

            try
            {
                await _unitOfWork.ExecuteInTransactionAsync(async () =>
                {
                    // Reload as TRACKED inside transaction to avoid
                    // unsafe graph traversal on Update with detached entities
                    adjustment = await _adjustmentRepo.GetWithLinesTrackedAsync(id, ct)
                        ?? throw new InventoryDomainException("التسوية غير موجودة.");

                    if (adjustment.Status != InvoiceStatus.Draft)
                        throw new InventoryDomainException("لا يمكن ترحيل تسوية مرحّلة بالفعل أو ملغاة.");

                    if (!adjustment.Lines.Any())
                        throw new InventoryDomainException("لا يمكن ترحيل تسوية بدون بنود.");

                    if (await ProductionHardening.IsProductionModeAsync(_systemSettingRepository, ct)
                        && ProductionHardening.IsBackdated(adjustment.AdjustmentDate, _dateTime.UtcNow))
                    {
                        throw new InventoryDomainException("لا يمكن الترحيل بتاريخ سابق أثناء وضع الإنتاج.");
                    }

                    var fiscalYear = await _fiscalYearRepo.GetActiveYearAsync(ct)
                        ?? throw new InventoryDomainException("لا توجد سنة مالية نشطة.");

                    var yearWithPeriods = await _fiscalYearRepo.GetWithPeriodsAsync(fiscalYear.Id, ct);
                    var period = yearWithPeriods.GetPeriod(adjustment.AdjustmentDate.Month)
                        ?? throw new InventoryDomainException("لا توجد فترة مالية للشهر المحدد.");

                    if (!period.IsOpen)
                        throw new InventoryDomainException($"الفترة المالية ({period.Year}-{period.Month:D2}) مقفلة.");

                    var inventoryAccount = await _accountRepo.GetByCodeAsync(InventoryAccountCode, ct)
                        ?? throw new InventoryDomainException("حساب المخزون غير موجود في شجرة الحسابات.");
                    var now = _dateTime.UtcNow;
                    var username = _currentUser.Username ?? "System";

                    decimal totalSurplus = 0;
                    decimal totalShortage = 0;
                    var allowNegativeStock = await IsNegativeStockAllowedAsync(ct);

                    foreach (var line in adjustment.Lines)
                    {
                        if (line.CostDifference > 0) totalSurplus += line.CostDifference;
                        else if (line.CostDifference < 0) totalShortage += Math.Abs(line.CostDifference);

                        // Update stock
                        var whProduct = await _whProductRepo.GetOrCreateAsync(
                            adjustment.WarehouseId, line.ProductId, ct);

                        var movementType = line.DifferenceInBaseUnit > 0
                            ? MovementType.AdjustmentIn
                            : MovementType.AdjustmentOut;

                        var absQty = Math.Abs(line.DifferenceInBaseUnit);
                        if (absQty > 0)
                        {
                            if (line.DifferenceInBaseUnit > 0)
                                whProduct.IncreaseStock(absQty);
                            else if (allowNegativeStock)
                                whProduct.DecreaseStockAllowNegative(absQty);
                            else
                                whProduct.DecreaseStock(absQty);

                            _whProductRepo.Update(whProduct);

                            var movement = new InventoryMovement(
                                line.ProductId,
                                adjustment.WarehouseId,
                                line.UnitId,
                                movementType,
                                absQty,
                                absQty,
                                line.UnitCost,
                                Math.Abs(line.CostDifference),
                                adjustment.AdjustmentDate,
                                adjustment.AdjustmentNumber,
                                SourceType.Adjustment,
                                sourceId: adjustment.Id,
                                notes: $"تسوية مخزنية — {adjustment.Reason}");

                            if (allowNegativeStock)
                                movement.SetBalanceAfterAllowNegative(whProduct.Quantity);
                            else
                                movement.SetBalanceAfter(whProduct.Quantity);
                            await _movementRepo.AddAsync(movement, ct);
                        }
                    }

                    // Surplus: DR Inventory / CR AdjustmentIncome
                    // Shortage: DR AdjustmentExpense / CR Inventory
                    var journalLines = new List<JournalLineSpec>();

                    if (totalSurplus > 0)
                    {
                        var incomeAccount = await _accountRepo.GetByCodeAsync(AdjustmentIncomeAccountCode, ct)
                            ?? throw new InventoryDomainException("حساب إيراد فائض المخزون غير موجود في شجرة الحسابات.");
                        journalLines.Add(new JournalLineSpec(inventoryAccount.Id, totalSurplus, 0,
                            $"فائض مخزني — تسوية {adjustment.AdjustmentNumber}"));
                        journalLines.Add(new JournalLineSpec(incomeAccount.Id, 0, totalSurplus,
                            $"إيراد فائض مخزني — تسوية {adjustment.AdjustmentNumber}"));
                    }

                    if (totalShortage > 0)
                    {
                        var expenseAccount = await _accountRepo.GetByCodeAsync(AdjustmentExpenseAccountCode, ct)
                            ?? throw new InventoryDomainException("حساب عجز المخزون غير موجود في شجرة الحسابات.");
                        journalLines.Add(new JournalLineSpec(expenseAccount.Id, totalShortage, 0,
                            $"عجز مخزني — تسوية {adjustment.AdjustmentNumber}"));
                        journalLines.Add(new JournalLineSpec(inventoryAccount.Id, 0, totalShortage,
                            $"مخزون — عجز تسوية {adjustment.AdjustmentNumber}"));
                    }

                    if (!journalLines.Any())
                        throw new InventoryDomainException("لا توجد فروقات جردية للترحيل.");

                    var journal = await _journalFactory.CreateAndPostAsync(
                        adjustment.AdjustmentDate,
                        $"تسوية مخزنية رقم {adjustment.AdjustmentNumber} — {adjustment.Reason}",
                        SourceType.Adjustment,
                        fiscalYear.Id,
                        period.Id,
                        journalLines,
                        username,
                        now,
                        referenceNumber: adjustment.AdjustmentNumber,
                        ct: ct);
                    await _unitOfWork.SaveChangesAsync(ct);

                    adjustment.Post(journal.Id);
                    // Entity is already tracked — no need for explicit Update
                    await _unitOfWork.SaveChangesAsync(ct);

                    saved = await _adjustmentRepo.GetWithLinesAsync(adjustment.Id, ct);
                }, IsolationLevel.Serializable, ct);

                return ServiceResult<InventoryAdjustmentDto>.Success(MapToDto(saved));
            }
            catch (InventoryDomainException ex)
            {
                return ServiceResult<InventoryAdjustmentDto>.Failure(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogError(ex, "InvalidOperationException while posting inventory adjustment.");
                return ServiceResult<InventoryAdjustmentDto>.Failure(
                    ErrorSanitizer.Sanitize(ex, "ترحيل تسوية المخزون"));
            }
            catch (ConcurrencyConflictException)
            {
                return ServiceResult<InventoryAdjustmentDto>.Failure("تعذر ترحيل التسوية بسبب تعارض تزامن. الرجاء إعادة المحاولة.");
            }
            catch (DuplicateRecordException)
            {
                return ServiceResult<InventoryAdjustmentDto>.Failure("تعذر ترحيل التسوية بسبب تعارض في البيانات. الرجاء إعادة المحاولة.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "PostAsync failed for InventoryAdjustment.");
                return ServiceResult<InventoryAdjustmentDto>.Failure(ErrorSanitizer.SanitizeGeneric(ex, "ترحيل تسوية المخزون"));
            }
        }

        public async Task<ServiceResult> CancelAsync(int id, CancellationToken ct = default)
        {
            _logger.LogInformation(
                "CancelAsync started for {Entity}Id {EntityId} operation {Operation}.",
                nameof(InventoryAdjustment),
                id,
                "Cancel");

            var adjustment = await _adjustmentRepo.GetWithLinesAsync(id, ct);
            if (adjustment == null)
                return ServiceResult.Failure("التسوية غير موجودة.");

            if (adjustment.Status != InvoiceStatus.Posted)
                return ServiceResult.Failure("لا يمكن إلغاء إلا التسويات المرحّلة.");

            try
            {
                await _unitOfWork.ExecuteInTransactionAsync(async () =>
                {
                    // Reload as TRACKED inside transaction
                    adjustment = await _adjustmentRepo.GetWithLinesTrackedAsync(id, ct)
                        ?? throw new InventoryDomainException("التسوية غير موجودة.");

                    if (adjustment.Status != InvoiceStatus.Posted)
                        throw new InventoryDomainException("لا يمكن إلغاء إلا التسويات المرحّلة.");

                    // Reverse stock movements
                    var allowNegativeStock = await IsNegativeStockAllowedAsync(ct);
                    foreach (var line in adjustment.Lines)
                    {
                        var whProduct = await _whProductRepo.GetOrCreateAsync(
                            adjustment.WarehouseId, line.ProductId, ct);

                        var absQty = Math.Abs(line.DifferenceInBaseUnit);
                        if (absQty > 0)
                        {
                            // Reverse: if original was increase, decrease now
                            if (line.DifferenceInBaseUnit > 0)
                            {
                                if (allowNegativeStock)
                                    whProduct.DecreaseStockAllowNegative(absQty);
                                else
                                    whProduct.DecreaseStock(absQty);
                            }
                            else
                                whProduct.IncreaseStock(absQty);

                            _whProductRepo.Update(whProduct);

                            var reverseType = line.DifferenceInBaseUnit > 0
                                ? MovementType.AdjustmentOut
                                : MovementType.AdjustmentIn;

                            var movement = new InventoryMovement(
                                line.ProductId,
                                adjustment.WarehouseId,
                                line.UnitId,
                                reverseType,
                                absQty,
                                absQty,
                                line.UnitCost,
                                Math.Abs(line.CostDifference),
                                _dateTime.UtcNow.Date,
                                adjustment.AdjustmentNumber,
                                SourceType.Adjustment,
                                sourceId: adjustment.Id,
                                notes: $"إلغاء تسوية مخزنية — {adjustment.AdjustmentNumber}");

                            if (allowNegativeStock)
                                movement.SetBalanceAfterAllowNegative(whProduct.Quantity);
                            else
                                movement.SetBalanceAfter(whProduct.Quantity);
                            await _movementRepo.AddAsync(movement, ct);
                        }
                    }

                    // Reverse journal
                    if (adjustment.JournalEntryId.HasValue)
                    {
                        var journal = await _journalRepo.GetWithLinesAsync(adjustment.JournalEntryId.Value, ct);
                        if (journal != null)
                        {
                            var fiscalYear = await _fiscalYearRepo.GetActiveYearAsync(ct)
                                ?? throw new InventoryDomainException("لا توجد سنة مالية مفتوحة.");
                            var yearWithPeriods = await _fiscalYearRepo.GetWithPeriodsAsync(fiscalYear.Id, ct);
                            var period = yearWithPeriods.GetPeriod(_dateTime.UtcNow.Month);

                            if (period == null)
                                throw new InvalidOperationException("لا توجد فترة محاسبية مفتوحة للشهر الحالي.");

                            if (!period.IsOpen)
                                throw new InvalidOperationException($"الفترة المحاسبية ({period.PeriodNumber}) مغلقة. لا يمكن إلغاء التسوية في فترة مغلقة.");

                            var reversal = journal.CreateReversal(
                                _dateTime.UtcNow.Date,
                                $"عكس تسوية مخزنية {adjustment.AdjustmentNumber}",
                                fiscalYear.Id,
                                period.Id);

                            var number = await _journalNumberGen.NextNumberAsync(fiscalYear.Id, ct);
                            reversal.Post(number, _currentUser.Username, _dateTime.UtcNow);
                            await _journalRepo.AddAsync(reversal, ct);
                            await _unitOfWork.SaveChangesAsync(ct);

                            journal.MarkAsReversed(reversal.Id);
                            _journalRepo.Update(journal);
                        }
                    }

                    adjustment.Cancel();
                    // Entity is already tracked — no need for explicit Update
                    await _unitOfWork.SaveChangesAsync(ct);
                }, IsolationLevel.Serializable, ct);

                return ServiceResult.Success();
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogError(ex, "InvalidOperationException while cancelling inventory adjustment.");
                return ServiceResult.Failure(
                    ErrorSanitizer.Sanitize(ex, "إلغاء تسوية المخزون"));
            }
            catch (ConcurrencyConflictException)
            {
                return ServiceResult.Failure("تعذر إلغاء التسوية بسبب تعارض تزامن. الرجاء إعادة المحاولة.");
            }
            catch (DuplicateRecordException)
            {
                return ServiceResult.Failure("تعذر إلغاء التسوية بسبب تعارض في البيانات. الرجاء إعادة المحاولة.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "CancelAsync failed for InventoryAdjustment.");
                return ServiceResult.Failure(ErrorSanitizer.SanitizeGeneric(ex, "إلغاء تسوية المخزون"));
            }
        }

        public async Task<ServiceResult> DeleteDraftAsync(int id, CancellationToken ct = default)
        {
            _logger.LogInformation("Operation={Operation} Entity={Entity} EntityId={EntityId}", "DeleteDraftAsync", "InventoryAdjustment", id);

            var adjustment = await _adjustmentRepo.GetByIdAsync(id, ct);
            if (adjustment == null) return ServiceResult.Failure("التسوية غير موجودة.");
            if (adjustment.Status != InvoiceStatus.Draft)
                return ServiceResult.Failure("لا يمكن حذف إلا التسويات المسودة.");

            adjustment.SoftDelete(_currentUser.Username, _dateTime.UtcNow);
            _adjustmentRepo.Update(adjustment);
            await _unitOfWork.SaveChangesAsync(ct);

            return ServiceResult.Success();
        }

        private static InventoryAdjustmentDto MapToDto(InventoryAdjustment entity) => new()
        {
            Id = entity.Id,
            AdjustmentNumber = entity.AdjustmentNumber,
            AdjustmentDate = entity.AdjustmentDate,
            WarehouseId = entity.WarehouseId,
            WarehouseName = entity.Warehouse?.NameAr,
            Reason = entity.Reason,
            Notes = entity.Notes,
            Status = entity.Status.ToString(),
            TotalCostDifference = entity.TotalCostDifference,
            JournalEntryId = entity.JournalEntryId,
            Lines = entity.Lines?.Select(l => new InventoryAdjustmentLineDto
            {
                Id = l.Id,
                ProductId = l.ProductId,
                ProductCode = l.Product?.Code,
                ProductName = l.Product?.NameAr,
                UnitId = l.UnitId,
                UnitName = l.Unit?.NameAr,
                SystemQuantity = l.SystemQuantity,
                ActualQuantity = l.ActualQuantity,
                DifferenceQuantity = l.DifferenceQuantity,
                UnitCost = l.UnitCost,
                CostDifference = l.CostDifference
            }).ToList() ?? new()
        };

        private async Task<bool> IsNegativeStockAllowedAsync(CancellationToken ct)
        {
            if (_featureService == null)
                return false;

            var result = await _featureService.IsEnabledAsync(FeatureKeys.AllowNegativeStock, ct);
            return result.IsSuccess && result.Data;
        }
    }
}
