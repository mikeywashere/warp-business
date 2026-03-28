using Microsoft.EntityFrameworkCore;
using WarpBusiness.Plugin.Abstractions;
using WarpBusiness.Plugin.Invoicing.Data;
using WarpBusiness.Plugin.Invoicing.Domain;
using WarpBusiness.Shared.Invoicing;

namespace WarpBusiness.Plugin.Invoicing.Services;

public class InvoiceSettingsService : IInvoiceSettingsService
{
    private readonly InvoicingDbContext _context;
    private readonly ITenantContext _tenantContext;

    public InvoiceSettingsService(InvoicingDbContext context, ITenantContext tenantContext)
    {
        _context = context;
        _tenantContext = tenantContext;
    }

    public async Task<InvoiceSettingsDto> GetSettingsAsync(CancellationToken ct = default)
    {
        var settings = await _context.InvoiceSettings.FirstOrDefaultAsync(ct);

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

        return ToDto(settings);
    }

    public async Task<InvoiceSettingsDto> UpdateSettingsAsync(
        UpdateInvoiceSettingsRequest request, string userId, CancellationToken ct = default)
    {
        var settings = await _context.InvoiceSettings.FirstOrDefaultAsync(ct);

        if (settings == null)
        {
            settings = new InvoiceSettings
            {
                Id = Guid.NewGuid(),
                TenantId = _tenantContext.TenantId,
                UpdatedAt = DateTimeOffset.UtcNow,
                UpdatedBy = userId
            };
            _context.InvoiceSettings.Add(settings);
        }

        settings.Prefix = request.Prefix;
        settings.NumberPadding = request.NumberPadding;
        settings.DefaultPaymentTerms = request.DefaultPaymentTerms;
        settings.DefaultDueDays = request.DefaultDueDays;
        settings.DefaultTaxRate = request.DefaultTaxRate;
        settings.DefaultCurrency = request.DefaultCurrency;
        settings.DefaultFooterText = request.DefaultFooterText;
        settings.DefaultCustomerNotes = request.DefaultCustomerNotes;
        settings.CompanyName = request.CompanyName;
        settings.CompanyAddress = request.CompanyAddress;
        settings.CompanyPhone = request.CompanyPhone;
        settings.CompanyEmail = request.CompanyEmail;
        settings.CompanyLogoUrl = request.CompanyLogoUrl;
        settings.UpdatedAt = DateTimeOffset.UtcNow;
        settings.UpdatedBy = userId;

        await _context.SaveChangesAsync(ct);
        return ToDto(settings);
    }

    private static InvoiceSettingsDto ToDto(InvoiceSettings s) => new(
        s.Id,
        s.Prefix,
        s.NextNumber,
        s.NumberPadding,
        s.DefaultPaymentTerms,
        s.DefaultDueDays,
        s.DefaultTaxRate,
        s.DefaultCurrency,
        s.DefaultFooterText,
        s.DefaultCustomerNotes,
        s.CompanyName,
        s.CompanyAddress,
        s.CompanyPhone,
        s.CompanyEmail,
        s.CompanyLogoUrl);
}
