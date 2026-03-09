using MarcoERP.Application.Common;
using MarcoERP.Application.DTOs.Inventory;
using MarcoERP.Application.Interfaces.Inventory;
using Microsoft.AspNetCore.Mvc;

namespace MarcoERP.API.Controllers.Inventory;

[Route("api/warehouses")]
public class WarehousesController : ApiControllerBase
{
    private readonly IWarehouseService _warehouseService;

    public WarehousesController(IWarehouseService warehouseService)
    {
        _warehouseService = warehouseService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var result = await _warehouseService.GetAllAsync(ct);
        return FromResult(result);
    }

    [HttpGet("active")]
    public async Task<IActionResult> GetActive(CancellationToken ct)
    {
        var result = await _warehouseService.GetActiveAsync(ct);
        return FromResult(result);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken ct)
    {
        var result = await _warehouseService.GetByIdAsync(id, ct);
        return FromResult(result);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateWarehouseDto dto, CancellationToken ct)
    {
        var result = await _warehouseService.CreateAsync(dto, ct);
        return FromResult(result);
    }

    [HttpPut]
    public async Task<IActionResult> Update([FromBody] UpdateWarehouseDto dto, CancellationToken ct)
    {
        var result = await _warehouseService.UpdateAsync(dto, ct);
        return FromResult(result);
    }

    [HttpPatch("{id:int}/set-default")]
    public async Task<IActionResult> SetDefault(int id, CancellationToken ct)
    {
        var result = await _warehouseService.SetDefaultAsync(id, ct);
        return FromResult(result);
    }

    [HttpPatch("{id:int}/activate")]
    public async Task<IActionResult> Activate(int id, CancellationToken ct)
    {
        var result = await _warehouseService.ActivateAsync(id, ct);
        return FromResult(result);
    }

    [HttpPatch("{id:int}/deactivate")]
    public async Task<IActionResult> Deactivate(int id, CancellationToken ct)
    {
        var result = await _warehouseService.DeactivateAsync(id, ct);
        return FromResult(result);
    }

    [HttpGet("{id:int}/stock")]
    public async Task<IActionResult> GetStockByWarehouse(int id, CancellationToken ct)
    {
        var result = await _warehouseService.GetStockByWarehouseAsync(id, ct);
        return FromResult(result);
    }

    [HttpGet("product/{productId:int}/stock")]
    public async Task<IActionResult> GetStockByProduct(int productId, CancellationToken ct)
    {
        var result = await _warehouseService.GetStockByProductAsync(productId, ct);
        return FromResult(result);
    }

    [HttpGet("below-minimum")]
    public async Task<IActionResult> GetBelowMinimumStock(CancellationToken ct)
    {
        var result = await _warehouseService.GetBelowMinimumStockAsync(ct);
        return FromResult(result);
    }
}
