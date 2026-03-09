using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using MarcoERP.Application.Common;
using MarcoERP.Application.DTOs.Purchases;
using MarcoERP.Application.Interfaces;
using MarcoERP.Application.Interfaces.Purchases;
using MarcoERP.Application.Mappers.Purchases;
using MarcoERP.Domain.Entities.Accounting;
using MarcoERP.Domain.Entities.Accounting.Policies;
using MarcoERP.Domain.Entities.Inventory;
using MarcoERP.Domain.Entities.Purchases;
using MarcoERP.Domain.Enums;
using MarcoERP.Domain.Exceptions;
using MarcoERP.Domain.Exceptions.Accounting;
using MarcoERP.Application.Interfaces.Settings;
using MarcoERP.Domain.Exceptions.Inventory;
using MarcoERP.Domain.Exceptions.Purchases;
using MarcoERP.Domain.Interfaces;
using MarcoERP.Domain.Interfaces.Inventory;
using MarcoERP.Domain.Interfaces.Purchases;
using MarcoERP.Domain.Interfaces.Settings;
using Microsoft.Extensions.Logging;

namespace MarcoERP.Application.Services.Purchases
{
    /// <summary>
    /// Implements purchase invoice lifecycle: Create → Edit → Post → (Cancel).
    /// On Post: auto-generates journal, updates WAC, creates stock movements.
    /// </summary>
    [Module(SystemModule.Purchases)]
    public sealed class PurchaseInvoiceService : IPurchaseInvoiceService
    {
        private readonly IPurchaseInvoiceRepository _invoiceRepo;
        private readonly IProductRepository _productRepo;
        private readonly IJournalEntryRepository _journalRepo;
        private readonly IAccountRepository _accountRepo;
        private readonly IFiscalYearRepository _fiscalYearRepo;
        private readonly IJournalNumberGenerator _journalNumberGen;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ICurrentUserService _currentUser;
        private readonly IDateTimeProvider _dateTime;
        private readonly IValidator<CreatePurchaseInvoiceDto> _createValidator;
        private readonly IValidator<UpdatePurchaseInvoiceDto> _updateValidator;
        private readonly ILogger<PurchaseInvoiceService> _logger;
        private readonly ISystemSettingRepository _systemSettingRepository;
        private readonly JournalEntryFactory _journalFactory;
        private readonly FiscalPeriodValidator _fiscalValidator;
        private readonly IFeatureService _featureService;
        private readonly StockManager _stockManager;
        private readonly ISupplierRepository _supplierRepo;

        // ── GL Account Codes (from SystemAccountSeed) ───────────
        // 1131 = المخزون — المستودع الرئيسي (Inventory)
        // 1141 = ضريبة مدخلات مستحقة (VAT Input)
        // 2111 = الدائنون — ذمم تجارية (AP — Trade Payables)
        private const string InventoryAccountCode = "1131";
        private const string VatInputAccountCode = "1141";
        private const string ApAccountCode = "2111";
        private const string InvoiceNotFoundMessage = "فاتورة الشراء غير موجودة.";

        public PurchaseInvoiceService(
            PurchaseInvoiceRepositories repos,
            PurchaseInvoiceServices services,
            PurchaseInvoiceValidators validators,
            JournalEntryFactory journalFactory,
            FiscalPeriodValidator fiscalValidator,
            StockManager stockManager,
            ILogger<PurchaseInvoiceService> logger = null,
            IFeatureService featureService = null)
        {
            if (repos == null) throw new ArgumentNullException(nameof(repos));
            if (services == null) throw new ArgumentNullException(nameof(services));
            if (validators == null) throw new ArgumentNullException(nameof(validators));

            _invoiceRepo = repos.InvoiceRepo;
            _productRepo = repos.ProductRepo;
            _journalRepo = repos.JournalRepo;
            _accountRepo = repos.AccountRepo;

            _fiscalYearRepo = services.FiscalYearRepo;
            _journalNumberGen = services.JournalNumberGen;
            _unitOfWork = services.UnitOfWork;
            _currentUser = services.CurrentUser;
            _dateTime = services.DateTime;
            _systemSettingRepository = services.SystemSettingRepo;

            _createValidator = validators.CreateValidator;
            _updateValidator = validators.UpdateValidator;
            _journalFactory = journalFactory ?? throw new ArgumentNullException(nameof(journalFactory));
            _fiscalValidator = fiscalValidator ?? throw new ArgumentNullException(nameof(fiscalValidator));
            _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<PurchaseInvoiceService>.Instance;
            _featureService = featureService;
            _stockManager = stockManager ?? throw new ArgumentNullException(nameof(stockManager));
            _supplierRepo = repos.SupplierRepo;
        }

