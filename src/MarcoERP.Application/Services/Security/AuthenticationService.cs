using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MarcoERP.Application.Common;
using MarcoERP.Application.DTOs.Security;
using MarcoERP.Application.Interfaces;
using MarcoERP.Application.Interfaces.Security;
using MarcoERP.Domain.Enums;
using MarcoERP.Domain.Exceptions;
using MarcoERP.Domain.Interfaces;
using MarcoERP.Domain.Interfaces.Security;

namespace MarcoERP.Application.Services.Security
{
    /// <summary>
    /// Handles user authentication: login, password change.
    /// SEC-AUTH-01: Locks account after 5 failed attempts.
    /// Uses BCrypt via IPasswordHasher.
    /// </summary>
    [Module(SystemModule.Security)]
    public sealed class AuthenticationService : IAuthenticationService
    {
        private readonly IUserRepository _userRepo;
        private readonly IRoleRepository _roleRepo;
        private readonly IPasswordHasher _passwordHasher;
        private readonly IDateTimeProvider _dateTimeProvider;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IAuditLogger _auditLogger;

        public AuthenticationService(
            IUserRepository userRepo,
            IRoleRepository roleRepo,
            IPasswordHasher passwordHasher,
            IDateTimeProvider dateTimeProvider,
            IUnitOfWork unitOfWork,
            IAuditLogger auditLogger)
        {
            _userRepo = userRepo ?? throw new ArgumentNullException(nameof(userRepo));
            _roleRepo = roleRepo ?? throw new ArgumentNullException(nameof(roleRepo));
            _passwordHasher = passwordHasher ?? throw new ArgumentNullException(nameof(passwordHasher));
            _dateTimeProvider = dateTimeProvider ?? throw new ArgumentNullException(nameof(dateTimeProvider));
            _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
            _auditLogger = auditLogger ?? throw new ArgumentNullException(nameof(auditLogger));
        }

        public async Task<ServiceResult<LoginResultDto>> LoginAsync(LoginDto dto, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(dto.Username) || string.IsNullOrWhiteSpace(dto.Password))
                return ServiceResult<LoginResultDto>.Failure("اسم المستخدم وكلمة المرور مطلوبين.");

            var user = await _userRepo.GetByUsernameAsync(dto.Username.Trim().ToLowerInvariant(), ct);
            if (user == null)
                return ServiceResult<LoginResultDto>.Failure("اسم المستخدم أو كلمة المرور غير صحيحة.");

            // Check if account is active
            if (!user.IsActive)
                return ServiceResult<LoginResultDto>.Failure("الحساب معطّل. يرجى التواصل مع المدير.");

            // Check if account is locked (AUTH-07: timed auto-unlock after LockoutMinutes)
            if (user.IsLockedAt(_dateTimeProvider.UtcNow, lockoutMinutes: 15))
            {
                // If auto-unlock happened, persist the change
                if (!user.IsLocked)
                {
                    _userRepo.Update(user);
                    await _unitOfWork.SaveChangesAsync(ct);
                }
                else
                {
                    var remaining = user.LockedAt.HasValue
                        ? Math.Max(0, (int)(user.LockedAt.Value.AddMinutes(15) - _dateTimeProvider.UtcNow).TotalMinutes)
                        : 0;
                    return ServiceResult<LoginResultDto>.Failure(
                        $"الحساب مقفل بسبب محاولات دخول فاشلة. يرجى الانتظار {remaining} دقيقة أو التواصل مع المدير.");
                }
            }

            // Verify password
            bool passwordValid;
            try
            {
                passwordValid = _passwordHasher.VerifyPassword(dto.Password, user.PasswordHash);
            }
            catch
            {
                return ServiceResult<LoginResultDto>.Failure("خطأ في التحقق من كلمة المرور.");
            }

            if (!passwordValid)
            {
                // Record failed attempt
                user.RecordFailedLogin(_dateTimeProvider.UtcNow, maxAttempts: 5);
                _userRepo.Update(user);
                await _unitOfWork.SaveChangesAsync(ct);

                // AUTH-05: Log failed login attempt to audit trail
                try
                {
                    await _auditLogger.LogAsync("User", user.Id, "FailedLogin",
                        dto.Username.Trim(),
                        $"محاولة دخول فاشلة. عدد المحاولات: {user.FailedLoginAttempts}", ct);
                }
                catch
                {
                    // Audit failures must not block authentication outcomes.
                }

                if (user.IsLocked)
                    return ServiceResult<LoginResultDto>.Failure("تم قفل الحساب بعد 5 محاولات فاشلة. يرجى التواصل مع المدير.");

                return ServiceResult<LoginResultDto>.Failure("اسم المستخدم أو كلمة المرور غير صحيحة.");
            }

