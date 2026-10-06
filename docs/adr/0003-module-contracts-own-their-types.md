# ADR-0003: Module contracts own their result types; Domain is hidden from Api

- Status: accepted
- Date: 2026-08-10

## Context

With vertical modules (ADR-0002), module contracts could either expose Domain types directly (`SyncResult` carrying `Snapshot`/`Money`) or define their own result records built from primitives. Exposing Domain types makes value objects the shared vocabulary and avoids mapping code, but couples every consumer — including the Api host — to the domain model, and Api endpoints could drift into manipulating domain objects directly. We want Api to be a thin callable: parse request, invoke a module use case, shape the result for HTTP. The layer that retrieves data from the domain (the module's application layer) should be the only one that touches it, and contract results may legitimately differ in shape from domain objects.

## Decision

- **Chosen:** Module contracts own their types. Public contract interfaces (e.g. `ISyncService`) return contract-defined records (e.g. `SyncResult`, `SnapshotSummary`) composed of primitives and other contract types — never Domain types. The module's application layer performs the domain-to-contract translation.
  - `Api` has no project reference to `Domain`.
  - `Api` sets `DisableTransitiveProjectReferences` so Domain cannot leak in through module references (SDK-style project references are transitive by default).
- **Why:** Api is meant to be a thin callable — parse request, invoke a use case, shape the result for HTTP — and only the module's own application layer should touch Domain. Letting Domain types cross into Api would make that boundary a convention someone could drift past; making it a compile error instead removes that chance. Domain is thereby a private implementation detail of the modules, enforced by the compiler, not a code-review rule.

## Consequences

Api physically cannot execute or construct domain logic — the thin-callable rule is enforced, not conventional. Contract shapes can evolve independently of the domain model. **Risk:** each module writes mirror records and mapping code, and primitive-based contracts give up value-object type safety (e.g. amount/currency pairing) at module boundaries — contract records should keep amount+currency together to compensate. Revisit if mapping ceremony grows disproportionate or a shared contract-primitives package becomes necessary.
