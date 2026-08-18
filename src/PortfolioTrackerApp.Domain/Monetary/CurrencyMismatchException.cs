namespace PortfolioTrackerApp.Domain.Monetary;

/// <summary>
/// Thrown when amounts in different currencies are combined without an explicit
/// exchange rate. Silent coercion is how trackers lie; mixing currencies is always
/// a bug or a missing FX-conversion step, never something to guess through.
/// </summary>
public sealed class CurrencyMismatchException : InvalidOperationException
{
    public CurrencyMismatchException(string operation, string leftCurrency, string rightCurrency)
        : base($"Cannot {operation} {leftCurrency} and {rightCurrency}: different currencies require an explicit exchange rate.")
    {
    }
}
