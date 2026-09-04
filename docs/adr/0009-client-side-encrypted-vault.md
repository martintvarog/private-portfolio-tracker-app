# ADR-0009: Client-side encrypted vault — one sealed blob, nothing readable at rest

- Status: accepted
- Date: 2026-08-28 (recorded 2026-09-03)

## Context

The product's defining property is "the operator can't see your data": credentials, sync results, and the aggregated portfolio must never exist server-side. The client still needs to survive a page reload and keep bank credentials between sessions. Options: browser `localStorage` in plaintext; a per-field encryption scheme; or a single encrypted container. Also to decide: key derivation (built-in PBKDF2 vs. Argon2id via WASM), and how sync results feed the UI.

## Decision

- **One sealed blob.** All client state (`syncResults`, `credentials`) is one JSON document, encrypted as a unit with AES-256-GCM and stored as a single IndexedDB record (`{ id, version, salt, iv, ciphertext }`). The record's shape reveals nothing about what the user owns — not even how many accounts exist.
- **Key from passphrase.** PBKDF2-SHA256, 600 000 iterations (OWASP 2023 baseline), 16-byte random salt stored beside the ciphertext and reused on every save. Fresh 12-byte IV per encryption. The derived `CryptoKey` is non-extractable. WebCrypto only — no crypto library shipped. Upgrading to Argon2id (memory-hard, needs WASM) is deferred to M3.
- **Wrong passphrase = GCM authentication failure.** No separate password check is stored; `decrypt` throwing is the signal, surfaced by the unlock page.
- **Credentials are saved only after a successful sync**, so a mistyped token never gets persisted.
- **The dashboard owns no state.** Everything rendered derives from `vault.data.syncResults` — raw per-source results (ADR-0005) accumulated in the vault. Sync updates the vault; the UI re-derives.
- **Vault format is versioned** (`version: 1` in the record, IndexedDB schema version 1) so future migrations have a hook from day one.

## Consequences

Server holds nothing; a database dump of the browser shows one opaque record. Losing the passphrase loses the vault — by design, there is no recovery, and the UI must say so. Every save re-encrypts the whole document (fine at this data volume; revisit if the vault grows large). Because the dashboard is a pure function of the vault, stale-but-honest data is shown until the next sync lands, and any new data source only has to write into `syncResults`. Rejected: plaintext `localStorage` (readable by any script on the origin, defeats the pitch); per-field encryption (leaks structure and counts).
