# ADR-0010: API enums serialize as strings

- Status: accepted
- Date: 2026-08-21 (recorded 2026-09-03)

## Context

`ConnectorSyncResult` carries `SyncStatus` (Ok, InvalidCredential, Unavailable, RateLimited, …), and more enums will follow. System.Text.Json's default is numeric enum values. The client is TypeScript and the results are stored long-term in the vault (ADR-0009).

## Decision

- **Chosen:** A global `JsonStringEnumConverter` in `ConfigureHttpJsonOptions` — every enum crosses the wire as its name (`"RateLimited"`), never as a number. The client models them as string-literal union types.
- **Why:** vault data (ADR-0009) outlives any single API version. With numeric enums, reordering or inserting a member on the server would silently change the meaning of values already persisted in a user's vault — a quiet, undetectable corruption. Strings carry their meaning with them.

## Consequences

Payloads are self-describing and readable in DevTools and logs. Cost: a few more bytes per field, and one place to remember when a new enum is introduced (none — the converter is global). **Risk:** renaming an enum member is now a breaking change for already-stored data and must be handled by a vault migration.