        public async Task<ServiceResult<IReadOnlyList<PurchaseInvoiceListDto>>> GetAllAsync(CancellationToken ct = default)
        {
            var entities = await _invoiceRepo.GetAllAsync(ct);
            return ServiceResult<IReadOnlyList<PurchaseInvoiceListDto>>.Success(
                entities.Select(PurchaseInvoiceMapper.ToListDto).ToList());
        }

        public async Task<ServiceResult<PurchaseInvoiceDto>> GetByIdAsync(int id, CancellationToken ct = default)
        {
            var entity = await _invoiceRepo.GetWithLinesAsync(id, ct);
            if (entity == null)
                return ServiceResult<PurchaseInvoiceDto>.Failure(InvoiceNotFoundMessage);

            return ServiceResult<PurchaseInvoiceDto>.Success(PurchaseInvoiceMapper.ToDto(entity));
        }

        public async Task<ServiceResult<string>> GetNextNumberAsync(CancellationToken ct = default)
        {
            var next = await _invoiceRepo.GetNextNumberAsync(ct);
            return ServiceResult<string>.Success(next);
        }

        public async Task<ServiceResult<PurchaseInvoiceDto>> CreateAsync(CreatePurchaseInvoiceDto dto, CancellationToken ct = default)
        {
            // Feature Guard — block operation if Purchases module is disabled
            if (_featureService != null)
            {
                var guard = await FeatureGuard.CheckAsync<PurchaseInvoiceDto>(_featureService, FeatureKeys.Purchases, ct);
                if (guard != null) return guard;
            }

            _logger.LogInformation("Operation={Operation} Entity={Entity} EntityId={EntityId}", "CreateAsync", "PurchaseInvoice", 0);

            var vr = await _createValidator.ValidateAsync(dto, ct);
            if (!vr.IsValid)
                return ServiceResult<PurchaseInvoiceDto>.Failure(
                    string.Join(" | ", vr.Errors.Select(e => e.ErrorMessage)));

            // SD-05 fix: Validate FK references exist and are not soft-deleted
            if (dto.SupplierId.HasValue)
            {
                var supplier = await _supplierRepo.GetByIdAsync(dto.SupplierId.Value, ct);
                if (supplier == null)
                    return ServiceResult<PurchaseInvoiceDto>.Failure("المورد غير موجود أو محذوف.");
            }

            const int maxRetries = 3;
            var attempt = 0;

            while (attempt < maxRetries)
            {
                attempt++;

                try
                {
                    PurchaseInvoice invoice = null;

                    await _unitOfWork.ExecuteInTransactionAsync(async () =>
                    {
                        var invoiceNumber = await _invoiceRepo.GetNextNumberAsync(ct);

                        invoice = new PurchaseInvoice(
                            invoiceNumber,
                            dto.InvoiceDate,
                            dto.SupplierId,
                            dto.WarehouseId,
                            dto.Notes,
                            salesRepresentativeId: dto.SalesRepresentativeId,
                            counterpartyType: dto.CounterpartyType,
                            customerId: dto.CounterpartyCustomerId,
                            invoiceType: dto.InvoiceType,
                            paymentMethod: dto.PaymentMethod,
                            dueDate: dto.DueDate);

                        // Apply header-level discount & delivery fee
                        invoice.UpdateHeader(
                            dto.InvoiceDate,
                            dto.SupplierId,
                            dto.WarehouseId,
                            dto.Notes,
                            salesRepresentativeId: dto.SalesRepresentativeId,
                            counterpartyType: dto.CounterpartyType,
                            customerId: dto.CounterpartyCustomerId,
                            headerDiscountPercent: dto.HeaderDiscountPercent,
                            headerDiscountAmount: dto.HeaderDiscountAmount,
                            deliveryFee: dto.DeliveryFee,
                            invoiceType: dto.InvoiceType,
                            paymentMethod: dto.PaymentMethod,
                            dueDate: dto.DueDate);

                        // Add lines — look up conversion factor from product units
                        foreach (var lineDto in dto.Lines)
                        {
                            var product = await _productRepo.GetByIdWithUnitsAsync(lineDto.ProductId, ct);
                            if (product == null)
                                throw new PurchaseInvoiceDomainException($"الصنف برقم {lineDto.ProductId} غير موجود.");

                            var productUnit = product.ProductUnits.FirstOrDefault(pu => pu.UnitId == lineDto.UnitId);
                            if (productUnit == null)
                                throw new PurchaseInvoiceDomainException(
                                    $"الوحدة المحددة غير مرتبطة بالصنف ({product.NameAr}).");

                            invoice.AddLine(
                                lineDto.ProductId,
                                lineDto.UnitId,
                                lineDto.Quantity,
                                lineDto.UnitPrice,
                                productUnit.ConversionFactor,
                                lineDto.DiscountPercent,
                                product.VatRate);
                        }

                        await _invoiceRepo.AddAsync(invoice, ct);
                        await _unitOfWork.SaveChangesAsync(ct);
                    }, IsolationLevel.Serializable, ct);

                    var saved = await _invoiceRepo.GetWithLinesAsync(invoice.Id, ct);
                    return ServiceResult<PurchaseInvoiceDto>.Success(PurchaseInvoiceMapper.ToDto(saved));
                }
                catch (DuplicateRecordException) when (attempt < maxRetries)
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(100 * attempt), ct);
                    continue;
                }
                catch (ConcurrencyConflictException)
                {
                    return ServiceResult<PurchaseInvoiceDto>.Failure("تم تعديل الفاتورة بواسطة مستخدم آخر. يرجى إعادة المحاولة.");
                }
                catch (DuplicateRecordException)
                {
                    return ServiceResult<PurchaseInvoiceDto>.Failure(
                        "فشل حفظ فاتورة الشراء بسبب تعارض في رقم الفاتورة. يرجى إعادة المحاولة.");
                }
                catch (PurchaseInvoiceDomainException ex)
                {
                    return ServiceResult<PurchaseInvoiceDto>.Failure(ex.Message);
                }
            }

