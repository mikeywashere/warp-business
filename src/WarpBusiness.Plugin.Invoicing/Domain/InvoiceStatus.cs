namespace WarpBusiness.Plugin.Invoicing.Domain;

public enum InvoiceStatus
{
    Draft,
    Sent,
    Paid,
    PartiallyPaid,
    Overdue,
    Cancelled,
    Void
}
