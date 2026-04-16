namespace WarpBusiness.Catalog.Models;

/// <summary>
/// Icon types for catalog notations (warnings, cautions, compliance labels, etc.).
/// Each value maps to a Bootstrap Icons CSS class.
/// </summary>
public enum NotationIcon
{
    /// <summary>No icon displayed.</summary>
    None,

    /// <summary>General warning symbol. Bootstrap icon: bi-exclamation-triangle-fill</summary>
    Warning,

    /// <summary>Information symbol. Bootstrap icon: bi-info-circle-fill</summary>
    Info,

    /// <summary>Note or documentation symbol. Bootstrap icon: bi-journal-text</summary>
    Note,

    /// <summary>Caution symbol. Bootstrap icon: bi-exclamation-circle-fill</summary>
    Caution,

    /// <summary>Danger or critical hazard symbol. Bootstrap icon: bi-x-octagon-fill</summary>
    Danger,

    /// <summary>Prohibited or restricted symbol. Bootstrap icon: bi-slash-circle</summary>
    Prohibited,

    /// <summary>Flammable materials symbol. Bootstrap icon: bi-fire</summary>
    Flammable,

    /// <summary>Chemical hazard symbol. Bootstrap icon: bi-droplet-fill</summary>
    Chemical,

    /// <summary>Electrical hazard symbol. Bootstrap icon: bi-lightning-fill</summary>
    ElectricalHazard,

    /// <summary>Recyclable materials symbol. Bootstrap icon: bi-recycle</summary>
    Recyclable,

    /// <summary>Eco-friendly or environmental symbol. Bootstrap icon: bi-leaf-fill</summary>
    EcoFriendly,

    /// <summary>Food allergen warning. Bootstrap icon: bi-egg-fill</summary>
    FoodAllergen,

    /// <summary>California Prop 65 warning. Bootstrap icon: bi-exclamation-diamond-fill</summary>
    Prop65,

    /// <summary>Compliance or certification mark. Bootstrap icon: bi-shield-check-fill</summary>
    Compliance,

    /// <summary>Temperature-sensitive symbol. Bootstrap icon: bi-thermometer-half</summary>
    Temperature
}
