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
using MarcoERP.Domain;
using MarcoERP.Domain.Entities.Security;
using MarcoERP.Domain.Enums;
using MarcoERP.Domain.Exceptions;
using MarcoERP.Domain.Interfaces;
using MarcoERP.Domain.Interfaces.Security;
using Microsoft.Extensions.Logging;

namespace MarcoERP.Application.Services.Security
{
    /// <summary>
    /// Application service for user CRUD management.
    /// </summary>
    [Module(SystemModule.Security)]
    public sealed class UserService : IUserService
    {
        private readonly IUserRepository _userRepo;
        private readonly IRoleRepository _roleRepo;
        private readonly IPasswordHasher _passwordHasher;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ICurrentUserService _currentUser;
        private readonly IFeatureService _featureService;
        private readonly IValidator<CreateUserDto> _createValidator;
        private readonly IValidator<UpdateUserDto> _updateValidator;
        private readonly IValidator<ResetPasswordDto> _resetValidator;
        private readonly ILogger<UserService> _logger;

        public UserService(
            IUserRepository userRepo,
            IRoleRepository roleRepo,
            IPasswordHasher passwordHasher,
            IUnitOfWork unitOfWork,
            ICurrentUserService currentUser,
            IFeatureService featureService,
            IValidator<CreateUserDto> createValidator,
            IValidator<UpdateUserDto> updateValidator,
            IValidator<ResetPasswordDto> resetValidator,
            ILogger<UserService> logger = null)
        {
            _userRepo = userRepo ?? throw new ArgumentNullException(nameof(userRepo));
            _roleRepo = roleRepo ?? throw new ArgumentNullException(nameof(roleRepo));
            _passwordHasher = passwordHasher ?? throw new ArgumentNullException(nameof(passwordHasher));
            _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
            _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
            _featureService = featureService ?? throw new ArgumentNullException(nameof(featureService));
            _createValidator = createValidator ?? throw new ArgumentNullException(nameof(createValidator));
            _updateValidator = updateValidator ?? throw new ArgumentNullException(nameof(updateValidator));
            _resetValidator = resetValidator ?? throw new ArgumentNullException(nameof(resetValidator));
            _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<UserService>.Instance;
        }

        public async Task<ServiceResult<IReadOnlyList<UserListDto>>> GetAllAsync(CancellationToken ct = default)
        {
            var entities = await _userRepo.GetAllWithRolesAsync(ct);
            return ServiceResult<IReadOnlyList<UserListDto>>.Success(
                entities.Select(UserMapper.ToListDto).ToList());
        }

        public async Task<ServiceResult<UserDto>> GetByIdAsync(int id, CancellationToken ct = default)
        {
            var entity = await _userRepo.GetByIdWithRoleAsync(id, ct);
            if (entity == null)
                return ServiceResult<UserDto>.Failure("المستخدم غير موجود.");
            return ServiceResult<UserDto>.Success(UserMapper.ToDto(entity));
        }

        public async Task<ServiceResult<UserDto>> CreateAsync(CreateUserDto dto, CancellationToken ct = default)
        {
            // Feature guard: UserManagement must be enabled
            var guard = await FeatureGuard.CheckAsync<UserDto>(_featureService, FeatureKeys.UserManagement, ct);
            if (guard != null) return guard;

            _logger.LogInformation("Operation={Operation} Entity={Entity} EntityId={EntityId}", "CreateAsync", "User", 0);

            var vr = await _createValidator.ValidateAsync(dto, ct);
            if (!vr.IsValid)
                return ServiceResult<UserDto>.Failure(
                    string.Join(" | ", vr.Errors.Select(e => e.ErrorMessage)));

            // Check username uniqueness
            var username = dto.Username.Trim().ToLowerInvariant();
            if (await _userRepo.UsernameExistsAsync(username, null, ct))
                return ServiceResult<UserDto>.Failure("اسم المستخدم مستخدم بالفعل.");

            // Verify role exists
            var role = await _roleRepo.GetByIdAsync(dto.RoleId, ct);
            if (role == null)
                return ServiceResult<UserDto>.Failure("الدور المحدد غير موجود.");

            try
            {
                var passwordHash = _passwordHasher.HashPassword(dto.Password);

                var entity = new User(
                    username,
                    passwordHash,
                    dto.FullNameAr,
                    dto.FullNameEn,
                    dto.Email,
                    dto.Phone,
                    dto.RoleId,
                    mustChangePassword: true);

                await _userRepo.AddAsync(entity, ct);
                await _unitOfWork.SaveChangesAsync(ct);

                // Reload with role for mapping
                var saved = await _userRepo.GetByIdWithRoleAsync(entity.Id, ct);
                return ServiceResult<UserDto>.Success(UserMapper.ToDto(saved));
            }
            catch (SecurityDomainException ex)
            {
                return ServiceResult<UserDto>.Failure(ex.Message);
            }
        }

