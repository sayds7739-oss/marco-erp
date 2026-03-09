using System.Security.Claims;
using MarcoERP.Application.Interfaces;

namespace MarcoERP.API.Services;

public class ApiCurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public ApiCurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    private ClaimsPrincipal? User => _httpContextAccessor.HttpContext?.User;

    public string Username => User?.FindFirstValue("username") ?? string.Empty;
    public bool IsAuthenticated => User?.Identity?.IsAuthenticated ?? false;
    public int UserId => int.TryParse(User?.FindFirstValue("userId"), out var id) ? id : 0;
    public int RoleId => int.TryParse(User?.FindFirstValue("roleId"), out var id) ? id : 0;
    public string RoleNameAr => User?.FindFirstValue("roleNameAr") ?? string.Empty;
    public string FullNameAr => User?.FindFirstValue("fullNameAr") ?? string.Empty;

    public IReadOnlyList<string> Permissions
    {
        get
        {
            var perms = User?.FindFirstValue("permissions");
            if (string.IsNullOrEmpty(perms)) return Array.Empty<string>();
            return perms.Split(',', StringSplitOptions.RemoveEmptyEntries);
        }
    }

    public bool HasPermission(string permissionKey)
    {
        if (!IsAuthenticated) return false;
        var perms = Permissions;
        return perms.Contains("*") || perms.Contains(permissionKey);
    }

    // These methods are not needed in API context (JWT is stateless)
    public void SetUser(string username) { }
    public void SetUser(int userId, string username, string fullNameAr, int roleId, string roleNameAr, IReadOnlyList<string> permissions) { }
    public void ClearUser() { }
}
