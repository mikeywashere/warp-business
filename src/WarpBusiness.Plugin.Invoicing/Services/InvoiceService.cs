using Microsoft.EntityFrameworkCore;
using WarpBusiness.Plugin.Abstractions;
using WarpBusiness.Plugin.Invoicing.Data;
using WarpBusiness.Plugin.Invoicing.Domain;
using WarpBusiness.Shared.Crm;
using WarpBusiness.Shared.Invoicing;

namespace WarpBusiness.Plugin.Invoicing.Services;

public class InvoiceService : IInvoiceService
{
    private readonly InvoicingDbContext _context;
    private readonly ITenantContext _tenantContext;
    private readonly IInvoiceNumberGenerator _numberGenerator;

    public InvoiceService(
        InvoicingDbContext context,
        ITenantContext tenantContext,
        IInvoiceNumberGenerator numberGenerator)
    {
        _context = context;
        _tenantContext = tenantContext;
        _numberGenerator = numberGenerator;
    }

    public async Task<PagedResult<InvoiceDto>> GetInvoicesAsync(
        int page, int pageSize,
        string? search, Guid? companyId, string? status,
        DateOnly? fromDate, DateOnly? toDate,
        CancellationToken ct = default)
    {
        var query = _context.Invoices
            .Include(i => i.LineItems)
            .AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(i =>
                i.InvoiceNumber.ToLower().Contains(term) ||
                (i.CompanyName != null && i.CompanyName.ToLower().Contains(term)) ||
                (i.ContactName != null && i.ContactName.ToLower().Contains(term)));
        }

