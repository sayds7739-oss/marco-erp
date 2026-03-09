using System.ComponentModel.DataAnnotations;
using MarcoERP.Application.DTOs.Sales;
using MarcoERP.Application.Interfaces.Sales;
using Microsoft.AspNetCore.Mvc;

namespace MarcoERP.API.Controllers.Sales;

[Route("api/pos")]
public class PosController : ApiControllerBase
{
    private readonly IPosService _service;

    public PosController(IPosService service)
    {
        _service = service;
    }

    // ── Session Management ──────────────────────────────────

    [HttpPost("sessions/open")]
    public async Task<IActionResult> OpenSession([FromBody] OpenPosSessionDto dto, CancellationToken ct)
        => FromResult(await _service.OpenSessionAsync(dto, ct));

    [HttpGet("sessions/current")]
    public async Task<IActionResult> GetCurrentSession(CancellationToken ct)
        => FromResult(await _service.GetCurrentSessionAsync(ct));

    [HttpPost("sessions/close")]
    public async Task<IActionResult> CloseSession([FromBody] ClosePosSessionDto dto, CancellationToken ct)
        => FromResult(await _service.CloseSessionAsync(dto, ct));

    [HttpGet("sessions/{id:int}")]
    public async Task<IActionResult> GetSessionById(int id, CancellationToken ct)
        => FromResult(await _service.GetSessionByIdAsync(id, ct));

    [HttpGet("sessions")]
    public async Task<IActionResult> GetAllSessions(CancellationToken ct)
        => FromResult(await _service.GetAllSessionsAsync(ct));

    // ── Product Lookup ──────────────────────────────────────

    [HttpGet("products/cache")]
    public async Task<IActionResult> LoadProductCache(CancellationToken ct)
        => FromResult(await _service.LoadProductCacheAsync(ct));

    [HttpGet("products/barcode/{barcode}")]
    public async Task<IActionResult> FindByBarcode(string barcode, CancellationToken ct)
        => FromResult(await _service.FindByBarcodeAsync(barcode, ct));

    [HttpGet("products/search")]
    public async Task<IActionResult> SearchProducts([FromQuery, StringLength(200)] string term, CancellationToken ct)
        => FromResult(await _service.SearchProductsAsync(term ?? string.Empty, ct));

    [HttpGet("products/stock")]
    public async Task<IActionResult> GetAvailableStock([FromQuery] int productId, [FromQuery] int warehouseId, CancellationToken ct)
        => FromResult(await _service.GetAvailableStockAsync(productId, warehouseId, ct));

    // ── Sale Completion ─────────────────────────────────────

    [HttpPost("sales/complete")]
    public async Task<IActionResult> CompleteSale([FromBody] CompletePoseSaleDto dto, CancellationToken ct)
        => FromResult(await _service.CompleteSaleAsync(dto, ct));

    [HttpPost("sales/{salesInvoiceId:int}/cancel")]
    public async Task<IActionResult> CancelSale(int salesInvoiceId, [FromQuery] int sessionId, CancellationToken ct)
        => FromResult(await _service.CancelSaleAsync(salesInvoiceId, sessionId, ct));

    // ── Reports ─────────────────────────────────────────────

    [HttpGet("reports/daily")]
    public async Task<IActionResult> GetDailyReport([FromQuery] DateTime date, CancellationToken ct)
        => FromResult(await _service.GetDailyReportAsync(date, ct));

    [HttpGet("reports/session/{sessionId:int}")]
    public async Task<IActionResult> GetSessionReport(int sessionId, CancellationToken ct)
        => FromResult(await _service.GetSessionReportAsync(sessionId, ct));

    [HttpGet("reports/profit")]
    public async Task<IActionResult> GetProfitReport([FromQuery] DateTime fromDate, [FromQuery] DateTime toDate, CancellationToken ct)
    {
        var err = ValidateDateRange(fromDate, toDate);
        if (err is not null) return err;
        return FromResult(await _service.GetProfitReportAsync(fromDate, toDate, ct));
    }

    [HttpGet("reports/cash-variance")]
    public async Task<IActionResult> GetCashVarianceReport([FromQuery] DateTime fromDate, [FromQuery] DateTime toDate, CancellationToken ct)
    {
        var err = ValidateDateRange(fromDate, toDate);
        if (err is not null) return err;
        return FromResult(await _service.GetCashVarianceReportAsync(fromDate, toDate, ct));
    }
}
