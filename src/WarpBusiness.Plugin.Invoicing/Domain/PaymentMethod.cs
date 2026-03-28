namespace WarpBusiness.Plugin.Invoicing.Domain;

public enum PaymentMethod
{
    Cash,
    Check,
    CreditCard,
    DebitCard,
    BankTransfer,
    Wire,
    PayPal,
    Stripe,
    Other
}
