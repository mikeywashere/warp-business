using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WarpBusiness.Plugin.Catalog.Services;
using WarpBusiness.Shared.Catalog;

namespace WarpBusiness.Plugin.Catalog.Controllers;

[Authorize(Policy = "RequireActiveTenant")]
[ApiController]
[Route("api/catalog/[controller]")]
public class CategoriesController : ControllerBase
{
    private readonly ICategoryService _categories;

    public CategoriesController(ICategoryService categories)
    {
        _categories = categories;
    }

    [HttpGet]
    public async Task<IActionResult> GetCategories(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        CancellationToken ct = default)
    {
        return Ok(await _categories.GetCategoriesAsync(page, pageSize, search, ct));
    }

    [HttpGet("all")]
    public async Task<IActionResult> GetAllCategories(CancellationToken ct = default)
    {
        return Ok(await _categories.GetAllCategoriesAsync(ct));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetCategory(Guid id, CancellationToken ct = default)
    {
        var category = await _categories.GetCategoryDetailAsync(id, ct);
        return category is null ? NotFound() : Ok(category);
    }

    [HttpPost]
    public async Task<IActionResult> CreateCategory(CreateCategoryRequest request, CancellationToken ct = default)
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")?.Value ?? string.Empty;
        var category = await _categories.CreateCategoryAsync(request, userId, ct);
        return CreatedAtAction(nameof(GetCategory), new { id = category.Id }, category);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateCategory(Guid id, UpdateCategoryRequest request, CancellationToken ct = default)
    {
        var category = await _categories.UpdateCategoryAsync(id, request, ct);
        return category is null ? NotFound() : Ok(category);
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteCategory(Guid id, CancellationToken ct = default)
    {
        var result = await _categories.DeleteCategoryAsync(id, ct);
        return result switch
        {
            DeleteCategoryResult.Deleted => NoContent(),
            DeleteCategoryResult.NotFound => NotFound(),
            DeleteCategoryResult.HasProducts => Conflict(new { message = "Category has products and cannot be deleted." }),
            DeleteCategoryResult.HasSubCategories => Conflict(new { message = "Category has sub-categories and cannot be deleted." }),
            _ => StatusCode(500)
        };
    }
}
