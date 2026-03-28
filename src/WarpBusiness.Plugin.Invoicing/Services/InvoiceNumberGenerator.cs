using Microsoft.EntityFrameworkCore;
using WarpBusiness.Plugin.Abstractions;
using WarpBusiness.Plugin.Invoicing.Data;
using WarpBusiness.Plugin.Invoicing.Domain;

namespace WarpBusiness.Plugin.Invoicing.Services;

internal interface IInvoiceNumberGenerator
{
    Task<string> GenerateNextAsync(CancellationToken ct = default);
}

internal class InvoiceNumberGenerator : IInvoiceNumberGenerator
{
    private readonly InvoicingDbContext _context;
    private readonly ITenantContext _tenantContext;

    public InvoiceNumberGenerator(InvoicingDbContext context, ITenantContext tenantContext)
    {
        _context = context;
        _tenantContext = tenantContext;
    }

    public async Task<string> GenerateNextAsync(CancellationToken ct = default)
    {
        const int maxRetries = 5;

        for (var attempt = 0; attempt < maxRetries; attempt++)
        {
            var settings = await _context.InvoiceSettings
                .FirstOrDefaultAsync(ct);

            if (settings == null)
            {
                settings = new InvoiceSettings
                {
                    Id = Guid.NewGuid(),
                    TenantId = _tenantContext.TenantId,
                    UpdatedAt = DateTimeOffset.UtcNow,
                    UpdatedBy = "system"
                };
                _context.InvoiceSettings.Add(settings);
                await _context.SaveChangesAsync(ct);
            }

            var number = settings.NextNumber;
            var formatted = $"{settings.Prefix}{number.ToString().PadLeft(settings.NumberPadding, '0')}";

            settings.NextNumber = number + 1;
            settings.UpdatedAt = DateTimeOffset.UtcNow;

            try
            {
                await _context.SaveChangesAsync(ct);
                return formatted;
            }
            catch (DbUpdateConcurrencyException) when (attempt < maxRetries - 1)
            {
                // Refresh and retry on concurrent access
                foreach (var entry in _context.ChangeTracker.Entries())
                {
                    await entry.ReloadAsync(ct);
                }
            }
        }

        throw new InvalidOperationException("Failed to generate invoice number after maximum retries.");
    }
}
