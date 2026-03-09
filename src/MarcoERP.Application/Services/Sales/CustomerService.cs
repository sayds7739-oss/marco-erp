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
using MarcoERP.Domain.Exceptions.Sales;
using MarcoERP.Domain.Interfaces;
using MarcoERP.Domain.Interfaces.Sales;
using MarcoERP.Application.Interfaces.Settings;
using Microsoft.Extensions.Logging;

namespace MarcoERP.Application.Services.Sales
{
    [Module(SystemModule.Sales)]
    public sealed class CustomerService : ICustomerService
    {
        /// <summary>GL control account code for Accounts Receivable (المدينون).</summary>
        private const string AccountsReceivableCode = "1121";
        private const string CustomerNotFoundMessage = "العميل غير موجود.";

        private readonly ICustomerRepository _customerRepo;
        private readonly IAccountRepository _accountRepo;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ICurrentUserService _currentUser;
        private readonly IDateTimeProvider _dateTime;
        private readonly IValidator<CreateCustomerDto> _createValidator;
        private readonly IValidator<UpdateCustomerDto> _updateValidator;
        private readonly ILogger<CustomerService> _logger;
        private readonly IFeatureService _featureService;

        public CustomerService(
            ICustomerRepository customerRepo,
            IAccountRepository accountRepo,
            IUnitOfWork unitOfWork,
            ICurrentUserService currentUser,
            IDateTimeProvider dateTime,
            IValidator<CreateCustomerDto> createValidator,
            IValidator<UpdateCustomerDto> updateValidator,
            ILogger<CustomerService> logger = null,
            IFeatureService featureService = null)
        {
            _customerRepo = customerRepo ?? throw new ArgumentNullException(nameof(customerRepo));
            _accountRepo = accountRepo ?? throw new ArgumentNullException(nameof(accountRepo));
            _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
            _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
            _dateTime = dateTime ?? throw new ArgumentNullException(nameof(dateTime));
            _createValidator = createValidator ?? throw new ArgumentNullException(nameof(createValidator));
            _updateValidator = updateValidator ?? throw new ArgumentNullException(nameof(updateValidator));
            _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<CustomerService>.Instance;
            _featureService = featureService;
        }

        public async Task<ServiceResult<IReadOnlyList<CustomerDto>>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            var entities = await _customerRepo.GetAllAsync(cancellationToken);
            return ServiceResult<IReadOnlyList<CustomerDto>>.Success(
                entities.Select(CustomerMapper.ToDto).ToList());
        }

        public async Task<ServiceResult<CustomerDto>> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        {
            var entity = await _customerRepo.GetByIdAsync(id, cancellationToken);
            if (entity == null)
                return ServiceResult<CustomerDto>.Failure(CustomerNotFoundMessage);
            return ServiceResult<CustomerDto>.Success(CustomerMapper.ToDto(entity));
        }

        public async Task<ServiceResult<IReadOnlyList<CustomerSearchResultDto>>> SearchAsync(string searchTerm, CancellationToken cancellationToken = default)
        {
            var results = await _customerRepo.SearchAsync(searchTerm, cancellationToken);
            return ServiceResult<IReadOnlyList<CustomerSearchResultDto>>.Success(
                results.Select(CustomerMapper.ToSearchResult).ToList());
        }

        public async Task<ServiceResult<string>> GetNextCodeAsync(CancellationToken cancellationToken = default)
        {
            var nextCode = await _customerRepo.GetNextCodeAsync(cancellationToken);
            return ServiceResult<string>.Success(nextCode);
        }

