using MarcoERP.Application.DTOs.Treasury;
using MarcoERP.Application.Interfaces.Treasury;
using Microsoft.AspNetCore.Mvc;

namespace MarcoERP.API.Controllers.Treasury;

[Route("api/bank-reconciliations")]
public class BankReconciliationsController : ApiControllerBase
{
    private readonly IBankReconciliationService _service;

    public BankReconciliationsController(IBankReconciliationService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct)
        => FromResult(await _service.GetAllAsync(ct));

    [HttpGet("bank-account/{bankAccountId:int}")]
    public async Task<IActionResult> GetByBankAccount(int bankAccountId, CancellationToken ct)
        => FromResult(await _service.GetByBankAccountAsync(bankAccountId, ct));

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken ct)
        => FromResult(await _service.GetByIdAsync(id, ct));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateBankReconciliationDto dto, CancellationToken ct)
        => FromResult(await _service.CreateAsync(dto, ct));

    [HttpPut]
    public async Task<IActionResult> Update([FromBody] UpdateBankReconciliationDto dto, CancellationToken ct)
        => FromResult(await _service.UpdateAsync(dto, ct));

    [HttpPost("{id:int}/items")]
    public async Task<IActionResult> AddItem([FromBody] CreateBankReconciliationItemDto dto, CancellationToken ct)
        => FromResult(await _service.AddItemAsync(dto, ct));

    [HttpDelete("{reconciliationId:int}/items/{itemId:int}")]
    public async Task<IActionResult> RemoveItem(int reconciliationId, int itemId, CancellationToken ct)
        => FromResult(await _service.RemoveItemAsync(reconciliationId, itemId, ct));

    [HttpPatch("{id:int}/complete")]
    public async Task<IActionResult> Complete(int id, CancellationToken ct)
        => FromResult(await _service.CompleteAsync(id, ct));

    [HttpPatch("{id:int}/reopen")]
    public async Task<IActionResult> Reopen(int id, CancellationToken ct)
        => FromResult(await _service.ReopenAsync(id, ct));

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
        => FromResult(await _service.DeleteAsync(id, ct));
}
