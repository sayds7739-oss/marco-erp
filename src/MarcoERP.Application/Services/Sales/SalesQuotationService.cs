using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using MarcoERP.Application.Common;
using MarcoERP.Application.DTOs.Sales;
using MarcoERP.Application.Interfaces;
using MarcoERP.Application.Interfaces.Sales;
using MarcoERP.Application.Mappers.Sales;
using MarcoERP.Domain.Entities.Sales;
using MarcoERP.Domain.Enums;
using MarcoERP.Domain.Exceptions;
using MarcoERP.Domain.Exceptions.Sales;
using System.Data;
using MarcoERP.Domain.Interfaces;
using MarcoERP.Domain.Interfaces.Inventory;
using MarcoERP.Domain.Interfaces.Sales;
using MarcoERP.Application.Interfaces.Settings;
using Microsoft.Extensions.Logging;

namespace MarcoERP.Application.Services.Sales
{
    /// <summary>
    /// Implements sales quotation lifecycle: Create → Send → Accept/Reject → Convert to Invoice.
    /// No journal entries or stock movements — those happen on the invoice.
    /// </summary>
    [Module(SystemModule.Sales)]
    public sealed class SalesQuotationService : ISalesQuotationService
    {
        private readonly ISalesQuotationRepository _quotationRepo;
        private readonly ISalesInvoiceRepository _invoiceRepo;
        private readonly IProductRepository _productRepo;
        private readonly ICustomerRepository _customerRepo;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ICurrentUserService _currentUser;
        private readonly IDateTimeProvider _dateTime;
        private readonly IValidator<CreateSalesQuotationDto> _createValidator;
        private readonly IValidator<UpdateSalesQuotationDto> _updateValidator;
        private readonly ILogger<SalesQuotationService> _logger;
        private readonly IFeatureService _featureService;

        private const string QuotationNotFoundMessage = "عرض السعر غير موجود.";

        public SalesQuotationService(
            ISalesQuotationRepository quotationRepo,
            ISalesInvoiceRepository invoiceRepo,
            IProductRepository productRepo,
            ICustomerRepository customerRepo,
            IUnitOfWork unitOfWork,
            ICurrentUserService currentUser,
            IDateTimeProvider dateTime,
            IValidator<CreateSalesQuotationDto> createValidator,
            IValidator<UpdateSalesQuotationDto> updateValidator,
            ILogger<SalesQuotationService> logger = null,
            IFeatureService featureService = null)
        {
            _quotationRepo = quotationRepo ?? throw new ArgumentNullException(nameof(quotationRepo));
            _invoiceRepo = invoiceRepo ?? throw new ArgumentNullException(nameof(invoiceRepo));
            _productRepo = productRepo ?? throw new ArgumentNullException(nameof(productRepo));
            _customerRepo = customerRepo ?? throw new ArgumentNullException(nameof(customerRepo));
            _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
            _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
            _dateTime = dateTime ?? throw new ArgumentNullException(nameof(dateTime));
            _createValidator = createValidator ?? throw new ArgumentNullException(nameof(createValidator));
            _updateValidator = updateValidator ?? throw new ArgumentNullException(nameof(updateValidator));
            _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<SalesQuotationService>.Instance;
            _featureService = featureService;
        }

        // ══════════════════════════════════════════════════════════
        //  QUERIES
        // ══════════════════════════════════════════════════════════

        public async Task<ServiceResult<IReadOnlyList<SalesQuotationListDto>>> GetAllAsync(CancellationToken ct = default)
        {
            var entities = await _quotationRepo.GetAllAsync(ct);
            return ServiceResult<IReadOnlyList<SalesQuotationListDto>>.Success(
                entities.Select(SalesQuotationMapper.ToListDto).ToList());
        }

        public async Task<ServiceResult<SalesQuotationDto>> GetByIdAsync(int id, CancellationToken ct = default)
        {
            var entity = await _quotationRepo.GetWithLinesAsync(id, ct);
            if (entity == null)
                return ServiceResult<SalesQuotationDto>.Failure(QuotationNotFoundMessage);
            return ServiceResult<SalesQuotationDto>.Success(SalesQuotationMapper.ToDto(entity));
        }

