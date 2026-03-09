using MarcoERP.Application.DTOs.Security;
using MarcoERP.Application.Interfaces.Security;
using Microsoft.AspNetCore.Mvc;

namespace MarcoERP.API.Controllers.Settings
{
    /// <summary>
    /// API controller for role management.
    /// </summary>
    public class RolesController : ApiControllerBase
    {
        private readonly IRoleService _roleService;

        public RolesController(IRoleService roleService)
        {
            _roleService = roleService;
        }

        // ── Queries ─────────────────────────────────────────────

        /// <summary>Gets all roles.</summary>
        [HttpGet]
        public async Task<IActionResult> GetAll(CancellationToken ct)
        {
            var result = await _roleService.GetAllAsync(ct);
            return FromResult(result);
        }

        /// <summary>Gets a role by ID with its permissions.</summary>
        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id, CancellationToken ct)
        {
            var result = await _roleService.GetByIdAsync(id, ct);
            return FromResult(result);
        }

        // ── Commands ────────────────────────────────────────────

        /// <summary>Creates a new role.</summary>
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateRoleDto dto, CancellationToken ct)
        {
            var result = await _roleService.CreateAsync(dto, ct);
            return FromResult(result);
        }

        /// <summary>Updates an existing role.</summary>
        [HttpPut]
        public async Task<IActionResult> Update([FromBody] UpdateRoleDto dto, CancellationToken ct)
        {
            var result = await _roleService.UpdateAsync(dto, ct);
            return FromResult(result);
        }

        /// <summary>Deletes a role (non-system only).</summary>
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id, CancellationToken ct)
        {
            var result = await _roleService.DeleteAsync(id, ct);
            return FromResult(result);
        }
    }
}
