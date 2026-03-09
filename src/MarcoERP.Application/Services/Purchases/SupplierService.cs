using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using MarcoERP.Application.Common;
using MarcoERP.Application.DTOs.Purchases;
using MarcoERP.Application.Interfaces;
using MarcoERP.Application.Interfaces.Purchases;
using MarcoERP.Application.Mappers.Purchases;
using MarcoERP.Domain.Entities.Purchases;
using MarcoERP.Domain.Enums;
using MarcoERP.Domain.Exceptions.Purchases;
using MarcoERP.Domain.Interfaces;
using MarcoERP.Domain.Interfaces.Purchases;
using MarcoERP.Application.Interfaces.Settings;
using Microsoft.Extensions.Logging;

namespace MarcoERP.Application.Services.Purchases
{
    [Module(SystemModule.Purchases)]
    public sealed class SupplierService : ISupplierService
    {
        /// <summary>GL control account code for Accounts Payable (الدائنون).</summary>
        private const string AccountsPayableCode = "2111";

        private readonly ISupplierRepository _supplierRepo;
        private readonly IAccountRepository _accountRepo;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ICurrentUserService _currentUser;
        private readonly IDateTimeProvider _dateTime;
        private readonly IValidator<CreateSupplierDto> _createValidator;
        private readonly IValidator<UpdateSupplierDto> _updateValidator;
        private readonly ILogger<SupplierService> _logger;
        private readonly IFeatureService _featureService;

        private const string SupplierNotFoundMessage = "المورد غير موجود.";

        public SupplierService(
            ISupplierRepository supplierRepo,
            IAccountRepository accountRepo,
            IUnitOfWork unitOfWork,
            ICurrentUserService currentUser,
            IDateTimeProvider dateTime,
            IValidator<CreateSupplierDto> createValidator,
            IValidator<UpdateSupplierDto> updateValidator,
            ILogger<SupplierService> logger = null,
            IFeatureService featureService = null)
        {
            _supplierRepo = supplierRepo ?? throw new ArgumentNullException(nameof(supplierRepo));
            _accountRepo = accountRepo ?? throw new ArgumentNullException(nameof(accountRepo));
            _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
            _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
            _dateTime = dateTime ?? throw new ArgumentNullException(nameof(dateTime));
            _createValidator = createValidator ?? throw new ArgumentNullException(nameof(createValidator));
            _updateValidator = updateValidator ?? throw new ArgumentNullException(nameof(updateValidator));
            _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<SupplierService>.Instance;
            _featureService = featureService;
        }

        public async Task<ServiceResult<IReadOnlyList<SupplierDto>>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            var entities = await _supplierRepo.GetAllAsync(cancellationToken);
            return ServiceResult<IReadOnlyList<SupplierDto>>.Success(
                entities.Select(SupplierMapper.ToDto).ToList());
        }

        public async Task<ServiceResult<SupplierDto>> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        {
            var entity = await _supplierRepo.GetByIdAsync(id, cancellationToken);
            if (entity == null)
                return ServiceResult<SupplierDto>.Failure(SupplierNotFoundMessage);
            return ServiceResult<SupplierDto>.Success(SupplierMapper.ToDto(entity));
        }

        public async Task<ServiceResult<IReadOnlyList<SupplierSearchResultDto>>> SearchAsync(string searchTerm, CancellationToken cancellationToken = default)
        {
            var results = await _supplierRepo.SearchAsync(searchTerm, cancellationToken);
            return ServiceResult<IReadOnlyList<SupplierSearchResultDto>>.Success(
                results.Select(SupplierMapper.ToSearchResult).ToList());
        }

        public async Task<ServiceResult<string>> GetNextCodeAsync(CancellationToken cancellationToken = default)
        {
            var nextCode = await _supplierRepo.GetNextCodeAsync(cancellationToken);
            return ServiceResult<string>.Success(nextCode);
        }

