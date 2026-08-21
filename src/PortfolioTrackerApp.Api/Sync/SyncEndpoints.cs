using PortfolioTrackerApp.Connectors.Contracts;

namespace PortfolioTrackerApp.Api.Sync;

/// <summary>One connector per call; the client owns the connector list and parallelism.</summary>
public sealed record SyncRequest(string? Source, string? Credential);

public static class SyncEndpoints
{
    public static IEndpointRouteBuilder MapSyncEndpoints(this IEndpointRouteBuilder app)
    {
        // 400 = the request itself is broken; connector outcomes (dead token, bank down)
        // are data inside a 200 — see SyncStatus.
        app.MapPost("/sync", async (
            SyncRequest request,
            IEnumerable<IConnector> connectors,
            CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(request.Source))
                return Results.Problem(title: "Source is required.", statusCode: StatusCodes.Status400BadRequest);

            if (string.IsNullOrWhiteSpace(request.Credential))
                return Results.Problem(title: "Credential is required.", statusCode: StatusCodes.Status400BadRequest);

            // The DI registry is the single source of truth for which connectors exist.
            var connector = connectors.FirstOrDefault(c =>
                string.Equals(c.SourceId, request.Source, StringComparison.OrdinalIgnoreCase));

            if (connector is null)
                return Results.Problem(
                    title: $"Unknown source '{request.Source}'.",
                    statusCode: StatusCodes.Status400BadRequest);

            var result = await connector.FetchHoldingsAsync(request.Credential, cancellationToken);
            return Results.Ok(result);
        });

        return app;
    }
}
