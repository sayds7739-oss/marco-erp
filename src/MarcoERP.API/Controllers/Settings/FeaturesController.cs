using MarcoERP.Application.DTOs.Settings;
using MarcoERP.Application.Interfaces.Settings;
using Microsoft.AspNetCore.Mvc;

namespace MarcoERP.API.Controllers.Settings
{
    /// <summary>
    /// API controller for feature governance management.
    /// </summary>
    public class FeaturesController : ApiControllerBase
    {
        private readonly IFeatureService _featureService;

        public FeaturesController(IFeatureService featureService)
        {
            _featureService = featureService;
        }

        // ── Queries ─────────────────────────────────────────────

        /// <summary>Gets all features.</summary>
        [HttpGet]
        public async Task<IActionResult> GetAll(CancellationToken ct)
        {
            var result = await _featureService.GetAllAsync(ct);
            return FromResult(result);
        }

        /// <summary>Gets a feature by its unique key.</summary>
        [HttpGet("{key}")]
        public async Task<IActionResult> GetByKey(string key, CancellationToken ct)
        {
            var result = await _featureService.GetByKeyAsync(key, ct);
            return FromResult(result);
        }

        /// <summary>Checks if a feature is enabled.</summary>
        [HttpGet("{key}/enabled")]
        public async Task<IActionResult> IsEnabled(string key, CancellationToken ct)
        {
            var result = await _featureService.IsEnabledAsync(key, ct);
            return FromResult(result);
        }

        // ── Commands ────────────────────────────────────────────

        /// <summary>Toggles a feature on/off.</summary>
        [HttpPost("toggle")]
        public async Task<IActionResult> Toggle([FromBody] ToggleFeatureDto dto, CancellationToken ct)
        {
            var result = await _featureService.ToggleAsync(dto, ct);
            return FromResult(result);
        }
    }
}
