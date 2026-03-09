using System.ComponentModel.DataAnnotations;
using MarcoERP.Application.Common;
using MarcoERP.Application.DTOs.Inventory;
using MarcoERP.Application.Interfaces.Inventory;
using Microsoft.AspNetCore.Mvc;

namespace MarcoERP.API.Controllers.Inventory;

[Route("api/products")]
public class ProductsController : ApiControllerBase
{
    private readonly IProductService _productService;

    public ProductsController(IProductService productService)
    {
        _productService = productService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var result = await _productService.GetAllAsync(ct);
        return FromResult(result);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken ct)
    {
        var result = await _productService.GetByIdAsync(id, ct);
        return FromResult(result);
    }

    [HttpGet("code/{code}")]
    public async Task<IActionResult> GetByCode(string code, CancellationToken ct)
    {
        var result = await _productService.GetByCodeAsync(code, ct);
        return FromResult(result);
    }

    [HttpGet("search")]
    public async Task<IActionResult> Search([FromQuery, StringLength(200)] string term, CancellationToken ct)
    {
        var result = await _productService.SearchAsync(term ?? string.Empty, ct);
        return FromResult(result);
    }

    [HttpGet("next-code")]
    public async Task<IActionResult> GetNextCode(CancellationToken ct)
    {
        var result = await _productService.GetNextCodeAsync(ct);
        return FromResult(result);
    }

    [HttpGet("category/{categoryId:int}")]
    public async Task<IActionResult> GetByCategory(int categoryId, CancellationToken ct)
    {
        var result = await _productService.GetByCategoryAsync(categoryId, ct);
        return FromResult(result);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateProductDto dto, CancellationToken ct)
    {
        var result = await _productService.CreateAsync(dto, ct);
        return FromResult(result);
    }

    [HttpPut]
    public async Task<IActionResult> Update([FromBody] UpdateProductDto dto, CancellationToken ct)
    {
        var result = await _productService.UpdateAsync(dto, ct);
        return FromResult(result);
    }

    [HttpPatch("{id:int}/activate")]
    public async Task<IActionResult> Activate(int id, CancellationToken ct)
    {
        var result = await _productService.ActivateAsync(id, ct);
        return FromResult(result);
    }

    [HttpPatch("{id:int}/deactivate")]
    public async Task<IActionResult> Deactivate(int id, CancellationToken ct)
    {
        var result = await _productService.DeactivateAsync(id, ct);
        return FromResult(result);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var result = await _productService.DeleteAsync(id, ct);
        return FromResult(result);
    }
}
