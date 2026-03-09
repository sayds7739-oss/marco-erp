using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MarcoERP.Application.Common;
using MarcoERP.Application.DTOs.Settings;
using MarcoERP.Application.Interfaces;
using MarcoERP.Application.Interfaces.Settings;
using MarcoERP.Application.Mappers.Settings;
using MarcoERP.Domain.Enums;
using MarcoERP.Domain.Interfaces;
using MarcoERP.Domain.Interfaces.Settings;

namespace MarcoERP.Application.Services.Settings
{
    /// <summary>
    /// Application service for Profile management.
    /// Phase 3: Progressive Complexity Layer.
    /// Applies complexity profiles by toggling Feature flags.
    /// </summary>
    [Module(SystemModule.Settings)]
    public sealed class ProfileService : IProfileService
    {
        private readonly IProfileRepository _profileRepo;
        private readonly IFeatureService _featureService;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ICurrentUserService _currentUser;

        public ProfileService(
            IProfileRepository profileRepo,
            IFeatureService featureService,
            IUnitOfWork unitOfWork,
            ICurrentUserService currentUser)
        {
            _profileRepo = profileRepo ?? throw new ArgumentNullException(nameof(profileRepo));
            _featureService = featureService ?? throw new ArgumentNullException(nameof(featureService));
            _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
            _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
        }

        public async Task<ServiceResult<IReadOnlyList<SystemProfileDto>>> GetAllProfilesAsync(CancellationToken ct = default)
        {
            var profiles = await _profileRepo.GetAllAsync(ct);
            return ServiceResult<IReadOnlyList<SystemProfileDto>>.Success(
                profiles.Select(ProfileMapper.ToDto).ToList());
        }

        public async Task<ServiceResult<string>> GetCurrentProfileAsync(CancellationToken ct = default)
        {
            var active = await _profileRepo.GetActiveProfileAsync(ct);
            if (active == null)
                return ServiceResult<string>.Success("Standard"); // fallback
            return ServiceResult<string>.Success(active.ProfileName);
        }

        public async Task<ServiceResult<IReadOnlyList<string>>> GetProfileFeaturesAsync(string profileName, CancellationToken ct = default)
        {
            var featureKeys = await _profileRepo.GetFeatureKeysForProfileAsync(profileName, ct);
            return ServiceResult<IReadOnlyList<string>>.Success(featureKeys);
        }

        public async Task<ServiceResult> ApplyProfileAsync(string profileName, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(profileName))
                return ServiceResult.Failure("اسم البروفايل مطلوب.");

            // 1. Validate profile exists
            var targetProfile = await _profileRepo.GetByNameAsync(profileName, ct);
            if (targetProfile == null)
                return ServiceResult.Failure($"البروفايل '{profileName}' غير موجود.");

            // 2. Get feature keys for the target profile
            var profileFeatureKeys = await _profileRepo.GetFeatureKeysForProfileAsync(targetProfile.Id, ct);
            var profileFeatureSet = new HashSet<string>(profileFeatureKeys, StringComparer.OrdinalIgnoreCase);

            // 3. Get all features
            var allFeaturesResult = await _featureService.GetAllAsync(ct);
            if (allFeaturesResult.IsFailure)
                return ServiceResult.Failure(allFeaturesResult.ErrorMessage);

            // 4–7. Batch all feature changes + profile activation in ONE transaction commit
            string toggleError = null;

            await _unitOfWork.ExecuteInTransactionAsync(async () =>
            {
                // 4. Toggle features: enable those in profile, disable those not in profile
                foreach (var feature in allFeaturesResult.Data)
                {
                    bool shouldBeEnabled = profileFeatureSet.Contains(feature.FeatureKey);

                    if (feature.IsEnabled != shouldBeEnabled)
                    {
                        // Safety: do not disable High risk features without explicit inclusion
                        var toggleDto = new ToggleFeatureDto
                        {
                            FeatureKey = feature.FeatureKey,
                            IsEnabled = shouldBeEnabled
                        };

                        var toggleResult = await _featureService.ToggleAsync(toggleDto, ct);
                        if (toggleResult.IsFailure)
                        {
                            toggleError = $"فشل تبديل الميزة '{feature.FeatureKey}': {toggleResult.ErrorMessage}";
                            return;
                        }
                    }
                }

                if (toggleError != null) return;

                // 5. Deactivate current active profile, activate the new one
                var currentActive = await _profileRepo.GetActiveProfileAsync(ct);
                if (currentActive != null && currentActive.Id != targetProfile.Id)
                {
                    currentActive.Deactivate();
                    _profileRepo.Update(currentActive);
                }

                targetProfile.Activate();
                _profileRepo.Update(targetProfile);
                await _unitOfWork.SaveChangesAsync(ct);

            }, IsolationLevel.ReadCommitted, ct);

            if (toggleError != null)
                return ServiceResult.Failure(toggleError);

            return ServiceResult.Success();
        }
    }
}
