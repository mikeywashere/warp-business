using Microsoft.EntityFrameworkCore;
using WarpBusiness.Plugin.Abstractions;
using WarpBusiness.Plugin.Invoicing.Data;
using WarpBusiness.Plugin.Invoicing.Domain;
using WarpBusiness.Shared.Invoicing;

namespace WarpBusiness.Plugin.Invoicing.Services;

public class InvoiceLineItemService : IInvoiceLineItemService
{
    private readonly InvoicingDbContext _context;
    private readonly ITenantContext _tenantContext;

    public InvoiceLineItemService(InvoicingDbContext context, ITenantContext tenantContext)
    {
        _context = context;
        _tenantContext = tenantContext;
    }

    public async Task<IReadOnlyList<InvoiceLineItemDto>> GetLineItemsAsync(
        Guid invoiceId, CancellationToken ct = default)
    {
        return await _context.InvoiceLineItems
            .AsNoTracking()
            .Where(li => li.InvoiceId == invoiceId)
            .OrderBy(li => li.LineNumber)
            .Select(li => ToDto(li))
            .ToListAsync(ct);
    }

    public async Task<InvoiceLineItemDto> AddLineItemAsync(
        Guid invoiceId, CreateInvoiceLineItemRequest request, CancellationToken ct = default)
    {
        var invoice = await _context.Invoices
            .Include(i => i.LineItems)
            .FirstOrDefaultAsync(i => i.Id == invoiceId, ct)
            ?? throw new InvalidOperationException("Invoice not found.");

        if (invoice.Status != InvoiceStatus.Draft)
            throw new InvalidOperationException("Line items can only be added to draft invoices.");

        var maxLineNumber = invoice.LineItems.Count > 0
            ? invoice.LineItems.Max(li => li.LineNumber)
            : 0;

        Enum.TryParse<LineItemType>(request.LineItemType, true, out var itemType);

        var lineItem = new InvoiceLineItem
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantContext.TenantId,
            InvoiceId = invoiceId,
            LineNumber = maxLineNumber + 1,
            LineItemType = itemType,
            Description = request.Description,
            ProductId = request.ProductId,
            ProductName = request.ProductName,
            ProductSku = request.ProductSku,
            ProductVariantId = request.ProductVariantId,
            VariantDescription = request.VariantDescription,
            TimeEntryId = request.TimeEntryId,
            EmployeeId = request.EmployeeId,
            EmployeeName = request.EmployeeName,
            ServiceDate = request.ServiceDate,
            Hours = request.Hours,
            Quantity = request.Quantity,
            UnitOfMeasure = request.UnitOfMeasure,
            UnitPrice = request.UnitPrice,
            DiscountPercent = request.DiscountPercent,
            DiscountAmount = request.DiscountAmount,
            IsTaxable = request.IsTaxable,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        InvoiceService.CalculateLineTotal(lineItem);
        _context.InvoiceLineItems.Add(lineItem);
        // EF auto-fixup adds lineItem to invoice.LineItems via InvoiceId FK
        InvoiceService.RecalculateInvoiceTotals(invoice);
        invoice.UpdatedAt = DateTimeOffset.UtcNow;

        await _context.SaveChangesAsync(ct);
        return ToDto(lineItem);
    }

    public async Task<InvoiceLineItemDto?> UpdateLineItemAsync(
        Guid invoiceId, Guid lineItemId,
        UpdateInvoiceLineItemRequest request, CancellationToken ct = default)
    {
        var invoice = await _context.Invoices
            .Include(i => i.LineItems)
            .FirstOrDefaultAsync(i => i.Id == invoiceId, ct);

        if (invoice == null || invoice.Status != InvoiceStatus.Draft) return null;

        var lineItem = invoice.LineItems.FirstOrDefault(li => li.Id == lineItemId);
        if (lineItem == null) return null;

        lineItem.Description = request.Description;
        lineItem.Quantity = request.Quantity;
        lineItem.UnitOfMeasure = request.UnitOfMeasure;
        lineItem.UnitPrice = request.UnitPrice;
        lineItem.DiscountPercent = request.DiscountPercent;
        lineItem.DiscountAmount = request.DiscountAmount;
        lineItem.IsTaxable = request.IsTaxable;
        lineItem.UpdatedAt = DateTimeOffset.UtcNow;

        InvoiceService.CalculateLineTotal(lineItem);
        InvoiceService.RecalculateInvoiceTotals(invoice);
        invoice.UpdatedAt = DateTimeOffset.UtcNow;

        await _context.SaveChangesAsync(ct);
        return ToDto(lineItem);
    }

    public async Task<bool> DeleteLineItemAsync(
        Guid invoiceId, Guid lineItemId, CancellationToken ct = default)
    {
        var invoice = await _context.Invoices
            .Include(i => i.LineItems)
            .FirstOrDefaultAsync(i => i.Id == invoiceId, ct);

        if (invoice == null || invoice.Status != InvoiceStatus.Draft) return false;

        var lineItem = invoice.LineItems.FirstOrDefault(li => li.Id == lineItemId);
        if (lineItem == null) return false;

        invoice.LineItems.Remove(lineItem);
        _context.InvoiceLineItems.Remove(lineItem);

        InvoiceService.RecalculateInvoiceTotals(invoice);
        invoice.UpdatedAt = DateTimeOffset.UtcNow;

        await _context.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> ReorderLineItemsAsync(
        Guid invoiceId, IReadOnlyList<Guid> orderedIds, CancellationToken ct = default)
    {
        var invoice = await _context.Invoices
            .Include(i => i.LineItems)
            .FirstOrDefaultAsync(i => i.Id == invoiceId, ct);

        if (invoice == null || invoice.Status != InvoiceStatus.Draft) return false;

        for (var i = 0; i < orderedIds.Count; i++)
        {
            var lineItem = invoice.LineItems.FirstOrDefault(li => li.Id == orderedIds[i]);
            if (lineItem != null)
                lineItem.LineNumber = i + 1;
        }

        invoice.UpdatedAt = DateTimeOffset.UtcNow;
        await _context.SaveChangesAsync(ct);
        return true;
    }

    private static InvoiceLineItemDto ToDto(InvoiceLineItem li) => new(
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
        li.IsTaxable);
}
