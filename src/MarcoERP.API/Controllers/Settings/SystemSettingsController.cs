using MarcoERP.Application.DTOs.Settings;
using MarcoERP.Application.Interfaces.Settings;
using Microsoft.AspNetCore.Mvc;

namespace MarcoERP.API.Controllers.Settings
{
    /// <summary>
    /// API controller for system settings management.
    /// </summary>
    public class SystemSettingsController : ApiControllerBase
    {
        private readonly ISystemSettingsService _systemSettingsService;

        public SystemSettingsController(ISystemSettingsService systemSettingsService)
        {
            _systemSettingsService = systemSettingsService;
        }

        // ── Queries ─────────────────────────────────────────────

        /// <summary>Gets all settings.</summary>
        [HttpGet]
        public async Task<IActionResult> GetAll(CancellationToken ct)
        {
            var result = await _systemSettingsService.GetAllAsync(ct);
            return FromResult(result);
        }

        /// <summary>Gets all settings organized by group.</summary>
        [HttpGet("grouped")]
        public async Task<IActionResult> GetAllGrouped(CancellationToken ct)
        {
            var result = await _systemSettingsService.GetAllGroupedAsync(ct);
            return FromResult(result);
        }

        /// <summary>Gets a single setting by key.</summary>
        [HttpGet("key/{key}")]
        public async Task<IActionResult> GetByKey(string key, CancellationToken ct)
        {
            var result = await _systemSettingsService.GetByKeyAsync(key, ct);
            return FromResult(result);
        }

        // ── Commands ────────────────────────────────────────────

        /// <summary>Updates a single setting value.</summary>
        [HttpPut]
        public async Task<IActionResult> Update([FromBody] UpdateSystemSettingDto dto, CancellationToken ct)
        {
            var result = await _systemSettingsService.UpdateAsync(dto, ct);
            return FromResult(result);
        }

        /// <summary>Batch-updates multiple settings.</summary>
        [HttpPut("batch")]
        public async Task<IActionResult> UpdateBatch(
            [FromBody] IEnumerable<UpdateSystemSettingDto> dtos,
            CancellationToken ct)
        {
            var list = dtos?.ToList();
            if (list is null || list.Count == 0)
                return BadRequest(new { success = false, message = "لم يتم إرسال أي إعدادات" });
            if (list.Count > 100)
                return BadRequest(new { success = false, message = "الحد الأقصى 100 إعداد في الدفعة الواحدة" });

            var result = await _systemSettingsService.UpdateBatchAsync(list, ct);
            return FromResult(result);
        }
    }
}