            // Successful login — load role with permissions
            var role = await _roleRepo.GetByIdWithPermissionsAsync(user.RoleId, ct);
            if (role == null)
                return ServiceResult<LoginResultDto>.Failure("الدور المعيّن للمستخدم غير موجود.");

            var utcNow = _dateTimeProvider.UtcNow;
            user.RecordSuccessfulLogin(utcNow);
            _userRepo.Update(user);
            await _unitOfWork.SaveChangesAsync(ct);

            // AUTH-05: Log successful login to audit trail
            try
            {
                await _auditLogger.LogAsync("User", user.Id, "Login",
                    user.Username, $"تسجيل دخول ناجح. الدور: {role.NameAr}", ct);
            }
            catch
            {
                // Audit failures must not block authentication outcomes.
            }

            var result = new LoginResultDto
            {
                UserId = user.Id,
                Username = user.Username,
                FullNameAr = user.FullNameAr,
                RoleId = role.Id,
                RoleNameAr = role.NameAr,
                RoleNameEn = role.NameEn,
                MustChangePassword = user.MustChangePassword,
                Permissions = role.Permissions.Select(p => p.PermissionKey).ToList(),
                LoginAt = utcNow
            };

            return ServiceResult<LoginResultDto>.Success(result);
        }

        public async Task<ServiceResult> ChangePasswordAsync(int userId, ChangePasswordDto dto, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(dto.CurrentPassword))
                return ServiceResult.Failure("كلمة المرور الحالية مطلوبة.");
            if (string.IsNullOrWhiteSpace(dto.NewPassword))
                return ServiceResult.Failure("كلمة المرور الجديدة مطلوبة.");
            if (dto.NewPassword != dto.ConfirmNewPassword)
                return ServiceResult.Failure("كلمة المرور الجديدة وتأكيدها غير متطابقتين.");

            var user = await _userRepo.GetByIdAsync(userId, ct);
            if (user == null)
                return ServiceResult.Failure("المستخدم غير موجود.");

            if (!_passwordHasher.VerifyPassword(dto.CurrentPassword, user.PasswordHash))
                return ServiceResult.Failure("كلمة المرور الحالية غير صحيحة.");

            var newHash = _passwordHasher.HashPassword(dto.NewPassword);

            try
            {
                user.ChangePassword(newHash);
                _userRepo.Update(user);
                await _unitOfWork.SaveChangesAsync(ct);
                return ServiceResult.Success();
            }
            catch (SecurityDomainException ex)
            {
                return ServiceResult.Failure(ex.Message);
            }
        }

        public async Task<ServiceResult<LoginResultDto>> GetUserForRefreshAsync(int userId, CancellationToken ct = default)
        {
            var user = await _userRepo.GetByIdAsync(userId, ct);
            if (user == null)
                return ServiceResult<LoginResultDto>.Failure("المستخدم غير موجود.");

            if (!user.IsActive)
                return ServiceResult<LoginResultDto>.Failure("الحساب معطّل. يرجى التواصل مع المدير.");

            if (user.IsLocked)
                return ServiceResult<LoginResultDto>.Failure("الحساب مقفل. يرجى التواصل مع المدير.");

            var role = await _roleRepo.GetByIdWithPermissionsAsync(user.RoleId, ct);
            if (role == null)
                return ServiceResult<LoginResultDto>.Failure("الدور المعيّن للمستخدم غير موجود.");

            var result = new LoginResultDto
            {
                UserId = user.Id,
                Username = user.Username,
                FullNameAr = user.FullNameAr,
                RoleId = role.Id,
                RoleNameAr = role.NameAr,
                RoleNameEn = role.NameEn,
                MustChangePassword = user.MustChangePassword,
                Permissions = role.Permissions.Select(p => p.PermissionKey).ToList(),
                LoginAt = _dateTimeProvider.UtcNow
            };

            return ServiceResult<LoginResultDto>.Success(result);
        }
    }
}
