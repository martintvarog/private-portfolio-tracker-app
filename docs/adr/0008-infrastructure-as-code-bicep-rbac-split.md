# ADR-0008: Infrastructure as code with Bicep; permissions in a human-only file

- Status: accepted
- Date: 2026-09-03

## Context

All Azure resources from ADR-0007 were first created by hand with `az` commands in a specific order, copying IDs between them. That is not reproducible (second environment, disaster recovery) and leaves no history of infra changes. Two further problems surfaced: the pipeline deploying with `az containerapp update` left any declared `image` value stale (a later infra deploy would silently roll the app back), and Azure's `Contributor` role deliberately cannot write role assignments, so a pipeline cannot manage permissions without being granted the power to escalate itself.

## Decision

- **Chosen:** `infra/main.bicep` declares the registry, Log Analytics workspace, Container Apps environment, container app, pipeline identity, and its federated credential. Dependencies are expressed as references (`env.id`, `logAnalytics.listKeys()`), not ordering. The image is a parameter.
  **Why:** manual `az` commands created the original environment with no history and no reproducibility; expressing dependencies as references instead of manual ordering removes an entire class of "I forgot a step" failure.
- **Chosen:** The pipeline is the only deployer of `main.bicep` — `az deployment group create … --parameters image=$IMAGE` replaces `az containerapp update`; `main.bicep` is never deployed by hand.
  **Why:** `az containerapp update` left the declared `image` value in the template stale, so a later infra deploy would silently roll the app back to whatever image the file last said. Making the pipeline the only deployer means the file can never disagree with what's live.
- **Chosen:** `infra/rbac.bicep` holds the three role assignments (app → `AcrPull`; pipeline → `AcrPush` and `Contributor` on the resource group), referencing other resources with `existing`, deployed only by a human. Role assignment names use `guid(scope, principal, role)` so redeploys are idempotent.
  **Why:** Azure's `Contributor` role deliberately cannot write role assignments, and the pipeline must never be handed the power to grant itself permissions — splitting this into a human-only file makes that true structurally, not just by policy.
- **Chosen:** Default deployment mode is Incremental; `what-if` runs before any manual deploy.
  **Why:** a dry-run before a hand-run change catches drift before it's applied.

## Consequences

The whole environment is reproducible from the repo plus a short manual bootstrap (login, provider registration, resource group, first `main.bicep` deploy as a human to create the pipeline identity, `rbac.bicep`, GitHub variables). Permission changes require a human, a commit, and `az login` — a merged PR alone cannot grant anything. **Risk:** the pipeline holds `Contributor` on the whole resource group (wider than before), accepted in exchange for a single deploy path; it still cannot touch who-may-do-what.

Rejected: **pipeline-managed RBAC** (`Contributor` + RBAC Administrator) — a compromised pipeline could grant itself anything. **Manual infra deploys from a laptop** — keeps the stale-image trap and hides changes from history. **Copying the file per environment** — when a second environment is needed, parameterise names with an environment suffix and use `.bicepparam` files; one deploy identity per environment, never shared across the prod boundary.
