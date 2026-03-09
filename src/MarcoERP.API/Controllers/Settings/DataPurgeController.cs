using MarcoERP.Application.DTOs.Settings;
using MarcoERP.Application.Interfaces.Settings;
using Microsoft.AspNetCore.Mvc;

namespace MarcoERP.API.Controllers.Settings
{
    /// <summary>
    /// API controller for selective data cleanup (purge).
    /// </summary>
    public class DataPurgeController : ApiControllerBase
    {
        private readonly IDataPurgeService _dataPurgeService;

        public DataPurgeController(IDataPurgeService dataPurgeService)
        {
            _dataPurgeService = dataPurgeService;
        }

        // ── Commands ────────────────────────────────────────────

        /// <summary>Purges data according to selected keep options.</summary>
        [HttpPost]
        public async Task<IActionResult> Purge([FromBody] DataPurgeOptionsDto options, CancellationToken ct)
        {
            var result = await _dataPurgeService.PurgeAsync(options, ct);
            return FromResult(result);
        }
    }
}
