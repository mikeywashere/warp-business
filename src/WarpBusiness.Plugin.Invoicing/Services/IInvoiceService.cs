using WarpBusiness.Shared.Crm;
using WarpBusiness.Shared.Invoicing;

namespace WarpBusiness.Plugin.Invoicing.Services;

public enum DeleteInvoiceResult { Deleted, NotFound, NotDraft }

public interface IInvoiceService
{
    Task<PagedResult<InvoiceDto>> GetInvoicesAsync(
        int page, int pageSize,
        string? search,
        Guid? companyId,
        string? status,
        DateOnly? fromDate,
        DateOnly? toDate,
        CancellationToken ct = default);

    Task<InvoiceDetailDto?> GetInvoiceAsync(Guid id, CancellationToken ct = default);

    Task<InvoiceDto> CreateInvoiceAsync(
        CreateInvoiceRequest request, string userId, CancellationToken ct = default);

    Task<InvoiceDto?> UpdateInvoiceAsync(
        Guid id, UpdateInvoiceRequest request, CancellationToken ct = default);

    Task<DeleteInvoiceResult> DeleteInvoiceAsync(Guid id, CancellationToken ct = default);

    Task<InvoiceDto?> SendInvoiceAsync(Guid id, CancellationToken ct = default);
    Task<InvoiceDto?> MarkOverdueAsync(Guid id, CancellationToken ct = default);
    Task<InvoiceDto?> CancelInvoiceAsync(Guid id, CancellationToken ct = default);
    Task<InvoiceDto?> VoidInvoiceAsync(Guid id, CancellationToken ct = default);

    Task<InvoiceSummaryDto> GetSummaryAsync(CancellationToken ct = default);
}
