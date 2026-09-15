# Project status & handoff

> Snapshot for anyone (human or Claude session) picking this up.
> Last updated: 2026-09-15. Update this file when a work block completes.

## Process — read first

STRICT mentor mode is in force (top-level `CLAUDE.md`, Martin's explicit request):
concept → ONE clear question → wait for Martin. No unprompted code/config, no
batching ahead. Martin is senior-track .NET, junior FE, NEW to infra/DevOps.
Refinements (2026-09-03): questions must be concrete and self-contained (what's
asked, what a valid answer looks like, why it matters) — no riddles; on
infra/Bicep/CI work Claude types names/IDs/syntax, Martin decides and reads
results; Martin still wants one question at the end of each answer. Deleted work
is acceptable; the learning is the deliverable.

## What works today

- **Backend** (`src/`): modular monolith. `POST /api/sync` (one connector per
  call, outcomes as data — ADR-0005), `GET /health`. FioConnector done. Enums as
  strings (ADR-0010). Connector outcome logging via `LoggingConnector` decorator;
  inbound request logging via `UseHttpLogging` (method/path/status/duration only).
- **Client** (`client/`): React+Vite+TS. Encrypted vault (ADR-0009) → dashboard →
  Fio sync → holdings + total → credential saved on success → F5 survives.
  Valuation: CZK cash 1:1 only; rest "—" + honest banner (needs MarketData).
  Non-Ok sync shows "Reference for support" (= X-Request-Id, click-to-copy chip).
  `index.html` is `no-cache`, `assets/*` immutable → deploys reach users on next load.
- **Tests**: 43 green (`dotnet test` at repo root). Domain 18, Connectors 17,
  Api 8 (new `tests/PortfolioTrackerApp.Api.Tests`, WebApplicationFactory).
  Logging tests guard the "never log credentials/IBAN/URLs/bodies" law at three
  layers: decorator unit, real DI + stubbed Fio HTTP, real app in-process.
- **Dev run**: `dotnet run --project src/PortfolioTrackerApp.Api` (:5018) +
  `cd client && npm run dev` (:5173, proxies `/api`). Production-style local run:
  `npm run build`, copy `client/dist/*` → `src/PortfolioTrackerApp.Api/wwwroot/`
  (gitignored), open :5018 with Vite off.

## Deployment — LIVE (ADR-0006/0007/0008)

Public URL: https://ca-portfoliotracker.graymoss-a8833994.germanywestcentral.azurecontainerapps.io/

- `git push` to `main` → `ci.yml`: `backend` (build+test) ∥ `client` (build) →
  `deploy` (needs both, push-to-main only): `azure/login` via OIDC → `docker build`
  → push `:<git sha>` to ACR → `az deployment group create infra/main.bicep
  --parameters image=$IMAGE`. ~5 min.
- **Azure** (all in `rg-portfoliotracker`, region `germanywestcentral`; West
  Europe refused new subscriptions): ACR `acrportfoliotrackerapp` (Basic), Log
  Analytics `workspace-rgportfoliotrackerk171`, environment `cae-portfoliotracker`
  (Consumption), app `ca-portfoliotracker` (0.5 vCPU/1Gi, scale 0–1, ingress 8080,
  system identity pulls from ACR), pipeline identity `id-github-deploy`
  (federated credential `github-main`).
- **Bicep**: `infra/main.bicep` (6 resources; deployed ONLY by the pipeline,
  never by hand). `infra/rbac.bicep` (3 role assignments: app→AcrPull on ACR;
  pipeline→AcrPush on ACR + Contributor on the RG; deployed ONLY by a human:
  `az deployment group create -g rg-portfoliotracker --template-file infra/rbac.bicep`).
  Always `what-if` first; `-` lines on unmentioned defaults and
  `"x" => "[reference(...)]"` lines are noise; look for `+`, whole-resource `-`,
  and `"old" => "new"` with two concrete values.
- **GitHub**: repo renamed to `martintvarog/private-portfolio-tracker-app`.
  Repository variables `AZURE_CLIENT_ID`, `AZURE_TENANT_ID`, `AZURE_SUBSCRIPTION_ID`
  (non-secret). No secrets anywhere.
- **Gotchas**: OIDC subject embeds numeric IDs
  (`repo:martintvarog@63610399/private-portfolio-tracker-app@1329731821:ref:refs/heads/main`)
  and changes on repo rename → update `githubSubject` param. Provider
  registration (`Microsoft.ContainerRegistry`, `Microsoft.App`,
  `Microsoft.OperationalInsights`) is one-time per subscription.
  `az role assignment list` needs `--all` to show sub-subscription scopes.
  Fio API: 1 request/token/30 s; a second sync inside the window hangs until
  the 30 s HttpClient timeout → `Unavailable`. Free trial = $200 ≈ €171 (not €200).
- **Fresh-subscription bootstrap** (manual, in order): `az login` → register
  providers → `az group create` → first `main.bicep` deploy AS A HUMAN (creates
  the pipeline identity; app needs an image in ACR to start) → `rbac.bicep` →
  set GitHub variables from outputs → pipeline owns `main.bicep` from then on.
- **Cost**: ~€5/month (ACR Basic); app scales to zero. Kill switch:
  `az group delete -n rg-portfoliotracker`.

## Logging / observability — current state

- Console → Log Analytics tables `ContainerAppConsoleLogs_CL` (app stdout) and
  `ContainerAppSystemLogs_CL` (platform). Live tail:
  `az containerapp logs show -n ca-portfoliotracker -g rg-portfoliotracker --follow`.
- Emitted per sync, as JSON lines (console formatter `json`, `IncludeScopes`):
  request line (HttpLogging: Method/Path/StatusCode/Duration) and outcome line
  `Sync {Source} finished with {Status} in {ElapsedMs} ms` (Information for Ok,
  Warning otherwise). Both carry the `RequestId` scope = `X-Request-Id` response
  header = "Reference for support" shown in the client on any non-Ok sync.
  `appsettings.json` needs `Microsoft.AspNetCore.HttpLogging: Information` or the
  request line is filtered out. Pipeline order: HttpLogging → X-Request-Id
  (OnStarting) → ExceptionHandler → static files/endpoints.
- Law: never log credential, request/response bodies, outbound URLs (Fio token is
  in the URL path), AccountLabel (IBAN). `AddHttpClient<FioConnector>` has
  `.RemoveAllLoggers()`; HttpLogging fields are an explicit allow-list. Both are
  test-guarded — mutating either fails CI with a message naming the leak.
- Tests: 43 green. Api tests run as `Production`, use their own temp web root, and capture scopes; they assert
  the header equals the RequestId on both lines, also on the 500 path.
- **Alert** `alert-sync-unavailable` (in `main.bicep`): log alert, every 5 min over
  15 min, fires when > `unavailableAlertThreshold` (=5) decorator lines have
  `Status == Unavailable`; `InvalidCredential` excluded on purpose. NO action
  group (Martin's choice — portal-only: Monitor → Alerts). Never test-fired; to
  prove it, push threshold 0, sync 6× with a bad token, wait ~10 min, restore 5.
- **Rollback** (drilled 2026-09-15, see `docs/runbook.md` §5): single-revision
  mode → `az containerapp update --image …:<good-sha>` creates a new revision
  with the old image (revisions 0000010/11 are the drill); then `git revert` +
  push so `main` agrees. `revision list` needs `--all` to show inactive ones.
- Ops block CLOSED except App Insights/OTel (needs outbound-URL redaction for
  the Fio client — its dependency tracking bypasses the removed loggers).

## Repo hygiene

- `.github/dependabot.yml` (2026-09-15): weekly Monday PRs for NuGet (grouped),
  npm (minor+patch grouped, majors separate), GitHub Actions, Docker base images.
  Runtime MAJORS are ignored by design — .NET 10→11/12 and Node 22→24 are done
  BY HAND as one coordinated branch (Directory.Build.props + Dockerfile + ci.yml
  + Microsoft.* packages). Plan: Node 24 ≈ spring 2027; .NET 10 is LTS to Nov 2028,
  skipping 11 (STS) is fine.
- No branch ruleset on `main` (Martin's explicit choice while solo). If/when
  added: block force-push + deletions, require `backend`+`client` checks.

## In flight / next — REACT TRACK (Martin's ask 2026-09-07/15)

Martin wants to deepen React; the client is "the actual application" and he is
junior there. One backlog feature per React concept, in this order (FE ladder
from `collaboration-style`: Claude writes the first component → walkthrough →
Martin predicts/changes one thing → Martin writes the next):

1. **Manual assets** (client-only vault CRUD: flat, car, cash) — forms, controlled
   inputs, validation, immutable list updates, vault as source of truth. Start
   Vitest + Testing Library here and add `npm test` to the `client` CI job.
   Open design question posed to Martin: what fields does a manual asset need
   (for the holdings table now, for valuation later)?
2. **Display-currency toggle** — where state lives, lifting vs context, derived values.
3. **FX + non-CZK valuation** — TanStack Query for real; backend: first MarketData
   endpoint (ČNB rates) = EF Core + Postgres + BackgroundService. Infra arrives
   WITH it: Postgres Flexible Server in Bicep, docker-compose returns, Key Vault
   only when a real secret exists, HTTP health probes.
4. **Net-worth history chart** (vault snapshots) — effects, memoisation.
5. Playwright E2E once there are two pages.

Parked: App Insights/OTel + redaction; Fio HttpClient timeout 30 s→15 s (Fio
stalls unknown tokens, so a bad token takes 30 s to fail — Martin's call);
alert test-fire; second environment (`envName` param + `.bicepparam`, one deploy
identity per env); k8s ladder; custom domain.

Learning-goals source of truth: business-technical-paper §7.4 (skill clusters →
where they live). Done: DevOps. Half: Observability, Testing. Not started:
Resilience/Polly, EF Core/Postgres, Messaging, Realtime, AuthN/Z, AI.

## Documentation map

- `CLAUDE.md` (repo root above) — mentor protocol.
- `README.md` — architecture overview (check the deploy section is current).
- `docs/adr/` 0002–0011 (+0001 in top-level `docs/adr/`). No known ADR gaps.
  (0006 amended with the cache policy; 0011 amended with JSON logs + X-Request-Id.)
- `docs/observability.md` — request→log-row pipeline, what each outcome leaves behind, KQL cookbook.
- `docs/runbook.md` — incident commands: health, revisions, live logs, ROLLBACK, registry, pipeline identity, drift, cost/kill switch.
- top-level `../backlog.md` — features by data dependency (moved out of `repository/docs` in `refactor`).
- top-level `docs/business-technical-paper.html` — §8 roadmap current
  (2026-08-23); §6.1/§7.2/§7.3 + meta block known-stale.
- Miro board (architecture + UI v2 mockups): https://miro.com/app/board/uXjVHz8auJM=/
