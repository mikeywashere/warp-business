using WarpBusiness.Shared.Invoicing;

namespace WarpBusiness.Plugin.Invoicing.Services;

public interface IInvoiceSettingsService
{
    Task<InvoiceSettingsDto> GetSettingsAsync(CancellationToken ct = default);

    Task<InvoiceSettingsDto> UpdateSettingsAsync(
        UpdateInvoiceSettingsRequest request, string userId, CancellationToken ct = default);
}
