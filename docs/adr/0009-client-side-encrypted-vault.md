# ADR-0009: Client-side encrypted vault — one sealed blob, nothing readable at rest

- Status: accepted
- Date: 2026-08-28 (recorded 2026-09-03)

## Context

The product's defining property is "the operator can't see your data": credentials, sync results, and the aggregated portfolio must never exist server-side. The client still needs to survive a page reload and keep bank credentials between sessions. Options: browser `localStorage` in plaintext; a per-field encryption scheme; or a single encrypted container. Also to decide: key derivation (built-in PBKDF2 vs. Argon2id via WASM), and how sync results feed the UI.

## Decision

- **Chosen — one sealed blob:** All client state (`syncResults`, `credentials`) is one JSON document, encrypted as a unit with AES-256-GCM and stored as a single IndexedDB record (`{ id, version, salt, iv, ciphertext }`).
  **Why:** the product's defining property is that the operator can't see your data. A per-field scheme would still leak structure and counts (how many accounts, how many syncs); one opaque blob reveals nothing — not even how many accounts exist.
- **Chosen — key from passphrase:** PBKDF2-SHA256, 600 000 iterations (OWASP 2023 baseline), 16-byte random salt stored beside the ciphertext and reused on every save, fresh 12-byte IV per encryption, non-extractable derived `CryptoKey`, WebCrypto only. Upgrading to Argon2id (memory-hard, needs WASM) is deferred to M3.
  **Why:** WebCrypto gives PBKDF2 natively with nothing extra to ship or trust; Argon2id is stronger but needs a WASM dependency, not justified yet.
- **Chosen:** No separate password check is stored — a wrong passphrase surfaces as a GCM authentication failure, caught by the unlock page.
  **Why:** GCM already authenticates on decrypt; storing a separate verifier would be redundant and one more thing to protect.
- **Chosen:** Credentials are saved only after a successful sync.
  **Why:** a mistyped token should never get persisted.
- **Chosen — dashboard owns no state:** everything rendered derives from `vault.data.syncResults`; sync writes to the vault, the UI re-derives.
  **Why:** a single source of truth makes "stale but honest" data between syncs trivial, and any new data source only has to write into `syncResults`.
- **Chosen:** Vault format is versioned from day one (`version: 1` in the record, IndexedDB schema version 1).
  **Why:** adding versioning later, once real data exists, would itself be a breaking migration — cheaper to have the hook from the start.

## Consequences

Server holds nothing; a database dump of the browser shows one opaque record. **Risk:** losing the passphrase loses the vault permanently — by design, there is no recovery, and the UI must say so clearly. Every save re-encrypts the whole document (fine at this data volume; revisit if the vault grows large). Because the dashboard is a pure function of the vault, stale-but-honest data is shown until the next sync lands. Rejected: plaintext `localStorage` (readable by any script on the origin, defeats the pitch); per-field encryption (leaks structure and counts).
