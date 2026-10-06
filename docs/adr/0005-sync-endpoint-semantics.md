# ADR-0005: Sync endpoint — one connector per call, outcomes as data

- Status: accepted
- Date: 2026-08-21

## Context

The client needs holdings from every configured connector to compute net worth. Options: one batch endpoint syncing all connectors per request, or one call per connector with the client orchestrating. Also to decide: how connector failures map to HTTP, whether to wrap responses in an envelope, and how the API resolves a source id to a connector.

- **Chosen:** `POST /sync` syncs exactly one connector per call (`{source, credential}` → `ConnectorSyncResult`). The client owns the connector list (it lives in the vault) and the parallelism; net worth is computed from the vault, which accumulates per-source results — no server-side batch is needed.
  **Why:** per-connector calls enable progressive UI and per-connector retry, which matters concretely for Fio's 30 s rate limit — a batch endpoint would let one slow or rate-limited connector hold up every other result.
- **Chosen:** Connector outcomes (invalid credential, institution down, rate limited) are data: HTTP 200 with `SyncStatus` inside. HTTP 400 + ProblemDetails only when the request itself is malformed (unknown source, missing credential). No custom `Response<T>` envelope.
  **Why:** a connector having a bad day is an expected, routine outcome, not a flaw in the request — HTTP already has a status line for "the request itself was bad" (400) and RFC 7807 for its shape; a custom envelope would just duplicate what HTTP already gives for free.
- **Chosen:** The DI registry is the single source of truth for available connectors — the endpoint matches `request.source` against registered `IConnector.SourceId` values.
  **Why:** avoids a hand-maintained source enum that could drift from what's actually registered; a future `GET /connectors` or OpenAPI can expose the same list to the client without a second place to keep in sync.

## Consequences

Client code loops/parallelizes syncs and can render results as they land, showing stale-but-honest data from the vault meanwhile. One dead connector can never poison others' results. Trade-off: N connectors = N HTTP calls (irrelevant at our scale). Adding a connector = registering it in DI; the endpoint never changes.
