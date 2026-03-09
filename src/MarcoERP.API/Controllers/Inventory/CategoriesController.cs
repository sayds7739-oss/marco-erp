using MarcoERP.Application.Common;
using MarcoERP.Application.DTOs.Inventory;
using MarcoERP.Application.Interfaces.Inventory;
using Microsoft.AspNetCore.Mvc;

namespace MarcoERP.API.Controllers.Inventory;

[Route("api/categories")]
public class CategoriesController : ApiControllerBase
{
    private readonly ICategoryService _categoryService;

    public CategoriesController(ICategoryService categoryService)
    {
        _categoryService = categoryService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var result = await _categoryService.GetAllAsync(ct);
        return FromResult(result);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken ct)
    {
        var result = await _categoryService.GetByIdAsync(id, ct);
        return FromResult(result);
    }

    [HttpGet("root")]
    public async Task<IActionResult> GetRootCategories(CancellationToken ct)
    {
        var result = await _categoryService.GetRootCategoriesAsync(ct);
        return FromResult(result);
    }

    [HttpGet("{parentId:int}/children")]
    public async Task<IActionResult> GetChildren(int parentId, CancellationToken ct)
    {
        var result = await _categoryService.GetChildrenAsync(parentId, ct);
        return FromResult(result);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateCategoryDto dto, CancellationToken ct)
    {
        var result = await _categoryService.CreateAsync(dto, ct);
        return FromResult(result);
    }

    [HttpPut]
    public async Task<IActionResult> Update([FromBody] UpdateCategoryDto dto, CancellationToken ct)
    {
        var result = await _categoryService.UpdateAsync(dto, ct);
        return FromResult(result);
    }

    [HttpPatch("{id:int}/activate")]
    public async Task<IActionResult> Activate(int id, CancellationToken ct)
    {
        var result = await _categoryService.ActivateAsync(id, ct);
        return FromResult(result);
    }

    [HttpPatch("{id:int}/deactivate")]
    public async Task<IActionResult> Deactivate(int id, CancellationToken ct)
    {
        var result = await _categoryService.DeactivateAsync(id, ct);
        return FromResult(result);
    }
}
