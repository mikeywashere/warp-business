using WarpBusiness.Shared.Invoicing;

namespace WarpBusiness.Plugin.Invoicing.Services;

public interface IInvoicePaymentService
{
    Task<IReadOnlyList<InvoicePaymentDto>> GetPaymentsAsync(
        Guid invoiceId, CancellationToken ct = default);

    Task<InvoicePaymentDto> RecordPaymentAsync(
        Guid invoiceId, CreateInvoicePaymentRequest request,
        string userId, CancellationToken ct = default);

    Task<bool> DeletePaymentAsync(
        Guid invoiceId, Guid paymentId, CancellationToken ct = default);
}
