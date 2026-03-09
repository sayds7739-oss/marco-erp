using System.Threading;
using System.Threading.Tasks;
using MarcoERP.Application.Common;
using MarcoERP.Application.DTOs.Security;

namespace MarcoERP.Application.Interfaces.Security
{
    /// <summary>
    /// Application service for user authentication.
    /// Handles login, password change, and session management.
    /// </summary>
    public interface IAuthenticationService
    {
        /// <summary>
        /// Authenticates a user with username and password.
        /// Returns login result with user info and permissions on success.
        /// Handles failed login counting and account locking per SEC-AUTH-01.
        /// </summary>
        Task<ServiceResult<LoginResultDto>> LoginAsync(LoginDto dto, CancellationToken ct = default);

        /// <summary>
        /// Changes the current user's own password.
        /// Validates current password before allowing change.
        /// </summary>
        Task<ServiceResult> ChangePasswordAsync(int userId, ChangePasswordDto dto, CancellationToken ct = default);

        /// <summary>
        /// Retrieves user data for refresh token flow.
        /// Loads the user by ID and returns a LoginResultDto with current permissions.
        /// Used when issuing a new access token from a valid refresh token.
        /// </summary>
        Task<ServiceResult<LoginResultDto>> GetUserForRefreshAsync(int userId, CancellationToken ct = default);
    }
}
