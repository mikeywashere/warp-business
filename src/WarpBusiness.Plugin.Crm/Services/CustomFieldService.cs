using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using WarpBusiness.Plugin.Abstractions;
using WarpBusiness.Plugin.Crm.Data;
using WarpBusiness.Plugin.Crm.Domain;
using WarpBusiness.Shared.Crm;

namespace WarpBusiness.Plugin.Crm.Services;

public class CustomFieldService : ICustomFieldService
{
    private readonly CrmDbContext _db;
    private readonly ITenantContext _tenantContext;

    public CustomFieldService(CrmDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

#pragma warning disable HAA0301 // Closure allocation is expected for EF Core queries
    public async Task<IReadOnlyList<CustomFieldDefinitionDto>> GetDefinitionsAsync(string entityType, CancellationToken ct = default)
    {
        return await _db.CustomFieldDefinitions
            .AsNoTracking()
            .Where(d => d.EntityType == entityType)
            .OrderBy(d => d.DisplayOrder)
            .Select(d => MapDefinitionToDto(d))
            .ToListAsync(ct);
    }
#pragma warning restore HAA0301

    public async Task<CustomFieldDefinitionDto?> GetDefinitionAsync(Guid id, CancellationToken ct = default)
    {
        var def = await _db.CustomFieldDefinitions.AsNoTracking().FirstOrDefaultAsync(d => d.Id == id, ct);
        return def is null ? null : MapDefinitionToDto(def);
    }

    public async Task<CustomFieldDefinitionDto> CreateDefinitionAsync(CreateCustomFieldDefinitionRequest request, CancellationToken ct = default)
    {
        var def = new CustomFieldDefinition
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantContext.TenantId,
            Name = request.Name,
            EntityType = request.EntityType,
            FieldType = request.FieldType,
            SelectOptions = request.SelectOptions is not null
                ? JsonSerializer.Serialize(request.SelectOptions)
                : null,
            IsRequired = request.IsRequired,
            DisplayOrder = request.DisplayOrder,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        _db.CustomFieldDefinitions.Add(def);
        await _db.SaveChangesAsync(ct);
        return MapDefinitionToDto(def);
    }

    public async Task<CustomFieldDefinitionDto?> UpdateDefinitionAsync(Guid id, UpdateCustomFieldDefinitionRequest request, CancellationToken ct = default)
    {
        var def = await _db.CustomFieldDefinitions.FindAsync([id], ct);
        if (def is null) return null;

        def.Name = request.Name;
        def.FieldType = request.FieldType;
        def.SelectOptions = request.SelectOptions is not null
            ? JsonSerializer.Serialize(request.SelectOptions)
            : null;
        def.IsRequired = request.IsRequired;
        def.DisplayOrder = request.DisplayOrder;
        def.IsActive = request.IsActive;
        def.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        return MapDefinitionToDto(def);
    }

    public async Task<bool> DeleteDefinitionAsync(Guid id, CancellationToken ct = default)
    {
        var def = await _db.CustomFieldDefinitions.FindAsync([id], ct);
        if (def is null) return false;
        _db.CustomFieldDefinitions.Remove(def);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<IReadOnlyList<CustomFieldValueDto>> GetValuesForContactAsync(Guid contactId, CancellationToken ct = default)
    {
        var definitions = await _db.CustomFieldDefinitions
            .AsNoTracking()
            .Where(d => d.EntityType == "Contact" && d.IsActive)
            .OrderBy(d => d.DisplayOrder)
            .ToListAsync(ct);

        var values = await _db.CustomFieldValues
            .AsNoTracking()
            .Where(v => v.ContactId == contactId)
            .ToListAsync(ct);

        var valueLookup = values.ToDictionary(v => v.FieldDefinitionId);

        return definitions.Select(d =>
        {
            valueLookup.TryGetValue(d.Id, out var val);
            return new CustomFieldValueDto(
                d.Id,
                d.Name,
                d.FieldType,
                DeserializeOptions(d.SelectOptions),
                d.IsRequired,
                val?.Value
            );
        }).ToList();
    }

    public async Task UpsertValuesAsync(Guid contactId, IEnumerable<UpsertCustomFieldValueRequest> values, CancellationToken ct = default)
    {
        var requests = values.ToList();
        if (requests.Count == 0) return;

        var defIds = requests.Select(r => r.FieldDefinitionId).ToHashSet();

        var validDefIds = await _db.CustomFieldDefinitions
            .AsNoTracking()
            .Where(d => defIds.Contains(d.Id))
            .Select(d => d.Id)
            .ToHashSetAsync(ct);

        var existing = await _db.CustomFieldValues
            .Where(v => v.ContactId == contactId && defIds.Contains(v.FieldDefinitionId))
            .ToListAsync(ct);

        var existingLookup = existing.ToDictionary(v => v.FieldDefinitionId);

        foreach (var req in requests)
        {
            if (!validDefIds.Contains(req.FieldDefinitionId)) continue;

            if (existingLookup.TryGetValue(req.FieldDefinitionId, out var existing_val))
            {
                existing_val.Value = req.Value;
                existing_val.UpdatedAt = DateTime.UtcNow;
            }
            else
            {
                _db.CustomFieldValues.Add(new CustomFieldValue
                {
                    Id = Guid.NewGuid(),
                    ContactId = contactId,
                    FieldDefinitionId = req.FieldDefinitionId,
                    Value = req.Value,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                });
            }
        }

        await _db.SaveChangesAsync(ct);
    }

    private static CustomFieldDefinitionDto MapDefinitionToDto(CustomFieldDefinition d) =>
        new(d.Id, d.Name, d.EntityType, d.FieldType,
            DeserializeOptions(d.SelectOptions),
            d.IsRequired, d.DisplayOrder, d.IsActive);

    private static string[]? DeserializeOptions(string? json)
    {
        if (string.IsNullOrEmpty(json)) return null;
        try { return JsonSerializer.Deserialize<string[]>(json); }
        catch { return null; }
    }
}
