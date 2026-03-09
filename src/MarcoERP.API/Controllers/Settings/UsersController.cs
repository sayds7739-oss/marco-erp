using MarcoERP.Application.DTOs.Security;
using MarcoERP.Application.Interfaces.Security;
using Microsoft.AspNetCore.Mvc;

namespace MarcoERP.API.Controllers.Settings
{
    /// <summary>
    /// API controller for user management.
    /// </summary>
    public class UsersController : ApiControllerBase
    {
        private readonly IUserService _userService;

        public UsersController(IUserService userService)
        {
            _userService = userService;
        }

        // ── Queries ─────────────────────────────────────────────

        /// <summary>Gets all users.</summary>
        [HttpGet]
        public async Task<IActionResult> GetAll(CancellationToken ct)
        {
            var result = await _userService.GetAllAsync(ct);
            return FromResult(result);
        }

        /// <summary>Gets a user by ID.</summary>
        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id, CancellationToken ct)
        {
            var result = await _userService.GetByIdAsync(id, ct);
            return FromResult(result);
        }

        // ── Commands ────────────────────────────────────────────

        /// <summary>Creates a new user account.</summary>
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateUserDto dto, CancellationToken ct)
        {
            var result = await _userService.CreateAsync(dto, ct);
            return FromResult(result);
        }

        /// <summary>Updates user profile information.</summary>
        [HttpPut]
        public async Task<IActionResult> Update([FromBody] UpdateUserDto dto, CancellationToken ct)
        {
            var result = await _userService.UpdateAsync(dto, ct);
            return FromResult(result);
        }

        /// <summary>Resets a user's password (admin action).</summary>
        [HttpPost("{id:int}/reset-password")]
        public async Task<IActionResult> ResetPassword(int id, [FromBody] ResetPasswordDto dto, CancellationToken ct)
        {
            // SECURITY: Enforce route parameter matches DTO to prevent privilege escalation
            if (dto.UserId != id)
                return BadRequest(new { success = false, errors = new[] { "معرف المستخدم في الطلب لا يتطابق مع المسار." } });

            var result = await _userService.ResetPasswordAsync(dto, ct);
            return FromResult(result);
        }

        /// <summary>Activates a user account.</summary>
        [HttpPatch("{id:int}/activate")]
        public async Task<IActionResult> Activate(int id, CancellationToken ct)
        {
            var result = await _userService.ActivateAsync(id, ct);
            return FromResult(result);
        }

        /// <summary>Deactivates a user account.</summary>
        [HttpPatch("{id:int}/deactivate")]
        public async Task<IActionResult> Deactivate(int id, CancellationToken ct)
        {
            var result = await _userService.DeactivateAsync(id, ct);
            return FromResult(result);
        }

        /// <summary>Unlocks a locked user account.</summary>
        [HttpPatch("{id:int}/unlock")]
        public async Task<IActionResult> Unlock(int id, CancellationToken ct)
        {
            var result = await _userService.UnlockAsync(id, ct);
            return FromResult(result);
        }
    }
}
