namespace WarpBusiness.Api.Models;

public record CurrencyResponse(string Code, string Name, string? Symbol, string? NumericCode, int? MinorUnit, bool IsActive);
public record UpdateCurrencyRequest(bool IsActive);