        public async Task<ServiceResult<string>> GetNextNumberAsync(CancellationToken ct = default)
        {
            var next = await _quotationRepo.GetNextNumberAsync(ct);
            return ServiceResult<string>.Success(next);
        }

        // ══════════════════════════════════════════════════════════
        //  CREATE (Draft)
        // ══════════════════════════════════════════════════════════

        public async Task<ServiceResult<SalesQuotationDto>> CreateAsync(CreateSalesQuotationDto dto, CancellationToken ct = default)
        {
            _logger.LogInformation("Operation={Operation} Entity={Entity} EntityId={EntityId}", "CreateAsync", "SalesQuotation", 0);
            // Feature Guard — block operation if Sales module is disabled
            if (_featureService != null)
            {
                var guard = await FeatureGuard.CheckAsync<SalesQuotationDto>(_featureService, FeatureKeys.Sales, ct);
                if (guard != null) return guard;
            }

            var vr = await _createValidator.ValidateAsync(dto, ct);
            if (!vr.IsValid)
                return ServiceResult<SalesQuotationDto>.Failure(
                    string.Join(" | ", vr.Errors.Select(e => e.ErrorMessage)));

            try
            {
                var quotationNumber = await _quotationRepo.GetNextNumberAsync(ct);
                var quotation = new SalesQuotation(
                    quotationNumber, dto.QuotationDate, dto.ValidUntil,
                    dto.CustomerId, dto.WarehouseId, dto.Notes, dto.SalesRepresentativeId);

                foreach (var lineDto in dto.Lines)
                {
                    var product = await _productRepo.GetByIdWithUnitsAsync(lineDto.ProductId, ct);
                    if (product == null)
                        return ServiceResult<SalesQuotationDto>.Failure($"الصنف برقم {lineDto.ProductId} غير موجود.");

                    var productUnit = product.ProductUnits.FirstOrDefault(pu => pu.UnitId == lineDto.UnitId);
                    if (productUnit == null)
                        return ServiceResult<SalesQuotationDto>.Failure(
                            $"الوحدة المحددة غير مرتبطة بالصنف ({product.NameAr}).");

                    quotation.AddLine(
                        lineDto.ProductId, lineDto.UnitId, lineDto.Quantity,
                        lineDto.UnitPrice, productUnit.ConversionFactor,
                        lineDto.DiscountPercent, product.VatRate);
                }

                await _quotationRepo.AddAsync(quotation, ct);
                await _unitOfWork.SaveChangesAsync(ct);

                var saved = await _quotationRepo.GetWithLinesAsync(quotation.Id, ct);
                return ServiceResult<SalesQuotationDto>.Success(SalesQuotationMapper.ToDto(saved));
            }
            catch (SalesQuotationDomainException ex)
            {
                return ServiceResult<SalesQuotationDto>.Failure(ex.Message);
            }
        }

        // ══════════════════════════════════════════════════════════
        //  UPDATE (Draft only)
        // ══════════════════════════════════════════════════════════