        public async Task<ServiceResult<SupplierDto>> CreateAsync(CreateSupplierDto dto, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Operation={Operation} Entity={Entity} EntityId={EntityId}", "CreateAsync", "Supplier", 0);

            // Feature Guard — block operation if Purchases module is disabled
            if (_featureService != null)
            {
                var guard = await FeatureGuard.CheckAsync<SupplierDto>(_featureService, FeatureKeys.Purchases, cancellationToken);
                if (guard != null) return guard;
            }

            // 1. Validate DTO
            var vr = await _createValidator.ValidateAsync(dto, cancellationToken);
            if (!vr.IsValid)
                return ServiceResult<SupplierDto>.Failure(
                    string.Join(" | ", vr.Errors.Select(e => e.ErrorMessage)));

            // 2. Check duplicate code
            if (await _supplierRepo.CodeExistsAsync(dto.Code, cancellationToken))
                return ServiceResult<SupplierDto>.Failure("كود المورد مستخدم بالفعل.");

            try
            {
                // 3. Create domain entity
                var entity = new Supplier(new SupplierDraft
                {
                    Code = dto.Code,
                    NameAr = dto.NameAr,
                    NameEn = dto.NameEn,
                    Phone = dto.Phone,
                    Mobile = dto.Mobile,
                    Address = dto.Address,
                    City = dto.City,
                    TaxNumber = dto.TaxNumber,
                    Email = dto.Email,
                    CommercialRegister = dto.CommercialRegister,
                    Country = dto.Country,
                    PostalCode = dto.PostalCode,
                    ContactPerson = dto.ContactPerson,
                    Website = dto.Website,
                    CreditLimit = dto.CreditLimit,
                    DaysAllowed = dto.DaysAllowed,
                    BankName = dto.BankName,
                    BankAccountName = dto.BankAccountName,
                    BankAccountNumber = dto.BankAccountNumber,
                    IBAN = dto.IBAN,
                    PreviousBalance = dto.PreviousBalance,
                    Notes = dto.Notes
                });

                await _supplierRepo.AddAsync(entity, cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);

                // 4. Auto-link to Accounts Payable GL account (2111)
                var apAccount = await _accountRepo.GetByCodeAsync(AccountsPayableCode, cancellationToken);
                if (apAccount != null)
                {
                    entity.SetAccountId(apAccount.Id);
                    _supplierRepo.Update(entity);
                    await _unitOfWork.SaveChangesAsync(cancellationToken);
                }

                return ServiceResult<SupplierDto>.Success(SupplierMapper.ToDto(entity));
            }
            catch (SupplierDomainException ex)
            {
                return ServiceResult<SupplierDto>.Failure(ex.Message);
            }
        }

        public async Task<ServiceResult<SupplierDto>> UpdateAsync(UpdateSupplierDto dto, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Operation={Operation} Entity={Entity} EntityId={EntityId}", "UpdateAsync", "Supplier", dto.Id);
            // 1. Validate DTO
            var vr = await _updateValidator.ValidateAsync(dto, cancellationToken);
            if (!vr.IsValid)
                return ServiceResult<SupplierDto>.Failure(
                    string.Join(" | ", vr.Errors.Select(e => e.ErrorMessage)));

            // 2. Fetch existing
            var entity = await _supplierRepo.GetByIdAsync(dto.Id, cancellationToken);
            if (entity == null)
                return ServiceResult<SupplierDto>.Failure(SupplierNotFoundMessage);

            try
            {
                // 3. Update domain entity
                entity.Update(new SupplierUpdate
                {
                    NameAr = dto.NameAr,
                    NameEn = dto.NameEn,
                    Phone = dto.Phone,
                    Mobile = dto.Mobile,
                    Address = dto.Address,
                    City = dto.City,
                    TaxNumber = dto.TaxNumber,
                    Email = dto.Email,
                    CommercialRegister = dto.CommercialRegister,
                    Country = dto.Country,
                    PostalCode = dto.PostalCode,
                    ContactPerson = dto.ContactPerson,
                    Website = dto.Website,
                    CreditLimit = dto.CreditLimit,
                    DaysAllowed = dto.DaysAllowed,
                    BankName = dto.BankName,
                    BankAccountName = dto.BankAccountName,
                    BankAccountNumber = dto.BankAccountNumber,
                    IBAN = dto.IBAN,
                    Notes = dto.Notes
                });

                _supplierRepo.Update(entity);
                await _unitOfWork.SaveChangesAsync(cancellationToken);

                return ServiceResult<SupplierDto>.Success(SupplierMapper.ToDto(entity));
            }
            catch (SupplierDomainException ex)
            {
                return ServiceResult<SupplierDto>.Failure(ex.Message);
            }
        }

        public async Task<ServiceResult> ActivateAsync(int id, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Operation={Operation} Entity={Entity} EntityId={EntityId}", "ActivateAsync", "Supplier", id);
            var entity = await _supplierRepo.GetByIdAsync(id, cancellationToken);
            if (entity == null)
                return ServiceResult.Failure(SupplierNotFoundMessage);

            entity.Activate();
            _supplierRepo.Update(entity);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return ServiceResult.Success();
        }

        public async Task<ServiceResult> DeactivateAsync(int id, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Operation={Operation} Entity={Entity} EntityId={EntityId}", "DeactivateAsync", "Supplier", id);
            var entity = await _supplierRepo.GetByIdAsync(id, cancellationToken);
            if (entity == null)
                return ServiceResult.Failure(SupplierNotFoundMessage);

            entity.Deactivate();
            _supplierRepo.Update(entity);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return ServiceResult.Success();
        }

        public async Task<ServiceResult> DeleteAsync(int id, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Operation={Operation} Entity={Entity} EntityId={EntityId}", "DeleteAsync", "Supplier", id);
            var entity = await _supplierRepo.GetByIdAsync(id, cancellationToken);
            if (entity == null)
                return ServiceResult.Failure(SupplierNotFoundMessage);

            try
            {
                entity.SoftDelete(
                    _currentUser.Username ?? "System",
                    _dateTime.UtcNow);

                _supplierRepo.Update(entity);
                await _unitOfWork.SaveChangesAsync(cancellationToken);

                return ServiceResult.Success();
            }
            catch (SupplierDomainException ex)
            {
                return ServiceResult.Failure(ex.Message);
            }
        }
    }
}
