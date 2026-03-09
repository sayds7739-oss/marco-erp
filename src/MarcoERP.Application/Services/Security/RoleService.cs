using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using MarcoERP.Application.Common;
using MarcoERP.Application.DTOs.Security;
using MarcoERP.Application.Interfaces;
using MarcoERP.Application.Interfaces.Security;
using MarcoERP.Application.Interfaces.Settings;
using MarcoERP.Application.Mappers.Security;
using MarcoERP.Domain.Entities.Security;
using MarcoERP.Domain.Enums;
using MarcoERP.Domain.Interfaces;
using MarcoERP.Domain.Interfaces.Security;
using Microsoft.Extensions.Logging;

namespace MarcoERP.Application.Services.Security
{
    /// <summary>
    /// Application service for role management.
    /// Supports CRUD operations with permission management.
    /// </summary>
    [Module(SystemModule.Security)]
    public sealed class RoleService : IRoleService
    {
        private readonly IRoleRepository _roleRepo;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IFeatureService _featureService;
        private readonly IValidator<CreateRoleDto> _createValidator;
        private readonly IValidator<UpdateRoleDto> _updateValidator;
        private readonly ILogger<RoleService> _logger;

        public RoleService(
            IRoleRepository roleRepo,
            IUnitOfWork unitOfWork,
            IFeatureService featureService,
            IValidator<CreateRoleDto> createValidator,
            IValidator<UpdateRoleDto> updateValidator,
            ILogger<RoleService> logger = null)
        {
            _roleRepo = roleRepo ?? throw new ArgumentNullException(nameof(roleRepo));
            _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
            _featureService = featureService ?? throw new ArgumentNullException(nameof(featureService));
            _createValidator = createValidator ?? throw new ArgumentNullException(nameof(createValidator));
            _updateValidator = updateValidator ?? throw new ArgumentNullException(nameof(updateValidator));
            _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<RoleService>.Instance;
        }

        public async Task<ServiceResult<IReadOnlyList<RoleListDto>>> GetAllAsync(CancellationToken ct = default)
        {
            var entities = await _roleRepo.GetAllWithPermissionsAsync(ct);
            return ServiceResult<IReadOnlyList<RoleListDto>>.Success(
                entities.Select(RoleMapper.ToListDto).ToList());
        }

        public async Task<ServiceResult<RoleDto>> GetByIdAsync(int id, CancellationToken ct = default)
        {
            var entity = await _roleRepo.GetByIdWithPermissionsAsync(id, ct);
            if (entity == null)
                return ServiceResult<RoleDto>.Failure("الدور غير موجود.");
            return ServiceResult<RoleDto>.Success(RoleMapper.ToDto(entity));
        }

        public async Task<ServiceResult<RoleDto>> CreateAsync(CreateRoleDto dto, CancellationToken ct = default)
        {
            // Feature guard: UserManagement must be enabled
            var guard = await FeatureGuard.CheckAsync<RoleDto>(_featureService, FeatureKeys.UserManagement, ct);
            if (guard != null) return guard;

            _logger.LogInformation("Operation={Operation} Entity={Entity} EntityId={EntityId}", "CreateAsync", "Role", 0);

            var vr = await _createValidator.ValidateAsync(dto, ct);
            if (!vr.IsValid)
                return ServiceResult<RoleDto>.Failure(vr.Errors[0].ErrorMessage);

            if (await _roleRepo.NameExistsAsync(dto.NameEn ?? dto.NameAr, null, ct))
                return ServiceResult<RoleDto>.Failure("اسم الدور موجود مسبقاً.");

            var entity = new Role(dto.NameAr, dto.NameEn ?? dto.NameAr, dto.Description);
            entity.SetPermissions(dto.Permissions ?? new List<string>());

            await _roleRepo.AddAsync(entity, ct);
            await _unitOfWork.SaveChangesAsync(ct);

            var saved = await _roleRepo.GetByIdWithPermissionsAsync(entity.Id, ct);
            return ServiceResult<RoleDto>.Success(RoleMapper.ToDto(saved));
        }

        /// <summary>
        /// Critical permissions that cannot be removed from system roles.
        /// Removing these would lock out all role management (C-02 fix).
        /// </summary>
        private static readonly HashSet<string> CriticalSystemPermissions = new(StringComparer.OrdinalIgnoreCase)
        {
            "roles.manage",
            "users.manage"
        };

