using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WarpBusiness.Api.Data;
using WarpBusiness.Api.Models;

namespace WarpBusiness.Api.Endpoints;

public static class CurrencyEndpoints
{
    public static void MapCurrencyEndpoints(this WebApplication app)
    {
        var currencies = app.MapGroup("/api/currencies")
            .RequireAuthorization();

        currencies.MapGet("/", GetAllCurrencies)
            .WithName("GetAllCurrencies");

        currencies.MapGet("/active", GetActiveCurrencies)
            .WithName("GetActiveCurrencies");

        currencies.MapGet("/{code}", GetCurrencyByCode)
            .WithName("GetCurrencyByCode");

        currencies.MapPut("/{code}", UpdateCurrency)
            .WithName("UpdateCurrency")
            .RequireAuthorization("SystemAdministrator");

        currencies.MapPost("/refresh", RefreshCurrencies)
            .WithName("RefreshCurrencies")
            .RequireAuthorization("SystemAdministrator");
    }

    private static async Task<IResult> GetAllCurrencies(
        WarpBusinessDbContext db,
        CancellationToken cancellationToken)
    {
        var currencies = await db.Currencies
            .OrderBy(c => c.Code)
            .Select(c => ToResponse(c))
            .ToListAsync(cancellationToken);

        return Results.Ok(currencies);
    }

    private static async Task<IResult> GetActiveCurrencies(
        WarpBusinessDbContext db,
        CancellationToken cancellationToken)
    {
        var currencies = await db.Currencies
            .Where(c => c.IsActive)
            .OrderBy(c => c.Name)
            .Select(c => ToResponse(c))
            .ToListAsync(cancellationToken);

        return Results.Ok(currencies);
    }

    private static async Task<IResult> GetCurrencyByCode(
        string code,
        WarpBusinessDbContext db,
        CancellationToken cancellationToken)
    {
        var currency = await db.Currencies.FindAsync([code.ToUpperInvariant()], cancellationToken);
        if (currency is null)
            return Results.NotFound();

        return Results.Ok(ToResponse(currency));
    }

    private static async Task<IResult> UpdateCurrency(
        string code,
        [FromBody] UpdateCurrencyRequest request,
        WarpBusinessDbContext db,
        CancellationToken cancellationToken)
    {
        var currency = await db.Currencies.FindAsync([code.ToUpperInvariant()], cancellationToken);
        if (currency is null)
            return Results.NotFound();

        currency.IsActive = request.IsActive;
        await db.SaveChangesAsync(cancellationToken);

        return Results.Ok(ToResponse(currency));
    }

    private static async Task<IResult> RefreshCurrencies(
        WarpBusinessDbContext db,
        ILogger<WarpBusinessDbContext> logger,
        CancellationToken cancellationToken)
    {
        var seedCurrencies = CurrencySeedData.GetAllCurrencies();
        var existingCodes = await db.Currencies
            .Select(c => c.Code)
            .ToHashSetAsync(cancellationToken);

        var added = 0;
        foreach (var currency in seedCurrencies)
        {
            if (!existingCodes.Contains(currency.Code))
            {
                db.Currencies.Add(currency);
                added++;
            }
        }

        if (added > 0)
        {
            await db.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Added {Count} new currencies from refresh", added);
        }

        return Results.Ok(new { message = $"Refresh complete. {added} new currencies added. Total: {existingCodes.Count + added}." });
    }

    private static CurrencyResponse ToResponse(Currency c) =>
        new(c.Code, c.Name, c.Symbol, c.NumericCode, c.MinorUnit, c.IsActive);
}
