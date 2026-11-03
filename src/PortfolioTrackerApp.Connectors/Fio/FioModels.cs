using System.Text.Json.Serialization;

namespace PortfolioTrackerApp.Connectors.Fio;

internal sealed record FioExport(
    [property: JsonPropertyName("accountStatement")] FioAccountStatement? AccountStatement);

internal sealed record FioAccountStatement(
    [property: JsonPropertyName("info")] FioInfo? Info);

internal sealed record FioInfo(
    [property: JsonPropertyName("accountId")] string? AccountId,
    [property: JsonPropertyName("currency")] string? Currency,
    [property: JsonPropertyName("iban")] string? Iban,
    [property: JsonPropertyName("closingBalance")] decimal ClosingBalance);