        public async Task<ServiceResult<UserDto>> UpdateAsync(UpdateUserDto dto, CancellationToken ct = default)
        {
            // Feature guard: UserManagement must be enabled
            var guard = await FeatureGuard.CheckAsync<UserDto>(_featureService, FeatureKeys.UserManagement, ct);
            if (guard != null) return guard;

            _logger.LogInformation("Operation={Operation} Entity={Entity} EntityId={EntityId}", "UpdateAsync", "User", dto.Id);
            var vr = await _updateValidator.ValidateAsync(dto, ct);
            if (!vr.IsValid)
                return ServiceResult<UserDto>.Failure(
                    string.Join(" | ", vr.Errors.Select(e => e.ErrorMessage)));

            var entity = await _userRepo.GetByIdWithRoleAsync(dto.Id, ct);
            if (entity == null)
                return ServiceResult<UserDto>.Failure("المستخدم غير موجود.");

            // Verify role exists
            var role = await _roleRepo.GetByIdAsync(dto.RoleId, ct);
            if (role == null)
                return ServiceResult<UserDto>.Failure("الدور المحدد غير موجود.");

            try
            {
                // H-11: Prevent changing the admin user's role
                if (entity.Username == DomainConstants.AdminUsername && dto.RoleId != entity.RoleId)
                    return ServiceResult<UserDto>.Failure("لا يمكن تغيير دور المدير الرئيسي.");

                entity.UpdateProfile(dto.FullNameAr, dto.FullNameEn, dto.Email, dto.Phone);
                entity.ChangeRole(dto.RoleId);
                _userRepo.Update(entity);
                await _unitOfWork.SaveChangesAsync(ct);

                var saved = await _userRepo.GetByIdWithRoleAsync(entity.Id, ct);
                return ServiceResult<UserDto>.Success(UserMapper.ToDto(saved));
            }
            catch (SecurityDomainException ex)
            {
                return ServiceResult<UserDto>.Failure(ex.Message);
            }
        }

        public async Task<ServiceResult> ResetPasswordAsync(ResetPasswordDto dto, CancellationToken ct = default)
        {
            // Feature guard: UserManagement must be enabled
            var guard = await FeatureGuard.CheckAsync(_featureService, FeatureKeys.UserManagement, ct);
            if (guard != null) return guard;

            _logger.LogInformation("Operation={Operation} Entity={Entity} EntityId={EntityId}", "ResetPasswordAsync", "User", dto.UserId);
            var vr = await _resetValidator.ValidateAsync(dto, ct);
            if (!vr.IsValid)
                return ServiceResult.Failure(
                    string.Join(" | ", vr.Errors.Select(e => e.ErrorMessage)));

            var entity = await _userRepo.GetByIdAsync(dto.UserId, ct);
            if (entity == null)
                return ServiceResult.Failure("المستخدم غير موجود.");

            try
            {
                var newHash = _passwordHasher.HashPassword(dto.NewPassword);
                entity.ResetPassword(newHash);
                _userRepo.Update(entity);
                await _unitOfWork.SaveChangesAsync(ct);
                return ServiceResult.Success();
            }
            catch (SecurityDomainException ex)
            {
                return ServiceResult.Failure(ex.Message);
            }
        }

        public async Task<ServiceResult> ActivateAsync(int id, CancellationToken ct = default)
        {
            _logger.LogInformation("Operation={Operation} Entity={Entity} EntityId={EntityId}", "ActivateAsync", "User", id);
            var entity = await _userRepo.GetByIdAsync(id, ct);
            if (entity == null) return ServiceResult.Failure("المستخدم غير موجود.");

            entity.Activate();
            _userRepo.Update(entity);
            await _unitOfWork.SaveChangesAsync(ct);
            return ServiceResult.Success();
        }

        public async Task<ServiceResult> DeactivateAsync(int id, CancellationToken ct = default)
        {
            _logger.LogInformation("Operation={Operation} Entity={Entity} EntityId={EntityId}", "DeactivateAsync", "User", id);
            var entity = await _userRepo.GetByIdAsync(id, ct);
            if (entity == null) return ServiceResult.Failure("المستخدم غير موجود.");

            // H-10: Prevent user from deactivating their own account
            if (_currentUser.UserId == id)
                return ServiceResult.Failure("لا يمكنك تعطيل حسابك الخاص.");

            try
            {
                entity.Deactivate();
                _userRepo.Update(entity);
                await _unitOfWork.SaveChangesAsync(ct);
                return ServiceResult.Success();
            }
            catch (SecurityDomainException ex)
            {
                return ServiceResult.Failure(ex.Message);
            }
        }

        public async Task<ServiceResult> UnlockAsync(int id, CancellationToken ct = default)
        {
            _logger.LogInformation("Operation={Operation} Entity={Entity} EntityId={EntityId}", "UnlockAsync", "User", id);
            var entity = await _userRepo.GetByIdAsync(id, ct);
            if (entity == null) return ServiceResult.Failure("المستخدم غير موجود.");

            entity.Unlock();
            _userRepo.Update(entity);
            await _unitOfWork.SaveChangesAsync(ct);
            return ServiceResult.Success();
        }
    }
}
