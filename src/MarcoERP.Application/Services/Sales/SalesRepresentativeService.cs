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
    public sealed class SalesRepresentativeService : ISalesRepresentativeService
    {
        private const string NotFoundMessage = "المندوب غير موجود.";

        private readonly ISalesRepresentativeRepository _repRepo;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ICurrentUserService _currentUser;
        private readonly IDateTimeProvider _dateTime;
        private readonly IValidator<CreateSalesRepresentativeDto> _createValidator;
        private readonly IValidator<UpdateSalesRepresentativeDto> _updateValidator;
        private readonly ILogger<SalesRepresentativeService> _logger;
        private readonly IFeatureService _featureService;

        public SalesRepresentativeService(
            ISalesRepresentativeRepository repRepo,
            IUnitOfWork unitOfWork,
            ICurrentUserService currentUser,
            IDateTimeProvider dateTime,
            IValidator<CreateSalesRepresentativeDto> createValidator,
            IValidator<UpdateSalesRepresentativeDto> updateValidator,
            ILogger<SalesRepresentativeService> logger = null,
            IFeatureService featureService = null)
        {
            _repRepo = repRepo ?? throw new ArgumentNullException(nameof(repRepo));
            _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
            _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
            _dateTime = dateTime ?? throw new ArgumentNullException(nameof(dateTime));
            _createValidator = createValidator ?? throw new ArgumentNullException(nameof(createValidator));
            _updateValidator = updateValidator ?? throw new ArgumentNullException(nameof(updateValidator));
            _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<SalesRepresentativeService>.Instance;
            _featureService = featureService;
        }

        public async Task<ServiceResult<IReadOnlyList<SalesRepresentativeDto>>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            var entities = await _repRepo.GetAllAsync(cancellationToken);
            return ServiceResult<IReadOnlyList<SalesRepresentativeDto>>.Success(
                entities.Select(SalesRepresentativeMapper.ToDto).ToList());
        }

        public async Task<ServiceResult<SalesRepresentativeDto>> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        {
            var entity = await _repRepo.GetByIdAsync(id, cancellationToken);
            if (entity == null)
                return ServiceResult<SalesRepresentativeDto>.Failure(NotFoundMessage);
            return ServiceResult<SalesRepresentativeDto>.Success(SalesRepresentativeMapper.ToDto(entity));
        }

        public async Task<ServiceResult<IReadOnlyList<SalesRepresentativeSearchResultDto>>> SearchAsync(string searchTerm, CancellationToken cancellationToken = default)
        {
            var results = await _repRepo.SearchAsync(searchTerm, cancellationToken);
            return ServiceResult<IReadOnlyList<SalesRepresentativeSearchResultDto>>.Success(
                results.Select(SalesRepresentativeMapper.ToSearchResult).ToList());
        }

        public async Task<ServiceResult<IReadOnlyList<SalesRepresentativeDto>>> GetActiveAsync(CancellationToken cancellationToken = default)
        {
            var entities = await _repRepo.GetActiveAsync(cancellationToken);
            return ServiceResult<IReadOnlyList<SalesRepresentativeDto>>.Success(
                entities.Select(SalesRepresentativeMapper.ToDto).ToList());
        }

        public async Task<ServiceResult<string>> GetNextCodeAsync(CancellationToken cancellationToken = default)
        {
            var nextCode = await _repRepo.GetNextCodeAsync(cancellationToken);
            return ServiceResult<string>.Success(nextCode);
        }

        public async Task<ServiceResult<SalesRepresentativeDto>> CreateAsync(CreateSalesRepresentativeDto dto, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Operation={Operation} Entity={Entity} EntityId={EntityId}", "CreateAsync", "SalesRepresentative", 0);
            // Feature Guard — block operation if Sales module is disabled
            if (_featureService != null)
            {
                var guard = await FeatureGuard.CheckAsync<SalesRepresentativeDto>(_featureService, FeatureKeys.Sales, cancellationToken);
                if (guard != null) return guard;
            }

            var vr = await _createValidator.ValidateAsync(dto, cancellationToken);
            if (!vr.IsValid)
                return ServiceResult<SalesRepresentativeDto>.Failure(
                    string.Join(" | ", vr.Errors.Select(e => e.ErrorMessage)));

            if (await _repRepo.CodeExistsAsync(dto.Code, cancellationToken))
                return ServiceResult<SalesRepresentativeDto>.Failure("كود المندوب مستخدم بالفعل.");

            try
            {
                var entity = new SalesRepresentative(new SalesRepresentative.SalesRepresentativeDraft
                {
                    Code = dto.Code,
                    NameAr = dto.NameAr,
                    NameEn = dto.NameEn,
                    Phone = dto.Phone,
                    Mobile = dto.Mobile,
                    Email = dto.Email,
                    CommissionRate = dto.CommissionRate,
                    CommissionBasedOn = (Domain.Enums.CommissionBasis)dto.CommissionBasedOn,
                    Notes = dto.Notes
                });

                await _repRepo.AddAsync(entity, cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);

                return ServiceResult<SalesRepresentativeDto>.Success(SalesRepresentativeMapper.ToDto(entity));
            }
            catch (SalesRepresentativeDomainException ex)
            {
                return ServiceResult<SalesRepresentativeDto>.Failure(ex.Message);
            }
        }

        public async Task<ServiceResult<SalesRepresentativeDto>> UpdateAsync(UpdateSalesRepresentativeDto dto, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Operation={Operation} Entity={Entity} EntityId={EntityId}", "UpdateAsync", "SalesRepresentative", dto.Id);
            var vr = await _updateValidator.ValidateAsync(dto, cancellationToken);
            if (!vr.IsValid)
                return ServiceResult<SalesRepresentativeDto>.Failure(
                    string.Join(" | ", vr.Errors.Select(e => e.ErrorMessage)));

            var entity = await _repRepo.GetByIdAsync(dto.Id, cancellationToken);
            if (entity == null)
                return ServiceResult<SalesRepresentativeDto>.Failure(NotFoundMessage);

            try
            {
                entity.Update(new SalesRepresentative.SalesRepresentativeUpdate
                {
                    NameAr = dto.NameAr,
                    NameEn = dto.NameEn,
                    Phone = dto.Phone,
                    Mobile = dto.Mobile,
                    Email = dto.Email,
                    CommissionRate = dto.CommissionRate,
                    CommissionBasedOn = (Domain.Enums.CommissionBasis)dto.CommissionBasedOn,
                    Notes = dto.Notes
                });

                _repRepo.Update(entity);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                return ServiceResult<SalesRepresentativeDto>.Success(SalesRepresentativeMapper.ToDto(entity));
            }
            catch (SalesRepresentativeDomainException ex)
            {
                return ServiceResult<SalesRepresentativeDto>.Failure(ex.Message);
            }
        }

        public async Task<ServiceResult> ActivateAsync(int id, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Operation={Operation} Entity={Entity} EntityId={EntityId}", "ActivateAsync", "SalesRepresentative", id);
            var entity = await _repRepo.GetByIdAsync(id, cancellationToken);
            if (entity == null) return ServiceResult.Failure(NotFoundMessage);

            entity.Activate();
            _repRepo.Update(entity);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return ServiceResult.Success();
        }

        public async Task<ServiceResult> DeactivateAsync(int id, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Operation={Operation} Entity={Entity} EntityId={EntityId}", "DeactivateAsync", "SalesRepresentative", id);
            var entity = await _repRepo.GetByIdAsync(id, cancellationToken);
            if (entity == null) return ServiceResult.Failure(NotFoundMessage);

            entity.Deactivate();
            _repRepo.Update(entity);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return ServiceResult.Success();
        }

        public async Task<ServiceResult> DeleteAsync(int id, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Operation={Operation} Entity={Entity} EntityId={EntityId}", "DeleteAsync", "SalesRepresentative", id);
            var entity = await _repRepo.GetByIdAsync(id, cancellationToken);
            if (entity == null) return ServiceResult.Failure(NotFoundMessage);

            try
            {
                entity.SoftDelete(_currentUser.Username ?? "System", _dateTime.UtcNow);
                _repRepo.Update(entity);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                return ServiceResult.Success();
            }
            catch (SalesRepresentativeDomainException ex)
            {
                return ServiceResult.Failure(ex.Message);
            }
        }
    }
}
