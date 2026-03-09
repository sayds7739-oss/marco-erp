using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MarcoERP.Application.Common;
using MarcoERP.Application.DTOs.Settings;
using MarcoERP.Application.Interfaces;
using MarcoERP.Application.Interfaces.Settings;
using MarcoERP.Application.Mappers.Settings;
using MarcoERP.Domain.Entities.Settings;
using MarcoERP.Domain;
using MarcoERP.Domain.Enums;
using MarcoERP.Domain.Interfaces;
using MarcoERP.Domain.Interfaces.Settings;
using FluentValidation;

namespace MarcoERP.Application.Services.Settings
{
    /// <summary>
    /// Application service for Feature Governance.
    /// Phase 2: Feature Governance Engine — safe mode.
    /// Records all enable/disable changes in FeatureChangeLog.
    /// </summary>
    [Module(SystemModule.Settings)]
    public sealed class FeatureService : IFeatureService
    {
        private readonly IFeatureRepository _featureRepo;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ICurrentUserService _currentUser;
        private readonly IDateTimeProvider _dateTime;
        private readonly IValidator<ToggleFeatureDto> _toggleValidator;

        public FeatureService(
            IFeatureRepository featureRepo,
            IUnitOfWork unitOfWork,
            ICurrentUserService currentUser,
            IDateTimeProvider dateTime,
            IValidator<ToggleFeatureDto> toggleValidator = null)
        {
            _featureRepo = featureRepo ?? throw new ArgumentNullException(nameof(featureRepo));
            _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
            _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
            _dateTime = dateTime ?? throw new ArgumentNullException(nameof(dateTime));
            _toggleValidator = toggleValidator;
        }

        public async Task<ServiceResult<IReadOnlyList<FeatureDto>>> GetAllAsync(CancellationToken ct = default)
        {
            var entities = await _featureRepo.GetAllAsync(ct);
            return ServiceResult<IReadOnlyList<FeatureDto>>.Success(
                entities.Select(FeatureMapper.ToDto).ToList());
        }

        public async Task<ServiceResult<FeatureDto>> GetByKeyAsync(string featureKey, CancellationToken ct = default)
        {
            var entity = await _featureRepo.GetByKeyAsync(featureKey, ct);
            if (entity == null)
                return ServiceResult<FeatureDto>.Failure($"الميزة '{featureKey}' غير موجودة.");
            return ServiceResult<FeatureDto>.Success(FeatureMapper.ToDto(entity));
        }

        public async Task<ServiceResult<bool>> IsEnabledAsync(string featureKey, CancellationToken ct = default)
        {
            var entity = await _featureRepo.GetByKeyAsync(featureKey, ct);
            if (entity == null)
                return ServiceResult<bool>.Failure($"الميزة '{featureKey}' غير موجودة.");
            return ServiceResult<bool>.Success(entity.IsEnabled);
        }

        public async Task<ServiceResult> ToggleAsync(ToggleFeatureDto dto, CancellationToken ct = default)
        {
            if (_toggleValidator != null)
            {
                var vr = await _toggleValidator.ValidateAsync(dto, ct);
                if (!vr.IsValid)
                    return ServiceResult.Failure(vr.Errors[0].ErrorMessage);
            }

            var entity = await _featureRepo.GetByKeyAsync(dto.FeatureKey, ct);
            if (entity == null)
                return ServiceResult.Failure($"الميزة '{dto.FeatureKey}' غير موجودة.");

            // No change needed
            if (entity.IsEnabled == dto.IsEnabled)
                return ServiceResult.Success();

            bool oldValue = entity.IsEnabled;

            if (dto.IsEnabled)
                entity.Enable();
            else
                entity.Disable();

            _featureRepo.Update(entity);

            // Record change log
            var log = new FeatureChangeLog(
                entity.Id,
                entity.FeatureKey,
                oldValue,
                dto.IsEnabled,
                _currentUser.Username ?? DomainConstants.SystemUser,
                _dateTime.UtcNow);

            await _featureRepo.AddChangeLogAsync(log, ct);

            // ── Cascade disable: when disabling a feature, also disable all
            //    features that depend on it (directly or transitively). ──────
            if (!dto.IsEnabled)
            {
                await CascadeDisableDependentsAsync(dto.FeatureKey, ct);
            }

            await _unitOfWork.SaveChangesAsync(ct);

            return ServiceResult.Success();
        }

        /// <summary>
        /// Finds all features whose DependsOn includes <paramref name="parentKey"/>
        /// and recursively disables them, recording change logs.
        /// Uses a visited set to prevent infinite loops from circular dependencies.
        /// </summary>
        private async Task CascadeDisableDependentsAsync(string parentKey, CancellationToken ct, HashSet<string> visited = null)
        {
            visited ??= new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (!visited.Add(parentKey)) return; // prevent infinite loop on circular DependsOn

            var allFeatures = await _featureRepo.GetAllAsync(ct);

            // Find enabled features that directly depend on the disabled parent
            var dependents = allFeatures.Where(f =>
                f.IsEnabled &&
                !string.IsNullOrWhiteSpace(f.DependsOn) &&
                f.DependsOn
                 .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                 .Any(d => string.Equals(d, parentKey, StringComparison.OrdinalIgnoreCase)))
                .ToList();

            foreach (var dep in dependents)
            {
                dep.Disable();
                _featureRepo.Update(dep);

                var depLog = new FeatureChangeLog(
                    dep.Id,
                    dep.FeatureKey,
                    true,   // was enabled
                    false,  // now disabled
                    _currentUser.Username ?? DomainConstants.SystemUser,
                    _dateTime.UtcNow);

                await _featureRepo.AddChangeLogAsync(depLog, ct);

                // Recurse: disable features that depend on THIS dependent
                await CascadeDisableDependentsAsync(dep.FeatureKey, ct, visited);
            }
        }
    }
}
