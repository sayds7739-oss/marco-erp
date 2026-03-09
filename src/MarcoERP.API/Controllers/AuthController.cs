using MarcoERP.API.Services;
using MarcoERP.Application.Common;
using MarcoERP.Application.DTOs.Security;
using MarcoERP.Application.Interfaces.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using System.Security.Claims;

namespace MarcoERP.API.Controllers;

public class AuthController : ApiControllerBase
{
    private readonly IAuthenticationService _authService;
    private readonly JwtTokenService _tokenService;
    private readonly RefreshTokenStore _tokenStore;
    private readonly JwtSettings _jwtSettings;

    public AuthController(
        IAuthenticationService authService,
        JwtTokenService tokenService,
        RefreshTokenStore tokenStore,
        IOptions<JwtSettings> jwtSettings)
    {
        _authService = authService;
        _tokenService = tokenService;
        _tokenStore = tokenStore;
        _jwtSettings = jwtSettings.Value;
    }

    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> Login([FromBody] LoginDto dto, CancellationToken ct)
    {
        var result = await _authService.LoginAsync(dto, ct);
        if (result.IsFailure)
            return BadRequest(new { success = false, errors = result.Errors });

        var data = result.Data;
        var accessToken = _tokenService.GenerateAccessToken(
            data.UserId, data.Username, data.FullNameAr,
            data.RoleId, data.RoleNameAr, data.Permissions);
        var refreshToken = _tokenService.GenerateRefreshToken();

        // Store refresh token for validation
        await _tokenStore.StoreAsync(refreshToken, data.UserId,
            DateTime.UtcNow.AddDays(_jwtSettings.RefreshTokenExpirationDays));

        return Ok(new
        {
            success = true,
            data = new
            {
                accessToken,
                refreshToken,
                expiresInMinutes = _jwtSettings.AccessTokenExpirationMinutes,
                user = new
                {
                    data.UserId,
                    data.Username,
                    data.FullNameAr,
                    data.RoleId,
                    data.RoleNameAr,
                    data.MustChangePassword,
                    data.Permissions
                }
            }
        });
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> Refresh([FromBody] RefreshTokenRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
            return BadRequest(new { success = false, errors = new[] { "رمز التحديث مطلوب." } });

        var entry = await _tokenStore.ValidateAsync(request.RefreshToken);
        if (entry == null)
            return Unauthorized(new { success = false, errors = new[] { "رمز التحديث غير صالح أو منتهي الصلاحية." } });

        // Get fresh user data
        var userResult = await _authService.GetUserForRefreshAsync(entry.UserId, ct);
        if (userResult.IsFailure)
            return Unauthorized(new { success = false, errors = userResult.Errors });

        var data = userResult.Data;

        // Revoke old refresh token (rotation)
        await _tokenStore.RevokeAsync(request.RefreshToken);

        // Issue new tokens
        var accessToken = _tokenService.GenerateAccessToken(
            data.UserId, data.Username, data.FullNameAr,
            data.RoleId, data.RoleNameAr, data.Permissions);
        var newRefreshToken = _tokenService.GenerateRefreshToken();

        await _tokenStore.StoreAsync(newRefreshToken, data.UserId,
            DateTime.UtcNow.AddDays(_jwtSettings.RefreshTokenExpirationDays));

        return Ok(new
        {
            success = true,
            data = new
            {
                accessToken,
                refreshToken = newRefreshToken,
                expiresInMinutes = _jwtSettings.AccessTokenExpirationMinutes
            }
        });
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout([FromBody] RefreshTokenRequest? request)
    {
        if (!string.IsNullOrWhiteSpace(request?.RefreshToken))
            await _tokenStore.RevokeAsync(request.RefreshToken);

        if (!int.TryParse(User.FindFirstValue("userId"), out var userId))
            return BadRequest(new { success = false, errors = new[] { "سياق المستخدم غير صالح." } });

        await _tokenStore.RevokeAllForUserAsync(userId);

        return Ok(new { success = true });
    }

    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordDto dto, CancellationToken ct)
    {
        if (!int.TryParse(User.FindFirstValue("userId"), out var userId))
            return BadRequest(new { success = false, errors = new[] { "سياق المستخدم غير صالح." } });

        var result = await _authService.ChangePasswordAsync(userId, dto, ct);
        if (result.IsSuccess)
        {
            // Revoke all refresh tokens on password change
            await _tokenStore.RevokeAllForUserAsync(userId);
        }
        return FromResult(result);
    }
}

public class RefreshTokenRequest
{
    public string? RefreshToken { get; set; }
}
