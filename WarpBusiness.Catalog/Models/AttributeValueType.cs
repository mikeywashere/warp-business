namespace WarpBusiness.Catalog.Models;

public enum AttributeValueType
{
    /// <summary>Value is chosen from a predefined list of options.</summary>
    Select,
    /// <summary>Value is a free-form string.</summary>
    FreeText,
    /// <summary>Value is a numeric quantity, optionally with a unit (e.g., 256 GB, 2.5 kg).</summary>
    Number
}