        public async Task<ServiceResult<RoleDto>> UpdateAsync(UpdateRoleDto dto, CancellationToken ct = default)
        {
            // Feature guard: UserManagement must be enabled
            var guard = await FeatureGuard.CheckAsync<RoleDto>(_featureService, FeatureKeys.UserManagement, ct);
            if (guard != null) return guard;

            _logger.LogInformation("Operation={Operation} Entity={Entity} EntityId={EntityId}", "UpdateAsync", "Role", dto.Id);
            var vr = await _updateValidator.ValidateAsync(dto, ct);
            if (!vr.IsValid)
                return ServiceResult<RoleDto>.Failure(vr.Errors[0].ErrorMessage);

            var entity = await _roleRepo.GetByIdWithPermissionsTrackedAsync(dto.Id, ct);
            if (entity == null)
                return ServiceResult<RoleDto>.Failure("الدور غير موجود.");

            // H-08: Prevent renaming system roles
            if (entity.IsSystem)
            {
                var nameChanged = !string.Equals(entity.NameAr, dto.NameAr, StringComparison.Ordinal)
                    || !string.Equals(entity.NameEn, dto.NameEn ?? dto.NameAr, StringComparison.OrdinalIgnoreCase);
                if (nameChanged)
                    return ServiceResult<RoleDto>.Failure("لا يمكن تغيير اسم دور النظام.");
            }

            // C-02: Prevent removing critical permissions from system roles
            if (entity.IsSystem)
            {
                var newPermissions = dto.Permissions ?? new List<string>();
                foreach (var critical in CriticalSystemPermissions)
                {
                    if (!newPermissions.Any(p => string.Equals(p, critical, StringComparison.OrdinalIgnoreCase)))
                        return ServiceResult<RoleDto>.Failure(
                            $"لا يمكن إزالة الصلاحية الحرجة '{critical}' من دور النظام. هذا سيؤدي إلى إقفال كامل للنظام.");
                }
            }

            // H-07: Check both NameEn and NameAr for duplicate names
            if (await _roleRepo.NameExistsAsync(dto.NameEn ?? dto.NameAr, dto.Id, ct))
                return ServiceResult<RoleDto>.Failure("اسم الدور موجود مسبقاً.");

            if (!string.IsNullOrWhiteSpace(dto.NameAr) && dto.NameAr != (dto.NameEn ?? dto.NameAr))
            {
                if (await _roleRepo.NameExistsAsync(dto.NameAr, dto.Id, ct))
                    return ServiceResult<RoleDto>.Failure("اسم الدور (عربي) موجود مسبقاً.");
            }

            entity.Update(dto.NameAr, dto.NameEn ?? dto.NameAr, dto.Description);
            entity.SetPermissions(dto.Permissions ?? new List<string>());

            _roleRepo.Update(entity);
            await _unitOfWork.SaveChangesAsync(ct);

            var saved = await _roleRepo.GetByIdWithPermissionsAsync(entity.Id, ct);
            return ServiceResult<RoleDto>.Success(RoleMapper.ToDto(saved));
        }

        public async Task<ServiceResult> DeleteAsync(int id, CancellationToken ct = default)
        {
            // Feature guard: UserManagement must be enabled
            var guard = await FeatureGuard.CheckAsync(_featureService, FeatureKeys.UserManagement, ct);
            if (guard != null) return guard;

            _logger.LogInformation("Operation={Operation} Entity={Entity} EntityId={EntityId}", "DeleteAsync", "Role", id);
            var entity = await _roleRepo.GetByIdWithPermissionsAsync(id, ct);
            if (entity == null)
                return ServiceResult.Failure("الدور غير موجود.");

            if (entity.IsSystem)
                return ServiceResult.Failure("لا يمكن حذف دور النظام.");

            if (entity.Users != null && entity.Users.Count > 0)
                return ServiceResult.Failure("لا يمكن حذف دور مرتبط بمستخدمين.");

            _roleRepo.Remove(entity);
            await _unitOfWork.SaveChangesAsync(ct);

            return ServiceResult.Success();
        }
    }
}
