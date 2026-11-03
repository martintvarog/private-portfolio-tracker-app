# Fio banka

- **Source id:** `fio`
- **Credential:** personal API token, created in Fio internet banking. One token = one
  account, so two Fio accounts are two connections (`docs/glossary.md`).
- **Account label:** the account's IBAN (`accountStatement.info.iban`).
- **Base URL:** `https://fioapi.fio.cz/`; HttpClient timeout 30 s.

## What we call

`GET v1/rest/periods/{token}/{today}/{today}/transactions.json`

- **`periods`, never `last`:** `last` moves a server-side bookmark, which breaks the
  idempotency law. `periods` changes nothing.
- **One-day window:** we only read the balance; `accountStatement.info.closingBalance` and
  `currency` come with every export, whatever the window.
- **The token is in the URL path.** Nothing may log the request URI: the HttpClient has no
  loggers, guarded by `ConnectorsModuleLoggingTests`.
- Only fields we read are modelled (`Fio/FioModels.cs`).
- The balance is promoted through Domain (`Money`, `Position.OfCash`), so a bad currency
  from Fio fails inside the connector and maps to `Unavailable`.

## Response mapping

| Fio response | `SyncStatus` | Why |
|---|---|---|
| 2xx with valid `info` | `Ok` | One cash position: balance in its currency. |
| **500** | `InvalidCredential` | Fio's spec (section 8): 500 means invalid or inactive token, not a server error. |
| 409 | `RateLimited` | Fio allows 1 request per token per 30 s. |
| other non-2xx, network error, timeout | `Unavailable` | |
| unparsable JSON, missing `info`, invalid currency | `Unavailable` | Data we can't trust counts as an outage for the user. |
| caller cancelled | (exception propagates) | Not a bank problem. |

## Known issues

- A second sync within the 30 s window stalls until our 30 s timeout → `Unavailable`
  instead of `RateLimited`. Unknown tokens stall the same way.