        public async Task<ServiceResult<SalesQuotationDto>> UpdateAsync(UpdateSalesQuotationDto dto, CancellationToken ct = default)
        {
            _logger.LogInformation("Operation={Operation} Entity={Entity} EntityId={EntityId}", "UpdateAsync", "SalesQuotation", dto.Id);
            var vr = await _updateValidator.ValidateAsync(dto, ct);
            if (!vr.IsValid)
                return ServiceResult<SalesQuotationDto>.Failure(
                    string.Join(" | ", vr.Errors.Select(e => e.ErrorMessage)));

            var quotation = await _quotationRepo.GetWithLinesTrackedAsync(dto.Id, ct);
            if (quotation == null)
                return ServiceResult<SalesQuotationDto>.Failure(QuotationNotFoundMessage);

            try
            {
                quotation.UpdateHeader(dto.QuotationDate, dto.ValidUntil,
                    dto.CustomerId, dto.WarehouseId, dto.Notes, dto.SalesRepresentativeId);

                var newLines = new List<SalesQuotationLine>();
                foreach (var lineDto in dto.Lines)
                {
                    var product = await _productRepo.GetByIdWithUnitsAsync(lineDto.ProductId, ct);
                    if (product == null)
                        return ServiceResult<SalesQuotationDto>.Failure($"الصنف برقم {lineDto.ProductId} غير موجود.");

                    var productUnit = product.ProductUnits.FirstOrDefault(pu => pu.UnitId == lineDto.UnitId);
                    if (productUnit == null)
                        return ServiceResult<SalesQuotationDto>.Failure(
                            $"الوحدة المحددة غير مرتبطة بالصنف ({product.NameAr}).");

                    newLines.Add(new SalesQuotationLine(
                        lineDto.ProductId, lineDto.UnitId, lineDto.Quantity,
                        lineDto.UnitPrice, productUnit.ConversionFactor,
                        lineDto.DiscountPercent, product.VatRate,
                        lineDto.Id));
                }

                quotation.ReplaceLines(newLines);
                // Entity is already tracked — no need for _quotationRepo.Update(quotation)
                await _unitOfWork.SaveChangesAsync(ct);

                var saved = await _quotationRepo.GetWithLinesAsync(quotation.Id, ct);
                return ServiceResult<SalesQuotationDto>.Success(SalesQuotationMapper.ToDto(saved));
            }
            catch (SalesQuotationDomainException ex)
            {
                return ServiceResult<SalesQuotationDto>.Failure(ex.Message);
            }
        }

        // ══════════════════════════════════════════════════════════
        //  STATUS TRANSITIONS
        // ══════════════════════════════════════════════════════════

        public async Task<ServiceResult> SendAsync(int id, CancellationToken ct = default)
        {
            _logger.LogInformation("Operation={Operation} Entity={Entity} EntityId={EntityId}", "SendAsync", "SalesQuotation", id);
            var quotation = await _quotationRepo.GetWithLinesAsync(id, ct);
            if (quotation == null) return ServiceResult.Failure(QuotationNotFoundMessage);

            try
            {
                quotation.Send();
                _quotationRepo.Update(quotation);
                await _unitOfWork.SaveChangesAsync(ct);
                return ServiceResult.Success();
            }
            catch (SalesQuotationDomainException ex)
            {
                return ServiceResult.Failure(ex.Message);
            }
        }

        public async Task<ServiceResult> AcceptAsync(int id, CancellationToken ct = default)
        {
            _logger.LogInformation("Operation={Operation} Entity={Entity} EntityId={EntityId}", "AcceptAsync", "SalesQuotation", id);
            var quotation = await _quotationRepo.GetByIdAsync(id, ct);
            if (quotation == null) return ServiceResult.Failure(QuotationNotFoundMessage);

            try
            {
                quotation.Accept(_dateTime.UtcNow);
                _quotationRepo.Update(quotation);
                await _unitOfWork.SaveChangesAsync(ct);
                return ServiceResult.Success();
            }
            catch (SalesQuotationDomainException ex)
            {
                return ServiceResult.Failure(ex.Message);
            }
        }

        public async Task<ServiceResult> RejectAsync(int id, string reason = null, CancellationToken ct = default)
        {
            _logger.LogInformation("Operation={Operation} Entity={Entity} EntityId={EntityId}", "RejectAsync", "SalesQuotation", id);
            var quotation = await _quotationRepo.GetByIdAsync(id, ct);
            if (quotation == null) return ServiceResult.Failure(QuotationNotFoundMessage);

            try
            {
                quotation.Reject(reason);
                _quotationRepo.Update(quotation);
                await _unitOfWork.SaveChangesAsync(ct);
                return ServiceResult.Success();
            }
            catch (SalesQuotationDomainException ex)
            {
                return ServiceResult.Failure(ex.Message);
            }
        }

        public async Task<ServiceResult> CancelAsync(int id, CancellationToken ct = default)
        {
            _logger.LogInformation("Operation={Operation} Entity={Entity} EntityId={EntityId}", "CancelAsync", "SalesQuotation", id);
            var quotation = await _quotationRepo.GetByIdAsync(id, ct);
            if (quotation == null) return ServiceResult.Failure(QuotationNotFoundMessage);

            try
            {
                quotation.Cancel();
                _quotationRepo.Update(quotation);
                await _unitOfWork.SaveChangesAsync(ct);
                return ServiceResult.Success();
            }
            catch (SalesQuotationDomainException ex)
            {
                return ServiceResult.Failure(ex.Message);
            }
        }