        public async Task<ServiceResult<CustomerDto>> CreateAsync(CreateCustomerDto dto, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Operation={Operation} Entity={Entity} EntityId={EntityId}", "CreateAsync", "Customer", 0);
            // Feature Guard — block operation if Sales module is disabled
            if (_featureService != null)
            {
                var guard = await FeatureGuard.CheckAsync<CustomerDto>(_featureService, FeatureKeys.Sales, cancellationToken);
                if (guard != null) return guard;
            }

            // 1. Validate DTO
            var vr = await _createValidator.ValidateAsync(dto, cancellationToken);
            if (!vr.IsValid)
                return ServiceResult<CustomerDto>.Failure(
                    string.Join(" | ", vr.Errors.Select(e => e.ErrorMessage)));

            // 2. Check duplicate code
            if (await _customerRepo.CodeExistsAsync(dto.Code, cancellationToken))
                return ServiceResult<CustomerDto>.Failure("كود العميل مستخدم بالفعل.");

            try
            {
                // 3. Create domain entity
                var entity = new Customer(new Customer.CustomerDraft
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
                    CustomerType = dto.CustomerType,
                    CommercialRegister = dto.CommercialRegister,
                    Country = dto.Country,
                    PostalCode = dto.PostalCode,
                    ContactPerson = dto.ContactPerson,
                    Website = dto.Website,
                    DefaultDiscountPercent = dto.DefaultDiscountPercent,
                    PreviousBalance = dto.PreviousBalance,
                    CreditLimit = dto.CreditLimit,
                    DaysAllowed = dto.DaysAllowed,
                    BlockedOnOverdue = dto.BlockedOnOverdue,
                    PriceListId = dto.PriceListId,
                    Notes = dto.Notes
                });

                await _unitOfWork.ExecuteInTransactionAsync(async () =>
                {
                    await _customerRepo.AddAsync(entity, cancellationToken);
                    await _unitOfWork.SaveChangesAsync(cancellationToken);

                    // 4. Auto-link to Accounts Receivable GL account (1121)
                    var arAccount = await _accountRepo.GetByCodeAsync(AccountsReceivableCode, cancellationToken);
                    if (arAccount != null)
                    {
                        entity.SetAccountId(arAccount.Id);
                        _customerRepo.Update(entity);
                        await _unitOfWork.SaveChangesAsync(cancellationToken);
                    }
                }, cancellationToken: cancellationToken);

                return ServiceResult<CustomerDto>.Success(CustomerMapper.ToDto(entity));
            }
            catch (CustomerDomainException ex)
            {
                return ServiceResult<CustomerDto>.Failure(ex.Message);
            }
        }

        public async Task<ServiceResult<CustomerDto>> UpdateAsync(UpdateCustomerDto dto, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Operation={Operation} Entity={Entity} EntityId={EntityId}", "UpdateAsync", "Customer", dto.Id);
            // 1. Validate DTO
            var vr = await _updateValidator.ValidateAsync(dto, cancellationToken);
            if (!vr.IsValid)
                return ServiceResult<CustomerDto>.Failure(
                    string.Join(" | ", vr.Errors.Select(e => e.ErrorMessage)));

            // 2. Fetch existing
            var entity = await _customerRepo.GetByIdAsync(dto.Id, cancellationToken);
            if (entity == null)
                return ServiceResult<CustomerDto>.Failure(CustomerNotFoundMessage);

            try
            {
                // 3. Update domain entity
                entity.Update(new Customer.CustomerUpdate
                {
                    NameAr = dto.NameAr,
                    NameEn = dto.NameEn,
                    Phone = dto.Phone,
                    Mobile = dto.Mobile,
                    Address = dto.Address,
                    City = dto.City,
                    TaxNumber = dto.TaxNumber,
                    Email = dto.Email,
                    CustomerType = dto.CustomerType,
                    CommercialRegister = dto.CommercialRegister,
                    Country = dto.Country,
                    PostalCode = dto.PostalCode,
                    ContactPerson = dto.ContactPerson,
                    Website = dto.Website,
                    DefaultDiscountPercent = dto.DefaultDiscountPercent,
                    CreditLimit = dto.CreditLimit,
                    DaysAllowed = dto.DaysAllowed,
                    BlockedOnOverdue = dto.BlockedOnOverdue,
                    PriceListId = dto.PriceListId,
                    Notes = dto.Notes
                });

                _customerRepo.Update(entity);
                await _unitOfWork.SaveChangesAsync(cancellationToken);

                return ServiceResult<CustomerDto>.Success(CustomerMapper.ToDto(entity));
            }
            catch (CustomerDomainException ex)
            {
                return ServiceResult<CustomerDto>.Failure(ex.Message);
            }
        }

        public async Task<ServiceResult> ActivateAsync(int id, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Operation={Operation} Entity={Entity} EntityId={EntityId}", "ActivateAsync", "Customer", id);
            var entity = await _customerRepo.GetByIdAsync(id, cancellationToken);
            if (entity == null)
                return ServiceResult.Failure(CustomerNotFoundMessage);

            entity.Activate();
            _customerRepo.Update(entity);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return ServiceResult.Success();
        }

        public async Task<ServiceResult> DeactivateAsync(int id, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Operation={Operation} Entity={Entity} EntityId={EntityId}", "DeactivateAsync", "Customer", id);
            var entity = await _customerRepo.GetByIdAsync(id, cancellationToken);
            if (entity == null)
                return ServiceResult.Failure(CustomerNotFoundMessage);

            entity.Deactivate();
            _customerRepo.Update(entity);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return ServiceResult.Success();
        }

        public async Task<ServiceResult> DeleteAsync(int id, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Operation={Operation} Entity={Entity} EntityId={EntityId}", "DeleteAsync", "Customer", id);
            var entity = await _customerRepo.GetByIdAsync(id, cancellationToken);
            if (entity == null)
                return ServiceResult.Failure(CustomerNotFoundMessage);

            try
            {
                entity.SoftDelete(
                    _currentUser.Username ?? "System",
                    _dateTime.UtcNow);

                _customerRepo.Update(entity);
                await _unitOfWork.SaveChangesAsync(cancellationToken);

                return ServiceResult.Success();
            }
            catch (CustomerDomainException ex)
            {
                return ServiceResult.Failure(ex.Message);
            }
        }
    }
}
