namespace WarpBusiness.Catalog.Models;

/// <summary>
/// Safe parser for <see cref="NotationIcon"/> values read from the database.
/// EF Core's default string-enum converter throws <see cref="InvalidOperationException"/>
/// when it encounters a stored value that doesn't match any enum name (e.g., legacy emoji
/// like '⚠' stored before the enum was introduced). This helper returns <c>null</c>
/// instead of throwing, making the conversion resilient to stale data.
/// </summary>
public static class NotationIconParser
{
    /// <summary>
    /// Parses a database string into a <see cref="NotationIcon"/> value, or returns
    /// <c>null</c> if the string is empty, null, or not a recognised enum name.
    /// </summary>
    public static NotationIcon? ParseOrNull(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return null;

        return Enum.TryParse<NotationIcon>(value, ignoreCase: false, out var result) && Enum.IsDefined(result)
            ? result
            : null;
    }
}