        public async Task<ServiceResult> DeleteDraftAsync(int id, CancellationToken ct = default)
        {
            _logger.LogInformation("Operation={Operation} Entity={Entity} EntityId={EntityId}", "DeleteDraftAsync", "SalesQuotation", id);
            var quotation = await _quotationRepo.GetByIdAsync(id, ct);
            if (quotation == null) return ServiceResult.Failure(QuotationNotFoundMessage);

            try
            {
                quotation.SoftDelete(_currentUser.Username, _dateTime.UtcNow);
                _quotationRepo.Update(quotation);
                await _unitOfWork.SaveChangesAsync(ct);
                return ServiceResult.Success();
            }
            catch (SalesQuotationDomainException ex)
            {
                return ServiceResult.Failure(ex.Message);
            }
        }

        // ══════════════════════════════════════════════════════════
        //  CONVERT TO INVOICE
        // ══════════════════════════════════════════════════════════

        public async Task<ServiceResult<int>> ConvertToInvoiceAsync(int quotationId, CancellationToken ct = default)
        {
            _logger.LogInformation("Operation={Operation} Entity={Entity} EntityId={EntityId}", "ConvertToInvoiceAsync", "SalesQuotation", quotationId);
            var quotation = await _quotationRepo.GetWithLinesAsync(quotationId, ct);
            if (quotation == null)
                return ServiceResult<int>.Failure(QuotationNotFoundMessage);

            if (quotation.Status != QuotationStatus.Accepted)
                return ServiceResult<int>.Failure("يجب قبول عرض السعر قبل التحويل لفاتورة.");

            if (quotation.IsExpired(_dateTime.UtcNow))
                return ServiceResult<int>.Failure("عرض السعر منتهي الصلاحية.");

            try
            {
                const int maxRetries = 3;
                var attempt = 0;

                while (attempt < maxRetries)
                {
                    attempt++;

                    try
                    {
                        int invoiceId = 0;

                        await _unitOfWork.ExecuteInTransactionAsync(async () =>
                        {
                            var invoiceNumber = await _invoiceRepo.GetNextNumberAsync(ct);

                            var invoice = new SalesInvoice(
                                invoiceNumber,
                                _dateTime.UtcNow.Date,
                                quotation.CustomerId,
                                quotation.WarehouseId,
                                $"محوّل من عرض سعر رقم {quotation.QuotationNumber}",
                                quotation.SalesRepresentativeId);

                            // Copy all lines from quotation to invoice
                            foreach (var qLine in quotation.Lines)
                            {
                                invoice.AddLine(
                                    qLine.ProductId, qLine.UnitId, qLine.Quantity,
                                    qLine.UnitPrice, qLine.ConversionFactor,
                                    qLine.DiscountPercent, qLine.VatRate);
                            }

                            await _invoiceRepo.AddAsync(invoice, ct);
                            await _unitOfWork.SaveChangesAsync(ct);

                            quotation.MarkAsConverted(invoice.Id, _dateTime.UtcNow);
                            _quotationRepo.Update(quotation);
                            await _unitOfWork.SaveChangesAsync(ct);

                            invoiceId = invoice.Id;
                        }, IsolationLevel.Serializable, ct);

                        return ServiceResult<int>.Success(invoiceId);
                    }
                    catch (DuplicateRecordException) when (attempt < maxRetries)
                    {
                        await Task.Delay(TimeSpan.FromMilliseconds(100 * attempt), ct);
                        continue;
                    }
                    catch (DuplicateRecordException)
                    {
                        return ServiceResult<int>.Failure("تعذر تحويل عرض السعر بسبب تعارض بيانات فريدة. يرجى إعادة المحاولة.");
                    }
                }

                return ServiceResult<int>.Failure("فشل تحويل عرض السعر لفاتورة بعد عدة محاولات.");
            }
            catch (SalesQuotationDomainException ex)
            {
                return ServiceResult<int>.Failure(ex.Message);
            }
            catch (SalesInvoiceDomainException ex)
            {
                return ServiceResult<int>.Failure(ex.Message);
            }
        }
    }
}
