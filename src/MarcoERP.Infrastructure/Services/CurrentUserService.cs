using System.Collections.Generic;
using System.Linq;
using MarcoERP.Application.Interfaces;

namespace MarcoERP.Infrastructure.Services
{
    /// <summary>
    /// Provides the currently authenticated user's identity.
    /// In WPF, this is set after successful login and read by the audit interceptor.
    /// Extended in Phase 5C with UserId, RoleId, RoleName, Permissions, HasPermission.
    /// Stored in instance state to avoid static mutable state.
    /// </summary>
    public sealed class CurrentUserService : ICurrentUserService
    {
        private string _username;
        private bool _isAuthenticated;
        private int _userId;
        private int _roleId;
        private string _roleNameAr;
        private string _fullNameAr;
        private List<string> _permissions = new();
        private bool _isSystemAdmin;

        /// <inheritdoc />
        public string Username => _username ?? "System";

        /// <inheritdoc />
        public bool IsAuthenticated => _isAuthenticated;

        /// <inheritdoc />
        public int UserId => _userId;

        /// <inheritdoc />
        public int RoleId => _roleId;

        /// <inheritdoc />
        public string RoleNameAr => _roleNameAr ?? string.Empty;

        /// <inheritdoc />
        public string FullNameAr => _fullNameAr ?? string.Empty;

        /// <inheritdoc />
        public IReadOnlyList<string> Permissions => _permissions.AsReadOnly();

        /// <inheritdoc />
        public bool HasPermission(string permissionKey)
        {
            if (string.IsNullOrWhiteSpace(permissionKey)) return false;
            // System administrators have all permissions — determined by role name or wildcard
            if (_isSystemAdmin) return true;
            return _permissions.Contains(permissionKey);
        }

        /// <summary>
        /// Sets the current user after successful authentication (legacy overload).
        /// </summary>
        public void SetUser(string username)
        {
            _username = username?.Trim();
            _isAuthenticated = !string.IsNullOrWhiteSpace(_username);
        }

        /// <summary>
        /// Sets the current user with full identity after successful authentication.
        /// </summary>
        public void SetUser(int userId, string username, string fullNameAr, int roleId, string roleNameAr, IReadOnlyList<string> permissions)
        {
            _userId = userId;
            _username = username?.Trim();
            _fullNameAr = fullNameAr?.Trim();
            _roleId = roleId;
            _roleNameAr = roleNameAr?.Trim();
            _permissions = permissions?.ToList() ?? new List<string>();
            _isAuthenticated = !string.IsNullOrWhiteSpace(_username);
            // Determine super-admin by wildcard permission (primary) or well-known role name (fallback)
            _isSystemAdmin = _permissions.Contains("*")
                          || string.Equals(_roleNameAr, "مدير النظام", System.StringComparison.Ordinal);
        }

        /// <summary>
        /// Clears the current user on logout.
        /// </summary>
        public void ClearUser()
        {
            _username = null;
            _isAuthenticated = false;
            _userId = 0;
            _roleId = 0;
            _roleNameAr = null;
            _fullNameAr = null;
            _permissions = new List<string>();
            _isSystemAdmin = false;
        }
    }
}
