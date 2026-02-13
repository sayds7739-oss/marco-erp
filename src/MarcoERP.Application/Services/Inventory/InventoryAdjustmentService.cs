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
using MarcoERP.Domain.Exceptions.Inventory;
using MarcoERP.Domain.Interfaces;
using MarcoERP.Domain.Interfaces.Inventory;

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
            IDateTimeProvider dateTime)
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
            var authCheck = AuthorizationGuard.Check<InventoryAdjustmentDto>(_currentUser, PermissionKeys.InventoryManage);
            if (authCheck != null) return authCheck;

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
        }

        public async Task<ServiceResult<InventoryAdjustmentDto>> UpdateAsync(
            UpdateInventoryAdjustmentDto dto, CancellationToken ct = default)
        {
            var authCheck = AuthorizationGuard.Check<InventoryAdjustmentDto>(_currentUser, PermissionKeys.InventoryManage);
            if (authCheck != null) return authCheck;

            if (dto.WarehouseId <= 0)
                return ServiceResult<InventoryAdjustmentDto>.Failure("يجب اختيار المخزن.");

            if (dto.Lines == null || dto.Lines.Count == 0)
                return ServiceResult<InventoryAdjustmentDto>.Failure("لا يمكن حفظ تسوية بدون بنود.");

            if (dto.Lines.Any(l => l.ProductId <= 0 || l.UnitId <= 0 || l.ActualQuantity < 0))
                return ServiceResult<InventoryAdjustmentDto>.Failure("يوجد بنود غير مكتملة (صنف أو وحدة أو كمية غير صحيحة).");

            var adjustment = await _adjustmentRepo.GetWithLinesAsync(dto.Id, ct);
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
                        1m, product.WeightedAverageCost));
                }

                adjustment.ReplaceLines(newLines);
                _adjustmentRepo.Update(adjustment);
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
            var authCheck = AuthorizationGuard.Check<InventoryAdjustmentDto>(_currentUser, PermissionKeys.InventoryManage);
            if (authCheck != null) return authCheck;

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
                    var fiscalYear = await _fiscalYearRepo.GetActiveYearAsync(ct)
                        ?? throw new InventoryDomainException("لا توجد سنة مالية نشطة.");

                    var yearWithPeriods = await _fiscalYearRepo.GetWithPeriodsAsync(fiscalYear.Id, ct);
                    var period = yearWithPeriods.GetPeriod(adjustment.AdjustmentDate.Month)
                        ?? throw new InventoryDomainException("لا توجد فترة مالية للشهر المحدد.");

                    if (!period.IsOpen)
                        throw new InventoryDomainException($"الفترة المالية ({period.Year}-{period.Month:D2}) مقفلة.");

                    var inventoryAccount = await _accountRepo.GetByCodeAsync(InventoryAccountCode, ct);
                    var now = _dateTime.UtcNow;
                    var username = _currentUser.Username ?? "System";

                    // Create journal entry
                    var journal = JournalEntry.CreateDraft(
                        adjustment.AdjustmentDate,
                        $"تسوية مخزنية رقم {adjustment.AdjustmentNumber} — {adjustment.Reason}",
                        SourceType.Adjustment,
                        fiscalYear.Id,
                        period.Id,
                        referenceNumber: adjustment.AdjustmentNumber);

                    decimal totalSurplus = 0;
                    decimal totalShortage = 0;

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

                            movement.SetBalanceAfter(whProduct.Quantity);
                            await _movementRepo.AddAsync(movement, ct);
                        }
                    }

                    // Surplus: DR Inventory / CR AdjustmentIncome
                    if (totalSurplus > 0)
                    {
                        var incomeAccount = await _accountRepo.GetByCodeAsync(AdjustmentIncomeAccountCode, ct);
                        if (inventoryAccount != null)
                            journal.AddLine(inventoryAccount.Id, totalSurplus, 0, now,
                                $"فائض مخزني — تسوية {adjustment.AdjustmentNumber}");
                        if (incomeAccount != null)
                            journal.AddLine(incomeAccount.Id, 0, totalSurplus, now,
                                $"إيراد فائض مخزني — تسوية {adjustment.AdjustmentNumber}");
                    }

                    // Shortage: DR AdjustmentExpense / CR Inventory
                    if (totalShortage > 0)
                    {
                        var expenseAccount = await _accountRepo.GetByCodeAsync(AdjustmentExpenseAccountCode, ct);
                        if (expenseAccount != null)
                            journal.AddLine(expenseAccount.Id, totalShortage, 0, now,
                                $"عجز مخزني — تسوية {adjustment.AdjustmentNumber}");
                        if (inventoryAccount != null)
                            journal.AddLine(inventoryAccount.Id, 0, totalShortage, now,
                                $"مخزون — عجز تسوية {adjustment.AdjustmentNumber}");
                    }

                    var journalNumber = _journalNumberGen.NextNumber(fiscalYear.Id);
                    journal.Post(journalNumber, username, now);
                    await _journalRepo.AddAsync(journal, ct);
                    await _unitOfWork.SaveChangesAsync(ct);

                    adjustment.Post(journal.Id);
                    _adjustmentRepo.Update(adjustment);
                    await _unitOfWork.SaveChangesAsync(ct);

                    saved = await _adjustmentRepo.GetWithLinesAsync(adjustment.Id, ct);
                }, IsolationLevel.Serializable, ct);

                return ServiceResult<InventoryAdjustmentDto>.Success(MapToDto(saved));
            }
            catch (InventoryDomainException ex)
            {
                return ServiceResult<InventoryAdjustmentDto>.Failure(ex.Message);
            }
            catch (Exception ex)
            {
                return ServiceResult<InventoryAdjustmentDto>.Failure($"خطأ أثناء ترحيل التسوية: {ex.Message}");
            }
        }

        public async Task<ServiceResult> CancelAsync(int id, CancellationToken ct = default)
        {
            var authCheck = AuthorizationGuard.Check(_currentUser, PermissionKeys.InventoryManage);
            if (authCheck != null) return authCheck;

            var adjustment = await _adjustmentRepo.GetWithLinesAsync(id, ct);
            if (adjustment == null)
                return ServiceResult.Failure("التسوية غير موجودة.");

            if (adjustment.Status != InvoiceStatus.Posted)
                return ServiceResult.Failure("لا يمكن إلغاء إلا التسويات المرحّلة.");

            try
            {
                await _unitOfWork.ExecuteInTransactionAsync(async () =>
                {
                    // Reverse stock movements
                    foreach (var line in adjustment.Lines)
                    {
                        var whProduct = await _whProductRepo.GetOrCreateAsync(
                            adjustment.WarehouseId, line.ProductId, ct);

                        var absQty = Math.Abs(line.DifferenceInBaseUnit);
                        if (absQty > 0)
                        {
                            // Reverse: if original was increase, decrease now
                            if (line.DifferenceInBaseUnit > 0)
                                whProduct.DecreaseStock(absQty);
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
                            var fiscalYear = await _fiscalYearRepo.GetActiveYearAsync(ct);
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

                            var number = _journalNumberGen.NextNumber(fiscalYear.Id);
                            reversal.Post(number, _currentUser.Username, _dateTime.UtcNow);
                            await _journalRepo.AddAsync(reversal, ct);
                            await _unitOfWork.SaveChangesAsync(ct);

                            journal.MarkAsReversed(reversal.Id);
                            _journalRepo.Update(journal);
                        }
                    }

                    adjustment.Cancel();
                    _adjustmentRepo.Update(adjustment);
                    await _unitOfWork.SaveChangesAsync(ct);
                }, IsolationLevel.Serializable, ct);

                return ServiceResult.Success();
            }
            catch (Exception ex)
            {
                return ServiceResult.Failure($"فشل إلغاء التسوية: {ex.Message}");
            }
        }

        public async Task<ServiceResult> DeleteDraftAsync(int id, CancellationToken ct = default)
        {
            var authCheck = AuthorizationGuard.Check(_currentUser, PermissionKeys.InventoryManage);
            if (authCheck != null) return authCheck;

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
    }
}
