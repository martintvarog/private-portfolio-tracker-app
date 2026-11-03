# Integrations

Each source the backend reads from has a connector (`IConnector`) and a page here with
the source's facts and quirks. Adding a connector = implement `IConnector`, register it
in `AddConnectorsModule` via `AddConnector<T>`, add a page here.

| Source id | Institution | Page |
|---|---|---|
| `fio` | Fio banka (CZ) | [fio.md](fio.md) |

## Connector laws

Every `IConnector` implementation must obey these.

1. **Outcomes are data.** Business failures (bad credential, source down, rate limit) are
   returned as `SyncStatus`, never thrown. Exceptions are only for programming errors and
   caller cancellation (ADR-0005).
2. **Idempotent.** Calling twice returns the same positions and changes no state anywhere,
   ours or the source's.
3. **Source-tagged.** Every result carries the connector's `SourceId`, so the client knows
   which stored positions to replace.
4. **Credentials pass through.** Never stored, never logged, including inside request URLs
   and **exception messages**: the framework logs unhandled exceptions verbatim (ADR-0011).
5. **Skip loudly.** Unsupported instruments go to `Warnings`, never silently dropped: the
   number shown to the user must be honest about its gaps.
6. **No logging inside connectors.** `AddConnector<T>` removes the HttpClient's loggers and
   wraps the connector in `LoggingConnector`, the only place sync activity is logged (ADR-0011).
