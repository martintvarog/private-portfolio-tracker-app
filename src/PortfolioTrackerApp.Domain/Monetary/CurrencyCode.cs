namespace PortfolioTrackerApp.Domain.Monetary;

/// <summary>
/// Shared validation for ISO-4217 currency codes ("CZK", "EUR").
/// Structural check only (3 ASCII letters) — we deliberately don't maintain the
/// real ISO list; unknown-but-well-formed codes must work (e.g. crypto tickers later).
/// </summary>
internal static class CurrencyCode
{
    public static string Normalize(string currency, string paramName)
    {
        if (string.IsNullOrWhiteSpace(currency))
            throw new ArgumentException("Currency code is required.", paramName);

        var normalized = currency.Trim().ToUpperInvariant();
        if (normalized.Length != 3 || !normalized.All(char.IsAsciiLetterUpper))
            throw new ArgumentException($"'{currency}' is not a valid ISO-4217 currency code.", paramName);

        return normalized;
    }
}
