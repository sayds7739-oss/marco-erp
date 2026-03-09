using MarcoERP.Application.Interfaces.Settings;
using Microsoft.AspNetCore.Mvc;

namespace MarcoERP.API.Controllers.Settings
{
    /// <summary>
    /// API controller for system profile management.
    /// </summary>
    public class ProfileController : ApiControllerBase
    {
        private readonly IProfileService _profileService;

        public ProfileController(IProfileService profileService)
        {
            _profileService = profileService;
        }

        // ── Queries ─────────────────────────────────────────────

        /// <summary>Gets all available profiles.</summary>
        [HttpGet]
        public async Task<IActionResult> GetAllProfiles(CancellationToken ct)
        {
            var result = await _profileService.GetAllProfilesAsync(ct);
            return FromResult(result);
        }

        /// <summary>Gets the currently active profile name.</summary>
        [HttpGet("current")]
        public async Task<IActionResult> GetCurrentProfile(CancellationToken ct)
        {
            var result = await _profileService.GetCurrentProfileAsync(ct);
            return FromResult(result);
        }

        /// <summary>Gets the feature keys that are enabled for a given profile.</summary>
        [HttpGet("{profileName}/features")]
        public async Task<IActionResult> GetProfileFeatures(string profileName, CancellationToken ct)
        {
            var result = await _profileService.GetProfileFeaturesAsync(profileName, ct);
            return FromResult(result);
        }

        // ── Commands ────────────────────────────────────────────

        /// <summary>Applies a profile by name (enables/disables features accordingly).</summary>
        [HttpPost("{profileName}/apply")]
        public async Task<IActionResult> ApplyProfile(string profileName, CancellationToken ct)
        {
            var result = await _profileService.ApplyProfileAsync(profileName, ct);
            return FromResult(result);
        }
    }
}
