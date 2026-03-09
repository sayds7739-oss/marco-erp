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
using MarcoERP.Domain.Enums;
using MarcoERP.Domain.Interfaces;
using MarcoERP.Domain.Interfaces.Settings;

namespace MarcoERP.Application.Services.Settings
{
    /// <summary>
    /// Application service for system settings management.
    /// </summary>
    [Module(SystemModule.Settings)]
    public sealed class SystemSettingsService : ISystemSettingsService
    {
        private readonly ISystemSettingRepository _settingRepo;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ICurrentUserService _currentUser;

        public SystemSettingsService(
            ISystemSettingRepository settingRepo,
            IUnitOfWork unitOfWork,
            ICurrentUserService currentUser)
        {
            _settingRepo = settingRepo ?? throw new ArgumentNullException(nameof(settingRepo));
            _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
            _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
        }

        public async Task<ServiceResult<IReadOnlyList<SystemSettingDto>>> GetAllAsync(CancellationToken ct = default)
        {
            var entities = await _settingRepo.GetAllAsync(ct);
            return ServiceResult<IReadOnlyList<SystemSettingDto>>.Success(
                entities.Select(SystemSettingMapper.ToDto).ToList());
        }

        public async Task<ServiceResult<IReadOnlyList<SettingGroupDto>>> GetAllGroupedAsync(CancellationToken ct = default)
        {
            var entities = await _settingRepo.GetAllAsync(ct);
            var groups = entities
                .GroupBy(s => s.GroupName ?? "عام")
                .Select(g => new SettingGroupDto
                {
                    GroupName = g.Key,
                    Settings = g.Select(SystemSettingMapper.ToDto).ToList()
                })
                .ToList();
            return ServiceResult<IReadOnlyList<SettingGroupDto>>.Success(groups);
        }

        public async Task<ServiceResult<SystemSettingDto>> GetByKeyAsync(string key, CancellationToken ct = default)
        {
            var entity = await _settingRepo.GetByKeyAsync(key, ct);
            if (entity == null)
                return ServiceResult<SystemSettingDto>.Failure($"الإعداد '{key}' غير موجود.");
            return ServiceResult<SystemSettingDto>.Success(SystemSettingMapper.ToDto(entity));
        }

        public async Task<ServiceResult> UpdateAsync(UpdateSystemSettingDto dto, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(dto.SettingKey))
                return ServiceResult.Failure("مفتاح الإعداد مطلوب.");

            var entity = await _settingRepo.GetByKeyAsync(dto.SettingKey, ct);
            if (entity == null)
                return ServiceResult.Failure($"الإعداد '{dto.SettingKey}' غير موجود.");

            entity.UpdateValue(dto.SettingValue);
            await _unitOfWork.SaveChangesAsync(ct);
            return ServiceResult.Success();
        }

        public async Task<ServiceResult> UpdateBatchAsync(IEnumerable<UpdateSystemSettingDto> dtos, CancellationToken ct = default)
        {
            foreach (var dto in dtos)
            {
                if (string.IsNullOrWhiteSpace(dto.SettingKey))
                    continue;

                var entity = await _settingRepo.GetByKeyAsync(dto.SettingKey, ct);
                if (entity == null)
                    return ServiceResult.Failure($"الإعداد '{dto.SettingKey}' غير موجود.");

                entity.UpdateValue(dto.SettingValue);
            }

            await _unitOfWork.SaveChangesAsync(ct);
            return ServiceResult.Success();
        }
    }
}
