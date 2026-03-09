using MarcoERP.Application.DTOs.Sales;
using MarcoERP.Application.Interfaces.Sales;
using Microsoft.AspNetCore.Mvc;

namespace MarcoERP.API.Controllers.Sales;

public class SalesQuotationsController : ApiControllerBase
{
    private readonly ISalesQuotationService _service;

    public SalesQuotationsController(ISalesQuotationService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct)
        => FromResult(await _service.GetAllAsync(ct));

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken ct)
        => FromResult(await _service.GetByIdAsync(id, ct));

    [HttpGet("next-number")]
    public async Task<IActionResult> GetNextNumber(CancellationToken ct)
        => FromResult(await _service.GetNextNumberAsync(ct));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateSalesQuotationDto dto, CancellationToken ct)
        => FromResult(await _service.CreateAsync(dto, ct));

    [HttpPut]
    public async Task<IActionResult> Update([FromBody] UpdateSalesQuotationDto dto, CancellationToken ct)
        => FromResult(await _service.UpdateAsync(dto, ct));

    [HttpPatch("{id:int}/send")]
    public async Task<IActionResult> Send(int id, CancellationToken ct)
        => FromResult(await _service.SendAsync(id, ct));

    [HttpPatch("{id:int}/accept")]
    public async Task<IActionResult> Accept(int id, CancellationToken ct)
        => FromResult(await _service.AcceptAsync(id, ct));

    [HttpPatch("{id:int}/reject")]
    public async Task<IActionResult> Reject(int id, [FromQuery] string reason, CancellationToken ct)
        => FromResult(await _service.RejectAsync(id, reason, ct));

    [HttpPatch("{id:int}/cancel")]
    public async Task<IActionResult> Cancel(int id, CancellationToken ct)
        => FromResult(await _service.CancelAsync(id, ct));

    [HttpPost("{id:int}/convert-to-invoice")]
    public async Task<IActionResult> ConvertToInvoice(int id, CancellationToken ct)
        => FromResult(await _service.ConvertToInvoiceAsync(id, ct));

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
        => FromResult(await _service.DeleteDraftAsync(id, ct));
}
