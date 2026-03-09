using MarcoERP.Application.Common;
using MarcoERP.Application.DTOs.Sales;
using MarcoERP.Application.Interfaces.Sales;
using Microsoft.AspNetCore.Mvc;

namespace MarcoERP.API.Controllers.Sales;

[Route("api/sales-returns")]
public class SalesReturnsController : ApiControllerBase
{
    private readonly ISalesReturnService _salesReturnService;

    public SalesReturnsController(ISalesReturnService salesReturnService)
    {
        _salesReturnService = salesReturnService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var result = await _salesReturnService.GetAllAsync(ct);
        return FromResult(result);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken ct)
    {
        var result = await _salesReturnService.GetByIdAsync(id, ct);
        return FromResult(result);
    }

    [HttpGet("next-number")]
    public async Task<IActionResult> GetNextNumber(CancellationToken ct)
    {
        var result = await _salesReturnService.GetNextNumberAsync(ct);
        return FromResult(result);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateSalesReturnDto dto, CancellationToken ct)
    {
        var result = await _salesReturnService.CreateAsync(dto, ct);
        return FromResult(result);
    }

    [HttpPut]
    public async Task<IActionResult> Update([FromBody] UpdateSalesReturnDto dto, CancellationToken ct)
    {
        var result = await _salesReturnService.UpdateAsync(dto, ct);
        return FromResult(result);
    }

    [HttpPatch("{id:int}/post")]
    public async Task<IActionResult> Post(int id, CancellationToken ct)
    {
        var result = await _salesReturnService.PostAsync(id, ct);
        return FromResult(result);
    }

    [HttpPatch("{id:int}/cancel")]
    public async Task<IActionResult> Cancel(int id, CancellationToken ct)
    {
        var result = await _salesReturnService.CancelAsync(id, ct);
        return FromResult(result);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var result = await _salesReturnService.DeleteDraftAsync(id, ct);
        return FromResult(result);
    }
}
