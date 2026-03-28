using WarpBusiness.Shared.Invoicing;

namespace WarpBusiness.Plugin.Invoicing.Services;

public interface IInvoiceLineItemService
{
    Task<IReadOnlyList<InvoiceLineItemDto>> GetLineItemsAsync(
        Guid invoiceId, CancellationToken ct = default);

    Task<InvoiceLineItemDto> AddLineItemAsync(
        Guid invoiceId, CreateInvoiceLineItemRequest request, CancellationToken ct = default);

    Task<InvoiceLineItemDto?> UpdateLineItemAsync(
        Guid invoiceId, Guid lineItemId,
        UpdateInvoiceLineItemRequest request, CancellationToken ct = default);

    Task<bool> DeleteLineItemAsync(
        Guid invoiceId, Guid lineItemId, CancellationToken ct = default);

    Task<bool> ReorderLineItemsAsync(
        Guid invoiceId, IReadOnlyList<Guid> orderedIds, CancellationToken ct = default);
}
