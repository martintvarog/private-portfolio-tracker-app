# Project status & handoff

> Snapshot for anyone (human or Claude session) picking this up.
> Last updated: 2026-09-03. Update this file when a work block completes.

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
- **Tests**: 38 green (`dotnet test` at repo root). Domain 18, Connectors 17,
  Api 3 (new `tests/PortfolioTrackerApp.Api.Tests`, WebApplicationFactory).
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
- Emitted per sync: `POST /api/sync 200 <duration>` (HttpLogging) and
  `Sync {Source} finished with {Status} in {ElapsedMs} ms` (Information for Ok,
  Warning otherwise). Nothing else. `appsettings.json` has
  `Microsoft.AspNetCore.HttpLogging: Information` — required or the request log
  is filtered out by `Microsoft.AspNetCore: Warning`.
- Law: never log credential, request/response bodies, outbound URLs (Fio token is
  in the URL path), AccountLabel (IBAN). `AddHttpClient<FioConnector>` has
  `.RemoveAllLoggers()`; HttpLogging fields are an explicit allow-list. Both are
  test-guarded — mutating either fails CI with a message naming the leak.
- Open question for the next block: App Insights/OpenTelemetry — its dependency
  tracking hooks outbound HTTP directly (not via the removed loggers), so it
  will need URL redaction for the Fio client.

## In flight / next

1. **Operate-the-app block** (Martin's ask): KQL queries over the two tables,
   revisions + rollback (`az containerapp revision list`, `--image <old sha>`),
   scaling rules, alerts (e.g. `Unavailable` spike across users vs one user's
   `InvalidCredential`), cost analysis, then App Insights/OTel with redaction.
2. Then features per `docs/backlog.md`: manual assets (client-side vault CRUD)
   or FX + non-CZK valuation (ČNB rates, first MarketData feature) → snapshots.
3. Later: second environment = parameterise names with an env suffix +
   `.bicepparam` files; one deploy identity per environment. Kubernetes far out.

## Documentation map

- `CLAUDE.md` (repo root above) — mentor protocol.
- `README.md` — architecture overview (check the deploy section is current).
- `docs/adr/` 0002–0010 (+0001 in top-level `docs/adr/`). No known ADR gaps.
- `docs/backlog.md` — features by data dependency.
- top-level `docs/business-technical-paper.html` — §8 roadmap current
  (2026-08-23); §6.1/§7.2/§7.3 + meta block known-stale.
- Miro board (architecture + UI v2 mockups): https://miro.com/app/board/uXjVHz8auJM=/
