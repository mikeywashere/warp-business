using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WarpBusiness.Plugin.Catalog.Services;
using WarpBusiness.Shared.Catalog;

namespace WarpBusiness.Plugin.Catalog.Controllers;

[Authorize(Policy = "RequireActiveTenant")]
[ApiController]
[Route("api/catalog/[controller]")]
public class ProductsController : ControllerBase
{
    private readonly IProductService _products;
    private readonly IProductImageService _images;
    private readonly IProductIngredientService _ingredients;
    private readonly IProductVariantService _variants;

    public ProductsController(
        IProductService products,
        IProductImageService images,
        IProductIngredientService ingredients,
        IProductVariantService variants)
    {
        _products = products;
        _images = images;
        _ingredients = ingredients;
        _variants = variants;
    }

    // --- Products ---

    [HttpGet]
    public async Task<IActionResult> GetProducts(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        [FromQuery] Guid? categoryId = null,
        [FromQuery] string? status = null,
        CancellationToken ct = default)
    {
        return Ok(await _products.GetProductsAsync(page, pageSize, search, categoryId, status, ct));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetProduct(Guid id, CancellationToken ct = default)
    {
        var product = await _products.GetProductDetailAsync(id, ct);
        return product is null ? NotFound() : Ok(product);
    }

    [HttpPost]
    public async Task<IActionResult> CreateProduct(CreateProductRequest request, CancellationToken ct = default)
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")?.Value ?? string.Empty;
        var product = await _products.CreateProductAsync(request, userId, ct);
        return CreatedAtAction(nameof(GetProduct), new { id = product.Id }, product);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateProduct(Guid id, UpdateProductRequest request, CancellationToken ct = default)
    {
        var product = await _products.UpdateProductAsync(id, request, ct);
        return product is null ? NotFound() : Ok(product);
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteProduct(Guid id, CancellationToken ct = default)
    {
        var result = await _products.DeleteProductAsync(id, ct);
        return result switch
        {
            DeleteProductResult.Deleted => NoContent(),
            DeleteProductResult.NotFound => NotFound(),
            _ => StatusCode(500)
        };
    }

    // --- Images ---

    [HttpGet("{productId:guid}/images")]
    public async Task<IActionResult> GetImages(Guid productId, CancellationToken ct = default)
    {
        return Ok(await _images.GetImagesAsync(productId, ct));
    }

    [HttpPost("{productId:guid}/images")]
    public async Task<IActionResult> AddImage(Guid productId, CreateProductImageRequest request, CancellationToken ct = default)
    {
        var image = await _images.AddImageAsync(productId, request, ct);
        return Created($"api/catalog/products/{productId}/images/{image.Id}", image);
    }

    [HttpDelete("{productId:guid}/images/{imageId:guid}")]
    public async Task<IActionResult> DeleteImage(Guid productId, Guid imageId, CancellationToken ct = default)
    {
        var result = await _images.DeleteImageAsync(productId, imageId, ct);
        return result ? NoContent() : NotFound();
    }

    // --- Ingredients ---

    [HttpGet("{productId:guid}/ingredients")]
    public async Task<IActionResult> GetIngredients(Guid productId, CancellationToken ct = default)
    {
        return Ok(await _ingredients.GetIngredientsAsync(productId, ct));
    }

    [HttpPost("{productId:guid}/ingredients")]
    public async Task<IActionResult> AddIngredient(Guid productId, CreateProductIngredientRequest request, CancellationToken ct = default)
    {
        var ingredient = await _ingredients.AddIngredientAsync(productId, request, ct);
        return Created($"api/catalog/products/{productId}/ingredients/{ingredient.Id}", ingredient);
    }

    [HttpPut("{productId:guid}/ingredients/{ingredientId:guid}")]
    public async Task<IActionResult> UpdateIngredient(Guid productId, Guid ingredientId, UpdateProductIngredientRequest request, CancellationToken ct = default)
    {
        var ingredient = await _ingredients.UpdateIngredientAsync(productId, ingredientId, request, ct);
        return ingredient is null ? NotFound() : Ok(ingredient);
    }

    [HttpDelete("{productId:guid}/ingredients/{ingredientId:guid}")]
    public async Task<IActionResult> DeleteIngredient(Guid productId, Guid ingredientId, CancellationToken ct = default)
    {
        var result = await _ingredients.DeleteIngredientAsync(productId, ingredientId, ct);
        return result ? NoContent() : NotFound();
    }

    // --- Options ---

    [HttpGet("{productId:guid}/options")]
    public async Task<IActionResult> GetOptions(Guid productId, CancellationToken ct = default)
    {
        return Ok(await _variants.GetOptionsAsync(productId, ct));
    }

    [HttpPost("{productId:guid}/options")]
    public async Task<IActionResult> AddOption(Guid productId, CreateProductOptionRequest request, CancellationToken ct = default)
    {
        var option = await _variants.AddOptionAsync(productId, request, ct);
        return Created($"api/catalog/products/{productId}/options/{option.Id}", option);
    }

    [HttpDelete("{productId:guid}/options/{optionId:guid}")]
    public async Task<IActionResult> DeleteOption(Guid productId, Guid optionId, CancellationToken ct = default)
    {
        var result = await _variants.DeleteOptionAsync(productId, optionId, ct);
        return result ? NoContent() : NotFound();
    }

    // --- Variants ---

    [HttpGet("{productId:guid}/variants")]
    public async Task<IActionResult> GetVariants(Guid productId, CancellationToken ct = default)
    {
        return Ok(await _variants.GetVariantsAsync(productId, ct));
    }

    [HttpPost("{productId:guid}/variants")]
    public async Task<IActionResult> CreateVariant(Guid productId, CreateProductVariantRequest request, CancellationToken ct = default)
    {
        var variant = await _variants.CreateVariantAsync(productId, request, ct);
        return Created($"api/catalog/products/{productId}/variants/{variant.Id}", variant);
    }

    [HttpPut("{productId:guid}/variants/{variantId:guid}")]
    public async Task<IActionResult> UpdateVariant(Guid productId, Guid variantId, UpdateProductVariantRequest request, CancellationToken ct = default)
    {
        var variant = await _variants.UpdateVariantAsync(productId, variantId, request, ct);
        return variant is null ? NotFound() : Ok(variant);
    }

    [HttpDelete("{productId:guid}/variants/{variantId:guid}")]
    public async Task<IActionResult> DeleteVariant(Guid productId, Guid variantId, CancellationToken ct = default)
    {
        var result = await _variants.DeleteVariantAsync(productId, variantId, ct);
        return result ? NoContent() : NotFound();
    }
}
