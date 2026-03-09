using MarcoERP.Application.Interfaces.Settings;
using Microsoft.AspNetCore.Mvc;

namespace MarcoERP.API.Controllers.Settings
{
    /// <summary>
    /// API controller for system version tracking.
    /// </summary>
    public class VersionController : ApiControllerBase
    {
        private readonly IVersionService _versionService;

        public VersionController(IVersionService versionService)
        {
            _versionService = versionService;
        }

        // ── Queries ─────────────────────────────────────────────

        /// <summary>Gets the current (latest) registered version number.</summary>
        [HttpGet("current")]
        public async Task<IActionResult> GetCurrentVersion(CancellationToken ct)
        {
            var version = await _versionService.GetCurrentVersionAsync(ct);
            return Ok(new { success = true, data = version });
        }

        // ── Commands ────────────────────────────────────────────

        /// <summary>Registers a new system version.</summary>
        [HttpPost]
        public async Task<IActionResult> RegisterNewVersion(
            [FromQuery] string version,
            [FromQuery] string description,
            CancellationToken ct)
        {
            var result = await _versionService.RegisterNewVersionAsync(version, description, ct);
            return FromResult(result);
        }
    }
}
