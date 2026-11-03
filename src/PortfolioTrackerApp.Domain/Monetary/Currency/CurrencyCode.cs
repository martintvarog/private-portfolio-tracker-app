namespace PortfolioTrackerApp.Domain.Monetary.Currency;

/// <summary>
/// Currency code rule: 3 ASCII letters, trimmed and upper-cased. Checks the shape only, not
/// the ISO-4217 list, so unknown but well-formed codes are accepted.
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
