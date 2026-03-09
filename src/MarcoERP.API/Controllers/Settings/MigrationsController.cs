using MarcoERP.Application.Interfaces.Settings;
using Microsoft.AspNetCore.Mvc;

namespace MarcoERP.API.Controllers.Settings
{
    /// <summary>
    /// API controller for controlled database migration execution.
    /// SECURITY: Migration execution is restricted to Development environment only.
    /// </summary>
    public class MigrationsController : ApiControllerBase
    {
        private readonly IMigrationExecutionService _migrationService;
        private readonly IWebHostEnvironment _env;

        public MigrationsController(IMigrationExecutionService migrationService, IWebHostEnvironment env)
        {
            _migrationService = migrationService;
            _env = env;
        }

        // ── Queries ─────────────────────────────────────────────

        /// <summary>Returns pending EF Core migration names (not yet applied to DB).</summary>
        [HttpGet("pending")]
        public async Task<IActionResult> GetPendingMigrations(CancellationToken ct)
        {
            var pending = await _migrationService.GetPendingMigrationsAsync(ct);
            return Ok(new { success = true, data = pending });
        }

        /// <summary>Returns the history of migration executions.</summary>
        [HttpGet("history")]
        public async Task<IActionResult> GetExecutionHistory(CancellationToken ct)
        {
            var history = await _migrationService.GetExecutionHistoryAsync(ct);
            return Ok(new { success = true, data = history });
        }

        // ── Commands ────────────────────────────────────────────

        /// <summary>Executes all pending migrations with safety protocol (backup, migrate, log).</summary>
        [HttpPost("execute")]
        public async Task<IActionResult> ExecuteMigrations(CancellationToken ct)
        {
            // SECURITY: Block migration execution via HTTP in non-development environments.
            // Production migrations should only be applied via CLI or startup configuration.
            if (!_env.IsDevelopment())
                return StatusCode(403, new { success = false, errors = new[] { "تنفيذ الترحيل عبر HTTP مقيد ببيئة التطوير فقط." } });

            var result = await _migrationService.ExecuteMigrationsAsync(ct);
            return FromResult(result);
        }
    }
}
