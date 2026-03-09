using MarcoERP.Application.Common;
using MarcoERP.Application.DTOs.Sales;
using MarcoERP.Application.Interfaces.Sales;
using Microsoft.AspNetCore.Mvc;

namespace MarcoERP.API.Controllers.Sales;

[Route("api/sales-invoices")]
public class SalesInvoicesController : ApiControllerBase
{
    private readonly ISalesInvoiceService _salesInvoiceService;

    public SalesInvoicesController(ISalesInvoiceService salesInvoiceService)
    {
        _salesInvoiceService = salesInvoiceService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var result = await _salesInvoiceService.GetAllAsync(ct);
        return FromResult(result);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken ct)
    {
        var result = await _salesInvoiceService.GetByIdAsync(id, ct);
        return FromResult(result);
    }

    [HttpGet("next-number")]
    public async Task<IActionResult> GetNextNumber(CancellationToken ct)
    {
        var result = await _salesInvoiceService.GetNextNumberAsync(ct);
        return FromResult(result);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateSalesInvoiceDto dto, CancellationToken ct)
    {
        var result = await _salesInvoiceService.CreateAsync(dto, ct);
        return FromResult(result);
    }

    [HttpPut]
    public async Task<IActionResult> Update([FromBody] UpdateSalesInvoiceDto dto, CancellationToken ct)
    {
        var result = await _salesInvoiceService.UpdateAsync(dto, ct);
        return FromResult(result);
    }

    [HttpPatch("{id:int}/post")]
    public async Task<IActionResult> Post(int id, CancellationToken ct)
    {
        var result = await _salesInvoiceService.PostAsync(id, ct);
        return FromResult(result);
    }

    [HttpPatch("{id:int}/cancel")]
    public async Task<IActionResult> Cancel(int id, CancellationToken ct)
    {
        var result = await _salesInvoiceService.CancelAsync(id, ct);
        return FromResult(result);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var result = await _salesInvoiceService.DeleteDraftAsync(id, ct);
        return FromResult(result);
    }
}
