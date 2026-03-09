using MarcoERP.Application.DTOs.Treasury;
using MarcoERP.Application.Interfaces.Treasury;
using Microsoft.AspNetCore.Mvc;

namespace MarcoERP.API.Controllers.Treasury;

public class CashboxesController : ApiControllerBase
{
    private readonly ICashboxService _service;

    public CashboxesController(ICashboxService service)
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

    [HttpGet("next-code")]
    public async Task<IActionResult> GetNextCode(CancellationToken ct)
        => FromResult(await _service.GetNextCodePreviewAsync(ct));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateCashboxDto dto, CancellationToken ct)
        => FromResult(await _service.CreateAsync(dto, ct));

    [HttpPut]
    public async Task<IActionResult> Update([FromBody] UpdateCashboxDto dto, CancellationToken ct)
        => FromResult(await _service.UpdateAsync(dto, ct));

    [HttpPatch("{id:int}/set-default")]
    public async Task<IActionResult> SetDefault(int id, CancellationToken ct)
        => FromResult(await _service.SetDefaultAsync(id, ct));

    [HttpPatch("{id:int}/activate")]
    public async Task<IActionResult> Activate(int id, CancellationToken ct)
        => FromResult(await _service.ActivateAsync(id, ct));

    [HttpPatch("{id:int}/deactivate")]
    public async Task<IActionResult> Deactivate(int id, CancellationToken ct)
        => FromResult(await _service.DeactivateAsync(id, ct));
}