        if (companyId.HasValue)
            query = query.Where(i => i.CompanyId == companyId);

        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<InvoiceStatus>(status, true, out var parsed))
            query = query.Where(i => i.Status == parsed);

        if (fromDate.HasValue)
            query = query.Where(i => i.IssueDate >= fromDate);

        if (toDate.HasValue)
            query = query.Where(i => i.IssueDate <= toDate);

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(i => i.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(i => new InvoiceDto(
                i.Id,
                i.InvoiceNumber,
                i.CompanyId,
                i.CompanyName,
                i.ContactId,
                i.ContactName,
                i.IssueDate,
                i.DueDate,
                i.TotalAmount,
                i.AmountPaid,
                i.BalanceDue,
                i.Currency,
                i.Status.ToString(),
                i.LineItems.Count,
                i.CreatedAt))
            .ToListAsync(ct);

        return new PagedResult<InvoiceDto>(items, totalCount, page, pageSize);
    }

    public async Task<InvoiceDetailDto?> GetInvoiceAsync(Guid id, CancellationToken ct = default)
    {
        return await _context.Invoices
            .Include(i => i.LineItems.OrderBy(li => li.LineNumber))
            .Include(i => i.Payments.OrderByDescending(p => p.PaymentDate))
            .AsNoTracking()
            .Where(i => i.Id == id)
            .Select(i => new InvoiceDetailDto(
                i.Id,
                i.InvoiceNumber,
                i.CompanyId,
                i.CompanyName,
                i.ContactId,
                i.ContactName,
                i.BillingAddress,
                i.ShippingAddress,
                i.IssueDate,
                i.DueDate,
                i.PaidDate,
                i.PaymentTerms,
                i.Subtotal,
                i.DiscountAmount,
                i.TaxAmount,
                i.TotalAmount,
                i.AmountPaid,
                i.BalanceDue,
                i.Currency,
                i.DiscountPercent,
                i.DiscountFixed,
                i.TaxRate,
                i.Status.ToString(),
                i.Notes,
                i.CustomerNotes,
                i.FooterText,
                i.CreatedAt,
                i.UpdatedAt,
                i.CreatedBy,
                i.LineItems.OrderBy(li => li.LineNumber).Select(li => new InvoiceLineItemDto(
                    li.Id,
                    li.LineNumber,
                    li.LineItemType.ToString(),
                    li.Description,
                    li.ProductId,
                    li.ProductName,
                    li.ProductSku,
                    li.ProductVariantId,
                    li.VariantDescription,
                    li.TimeEntryId,
                    li.EmployeeId,
                    li.EmployeeName,
                    li.ServiceDate,
                    li.Hours,
                    li.Quantity,
                    li.UnitOfMeasure,
                    li.UnitPrice,
                    li.DiscountPercent,
                    li.DiscountAmount,
                    li.LineTotal,
                    li.IsTaxable)).ToList(),
                i.Payments.OrderByDescending(p => p.PaymentDate).Select(p => new InvoicePaymentDto(
                    p.Id,
                    p.InvoiceId,
                    p.Amount,
                    p.Currency,
                    p.PaymentDate,
                    p.PaymentMethod.ToString(),
                    p.ReferenceNumber,
                    p.Notes,
                    p.CreatedAt,
                    p.CreatedBy)).ToList()))
            .FirstOrDefaultAsync(ct);
    }

    public async Task<InvoiceDto> CreateInvoiceAsync(
        CreateInvoiceRequest request, string userId, CancellationToken ct = default)
    {
        var settings = await _context.InvoiceSettings.FirstOrDefaultAsync(ct);
        var invoiceNumber = await _numberGenerator.GenerateNextAsync(ct);

        var issueDate = request.IssueDate ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var dueDays = settings?.DefaultDueDays ?? 30;
        var dueDate = request.DueDate ?? issueDate.AddDays(dueDays);

        var invoice = new Invoice
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantContext.TenantId,
            InvoiceNumber = invoiceNumber,
            CompanyId = request.CompanyId,
            CompanyName = request.CompanyName,
            ContactId = request.ContactId,
            ContactName = request.ContactName,
            BillingAddress = request.BillingAddress,
            ShippingAddress = request.ShippingAddress,
            IssueDate = issueDate,
            DueDate = dueDate,
            PaymentTerms = request.PaymentTerms ?? settings?.DefaultPaymentTerms ?? "Net 30",
            Currency = request.Currency,
            DiscountPercent = request.DiscountPercent,
            DiscountFixed = request.DiscountFixed,
            TaxRate = request.TaxRate ?? settings?.DefaultTaxRate,
            Notes = request.Notes,
            CustomerNotes = request.CustomerNotes ?? settings?.DefaultCustomerNotes,
            FooterText = request.FooterText ?? settings?.DefaultFooterText,
            Status = InvoiceStatus.Draft,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            CreatedBy = userId
        };

        if (request.LineItems is { Count: > 0 })
        {
            var lineNumber = 1;
            foreach (var li in request.LineItems)
            {
                var lineItem = CreateLineItemEntity(invoice.Id, li, lineNumber++);
                invoice.LineItems.Add(lineItem);
            }
        }

        RecalculateInvoiceTotals(invoice);
        _context.Invoices.Add(invoice);
        await _context.SaveChangesAsync(ct);

        return ToDto(invoice);
    }

    public async Task<InvoiceDto?> UpdateInvoiceAsync(
        Guid id, UpdateInvoiceRequest request, CancellationToken ct = default)
    {
        var invoice = await _context.Invoices
            .Include(i => i.LineItems)
            .FirstOrDefaultAsync(i => i.Id == id, ct);

        if (invoice == null) return null;
        if (invoice.Status != InvoiceStatus.Draft) return null;

        invoice.CompanyId = request.CompanyId;
        invoice.CompanyName = request.CompanyName;
        invoice.ContactId = request.ContactId;
        invoice.ContactName = request.ContactName;
        invoice.BillingAddress = request.BillingAddress;
        invoice.ShippingAddress = request.ShippingAddress;
        invoice.IssueDate = request.IssueDate;
        invoice.DueDate = request.DueDate;
        invoice.PaymentTerms = request.PaymentTerms;
        invoice.Currency = request.Currency;
        invoice.DiscountPercent = request.DiscountPercent;
        invoice.DiscountFixed = request.DiscountFixed;
        invoice.TaxRate = request.TaxRate;
        invoice.Notes = request.Notes;
        invoice.CustomerNotes = request.CustomerNotes;
        invoice.FooterText = request.FooterText;
        invoice.UpdatedAt = DateTimeOffset.UtcNow;

        RecalculateInvoiceTotals(invoice);
        await _context.SaveChangesAsync(ct);

        return ToDto(invoice);
    }

    public async Task<DeleteInvoiceResult> DeleteInvoiceAsync(Guid id, CancellationToken ct = default)
    {
        var invoice = await _context.Invoices.FirstOrDefaultAsync(i => i.Id == id, ct);
        if (invoice == null) return DeleteInvoiceResult.NotFound;
        if (invoice.Status != InvoiceStatus.Draft) return DeleteInvoiceResult.NotDraft;

        _context.Invoices.Remove(invoice);
        await _context.SaveChangesAsync(ct);
        return DeleteInvoiceResult.Deleted;
    }

    public async Task<InvoiceDto?> SendInvoiceAsync(Guid id, CancellationToken ct = default)
    {
        var invoice = await _context.Invoices
            .Include(i => i.LineItems)
            .FirstOrDefaultAsync(i => i.Id == id, ct);

        if (invoice == null || invoice.Status != InvoiceStatus.Draft) return null;
        if (invoice.LineItems.Count == 0) return null;

        invoice.Status = InvoiceStatus.Sent;
        invoice.UpdatedAt = DateTimeOffset.UtcNow;
        await _context.SaveChangesAsync(ct);

        return ToDto(invoice);
    }

    public async Task<InvoiceDto?> MarkOverdueAsync(Guid id, CancellationToken ct = default)
    {
        var invoice = await _context.Invoices
            .Include(i => i.LineItems)
            .FirstOrDefaultAsync(i => i.Id == id, ct);

        if (invoice == null) return null;
        if (invoice.Status != InvoiceStatus.Sent && invoice.Status != InvoiceStatus.PartiallyPaid) return null;

        invoice.Status = InvoiceStatus.Overdue;
        invoice.UpdatedAt = DateTimeOffset.UtcNow;
        await _context.SaveChangesAsync(ct);

        return ToDto(invoice);
    }

    public async Task<InvoiceDto?> CancelInvoiceAsync(Guid id, CancellationToken ct = default)
    {
        var invoice = await _context.Invoices
            .Include(i => i.LineItems)
            .FirstOrDefaultAsync(i => i.Id == id, ct);

        if (invoice == null) return null;
        if (invoice.Status != InvoiceStatus.Draft &&
            invoice.Status != InvoiceStatus.Sent &&
            invoice.Status != InvoiceStatus.Overdue)
            return null;

        invoice.Status = InvoiceStatus.Cancelled;
        invoice.UpdatedAt = DateTimeOffset.UtcNow;
        await _context.SaveChangesAsync(ct);

        return ToDto(invoice);
    }

    public async Task<InvoiceDto?> VoidInvoiceAsync(Guid id, CancellationToken ct = default)
    {
        var invoice = await _context.Invoices
            .Include(i => i.LineItems)
            .FirstOrDefaultAsync(i => i.Id == id, ct);

        if (invoice == null) return null;
        if (invoice.Status != InvoiceStatus.Sent &&
            invoice.Status != InvoiceStatus.PartiallyPaid &&
            invoice.Status != InvoiceStatus.Paid &&
            invoice.Status != InvoiceStatus.Overdue)
            return null;

        invoice.Status = InvoiceStatus.Void;
        invoice.UpdatedAt = DateTimeOffset.UtcNow;
        await _context.SaveChangesAsync(ct);

        return ToDto(invoice);
    }

    public async Task<InvoiceSummaryDto> GetSummaryAsync(CancellationToken ct = default)
    {
        var invoices = await _context.Invoices
            .AsNoTracking()
            .Select(i => new { i.Status, i.TotalAmount, i.BalanceDue, i.PaidDate })
            .ToListAsync(ct);

        var now = DateTimeOffset.UtcNow;
        var startOfMonth = new DateOnly(now.Year, now.Month, 1);

        return new InvoiceSummaryDto(
            TotalInvoices: invoices.Count,
            DraftCount: invoices.Count(i => i.Status == InvoiceStatus.Draft),
            SentCount: invoices.Count(i => i.Status == InvoiceStatus.Sent),
            OverdueCount: invoices.Count(i => i.Status == InvoiceStatus.Overdue),
            PaidCount: invoices.Count(i => i.Status == InvoiceStatus.Paid),
            TotalOutstanding: invoices
                .Where(i => i.Status is InvoiceStatus.Sent or InvoiceStatus.PartiallyPaid or InvoiceStatus.Overdue)
                .Sum(i => i.BalanceDue),
            TotalOverdue: invoices
                .Where(i => i.Status == InvoiceStatus.Overdue)
                .Sum(i => i.BalanceDue),
            TotalPaidThisMonth: invoices
                .Where(i => i.Status == InvoiceStatus.Paid && i.PaidDate.HasValue && i.PaidDate.Value >= startOfMonth)
                .Sum(i => i.TotalAmount));
    }

    internal static void RecalculateInvoiceTotals(Invoice invoice)
    {
        var subtotal = invoice.LineItems.Sum(li => li.LineTotal);
        invoice.Subtotal = subtotal;

        decimal discountAmount = 0;
        if (invoice.DiscountPercent.HasValue && invoice.DiscountPercent.Value > 0)
            discountAmount = subtotal * (invoice.DiscountPercent.Value / 100m);
        else if (invoice.DiscountFixed.HasValue && invoice.DiscountFixed.Value > 0)
            discountAmount = invoice.DiscountFixed.Value;
        invoice.DiscountAmount = discountAmount;

        decimal taxAmount = 0;
        if (invoice.TaxRate.HasValue && invoice.TaxRate.Value > 0)
        {
            var taxableSubtotal = invoice.LineItems.Where(li => li.IsTaxable).Sum(li => li.LineTotal);
            // Apply proportional discount to taxable amount
            var proportionalDiscount = subtotal > 0
                ? discountAmount * (taxableSubtotal / subtotal)
                : 0;
            taxAmount = (taxableSubtotal - proportionalDiscount) * (invoice.TaxRate.Value / 100m);
        }
        invoice.TaxAmount = taxAmount;

        invoice.TotalAmount = subtotal - discountAmount + taxAmount;
        invoice.BalanceDue = invoice.TotalAmount - invoice.AmountPaid;
    }

    internal static void CalculateLineTotal(InvoiceLineItem lineItem)
    {
        var gross = lineItem.Quantity * lineItem.UnitPrice;
        decimal discount = 0;
        if (lineItem.DiscountPercent.HasValue && lineItem.DiscountPercent.Value > 0)
            discount = gross * (lineItem.DiscountPercent.Value / 100m);
        else if (lineItem.DiscountAmount.HasValue && lineItem.DiscountAmount.Value > 0)
            discount = lineItem.DiscountAmount.Value;
        lineItem.LineTotal = gross - discount;
    }

    private InvoiceLineItem CreateLineItemEntity(Guid invoiceId, CreateInvoiceLineItemRequest li, int lineNumber)
    {
        Enum.TryParse<LineItemType>(li.LineItemType, true, out var itemType);

        var lineItem = new InvoiceLineItem
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantContext.TenantId,
            InvoiceId = invoiceId,
            LineNumber = lineNumber,
            LineItemType = itemType,
            Description = li.Description,
            ProductId = li.ProductId,
            ProductName = li.ProductName,
            ProductSku = li.ProductSku,
            ProductVariantId = li.ProductVariantId,
            VariantDescription = li.VariantDescription,
            TimeEntryId = li.TimeEntryId,
            EmployeeId = li.EmployeeId,
            EmployeeName = li.EmployeeName,
            ServiceDate = li.ServiceDate,
            Hours = li.Hours,
            Quantity = li.Quantity,
            UnitOfMeasure = li.UnitOfMeasure,
            UnitPrice = li.UnitPrice,
            DiscountPercent = li.DiscountPercent,
            DiscountAmount = li.DiscountAmount,
            IsTaxable = li.IsTaxable,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        CalculateLineTotal(lineItem);
        return lineItem;
    }

    private static InvoiceDto ToDto(Invoice i) => new(
        i.Id,
        i.InvoiceNumber,
        i.CompanyId,
        i.CompanyName,
        i.ContactId,
        i.ContactName,
        i.IssueDate,
        i.DueDate,
        i.TotalAmount,
        i.AmountPaid,
        i.BalanceDue,
        i.Currency,
        i.Status.ToString(),
        i.LineItems.Count,
        i.CreatedAt);
}
