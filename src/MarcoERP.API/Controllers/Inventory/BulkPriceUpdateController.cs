using MarcoERP.Application.DTOs.Inventory;
using MarcoERP.Application.Interfaces.Inventory;
using Microsoft.AspNetCore.Mvc;

namespace MarcoERP.API.Controllers.Inventory;

[Route("api/bulk-price-update")]
public class BulkPriceUpdateController : ApiControllerBase
{
    private readonly IBulkPriceUpdateService _service;

    public BulkPriceUpdateController(IBulkPriceUpdateService service)
    {
        _service = service;
    }

    [HttpPost("preview")]
    public async Task<IActionResult> Preview([FromBody] BulkPriceUpdateRequestDto dto, CancellationToken ct)
        => FromResult(await _service.PreviewAsync(dto, ct));

    [HttpPost("apply")]
    public async Task<IActionResult> Apply([FromBody] BulkPriceUpdateRequestDto dto, CancellationToken ct)
        => FromResult(await _service.ApplyAsync(dto, ct));
}
