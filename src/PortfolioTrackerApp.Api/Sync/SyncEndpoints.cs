using PortfolioTrackerApp.Connectors.Contracts;

namespace PortfolioTrackerApp.Api.Sync;

/// <summary>Body of <c>POST /api/sync</c>. Both fields are required.</summary>
/// <param name="Source">Source to sync: a connector's <see cref="IConnector.SourceId"/>, case-insensitive, e.g. "fio".</param>
/// <param name="Credential">Source-specific secret, never stored or logged, e.g. the Fio API token from Fio internet banking.</param>
public sealed record SyncRequest(string? Source, string? Credential);

public static class SyncEndpoints
{
    public static IEndpointRouteBuilder MapSyncEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/sync", Sync);
        return app;
    }

    /// <summary>
    /// <c>POST /api/sync</c> — fetches current positions from one source (ADR-0005).
    /// 200: <see cref="ConnectorSyncResult"/>, also when the source fails — the outcome is its <see cref="SyncStatus"/>.
    /// 400: ProblemDetails when source or credential is missing, or the source is unknown.
    /// </summary>
    private static async Task<IResult> Sync(
        SyncRequest request,
        IEnumerable<IConnector> connectors,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Source))
            return Results.Problem(title: "Source is required.", statusCode: StatusCodes.Status400BadRequest);

        if (string.IsNullOrWhiteSpace(request.Credential))
            return Results.Problem(title: "Credential is required.", statusCode: StatusCodes.Status400BadRequest);

        var connector = connectors.FirstOrDefault(c =>
            string.Equals(c.SourceId, request.Source, StringComparison.OrdinalIgnoreCase));

        if (connector is null)
            return Results.Problem(
                title: $"Unknown source '{request.Source}'.",
                statusCode: StatusCodes.Status400BadRequest);

        var result = await connector.FetchPositionsAsync(request.Credential, cancellationToken);
        return Results.Ok(result);
    }
}
