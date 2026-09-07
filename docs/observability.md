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
10. Console provider         simple formatter → text on stdout (placeholders flattened; HttpLogging entry is multi-line)
11. Container Apps agent     one row PER LINE; adds TimeGenerated, RevisionName_s, ReplicaName_s
12. Log Analytics            ContainerAppConsoleLogs_CL (Log_s) + ContainerAppSystemLogs_CL; 30-day retention
13. You                      KQL in the portal, or live: az containerapp logs show -n ca-portfoliotracker -g rg-portfoliotracker --follow
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

Prove a secret never landed (must return zero rows):
```kql
ContainerAppConsoleLogs_CL
| where Log_s contains "<the token you used>"
```

Failures in a window:
```kql
ContainerAppConsoleLogs_CL
| where TimeGenerated between (datetime(2026-09-03 14:30) .. datetime(2026-09-03 14:35))
| where Log_s has "Sync" and (Log_s has "Unavailable" or Log_s has "InvalidCredential" or Log_s has "RateLimited")
| project TimeGenerated, Log_s
| order by TimeGenerated asc
```

Everything around a moment (pair the outcome line with its request line by time):
```kql
ContainerAppConsoleLogs_CL
| where TimeGenerated between (datetime(2026-09-03 14:32:00) .. datetime(2026-09-03 14:33:00))
| project TimeGenerated, Log_s
| order by TimeGenerated asc
```

Outcomes per hour (regex until the JSON formatter is on):
```kql
ContainerAppConsoleLogs_CL
| where Log_s has "finished with"
| extend Status = extract(@"finished with (\w+)", 1, Log_s)
| summarize count() by Status, bin(TimeGenerated, 1h)
```

Crashes:
```kql
ContainerAppConsoleLogs_CL
| where Log_s has "ExceptionHandlerMiddleware" or Log_s has "Exception:"
| project TimeGenerated, Log_s
```

Platform events (scale, probes, image pull):
```kql
ContainerAppSystemLogs_CL
| project TimeGenerated, Reason_s, Log_s
| order by TimeGenerated desc
```

## Known gaps (see ADR-0011)

1. No correlation ID between the outcome line and the request line.
2. Plain-text formatter: `Status` is a substring, not a field; multi-line entries become several rows.
3. No App Insights / OpenTelemetry yet; when added, redact the Fio client's outbound URL.
