namespace PortfolioTrackerApp.Domain.Monetary.Currency;

/// <summary>
/// Amounts in different currencies were combined without an exchange rate. Always a bug or a
/// missing FX step; the domain never converts silently.
/// </summary>
public sealed class CurrencyMismatchException : InvalidOperationException
{
    public CurrencyMismatchException(string operation, string leftCurrency, string rightCurrency)
        : base($"Cannot {operation} {leftCurrency} and {rightCurrency}: different currencies require an explicit exchange rate.")
    {
    }
}
