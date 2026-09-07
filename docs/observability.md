# Observability — how a request becomes a log row, and how to read it back

Companion to ADR-0011. Keep in sync when emitters, formatter, or transport change.

## The pipeline

```
Browser ──HTTPS POST /api/sync {source, credential}──►

 1. Container Apps ingress   TLS terminated; plain HTTP to the container on :8080.
                             Scale-from-zero → replica start event in ContainerAppSystemLogs_CL.
 2. Kestrel → ASP.NET pipeline
 3. UseHttpLogging           OUTERMOST. Starts timing, remembers method + path.
 4.  UseExceptionHandler     Crash → 500 ProblemDetails (no internals).
                             ▸ Error: exception, once. Category Microsoft.AspNetCore.Diagnostics.ExceptionHandlerMiddleware
 5.   Static files           not a file → pass through
 6.   /api/sync handler      400 ProblemDetails if malformed; resolves IConnector → LoggingConnector
 7.    LoggingConnector      starts timing
 8.     FioConnector         token in URL path; HttpClient has NO loggers → emits nothing
                             maps response/timeout → SyncStatus
 7.    LoggingConnector      ▸ "Sync {Source} finished with {Status} in {ElapsedMs} ms"
                               Information if Ok, else Warning. Category ...Logging.LoggingConnector
 6.   handler                200 + ConnectorSyncResult (enums as strings)
 3. UseHttpLogging           ▸ "Request and Response: Method POST, Path /api/sync, StatusCode 200, Duration N"
                               Information. Category Microsoft.AspNetCore.HttpLogging.HttpLoggingMiddleware
◄── 200 to the browser (IBAN + balances travel here, and only here)

 9. ILogger filter           appsettings Logging:LogLevel by category prefix (longest wins)
10. Console provider         JSON formatter, IncludeScopes=true → ONE JSON object per line on stdout:
                             {Timestamp, LogLevel, Category, Message, State{placeholders…}, Scopes[{RequestId, RequestPath}, {TraceId, SpanId}, …]}
11. Container Apps agent     one row PER LINE (= one JSON object); adds TimeGenerated, RevisionName_s, ReplicaName_s
12. Log Analytics            ContainerAppConsoleLogs_CL (Log_s holds the JSON) + ContainerAppSystemLogs_CL; 30-day retention
13. You                      KQL in the portal, or live: az containerapp logs show -n ca-portfoliotracker -g rg-portfoliotracker --follow
```

## Correlation

Every response carries `X-Request-Id` = ASP.NET's `HttpContext.TraceIdentifier` (e.g. `0HNOCPCO6ICK3:00000001`),
set in an `OnStarting` callback so it survives the exception handler's `Response.Clear()`. The same value is the
`RequestId` scope on every log line of that request (hosting adds it; `IncludeScopes` prints it). The client shows
it as "Reference for support" whenever a sync is not `Ok`. It identifies a request, never a user.

A real pair, one sync with an unknown token (Fio stalls → 30 s timeout → `Unavailable`):

```json
{"Timestamp":"2026-09-07T10:32:04.113Z","LogLevel":"Warning","Category":"PortfolioTrackerApp.Connectors.Logging.LoggingConnector",
 "Message":"Sync fio finished with Unavailable in 30031 ms",
 "State":{"Source":"fio","Status":"Unavailable","ElapsedMs":30031},
 "Scopes":[{"TraceId":"b4083b29…","SpanId":"fd8bbded…"},{"ConnectionId":"0HNOCPCO6ICK3"},{"RequestId":"0HNOCPCO6ICK3:00000001","RequestPath":"/api/sync"}]}
{"Timestamp":"2026-09-07T10:32:04.126Z","LogLevel":"Information","Category":"Microsoft.AspNetCore.HttpLogging.HttpLoggingMiddleware",
 "State":{"Method":"POST","Path":"/api/sync","StatusCode":200,"Duration":30055.46},
 "Scopes":[…,{"RequestId":"0HNOCPCO6ICK3:00000001","RequestPath":"/api/sync"}]}
```

## What one sync leaves behind

| Outcome | Lines in ContainerAppConsoleLogs_CL |
|---|---|
| Ok | `Sync fio finished with Ok in 412 ms` (Information) · request line `200` |
| bad token | `Sync fio finished with InvalidCredential in 380 ms` (Warning) · request line `200` |
| Fio down / rate-limit stall | `Sync fio finished with Unavailable in 30012 ms` (Warning) · request line `200` |
| connector threw | Error line with exception · request line `500` (no outcome line — the decorator never resumed) |

Never present, by construction and by test: the credential, the outbound URL, request/response bodies, the IBAN.

## Queries

`Log_s` is JSON, so parse once and work with fields. A reusable prefix:

```kql
let App = ContainerAppConsoleLogs_CL
| extend j = parse_json(Log_s)
| extend Level = tostring(j.LogLevel), Category = tostring(j.Category), Message = tostring(j.Message),
         RequestId = tostring(j.Scopes[2].RequestId), Path = tostring(j.Scopes[2].RequestPath),
         Source = tostring(j.State.Source), Status = tostring(j.State.Status), ElapsedMs = toint(j.State.ElapsedMs),
         HttpStatus = toint(j.State.StatusCode), Method = tostring(j.State.Method);
```
(`Scopes[2]` is the hosting scope in today's shape; if it moves, use `mv-apply s = j.Scopes on (where isnotempty(s.RequestId))`.)

A user quotes a reference — everything about exactly that request:
```kql
App | where RequestId == "0HNOCPCO6ICK3:00000001" | project TimeGenerated, Level, Category, Message, Status, HttpStatus, ElapsedMs
```

Prove a secret never landed (must return zero rows):
```kql
ContainerAppConsoleLogs_CL | where Log_s contains "<the token you used>"
```

Failures in a window:
```kql
App
| where TimeGenerated between (datetime(2026-09-03 14:30) .. datetime(2026-09-03 14:35))
| where Status in ("Unavailable", "InvalidCredential", "RateLimited")
| project TimeGenerated, Source, Status, ElapsedMs, RequestId
```

Outcomes per hour — bank outage (many Unavailable) vs. one user's dead token (one InvalidCredential):
```kql
App | where isnotempty(Status) | summarize count() by Status, bin(TimeGenerated, 1h)
```

Slow syncs (the 30 s stall is Fio's rate limit or outage):
```kql
App | where ElapsedMs > 10000 | project TimeGenerated, Source, Status, ElapsedMs, RequestId
```

Crashes:
```kql
App | where Level == "Error" | project TimeGenerated, Category, Message, RequestId
```

HTTP status distribution (5xx here means OUR bug, not the bank's):
```kql
App | where isnotempty(HttpStatus) | summarize count() by HttpStatus, Path, bin(TimeGenerated, 1h)
```

Platform events (scale, probes, image pull):
```kql
ContainerAppSystemLogs_CL | project TimeGenerated, Reason_s, Log_s | order by TimeGenerated desc
```

## Known gaps (see ADR-0011)

1. No App Insights / OpenTelemetry yet; when added, its dependency tracking hooks outbound HTTP directly —
   redact the Fio client's outbound URL. Correlate on the W3C `TraceId` scope then (ProblemDetails `traceId` already is it).
2. Failure references are shown in the UI but not persisted in the vault; a user asking about yesterday's failure has nothing to quote.
