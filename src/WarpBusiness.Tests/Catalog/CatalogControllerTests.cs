using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using WarpBusiness.Shared.Auth;
using WarpBusiness.Shared.Catalog;
using WarpBusiness.Shared.Crm;
using WarpBusiness.Tests.Infrastructure;

namespace WarpBusiness.Tests.Catalog;

public class CatalogControllerTests : IClassFixture<WarpTestFactory>
{
    private readonly WarpTestFactory _factory;

    public CatalogControllerTests(WarpTestFactory factory)
    {
        _factory = factory;
    }

    private async Task<(HttpClient client, string token)> AuthenticateAsync(string? email = null)
    {
        var client = _factory.CreateClient();
        email ??= $"cat-{Guid.NewGuid()}@example.com";
        var token = await AuthHelper.RegisterAndGetTenantTokenAsync(_factory, client, email);
        client.SetBearerToken(token);
        return (client, token);
    }

    private async Task<(HttpClient client, string token)> AuthenticateAsAdminAsync()
    {
        var client = _factory.CreateClient();
        var email = $"cat-admin-{Guid.NewGuid()}@example.com";
        var token = await AuthHelper.RegisterAndGetTenantTokenAsync(_factory, client, email);
        client.SetBearerToken(token);
        await AuthHelper.PromoteToAdminAsync(_factory, email);
        var loginResponse = await client.PostAsJsonAsync("api/auth/login", new LoginRequest(email, "Test1234!"));
        var auth = await loginResponse.Content.ReadFromJsonAsync<AuthResponse>();
        client.SetBearerToken(auth!.Token);
        return (client, auth.Token);
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  Auth Tests
    // ══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task GetCategories_ReturnsUnauthorized_WhenNotAuthenticated()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("api/catalog/categories");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetProducts_ReturnsUnauthorized_WhenNotAuthenticated()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("api/catalog/products");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetCategories_ReturnsForbidden_WhenNoTenantToken()
    {
        var client = _factory.CreateClient();
        var token = await AuthHelper.RegisterAndGetTokenAsync(client, $"notenant-{Guid.NewGuid()}@example.com");
        client.SetBearerToken(token);

        var response = await client.GetAsync("api/catalog/categories");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  Category Tests
    // ══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task CreateCategory_ReturnsCreated_WithValidData()
    {
        var (client, _) = await AuthenticateAsync();
        var request = new CreateCategoryRequest("Electronics", "Electronic products", "electronics", null, null);

        var response = await client.PostAsJsonAsync("api/catalog/categories", request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var category = await response.Content.ReadFromJsonAsync<CategoryDto>();
        category!.Name.Should().Be("Electronics");
        category.Description.Should().Be("Electronic products");
        category.Slug.Should().Be("electronics");
        category.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task GetCategories_ReturnsPaged_WhenCategoriesExist()
    {
        var (client, _) = await AuthenticateAsync();
        await CreateTestCategoryAsync(client, "Clothing");
        await CreateTestCategoryAsync(client, "Furniture");

        var response = await client.GetAsync("api/catalog/categories");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<PagedResult<CategoryDto>>();
        result.Should().NotBeNull();
        result!.Items.Should().Contain(c => c.Name == "Clothing");
        result.Items.Should().Contain(c => c.Name == "Furniture");
    }

    [Fact]
    public async Task GetCategory_ReturnsDetail_WhenExists()
    {
        var (client, _) = await AuthenticateAsync();
        var created = await CreateTestCategoryAsync(client, "Books");

        var response = await client.GetAsync($"api/catalog/categories/{created.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var detail = await response.Content.ReadFromJsonAsync<CategoryDetailDto>();
        detail!.Id.Should().Be(created.Id);
        detail.Name.Should().Be("Books");
    }

    [Fact]
    public async Task UpdateCategory_ReturnsOk_WithValidData()
    {
        var (client, _) = await AuthenticateAsync();
        var created = await CreateTestCategoryAsync(client, "Old Category");
        var request = new UpdateCategoryRequest("Updated Category", "Updated desc", "updated-cat", null, null, 5, true);

        var response = await client.PutAsJsonAsync($"api/catalog/categories/{created.Id}", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await response.Content.ReadFromJsonAsync<CategoryDto>();
        updated!.Name.Should().Be("Updated Category");
        updated.Description.Should().Be("Updated desc");
        updated.DisplayOrder.Should().Be(5);
    }

    [Fact]
    public async Task DeleteCategory_ReturnsNoContent_WhenAdmin()
    {
        var (client, _) = await AuthenticateAsAdminAsync();
        var created = await CreateTestCategoryAsync(client, "Delete Me Category");

        var response = await client.DeleteAsync($"api/catalog/categories/{created.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task DeleteCategory_ReturnsForbidden_WhenNonAdmin()
    {
        var (client, _) = await AuthenticateAsync();
        var created = await CreateTestCategoryAsync(client, "No Delete Category");

        var response = await client.DeleteAsync($"api/catalog/categories/{created.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task DeleteCategory_ReturnsConflict_WhenHasProducts()
    {
        var (client, _) = await AuthenticateAsAdminAsync();
        var category = await CreateTestCategoryAsync(client, "Category With Products");
        await CreateTestProductAsync(client, "Product In Category", categoryId: category.Id);

        var response = await client.DeleteAsync($"api/catalog/categories/{category.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task CreateSubcategory_ReturnsCreated_WithParentId()
    {
        var (client, _) = await AuthenticateAsync();
        var parent = await CreateTestCategoryAsync(client, "Parent Category");
        var request = new CreateCategoryRequest("Sub Category", null, null, parent.Id, null);

        var response = await client.PostAsJsonAsync("api/catalog/categories", request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var sub = await response.Content.ReadFromJsonAsync<CategoryDto>();
        sub!.ParentCategoryId.Should().Be(parent.Id);

        // Verify parent name is populated when fetched via detail endpoint
        var detailResponse = await client.GetAsync($"api/catalog/categories/{sub.Id}");
        var detail = await detailResponse.Content.ReadFromJsonAsync<CategoryDetailDto>();
        detail!.ParentCategoryName.Should().Be("Parent Category");
    }

    [Fact]
    public async Task GetAllCategories_ReturnsFlatList()
    {
        var (client, _) = await AuthenticateAsync();
        await CreateTestCategoryAsync(client, "All-Cat-1");
        await CreateTestCategoryAsync(client, "All-Cat-2");

        var response = await client.GetAsync("api/catalog/categories/all");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var categories = await response.Content.ReadFromJsonAsync<List<CategoryDto>>();
        categories.Should().NotBeNull();
        categories!.Count.Should().BeGreaterThanOrEqualTo(2);
    }

    [Fact]
    public async Task DeleteCategory_ReturnsConflict_WhenHasSubCategories()
    {
        var (client, _) = await AuthenticateAsAdminAsync();
        var parent = await CreateTestCategoryAsync(client, "Parent To Delete");
        var request = new CreateCategoryRequest("Child Category", null, null, parent.Id, null);
        var childResponse = await client.PostAsJsonAsync("api/catalog/categories", request);
        childResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var response = await client.DeleteAsync($"api/catalog/categories/{parent.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  Product Tests
    // ══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task CreateProduct_ReturnsCreated_WithValidData()
    {
        var (client, _) = await AuthenticateAsync();
        var request = new CreateProductRequest(
            "Widget", "A test widget", null, null, "WGT-001", null, "Acme", null,
            null, "General", "Active", 19.99m);

        var response = await client.PostAsJsonAsync("api/catalog/products", request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var product = await response.Content.ReadFromJsonAsync<ProductDto>();
        product!.Name.Should().Be("Widget");
        product.Sku.Should().Be("WGT-001");
        product.BasePrice.Should().Be(19.99m);
        product.Status.Should().Be("Active");
    }

    [Fact]
    public async Task GetProducts_ReturnsPaged_WhenProductsExist()
    {
        var (client, _) = await AuthenticateAsync();
        await CreateTestProductAsync(client, "Paged Product A");
        await CreateTestProductAsync(client, "Paged Product B");

        var response = await client.GetAsync("api/catalog/products");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<PagedResult<ProductDto>>();
        result.Should().NotBeNull();
        result!.Items.Should().Contain(p => p.Name == "Paged Product A");
        result.Items.Should().Contain(p => p.Name == "Paged Product B");
    }

    [Fact]
    public async Task GetProducts_FiltersByCategory()
    {
        var (client, _) = await AuthenticateAsync();
        var catA = await CreateTestCategoryAsync(client, "Filter Cat A");
        var catB = await CreateTestCategoryAsync(client, "Filter Cat B");
        await CreateTestProductAsync(client, "In Cat A", categoryId: catA.Id);
        await CreateTestProductAsync(client, "In Cat B", categoryId: catB.Id);

        var response = await client.GetAsync($"api/catalog/products?categoryId={catA.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<PagedResult<ProductDto>>();
        result!.Items.Should().Contain(p => p.Name == "In Cat A");
        result.Items.Should().NotContain(p => p.Name == "In Cat B");
    }

    [Fact]
    public async Task GetProduct_ReturnsDetail_WhenExists()
    {
        var (client, _) = await AuthenticateAsync();
        var created = await CreateTestProductAsync(client, "Detail Product");

        var response = await client.GetAsync($"api/catalog/products/{created.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var detail = await response.Content.ReadFromJsonAsync<ProductDetailDto>();
        detail!.Id.Should().Be(created.Id);
        detail.Name.Should().Be("Detail Product");
    }

    [Fact]
    public async Task UpdateProduct_ReturnsOk_WithValidData()
    {
        var (client, _) = await AuthenticateAsync();
        var created = await CreateTestProductAsync(client, "Old Product");
        var request = new UpdateProductRequest(
            "Updated Product", "New desc", null, null, null, null, "NewBrand", null,
            null, "General", "Active", 49.99m, null, null, "USD",
            null, null, null, null, null, null, true, null, null, null, null);

        var response = await client.PutAsJsonAsync($"api/catalog/products/{created.Id}", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await response.Content.ReadFromJsonAsync<ProductDto>();
        updated!.Name.Should().Be("Updated Product");
        updated.BasePrice.Should().Be(49.99m);
        updated.Brand.Should().Be("NewBrand");
    }

    [Fact]
    public async Task DeleteProduct_ReturnsNoContent_WhenAdmin()
    {
        var (client, _) = await AuthenticateAsAdminAsync();
        var created = await CreateTestProductAsync(client, "Delete Me Product");

        var response = await client.DeleteAsync($"api/catalog/products/{created.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task DeleteProduct_ReturnsForbidden_WhenNonAdmin()
    {
        var (client, _) = await AuthenticateAsync();
        var created = await CreateTestProductAsync(client, "No Delete Product");

        var response = await client.DeleteAsync($"api/catalog/products/{created.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  Options & Variants (Apparel Lifecycle)
    // ══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task ApparelProduct_OptionsAndVariants_FullLifecycle()
    {
        var (client, _) = await AuthenticateAsync();

        // Create apparel product
        var product = await CreateTestProductAsync(client, "Test T-Shirt", productType: "Apparel", basePrice: 29.99m);

        // Add options with values
        var sizeOption = await AddOptionAsync(client, product.Id, "Size", new[] { "S", "M", "L" });
        sizeOption.Name.Should().Be("Size");
        sizeOption.Values.Should().HaveCount(3);

        var colorOption = await AddOptionAsync(client, product.Id, "Color", new[] { "Red", "Blue" });
        colorOption.Name.Should().Be("Color");
        colorOption.Values.Should().HaveCount(2);

        // Verify options are listed
        var optionsResponse = await client.GetAsync($"api/catalog/products/{product.Id}/options");
        optionsResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var options = await optionsResponse.Content.ReadFromJsonAsync<List<ProductOptionDto>>();
        options.Should().HaveCount(2);

        // Create a variant using option value IDs
        var smallRedValueIds = new List<Guid>
        {
            sizeOption.Values.First(v => v.Value == "S").Id,
            colorOption.Values.First(v => v.Value == "Red").Id
        };
        var variantRequest = new CreateProductVariantRequest(
            Sku: "TSH-S-RED", Price: 34.99m, StockQuantity: 100,
            TrackInventory: true, OptionValueIds: smallRedValueIds);
        var variantResponse = await client.PostAsJsonAsync($"api/catalog/products/{product.Id}/variants", variantRequest);
        variantResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var variant = await variantResponse.Content.ReadFromJsonAsync<ProductVariantDto>();
        variant!.Sku.Should().Be("TSH-S-RED");
        variant.Price.Should().Be(34.99m);
        variant.StockQuantity.Should().Be(100);

        // Get variants — option values are populated on read, not on create
        var listResponse = await client.GetAsync($"api/catalog/products/{product.Id}/variants");
        listResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var variants = await listResponse.Content.ReadFromJsonAsync<List<ProductVariantDto>>();
        variants.Should().HaveCount(1);
        variants![0].OptionValues.Should().HaveCount(2);

        // Update variant
        var updateVariant = new UpdateProductVariantRequest(
            "TSH-S-RED-UPD", null, 39.99m, null, null, 50, null, true, true, 0);
        var updateResponse = await client.PutAsJsonAsync(
            $"api/catalog/products/{product.Id}/variants/{variant.Id}", updateVariant);
        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var updatedVariant = await updateResponse.Content.ReadFromJsonAsync<ProductVariantDto>();
        updatedVariant!.Sku.Should().Be("TSH-S-RED-UPD");
        updatedVariant.Price.Should().Be(39.99m);
        updatedVariant.StockQuantity.Should().Be(50);

        // Delete variant
        var deleteVariantResponse = await client.DeleteAsync(
            $"api/catalog/products/{product.Id}/variants/{variant.Id}");
        deleteVariantResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Delete option
        var deleteOptionResponse = await client.DeleteAsync(
            $"api/catalog/products/{product.Id}/options/{sizeOption.Id}");
        deleteOptionResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  Image Tests
    // ══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task AddImage_ReturnsCreated()
    {
        var (client, _) = await AuthenticateAsync();
        var product = await CreateTestProductAsync(client, "Image Product");
        var request = new CreateProductImageRequest(
            "https://example.com/image.jpg", "image.jpg", "Product image", "image/jpeg", 1024, null, true);

        var response = await client.PostAsJsonAsync($"api/catalog/products/{product.Id}/images", request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var image = await response.Content.ReadFromJsonAsync<ProductImageDto>();
        image!.Url.Should().Be("https://example.com/image.jpg");
        image.FileName.Should().Be("image.jpg");
        image.IsPrimary.Should().BeTrue();
    }

    [Fact]
    public async Task DeleteImage_ReturnsNoContent()
    {
        var (client, _) = await AuthenticateAsync();
        var product = await CreateTestProductAsync(client, "Image Delete Product");
        var image = await AddImageAsync(client, product.Id);

        var response = await client.DeleteAsync($"api/catalog/products/{product.Id}/images/{image.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Verify it's gone
        var listResponse = await client.GetAsync($"api/catalog/products/{product.Id}/images");
        var images = await listResponse.Content.ReadFromJsonAsync<List<ProductImageDto>>();
        images.Should().BeEmpty();
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  Ingredient Tests
    // ══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task AddIngredient_ReturnsCreated()
    {
        var (client, _) = await AuthenticateAsync();
        var product = await CreateTestProductAsync(client, "Food Product", productType: "Food");
        var request = new CreateProductIngredientRequest(
            "Flour", "200", "grams", IsAllergen: true, AllergenType: "Gluten");

        var response = await client.PostAsJsonAsync($"api/catalog/products/{product.Id}/ingredients", request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var ingredient = await response.Content.ReadFromJsonAsync<ProductIngredientDto>();
        ingredient!.Name.Should().Be("Flour");
        ingredient.Quantity.Should().Be("200");
        ingredient.Unit.Should().Be("grams");
        ingredient.IsAllergen.Should().BeTrue();
        ingredient.AllergenType.Should().Be("Gluten");
    }

    [Fact]
    public async Task UpdateIngredient_ReturnsOk()
    {
        var (client, _) = await AuthenticateAsync();
        var product = await CreateTestProductAsync(client, "Update Ingredient Product", productType: "Food");
        var ingredient = await AddIngredientAsync(client, product.Id, "Sugar");
        var update = new UpdateProductIngredientRequest(
            "Brown Sugar", "150", "grams", false, null, 1, "Organic only");

        var response = await client.PutAsJsonAsync(
            $"api/catalog/products/{product.Id}/ingredients/{ingredient.Id}", update);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await response.Content.ReadFromJsonAsync<ProductIngredientDto>();
        updated!.Name.Should().Be("Brown Sugar");
        updated.Quantity.Should().Be("150");
        updated.Notes.Should().Be("Organic only");
    }

    [Fact]
    public async Task DeleteIngredient_ReturnsNoContent()
    {
        var (client, _) = await AuthenticateAsync();
        var product = await CreateTestProductAsync(client, "Delete Ingredient Product", productType: "Food");
        var ingredient = await AddIngredientAsync(client, product.Id, "Salt");

        var response = await client.DeleteAsync(
            $"api/catalog/products/{product.Id}/ingredients/{ingredient.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Verify it's gone
        var listResponse = await client.GetAsync($"api/catalog/products/{product.Id}/ingredients");
        var ingredients = await listResponse.Content.ReadFromJsonAsync<List<ProductIngredientDto>>();
        ingredients.Should().BeEmpty();
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  Helpers
    // ══════════════════════════════════════════════════════════════════════════

    private static async Task<CategoryDto> CreateTestCategoryAsync(HttpClient client, string name)
    {
        var request = new CreateCategoryRequest(name, null, null, null, null);
        var response = await client.PostAsJsonAsync("api/catalog/categories", request);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<CategoryDto>())!;
    }

    private static async Task<ProductDto> CreateTestProductAsync(
        HttpClient client,
        string name,
        Guid? categoryId = null,
        string productType = "General",
        decimal basePrice = 9.99m)
    {
        var request = new CreateProductRequest(
            name, null, null, null, null, null, null, null,
            categoryId, productType, "Active", basePrice);
        var response = await client.PostAsJsonAsync("api/catalog/products", request);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ProductDto>())!;
    }

    private static async Task<ProductOptionDto> AddOptionAsync(
        HttpClient client, Guid productId, string name, string[] values)
    {
        var request = new CreateProductOptionRequest(name, 0, values);
        var response = await client.PostAsJsonAsync($"api/catalog/products/{productId}/options", request);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ProductOptionDto>())!;
    }

    private static async Task<ProductImageDto> AddImageAsync(HttpClient client, Guid productId)
    {
        var request = new CreateProductImageRequest(
            $"https://example.com/{Guid.NewGuid()}.jpg", "test.jpg", "Test image", "image/jpeg", 512);
        var response = await client.PostAsJsonAsync($"api/catalog/products/{productId}/images", request);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ProductImageDto>())!;
    }

    private static async Task<ProductIngredientDto> AddIngredientAsync(
        HttpClient client, Guid productId, string name)
    {
        var request = new CreateProductIngredientRequest(name, "100", "grams");
        var response = await client.PostAsJsonAsync($"api/catalog/products/{productId}/ingredients", request);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ProductIngredientDto>())!;
    }
}
