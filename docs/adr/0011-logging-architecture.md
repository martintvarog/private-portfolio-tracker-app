# ADR-0011: Logging — outcome line per sync, request line per HTTP call, nothing personal

- Status: accepted
- Date: 2026-09-03

## Context

Once deployed (ADR-0007) the app emitted nothing on the one path a user would ask about: by ADR-0005 a failed sync is a 200 with `SyncStatus` in the body — no exception, no warning, no log line. Meanwhile the product law "the operator can't see your data" forbids the usual fixes: the Fio token travels in the outbound URL path, the sync request body carries the credential, the response body carries the IBAN and balances. Any general-purpose logging (HttpClient factory loggers, `HttpLoggingFields.All`, App Insights dependency tracking) would record exactly what must never be recorded. Also found while investigating: with no exception handler, an unhandled exception in Production left no log line and the request log recorded `200` for a crash.

## Decision

- **Two emitters, both cross-cutting, no `ILogger` in connectors or endpoints.**
  - `LoggingConnector`, a decorator around every `IConnector` (wired in `AddConnectorsModule` via `AddConnector<T>()`): one structured line per sync, `Sync {Source} finished with {Status} in {ElapsedMs} ms` — `Information` for `Ok`, `Warning` for any other status. Those three placeholders and nothing else.
  - `UseHttpLogging` with an explicit allow-list — `RequestMethod | RequestPath | ResponseStatusCode | Duration`, `CombineLogs = true`. Never bodies, never headers. Requires `Microsoft.AspNetCore.HttpLogging: Information` in `appsettings.json` because `Microsoft.AspNetCore` is at `Warning`.
- **Pipeline order:** `UseHttpLogging` outermost, `UseExceptionHandler` (+ `AddProblemDetails`) inside it, then static files and endpoints. The handler turns a crash into an RFC 7807 500 with no internals and the framework logs the exception once at `Error`; because HttpLogging wraps it, the request line records the real status.
- **The bank `HttpClient` has no loggers** (`RemoveAllLoggers()`); the `IConnector` law extends to exception messages, since the framework logs unhandled exceptions verbatim.
- **Transport is stdout → Container Apps → Log Analytics** (`ContainerAppConsoleLogs_CL`, plus `ContainerAppSystemLogs_CL` for platform events), 30-day retention. No second provider yet.
- **Test-guarded at three layers** (decorator unit; real DI with stubbed Fio HTTP capturing every category; the real app in-process in `Production`): the request and outcome lines exist with the expected content and level; no line in any category contains the credential, IBAN, balance, outbound URL, or body markers; the HttpClient handler chain has no logging handler. Two control tests build the leaky configuration on purpose and assert the leak happens, proving the guards are sensitive.

## Consequences

"What happened at 14:32" is answerable from two adjacent lines: outcome (with status and duration) and request (with HTTP status). A bad token or a bank outage is a `Warning`, distinguishable by status; a crash is an `Error` plus a `500` request line. Removing `RemoveAllLoggers()`, widening the HttpLogging fields, or breaking the middleware order fails CI with a message naming the leak.

Known gaps, deliberately deferred: (1) the two lines share no correlation ID — pairing is by timestamp, fine for one user, not for many (`HttpContext.TraceIdentifier` via a logging scope is the fix); (2) the console formatter is plain text, so `Status` is a substring, not a field — the JSON console formatter (`Logging:Console:FormatterName=json`) makes placeholders queryable and avoids multi-line entries splitting into several rows; (3) App Insights/OpenTelemetry, when added, hooks outbound HTTP directly and will need URL redaction for the Fio client — a fourth test layer.

Rejected: logging inside `FioConnector` (repeats per connector, puts an `ILogger` next to the token); an endpoint filter (couples the outcome log to HTTP); a custom `IExceptionHandler` that drops exception messages (loses the most useful diagnostic for a risk that does not exist in the code today); `HttpLoggingFields.All` (logs the credential).
