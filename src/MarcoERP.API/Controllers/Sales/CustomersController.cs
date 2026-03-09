using System.ComponentModel.DataAnnotations;
using MarcoERP.Application.Common;
using MarcoERP.Application.DTOs.Sales;
using MarcoERP.Application.Interfaces.Sales;
using Microsoft.AspNetCore.Mvc;

namespace MarcoERP.API.Controllers.Sales;

[Route("api/customers")]
public class CustomersController : ApiControllerBase
{
    private readonly ICustomerService _customerService;

    public CustomersController(ICustomerService customerService)
    {
        _customerService = customerService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var result = await _customerService.GetAllAsync(ct);
        return FromResult(result);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken ct)
    {
        var result = await _customerService.GetByIdAsync(id, ct);
        return FromResult(result);
    }

    [HttpGet("search")]
    public async Task<IActionResult> Search([FromQuery, StringLength(200)] string term, CancellationToken ct)
    {
        var result = await _customerService.SearchAsync(term ?? string.Empty, ct);
        return FromResult(result);
    }

    [HttpGet("next-code")]
    public async Task<IActionResult> GetNextCode(CancellationToken ct)
    {
        var result = await _customerService.GetNextCodeAsync(ct);
        return FromResult(result);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateCustomerDto dto, CancellationToken ct)
    {
        var result = await _customerService.CreateAsync(dto, ct);
        return FromResult(result);
    }

    [HttpPut]
    public async Task<IActionResult> Update([FromBody] UpdateCustomerDto dto, CancellationToken ct)
    {
        var result = await _customerService.UpdateAsync(dto, ct);
        return FromResult(result);
    }

    [HttpPatch("{id:int}/activate")]
    public async Task<IActionResult> Activate(int id, CancellationToken ct)
    {
        var result = await _customerService.ActivateAsync(id, ct);
        return FromResult(result);
    }

    [HttpPatch("{id:int}/deactivate")]
    public async Task<IActionResult> Deactivate(int id, CancellationToken ct)
    {
        var result = await _customerService.DeactivateAsync(id, ct);
        return FromResult(result);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var result = await _customerService.DeleteAsync(id, ct);
        return FromResult(result);
    }
}
