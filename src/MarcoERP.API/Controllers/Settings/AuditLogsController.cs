using MarcoERP.Application.Interfaces.Settings;
using Microsoft.AspNetCore.Mvc;

namespace MarcoERP.API.Controllers.Settings
{
    /// <summary>
    /// API controller for audit log viewing.
    /// </summary>
    public class AuditLogsController : ApiControllerBase
    {
        private readonly IAuditLogService _auditLogService;

        public AuditLogsController(IAuditLogService auditLogService)
        {
            _auditLogService = auditLogService;
        }

        /// <summary>Returns all audit log records ordered by timestamp descending.</summary>
        [HttpGet]
        public async Task<IActionResult> GetAll(CancellationToken ct)
        {
            var result = await _auditLogService.GetAllAsync(ct);
            return FromResult(result);
        }

        /// <summary>Returns audit log records for a specific entity type and entity ID.</summary>
        [HttpGet("entity/{entityType}/{entityId:int}")]
        public async Task<IActionResult> GetByEntity(string entityType, int entityId, CancellationToken ct)
        {
            var result = await _auditLogService.GetByEntityAsync(entityType, entityId, ct);
            return FromResult(result);
        }

        /// <summary>Returns audit log records within a date range.</summary>
        [HttpGet("date-range")]
        public async Task<IActionResult> GetByDateRange(
            [FromQuery] DateTime start,
            [FromQuery] DateTime end,
            CancellationToken ct)
        {
            var result = await _auditLogService.GetByDateRangeAsync(start, end, ct);
            return FromResult(result);
        }

        /// <summary>Returns audit log records performed by a specific user.</summary>
        [HttpGet("user/{username}")]
        public async Task<IActionResult> GetByUser(string username, CancellationToken ct)
        {
            var result = await _auditLogService.GetByUserAsync(username, ct);
            return FromResult(result);
        }
    }
}
