using MarcoERP.Application.Interfaces.Settings;
using Microsoft.AspNetCore.Mvc;

namespace MarcoERP.API.Controllers.Settings
{
    /// <summary>
    /// API controller for viewing and managing soft-deleted records (recycle bin).
    /// </summary>
    public class RecycleBinController : ApiControllerBase
    {
        private readonly IRecycleBinService _recycleBinService;

        public RecycleBinController(IRecycleBinService recycleBinService)
        {
            _recycleBinService = recycleBinService;
        }

        // ── Queries ─────────────────────────────────────────────

        /// <summary>Gets the list of supported entity types for the recycle bin.</summary>
        [HttpGet("entity-types")]
        public IActionResult GetSupportedEntityTypes()
        {
            var types = _recycleBinService.GetSupportedEntityTypes();
            var data = types.Select(t => new { key = t.Key, arabicName = t.ArabicName });
            return Ok(new { success = true, data });
        }

        /// <summary>Gets all deleted records across supported entity types.</summary>
        [HttpGet]
        public async Task<IActionResult> GetAllDeleted(CancellationToken ct)
        {
            var result = await _recycleBinService.GetAllDeletedAsync(ct);
            return FromResult(result);
        }

        /// <summary>Gets deleted records for a specific entity type.</summary>
        [HttpGet("{entityType}")]
        public async Task<IActionResult> GetByEntityType(string entityType, CancellationToken ct)
        {
            if (!IsValidEntityType(entityType))
                return BadRequest(new { success = false, message = "نوع الكيان غير مدعوم" });

            var result = await _recycleBinService.GetByEntityTypeAsync(entityType, ct);
            return FromResult(result);
        }

        // ── Commands ────────────────────────────────────────────

        /// <summary>Restores a deleted record (sets IsDeleted = false).</summary>
        [HttpPost("{entityType}/{entityId:int}/restore")]
        public async Task<IActionResult> Restore(string entityType, int entityId, CancellationToken ct)
        {
            if (!IsValidEntityType(entityType))
                return BadRequest(new { success = false, message = "نوع الكيان غير مدعوم" });

            var result = await _recycleBinService.RestoreAsync(entityType, entityId, ct);
            return FromResult(result);
        }

        private bool IsValidEntityType(string entityType)
        {
            var supported = _recycleBinService.GetSupportedEntityTypes();
            return supported.Any(t => string.Equals(t.Key, entityType, StringComparison.OrdinalIgnoreCase));
        }
    }
}
