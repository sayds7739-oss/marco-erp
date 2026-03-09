using MarcoERP.Application.DTOs.Sales;
using MarcoERP.Application.Interfaces.Sales;
using Microsoft.AspNetCore.Mvc;

namespace MarcoERP.API.Controllers.Sales;

[Route("api/price-lists")]
public class PriceListsController : ApiControllerBase
{
    private readonly IPriceListService _service;

    public PriceListsController(IPriceListService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct)
        => FromResult(await _service.GetAllAsync(ct));

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken ct)
        => FromResult(await _service.GetByIdAsync(id, ct));

    [HttpGet("next-code")]
    public async Task<IActionResult> GetNextCode(CancellationToken ct)
        => FromResult(await _service.GetNextCodeAsync(ct));

    [HttpGet("best-price")]
    public async Task<IActionResult> GetBestPriceForCustomer(
        [FromQuery] int customerId,
        [FromQuery] int productId,
        [FromQuery] decimal quantity,
        CancellationToken ct)
        => FromResult(await _service.GetBestPriceForCustomerAsync(customerId, productId, quantity, ct));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreatePriceListDto dto, CancellationToken ct)
        => FromResult(await _service.CreateAsync(dto, ct));

    [HttpPut]
    public async Task<IActionResult> Update([FromBody] UpdatePriceListDto dto, CancellationToken ct)
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
