using Microsoft.EntityFrameworkCore;
using WarpBusiness.Plugin.Abstractions;
using WarpBusiness.Plugin.Invoicing.Data;
using WarpBusiness.Plugin.Invoicing.Domain;
using WarpBusiness.Shared.Invoicing;

namespace WarpBusiness.Plugin.Invoicing.Services;

public class InvoicePaymentService : IInvoicePaymentService
{
    private readonly InvoicingDbContext _context;
    private readonly ITenantContext _tenantContext;

    public InvoicePaymentService(InvoicingDbContext context, ITenantContext tenantContext)
    {
        _context = context;
        _tenantContext = tenantContext;
    }

    public async Task<IReadOnlyList<InvoicePaymentDto>> GetPaymentsAsync(
        Guid invoiceId, CancellationToken ct = default)
    {
        return await _context.InvoicePayments
            .AsNoTracking()
            .Where(p => p.InvoiceId == invoiceId)
            .OrderByDescending(p => p.PaymentDate)
            .Select(p => ToDto(p))
            .ToListAsync(ct);
    }

    public async Task<InvoicePaymentDto> RecordPaymentAsync(
        Guid invoiceId, CreateInvoicePaymentRequest request,
        string userId, CancellationToken ct = default)
    {
        var invoice = await _context.Invoices
            .Include(i => i.Payments)
            .Include(i => i.LineItems)
            .FirstOrDefaultAsync(i => i.Id == invoiceId, ct)
            ?? throw new InvalidOperationException("Invoice not found.");

        if (invoice.Status != InvoiceStatus.Sent &&
            invoice.Status != InvoiceStatus.PartiallyPaid &&
            invoice.Status != InvoiceStatus.Overdue)
            throw new InvalidOperationException("Payments can only be recorded on sent, partially paid, or overdue invoices.");

        Enum.TryParse<PaymentMethod>(request.PaymentMethod, true, out var method);

        var payment = new InvoicePayment
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantContext.TenantId,
            InvoiceId = invoiceId,
            Amount = request.Amount,
            Currency = invoice.Currency,
            PaymentDate = request.PaymentDate,
            PaymentMethod = method,
            ReferenceNumber = request.ReferenceNumber,
            Notes = request.Notes,
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedBy = userId
        };

        _context.InvoicePayments.Add(payment);

        invoice.AmountPaid = invoice.Payments.Sum(p => p.Amount) + request.Amount;
        invoice.BalanceDue = invoice.TotalAmount - invoice.AmountPaid;
        invoice.UpdatedAt = DateTimeOffset.UtcNow;

        if (invoice.BalanceDue <= 0)
        {
            invoice.Status = InvoiceStatus.Paid;
            invoice.PaidDate = request.PaymentDate;
            invoice.BalanceDue = 0;
        }
        else
        {
            invoice.Status = InvoiceStatus.PartiallyPaid;
        }

        await _context.SaveChangesAsync(ct);
        return ToDto(payment);
    }

    public async Task<bool> DeletePaymentAsync(
        Guid invoiceId, Guid paymentId, CancellationToken ct = default)
    {
        var invoice = await _context.Invoices
            .Include(i => i.Payments)
            .Include(i => i.LineItems)
            .FirstOrDefaultAsync(i => i.Id == invoiceId, ct);

        if (invoice == null) return false;

        var payment = invoice.Payments.FirstOrDefault(p => p.Id == paymentId);
        if (payment == null) return false;

        invoice.Payments.Remove(payment);
        _context.InvoicePayments.Remove(payment);

        invoice.AmountPaid = invoice.Payments.Sum(p => p.Amount);
        invoice.BalanceDue = invoice.TotalAmount - invoice.AmountPaid;
        invoice.UpdatedAt = DateTimeOffset.UtcNow;

        if (invoice.AmountPaid <= 0)
        {
            invoice.Status = InvoiceStatus.Sent;
            invoice.PaidDate = null;
            invoice.AmountPaid = 0;
        }
        else
        {
            invoice.Status = InvoiceStatus.PartiallyPaid;
            invoice.PaidDate = null;
        }

        await _context.SaveChangesAsync(ct);
        return true;
    }

    private static InvoicePaymentDto ToDto(InvoicePayment p) => new(
        p.Id,
        p.InvoiceId,
        p.Amount,
        p.Currency,
        p.PaymentDate,
        p.PaymentMethod.ToString(),
        p.ReferenceNumber,
        p.Notes,
        p.CreatedAt,
        p.CreatedBy);
}
