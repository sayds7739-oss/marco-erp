using MarcoERP.Application.DTOs.Inventory;
using MarcoERP.Application.Interfaces.Inventory;
using Microsoft.AspNetCore.Mvc;

namespace MarcoERP.API.Controllers.Inventory;

[Route("api/inventory-adjustments")]
public class InventoryAdjustmentsController : ApiControllerBase
{
    private readonly IInventoryAdjustmentService _service;

    public InventoryAdjustmentsController(IInventoryAdjustmentService service)
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
    public async Task<IActionResult> Create([FromBody] CreateInventoryAdjustmentDto dto, CancellationToken ct)
        => FromResult(await _service.CreateAsync(dto, ct));

    [HttpPut]
    public async Task<IActionResult> Update([FromBody] UpdateInventoryAdjustmentDto dto, CancellationToken ct)
        => FromResult(await _service.UpdateAsync(dto, ct));

    [HttpPatch("{id:int}/post")]
    public async Task<IActionResult> Post(int id, CancellationToken ct)
        => FromResult(await _service.PostAsync(id, ct));

    [HttpPatch("{id:int}/cancel")]
    public async Task<IActionResult> Cancel(int id, CancellationToken ct)
        => FromResult(await _service.CancelAsync(id, ct));

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
        => FromResult(await _service.DeleteDraftAsync(id, ct));
}
