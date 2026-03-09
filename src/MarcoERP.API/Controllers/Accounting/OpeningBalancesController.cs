using MarcoERP.Application.DTOs.Accounting;
using MarcoERP.Application.Interfaces.Accounting;
using Microsoft.AspNetCore.Mvc;

namespace MarcoERP.API.Controllers.Accounting;

[Route("api/opening-balances")]
public class OpeningBalancesController : ApiControllerBase
{
    private readonly IOpeningBalanceService _service;

    public OpeningBalancesController(IOpeningBalanceService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct)
        => FromResult(await _service.GetAllAsync(ct));

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken ct)
        => FromResult(await _service.GetByIdAsync(id, ct));

    [HttpGet("fiscal-year/{fiscalYearId:int}")]
    public async Task<IActionResult> GetByFiscalYear(int fiscalYearId, CancellationToken ct)
        => FromResult(await _service.GetByFiscalYearAsync(fiscalYearId, ct));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateOpeningBalanceDto dto, CancellationToken ct)
        => FromResult(await _service.CreateAsync(dto, ct));

    [HttpPut]
    public async Task<IActionResult> Update([FromBody] UpdateOpeningBalanceDto dto, CancellationToken ct)
        => FromResult(await _service.UpdateAsync(dto, ct));

    [HttpPatch("{id:int}/post")]
    public async Task<IActionResult> Post(int id, CancellationToken ct)
        => FromResult(await _service.PostAsync(id, ct));

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
        => FromResult(await _service.DeleteDraftAsync(id, ct));
}
