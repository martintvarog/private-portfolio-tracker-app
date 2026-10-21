# Glossary — ubiquitous language

One name per domain concept, used the same way in code, UI and diagrams.
Agreed with Martin on 2026-10-21. When a term changes, change it here first, then in code.

| Term | Meaning | Code | UI |
|---|---|---|---|
| **Source** | Where positions come from, identified by a short id (`fio`, later `ibkr`). | `source` (always the id), `IConnector.SourceId` | Display name, e.g. "Fio banka". "Institution" for banks/brokers. |
| **Connector** | Backend adapter that reads one source's API and translates the response. One per source. | `IConnector`, `FioConnector` | Never shown. |
| **Connection** | The user's stored link to a source: one credential at one source, plus the account label. Created by the first successful sync; lives only in the vault. | `Connection`, vault `connections` | "Connection" |
| **Credential** | The secret a connection uses to read from its source (Fio: API token; later e.g. a public address). | `credential` | Source-specific wording, e.g. "Fio API token". |
| **Account label** | How the source names the account (Fio: IBAN). Shown to users; tells connections at one source apart (connection identity = source + account label). | `accountLabel` | "Account" |
| **Sync** | One fetch of a connection's current positions from its source. | `POST /api/sync`, `FetchPositionsAsync` | "Sync" |
| **Sync result** | Outcome of one sync: status, account label, asOf, positions, warnings. | `ConnectorSyncResult`, vault `syncResults` | Status chip, "Last sync" |
| **Asset** | The thing owned and priced (VWCE, CZK, BTC) — the same whichever source holds it. | Domain `Asset` | "Asset" |
| **Asset type** | Classification of an asset: cash, security, crypto, other. | Domain `AssetType`, wire/client `assetType` | "Type" |
| **Position** | Quantity of one asset at one source at one moment: asset + quantity + source + asOf. Negative = liability or short. Value object (no identity). On the client a position belongs to the connection whose sync produced it. | `Position` (Domain, client), wire `SyncedPosition`, `positions` | "Holdings" — until Holding exists, the holdings table lists positions. |
| **Holding** *(planned)* | The user's total of one asset across all connections. See below. | not in code yet | "Holding(s)" |
| **Value** | A position's worth = quantity × unit price, in the asset's quote currency. Shown in the display currency (CZK for now); converting needs FX, except CZK cash (1:1). | Domain `ValueAt`, client `displayValue` | "Value (CZK)" |
| **Net worth** | Sum of all valued positions in the display currency. Negative positions subtract; unvalued positions are left out and reported. | `netWorth`, `netWorthDelta` | "Total net worth" |

## Rules

- **Connection vs account.** A connection is tied to one credential, not to one account.
  How many accounts a credential reads depends on the source. Fio issues one token per
  account, so two Fio accounts = two connections. There is no Account concept yet;
  introduce one when a source needs one login → many accounts.
- **Connector vs connection.** Connector is the backend's word (stateless; it only gets
  source + credential). Connection is the client's word (stored in the vault).
- **`ConnectorSyncResult`, not `SyncResult`.** Specific and backend-owned (ADR-0003);
  `SyncResult` stays free for a future base type or another kind of sync.
- **Type names are not on the wire.** Renaming a C# record doesn't change the JSON;
  renaming a property does.

## Holding (planned — start here when building it)

- **Definition:** the user's total of one asset across all connections.
- **Built from:** the **latest position of each connection** for that asset, summed.
  Connections sync at different moments, so the inputs can have different ages.
- **asOf:** the **oldest** asOf among those positions — a holding is only as fresh as its
  stalest input.
- **Open:** asset identity for aggregation. Brokers may call the same asset `VWCE` or
  `VWCE.DE`. Decide whether to match by symbol, ISIN or a mapping.
- **Until it exists:** the dashboard's "Holdings" table shows positions (one row per source).

## Not defined yet

- **Manual asset**: backlog feature, not specified. See `docs/uml/02-manual-asset-classes.puml`
  (draft, predates this glossary).
