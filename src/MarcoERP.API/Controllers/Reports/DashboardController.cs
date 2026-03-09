using MarcoERP.Application.Interfaces.Reports;
using Microsoft.AspNetCore.Mvc;

namespace MarcoERP.API.Controllers.Reports
{
    /// <summary>
    /// Simplified dashboard endpoint for mobile app consumption.
    /// </summary>
    public class DashboardController : ApiControllerBase
    {
        private readonly IReportService _reportService;

        public DashboardController(IReportService reportService)
        {
            _reportService = reportService;
        }

        /// <summary>Gets the dashboard summary with key business metrics.</summary>
        [HttpGet]
        public async Task<IActionResult> GetDashboardSummary(CancellationToken ct)
        {
            var result = await _reportService.GetDashboardSummaryAsync(ct);
            return FromResult(result);
        }
    }
}
