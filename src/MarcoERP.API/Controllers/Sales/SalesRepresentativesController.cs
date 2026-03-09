using System.ComponentModel.DataAnnotations;
using MarcoERP.Application.DTOs.Sales;
using MarcoERP.Application.Interfaces.Sales;
using Microsoft.AspNetCore.Mvc;

namespace MarcoERP.API.Controllers.Sales;

public class SalesRepresentativesController : ApiControllerBase
{
    private readonly ISalesRepresentativeService _service;

    public SalesRepresentativesController(ISalesRepresentativeService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct)
        => FromResult(await _service.GetAllAsync(ct));

    [HttpGet("active")]
    public async Task<IActionResult> GetActive(CancellationToken ct)
        => FromResult(await _service.GetActiveAsync(ct));

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken ct)
        => FromResult(await _service.GetByIdAsync(id, ct));

    [HttpGet("search")]
    public async Task<IActionResult> Search([FromQuery, StringLength(200)] string term, CancellationToken ct)
        => FromResult(await _service.SearchAsync(term ?? string.Empty, ct));

    [HttpGet("next-code")]
    public async Task<IActionResult> GetNextCode(CancellationToken ct)
        => FromResult(await _service.GetNextCodeAsync(ct));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateSalesRepresentativeDto dto, CancellationToken ct)
        => FromResult(await _service.CreateAsync(dto, ct));

    [HttpPut]
    public async Task<IActionResult> Update([FromBody] UpdateSalesRepresentativeDto dto, CancellationToken ct)
        => FromResult(await _service.UpdateAsync(dto, ct));

    [HttpPatch("{id:int}/activate")]
    public async Task<IActionResult> Activate(int id, CancellationToken ct)
        => FromResult(await _service.ActivateAsync(id, ct));

    [HttpPatch("{id:int}/deactivate")]
    public async Task<IActionResult> Deactivate(int id, CancellationToken ct)
        => FromResult(await _service.DeactivateAsync(id, ct));

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
        => FromResult(await _service.DeleteAsync(id, ct));
}
