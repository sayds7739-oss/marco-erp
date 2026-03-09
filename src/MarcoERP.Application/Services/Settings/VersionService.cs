using System;
using System.Threading;
using System.Threading.Tasks;
using MarcoERP.Application.Common;
using MarcoERP.Application.Interfaces;
using MarcoERP.Application.Interfaces.Settings;
using MarcoERP.Domain.Enums;
using MarcoERP.Domain.Interfaces;

namespace MarcoERP.Application.Services.Settings
{
    /// <summary>
    /// Manages system version registration and retrieval.
    /// Phase 5: Version &amp; Integrity Engine — tracking only.
    /// </summary>
    [Module(SystemModule.Settings)]
    public sealed class VersionService : IVersionService
    {
        private readonly IVersionRepository _versionRepo;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ICurrentUserService _currentUser;
        private readonly IDateTimeProvider _dateTime;

        public VersionService(
            IVersionRepository versionRepo,
            IUnitOfWork unitOfWork,
            ICurrentUserService currentUser,
            IDateTimeProvider dateTime)
        {
            _versionRepo = versionRepo ?? throw new ArgumentNullException(nameof(versionRepo));
            _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
            _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
            _dateTime = dateTime ?? throw new ArgumentNullException(nameof(dateTime));
        }

        public async Task<string> GetCurrentVersionAsync(CancellationToken ct = default)
        {
            var latest = await _versionRepo.GetLatestVersionAsync(ct);
            return latest?.VersionNumber ?? "0.0.0";
        }

        public async Task<ServiceResult> RegisterNewVersionAsync(string version, string description, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(version))
                return ServiceResult.Failure("رقم الإصدار مطلوب.");

            // Check duplicate
            if (await _versionRepo.VersionExistsAsync(version, ct))
                return ServiceResult.Failure($"الإصدار '{version}' مسجل مسبقاً.");

            var entity = new Domain.Entities.Settings.SystemVersion(
                version,
                _currentUser.Username ?? Domain.DomainConstants.SystemUser,
                description,
                _dateTime.UtcNow);

            await _versionRepo.AddAsync(entity, ct);
            await _unitOfWork.SaveChangesAsync(ct);

            return ServiceResult.Success();
        }
    }
}