            return ServiceResult<PurchaseInvoiceDto>.Failure("فشل حفظ فاتورة الشراء بعد عدة محاولات.");
        }

        public async Task<ServiceResult<PurchaseInvoiceDto>> UpdateAsync(UpdatePurchaseInvoiceDto dto, CancellationToken ct = default)
        {
            _logger.LogInformation("Operation={Operation} Entity={Entity} EntityId={EntityId}", "UpdateAsync", "PurchaseInvoice", dto.Id);

            var vr = await _updateValidator.ValidateAsync(dto, ct);
            if (!vr.IsValid)
                return ServiceResult<PurchaseInvoiceDto>.Failure(
                    string.Join(" | ", vr.Errors.Select(e => e.ErrorMessage)));

            var invoice = await _invoiceRepo.GetWithLinesTrackedAsync(dto.Id, ct);
            if (invoice == null)
                return ServiceResult<PurchaseInvoiceDto>.Failure(InvoiceNotFoundMessage);

            if (invoice.Status != InvoiceStatus.Draft)
                return ServiceResult<PurchaseInvoiceDto>.Failure("لا يمكن تعديل فاتورة مرحّلة أو ملغاة.");

            try
            {
                invoice.UpdateHeader(dto.InvoiceDate, dto.SupplierId, dto.WarehouseId, dto.Notes,
                    salesRepresentativeId: dto.SalesRepresentativeId,
                    counterpartyType: dto.CounterpartyType, customerId: dto.CounterpartyCustomerId,
                    headerDiscountPercent: dto.HeaderDiscountPercent,
                    headerDiscountAmount: dto.HeaderDiscountAmount,
                    deliveryFee: dto.DeliveryFee,
                    invoiceType: dto.InvoiceType,
                    paymentMethod: dto.PaymentMethod,
                    dueDate: dto.DueDate);

                // Rebuild lines from DTO
                var newLines = new List<PurchaseInvoiceLine>();
                foreach (var lineDto in dto.Lines)
                {
                    var product = await _productRepo.GetByIdWithUnitsAsync(lineDto.ProductId, ct);
                    if (product == null)
                        return ServiceResult<PurchaseInvoiceDto>.Failure($"الصنف برقم {lineDto.ProductId} غير موجود.");

                    var productUnit = product.ProductUnits.FirstOrDefault(pu => pu.UnitId == lineDto.UnitId);
                    if (productUnit == null)
                        return ServiceResult<PurchaseInvoiceDto>.Failure(
                            $"الوحدة المحددة غير مرتبطة بالصنف ({product.NameAr}).");

                    newLines.Add(new PurchaseInvoiceLine(
                        lineDto.ProductId,
                        lineDto.UnitId,
                        lineDto.Quantity,
                        lineDto.UnitPrice,
                        productUnit.ConversionFactor,
                        lineDto.DiscountPercent,
                        product.VatRate,
                        lineDto.Id));
                }

                invoice.ReplaceLines(newLines);
                // Entity is already tracked — no need for _invoiceRepo.Update(invoice)
                await _unitOfWork.SaveChangesAsync(ct);

                var saved = await _invoiceRepo.GetWithLinesAsync(invoice.Id, ct);
                return ServiceResult<PurchaseInvoiceDto>.Success(PurchaseInvoiceMapper.ToDto(saved));
            }
            catch (PurchaseInvoiceDomainException ex)
            {
                return ServiceResult<PurchaseInvoiceDto>.Failure(ex.Message);
            }
        }

        /// <summary>
        /// Posts a draft purchase invoice. This triggers:
        /// 1. Auto journal: DR Inventory + DR VAT Input / CR AP
        /// 2. WAC recalculation for each product
        /// 3. Stock movement records + warehouse stock increase
        /// </summary>
        public async Task<ServiceResult<PurchaseInvoiceDto>> PostAsync(int id, CancellationToken ct = default)
        {
            _logger.LogInformation("Operation={Operation} Entity={Entity} EntityId={EntityId}", "PostAsync", "PurchaseInvoice", id);

            var invoice = await _invoiceRepo.GetWithLinesAsync(id, ct);
            if (invoice == null)
                return ServiceResult<PurchaseInvoiceDto>.Failure(InvoiceNotFoundMessage);

            if (invoice.Status != InvoiceStatus.Draft)
                return ServiceResult<PurchaseInvoiceDto>.Failure("لا يمكن ترحيل فاتورة مرحّلة بالفعل أو ملغاة.");

            if (!invoice.Lines.Any())
                return ServiceResult<PurchaseInvoiceDto>.Failure("لا يمكن ترحيل فاتورة بدون بنود.");

            try
            {
                var saved = await ExecutePostAsync(invoice, ct);
                return ServiceResult<PurchaseInvoiceDto>.Success(PurchaseInvoiceMapper.ToDto(saved));
            }
            catch (PurchaseInvoiceDomainException ex)
            {
                return ServiceResult<PurchaseInvoiceDto>.Failure(ex.Message);
            }
            catch (JournalEntryDomainException ex)
            {
                return ServiceResult<PurchaseInvoiceDto>.Failure(ex.Message);
            }
            catch (InventoryDomainException ex)
            {
                return ServiceResult<PurchaseInvoiceDto>.Failure(ex.Message);
            }
            catch (AccountDomainException ex)
            {
                return ServiceResult<PurchaseInvoiceDto>.Failure(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogError(ex, "InvalidOperationException while posting purchase invoice {InvoiceId}.", invoice?.Id);
                return ServiceResult<PurchaseInvoiceDto>.Failure(
                    ErrorSanitizer.Sanitize(ex, "ترحيل فاتورة الشراء"));
            }
            catch (ConcurrencyConflictException)
            {
                return ServiceResult<PurchaseInvoiceDto>.Failure("تعذر ترحيل الفاتورة بسبب تعارض تحديث متزامن. يرجى إعادة المحاولة.");
            }
            catch (DuplicateRecordException)
            {
                return ServiceResult<PurchaseInvoiceDto>.Failure("تعذر ترحيل الفاتورة بسبب تعارض بيانات فريدة. يرجى إعادة المحاولة.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to post purchase invoice {InvoiceId}.", invoice?.Id);
                return ServiceResult<PurchaseInvoiceDto>.Failure("حدث خطأ غير متوقع أثناء ترحيل الفاتورة.");
            }
        }

        public async Task<ServiceResult> CancelAsync(int id, CancellationToken ct = default)
        {
            _logger.LogInformation("Operation={Operation} Entity={Entity} EntityId={EntityId}", "CancelAsync", "PurchaseInvoice", id);

            var invoice = await _invoiceRepo.GetWithLinesAsync(id, ct);
            if (invoice == null)
                return ServiceResult.Failure(InvoiceNotFoundMessage);

            if (invoice.Status != InvoiceStatus.Posted)
                return ServiceResult.Failure("لا يمكن إلغاء إلا الفواتير المرحّلة.");

            if (!invoice.JournalEntryId.HasValue)
                return ServiceResult.Failure("لا يمكن إلغاء فاتورة بدون قيد محاسبي.");

            if (invoice.PaidAmount > 0)
                return ServiceResult.Failure(
                    $"لا يمكن إلغاء فاتورة عليها دفعات ({invoice.PaidAmount:N2}). يجب إلغاء سندات الصرف المرتبطة أولاً.");

            try
            {
                var context = await _fiscalValidator.ValidateForCancelAsync(invoice.InvoiceDate, ct);
                await ExecuteCancelAsync(invoice, context, ct);
                return ServiceResult.Success();
            }
            catch (PurchaseInvoiceDomainException ex)
            {
                return ServiceResult.Failure(ex.Message);
            }
            catch (JournalEntryDomainException ex)
            {
                return ServiceResult.Failure(ex.Message);
            }
            catch (InventoryDomainException ex)
            {
                return ServiceResult.Failure(ex.Message);
            }
            catch (AccountDomainException ex)
            {
                return ServiceResult.Failure(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogError(ex, "InvalidOperationException while cancelling purchase invoice {InvoiceId}.", invoice?.Id);
                return ServiceResult.Failure(
                    ErrorSanitizer.Sanitize(ex, "إلغاء فاتورة الشراء"));
            }
            catch (ConcurrencyConflictException)
            {
                return ServiceResult.Failure("تعذر إلغاء الفاتورة بسبب تعارض تحديث متزامن. يرجى إعادة المحاولة.");
            }
            catch (DuplicateRecordException)
            {
                return ServiceResult.Failure("تعذر إلغاء الفاتورة بسبب تعارض بيانات فريدة. يرجى إعادة المحاولة.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to cancel purchase invoice {InvoiceId}.", invoice?.Id);
                return ServiceResult.Failure("حدث خطأ غير متوقع أثناء إلغاء فاتورة الشراء.");
            }
        }

        public async Task<ServiceResult> DeleteDraftAsync(int id, CancellationToken ct = default)
        {
            _logger.LogInformation("Operation={Operation} Entity={Entity} EntityId={EntityId}", "DeleteDraftAsync", "PurchaseInvoice", id);

            var invoice = await _invoiceRepo.GetWithLinesAsync(id, ct);
            if (invoice == null)
                return ServiceResult.Failure(InvoiceNotFoundMessage);

            if (invoice.Status != InvoiceStatus.Draft)
                return ServiceResult.Failure("لا يمكن حذف إلا الفواتير المسودة.");

            invoice.SoftDelete(_currentUser.Username, _dateTime.UtcNow);
            _invoiceRepo.Update(invoice);
            await _unitOfWork.SaveChangesAsync(ct);

            return ServiceResult.Success();
        }

        private async Task<PurchaseInvoice> ExecutePostAsync(PurchaseInvoice invoice, CancellationToken ct)
        {
            PurchaseInvoice saved = null;

            await _unitOfWork.ExecuteInTransactionAsync(async () =>
            {
                // MUST use tracked query inside posting transaction to avoid
                // EF Core "duplicate key" tracking conflicts when calling
                // Update() on detached entities with shared navigation graphs.
                var reloaded = await _invoiceRepo.GetWithLinesTrackedAsync(invoice.Id, ct);
                if (reloaded == null)
                    throw new PurchaseInvoiceDomainException(InvoiceNotFoundMessage);
                if (reloaded.Status != InvoiceStatus.Draft)
                    throw new PurchaseInvoiceDomainException("لا يمكن ترحيل فاتورة مرحّلة بالفعل أو ملغاة.");

                var context = await _fiscalValidator.ValidateForPostingAsync(reloaded.InvoiceDate, ct);
                var accounts = await ResolveAccountsAsync(ct);

                var journalEntry = await CreateJournalEntryAsync(reloaded, accounts, context, ct);

                await _unitOfWork.SaveChangesAsync(ct);

                await ApplyStockAndWacAsync(reloaded, ct);

                reloaded.Post(journalEntry.Id);
                // Entity is already tracked — no need for explicit Update
                await _unitOfWork.SaveChangesAsync(ct);

                saved = await _invoiceRepo.GetWithLinesAsync(reloaded.Id, ct);
            }, IsolationLevel.Serializable, ct);

            return saved ?? invoice;
        }



        private async Task<PurchaseInvoiceAccounts> ResolveAccountsAsync(CancellationToken ct)
        {
            var inventoryAccount = await _accountRepo.GetByCodeAsync(InventoryAccountCode, ct);
            var vatInputAccount = await _accountRepo.GetByCodeAsync(VatInputAccountCode, ct);
            var apAccount = await _accountRepo.GetByCodeAsync(ApAccountCode, ct);

            if (inventoryAccount == null || vatInputAccount == null || apAccount == null)
                throw new PurchaseInvoiceDomainException(
                    "حسابات النظام المطلوبة (مخزون / ضريبة مدخلات / دائنون) غير موجودة. تأكد من تشغيل Seed.");

            return new PurchaseInvoiceAccounts
            {
                Inventory = inventoryAccount,
                VatInput = vatInputAccount,
                Ap = apAccount
            };
        }

        private async Task<JournalEntry> CreateJournalEntryAsync(
            PurchaseInvoice invoice,
            PurchaseInvoiceAccounts accounts,
            PostingContext context,
            CancellationToken ct)
        {
            var netExVat = invoice.Subtotal - invoice.DiscountTotal + invoice.DeliveryFee;
            var lines = new List<JournalLineSpec>();

            if (netExVat > 0)
                lines.Add(new JournalLineSpec(accounts.Inventory.Id, netExVat, 0,
                    $"مخزون — فاتورة شراء {invoice.InvoiceNumber}"));

            if (invoice.VatTotal > 0)
                lines.Add(new JournalLineSpec(accounts.VatInput.Id, invoice.VatTotal, 0,
                    $"ضريبة مدخلات — فاتورة شراء {invoice.InvoiceNumber}"));

            lines.Add(new JournalLineSpec(accounts.Ap.Id, 0, invoice.NetTotal,
                $"مورد — فاتورة شراء {invoice.InvoiceNumber}"));

            return await _journalFactory.CreateAndPostAsync(
                invoice.InvoiceDate,
                $"فاتورة شراء رقم {invoice.InvoiceNumber}",
                SourceType.PurchaseInvoice,
                context.FiscalYear.Id,
                context.Period.Id,
                lines,
                context.Username,
                context.Now,
                referenceNumber: invoice.InvoiceNumber,
                sourceId: invoice.Id,
                ct: ct);
        }

        private async Task ApplyStockAndWacAsync(PurchaseInvoice invoice, CancellationToken ct)
        {
            var runningTotals = new Dictionary<int, (Product product, decimal totalQty)>();

            // Pre-compute proportional cost denominator once for the whole invoice.
            // This allocates the header discount and delivery fee across lines by their weight.
            var sumLineNetTotal = invoice.Lines.Sum(l => l.NetTotal);
            var totalExVat      = invoice.NetTotal - invoice.VatTotal;

            foreach (var line in invoice.Lines)
            {
                if (!runningTotals.TryGetValue(line.ProductId, out var state))
                {
                    var product = await _productRepo.GetByIdWithUnitsAsync(line.ProductId, ct);
                    var existingTotalQty = await _stockManager.GetTotalStockAsync(line.ProductId, ct);
                    state = (product, existingTotalQty);
                }

                // Proportional cost: each line carries its share of header discount + delivery fee.
                decimal costPerBaseUnit = 0m;
                if (line.BaseQuantity > 0)
                {
                    if (sumLineNetTotal > 0)
                    {
                        var lineShare      = line.NetTotal / sumLineNetTotal;
                        var effectiveCost  = Math.Round(totalExVat * lineShare, 4);
                        costPerBaseUnit    = Math.Round(effectiveCost / line.BaseQuantity, 4);
                    }
                    else
                    {
                        costPerBaseUnit = Math.Round(line.NetTotal / line.BaseQuantity, 4);
                    }
                }

                // Use running totals to keep WAC accurate across multiple lines for the same product.
                state.product.UpdateWeightedAverageCost(state.totalQty, line.BaseQuantity, costPerBaseUnit);
                state.totalQty += line.BaseQuantity;
                runningTotals[line.ProductId] = state;
                _productRepo.Update(state.product);

                await _stockManager.IncreaseAsync(new StockOperation
                {
                    ProductId = line.ProductId,
                    WarehouseId = invoice.WarehouseId,
                    UnitId = line.UnitId,
                    MovementType = MovementType.PurchaseIn,
                    Quantity = line.Quantity,
                    BaseQuantity = line.BaseQuantity,
                    CostPerBaseUnit = costPerBaseUnit,
                    DocumentDate = invoice.InvoiceDate,
                    DocumentNumber = invoice.InvoiceNumber,
                    SourceType = SourceType.PurchaseInvoice,
                    SourceId = invoice.Id,
                    Notes = $"فاتورة شراء رقم {invoice.InvoiceNumber}",
                }, ct);
            }
        }

        private async Task ExecuteCancelAsync(
            PurchaseInvoice invoice,
            CancelContext context,
            CancellationToken ct)
        {
            await _unitOfWork.ExecuteInTransactionAsync(async () =>
            {
                // Reload as tracked inside the transaction to avoid
                // unsafe graph traversal when calling Update on an untracked entity
                var tracked = await _invoiceRepo.GetWithLinesTrackedAsync(invoice.Id, ct)
                    ?? throw new PurchaseInvoiceDomainException(InvoiceNotFoundMessage);

                // Re-validate preconditions on the freshly-loaded entity inside the Serializable transaction
                // to guard against TOCTOU races (e.g., a payment applied between outer check and lock acquisition)
                if (tracked.Status != InvoiceStatus.Posted)
                    throw new PurchaseInvoiceDomainException("لا يمكن إلغاء فاتورة غير مرحّلة.");
                if (!tracked.JournalEntryId.HasValue)
                    throw new PurchaseInvoiceDomainException("لا يمكن إلغاء فاتورة بدون قيد محاسبي.");
                if (tracked.PaidAmount > 0)
                    throw new PurchaseInvoiceDomainException("لا يمكن إلغاء فاتورة تم سداد جزء منها أو كلها. يجب إلغاء سندات الصرف أولاً.");

                await ReverseStockAsync(tracked, context, ct);

                await ReverseJournalAsync(
                    tracked.JournalEntryId.Value,
                    $"عكس فاتورة شراء رقم {tracked.InvoiceNumber}",
                    "القيد المحاسبي الأصلي غير موجود.",
                    context,
                    ct);

                tracked.Cancel();
                // Entity is already tracked — no need for explicit Update
                await _unitOfWork.SaveChangesAsync(ct);

            }, IsolationLevel.Serializable, ct);
        }

        private async Task ReverseStockAsync(
            PurchaseInvoice invoice,
            CancelContext context,
            CancellationToken ct)
        {
            // Running totals to handle duplicate product lines in same invoice (mirrors posting logic)
            var runningDeductions = new Dictionary<int, decimal>();
            var runningProducts = new Dictionary<int, Product>();
            var allowNegativeStock = await IsNegativeStockAllowedAsync(ct);

            // Pre-compute proportional cost denominator once (must match ApplyStockAndWacAsync).
            var sumLineNetTotal = invoice.Lines.Sum(l => l.NetTotal);
            var totalExVat      = invoice.NetTotal - invoice.VatTotal;

            foreach (var line in invoice.Lines)
            {
                // Proportional cost: each line carries its share of header discount + delivery fee.
                decimal costPerBaseUnit = 0m;
                if (line.BaseQuantity > 0)
                {
                    if (sumLineNetTotal > 0)
                    {
                        var lineShare      = line.NetTotal / sumLineNetTotal;
                        var effectiveCost  = Math.Round(totalExVat * lineShare, 4);
                        costPerBaseUnit    = Math.Round(effectiveCost / line.BaseQuantity, 4);
                    }
                    else
                    {
                        costPerBaseUnit = Math.Round(line.NetTotal / line.BaseQuantity, 4);
                    }
                }

                await _stockManager.DecreaseAsync(new StockOperation
                {
                    ProductId = line.ProductId,
                    WarehouseId = invoice.WarehouseId,
                    UnitId = line.UnitId,
                    MovementType = MovementType.PurchaseReturn,
                    Quantity = line.Quantity,
                    BaseQuantity = line.BaseQuantity,
                    CostPerBaseUnit = costPerBaseUnit,
                    DocumentDate = context.Today,
                    DocumentNumber = invoice.InvoiceNumber,
                    SourceType = SourceType.PurchaseInvoice,
                    SourceId = invoice.Id,
                    Notes = $"إلغاء فاتورة شراء رقم {invoice.InvoiceNumber}",
                    AllowCreate = false,
                    AllowNegativeStock = allowNegativeStock,
                }, ct);

                // Track cumulative deductions per product for stale-DB correction
                if (!runningDeductions.TryGetValue(line.ProductId, out var previousDeductions))
                    previousDeductions = 0;

                // Recalculate WAC after removing this purchase batch's contribution
                if (!runningProducts.TryGetValue(line.ProductId, out var product))
                {
                    product = await _productRepo.GetByIdWithUnitsAsync(line.ProductId, ct);
                    runningProducts[line.ProductId] = product;
                }
                var dbTotalQty = await _stockManager.GetTotalStockAsync(line.ProductId, ct);
                // DB hasn't been saved yet, so subtract all deductions made in this loop
                var remainingTotalQty = dbTotalQty - previousDeductions - line.BaseQuantity;

                if (remainingTotalQty > 0)
                {
                    // Reverse the WAC: remove this batch's cost contribution
                    var totalValueBefore = product.WeightedAverageCost * (remainingTotalQty + line.BaseQuantity);
                    var batchValue = line.BaseQuantity * costPerBaseUnit;
                    var newWac = (totalValueBefore - batchValue) / remainingTotalQty;
                    product.SetWeightedAverageCost(Math.Round(newWac, 4));
                }
                else
                {
                    product.SetWeightedAverageCost(product.CostPrice); // Fallback to base cost
                }
                _productRepo.Update(product);

                runningDeductions[line.ProductId] = previousDeductions + line.BaseQuantity;
            }
        }

        private async Task ReverseJournalAsync(
            int journalId,
            string description,
            string notFoundMessage,
            CancelContext context,
            CancellationToken ct)
        {
            var originalJournal = await _journalRepo.GetWithLinesAsync(journalId, ct);
            if (originalJournal == null)
                throw new PurchaseInvoiceDomainException(notFoundMessage);

            var reversalEntry = originalJournal.CreateReversal(
                context.Today,
                description,
                context.FiscalYear.Id,
                context.Period.Id);

            var reversalNumber = await _journalNumberGen.NextNumberAsync(context.FiscalYear.Id, ct);
            var username = _currentUser.Username ?? "System";
            var now = _dateTime.UtcNow;
            reversalEntry.Post(reversalNumber, username, now);
            await _journalRepo.AddAsync(reversalEntry, ct);
            await _unitOfWork.SaveChangesAsync(ct);

            originalJournal.MarkAsReversed(reversalEntry.Id);
            _journalRepo.Update(originalJournal);
        }

        private sealed class PurchaseInvoiceAccounts
        {
            public Account Inventory { get; init; } = default!;
            public Account VatInput { get; init; } = default!;
            public Account Ap { get; init; } = default!;
        }

        private async Task<bool> IsNegativeStockAllowedAsync(CancellationToken ct)
        {
            if (_featureService == null)
                return false;

            var result = await _featureService.IsEnabledAsync(FeatureKeys.AllowNegativeStock, ct);
            return result.IsSuccess && result.Data;
        }

    }

    public sealed class PurchaseInvoiceRepositories
    {
        public PurchaseInvoiceRepositories(
            IPurchaseInvoiceRepository invoiceRepo,
            IProductRepository productRepo,
            IWarehouseProductRepository whProductRepo,
            IInventoryMovementRepository movementRepo,
            IJournalEntryRepository journalRepo,
            IAccountRepository accountRepo,
            ISupplierRepository supplierRepo)
        {
            InvoiceRepo = invoiceRepo ?? throw new ArgumentNullException(nameof(invoiceRepo));
            ProductRepo = productRepo ?? throw new ArgumentNullException(nameof(productRepo));
            WhProductRepo = whProductRepo ?? throw new ArgumentNullException(nameof(whProductRepo));
            MovementRepo = movementRepo ?? throw new ArgumentNullException(nameof(movementRepo));
            JournalRepo = journalRepo ?? throw new ArgumentNullException(nameof(journalRepo));
            AccountRepo = accountRepo ?? throw new ArgumentNullException(nameof(accountRepo));
            SupplierRepo = supplierRepo ?? throw new ArgumentNullException(nameof(supplierRepo));
        }

        public IPurchaseInvoiceRepository InvoiceRepo { get; }
        public IProductRepository ProductRepo { get; }
        public IWarehouseProductRepository WhProductRepo { get; }
        public IInventoryMovementRepository MovementRepo { get; }
        public IJournalEntryRepository JournalRepo { get; }
        public IAccountRepository AccountRepo { get; }
        public ISupplierRepository SupplierRepo { get; }
    }

    public sealed class PurchaseInvoiceServices
    {
        public PurchaseInvoiceServices(
            IFiscalYearRepository fiscalYearRepo,
            IJournalNumberGenerator journalNumberGen,
            IUnitOfWork unitOfWork,
            ICurrentUserService currentUser,
            IDateTimeProvider dateTime,
            ISystemSettingRepository systemSettingRepo = null)
        {
            FiscalYearRepo = fiscalYearRepo ?? throw new ArgumentNullException(nameof(fiscalYearRepo));
            JournalNumberGen = journalNumberGen ?? throw new ArgumentNullException(nameof(journalNumberGen));
            UnitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
            CurrentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
            DateTime = dateTime ?? throw new ArgumentNullException(nameof(dateTime));
            SystemSettingRepo = systemSettingRepo;
        }

        public IFiscalYearRepository FiscalYearRepo { get; }
        public IJournalNumberGenerator JournalNumberGen { get; }
        public IUnitOfWork UnitOfWork { get; }
        public ICurrentUserService CurrentUser { get; }
        public IDateTimeProvider DateTime { get; }
        public ISystemSettingRepository SystemSettingRepo { get; }
    }

    public sealed class PurchaseInvoiceValidators
    {
        public PurchaseInvoiceValidators(
            IValidator<CreatePurchaseInvoiceDto> createValidator,
            IValidator<UpdatePurchaseInvoiceDto> updateValidator)
        {
            CreateValidator = createValidator ?? throw new ArgumentNullException(nameof(createValidator));
            UpdateValidator = updateValidator ?? throw new ArgumentNullException(nameof(updateValidator));
        }

        public IValidator<CreatePurchaseInvoiceDto> CreateValidator { get; }
        public IValidator<UpdatePurchaseInvoiceDto> UpdateValidator { get; }
    }
}
