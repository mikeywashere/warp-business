using Microsoft.EntityFrameworkCore;
using WarpBusiness.Catalog.Data;
using WarpBusiness.Catalog.Models;

namespace WarpBusiness.Catalog.Services;

public class VariantGenerationService
{
    private readonly CatalogDbContext _db;

    public VariantGenerationService(CatalogDbContext db) => _db = db;

    public async Task<List<List<(Guid OptionId, Guid OptionValueId)>>> GetVariantCombinationsAsync(Guid productId, CancellationToken ct = default)
    {
        var axisOptions = await _db.ProductOptions
            .Where(o => o.ProductId == productId && o.IsVariantAxis)
            .OrderBy(o => o.SortOrder)
            .Include(o => o.Values)
            .ToListAsync(ct);

        if (!axisOptions.Any()) return new List<List<(Guid, Guid)>>();

        return axisOptions
            .Select(o => o.Values.OrderBy(v => v.SortOrder).Select(v => (o.Id, v.Id)).ToList())
            .Aggregate(
                new List<List<(Guid, Guid)>> { new() },
                (combos, axis) => combos
                    .SelectMany(combo => axis.Select(v =>
                    {
                        var next = new List<(Guid, Guid)>(combo) { v };
                        return next;
                    }))
                    .ToList());
    }

    public async Task<int> GenerateVariantsAsync(Guid productId, Guid tenantId, CancellationToken ct = default)
    {
        var combos = await GetVariantCombinationsAsync(productId, ct);

        var existingVariants = await _db.ProductVariants
            .Where(v => v.ProductId == productId)
            .Include(v => v.OptionValues)
            .ToListAsync(ct);

        int created = 0;
        foreach (var combo in combos)
        {
            var comboSet = combo.Select(x => x.OptionValueId).ToHashSet();
            var alreadyExists = existingVariants.Any(v =>
                v.OptionValues.Count == combo.Count &&
                v.OptionValues.All(ov => comboSet.Contains(ov.OptionValueId)));

            if (alreadyExists) continue;

            var variantId = Guid.NewGuid();
            var variant = new ProductVariant
            {
                Id = variantId,
                ProductId = productId,
                TenantId = tenantId,
                IsActive = true,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
                OptionValues = combo.Select(x => new VariantOptionValue
                {
                    VariantId = variantId,
                    OptionId = x.OptionId,
                    OptionValueId = x.OptionValueId
                }).ToList()
            };

            _db.ProductVariants.Add(variant);
            created++;
        }

        if (created > 0) await _db.SaveChangesAsync(ct);
        return created;
    }
}
