# STATUS — LLM handoff context

> Audience: an LLM session picking up this repo. Dense on purpose; not for humans.
> Scope: facts derived ONLY from `repository/` (code, config, docs, git log).
> Last full sweep: 2026-10-09 (HEAD `0c5a586`). Update when a work block completes.
> Paths below are relative to `repository/` unless stated.

## 0. Who / how

- Collaboration rules live in root `CLAUDE.md` (decision / learning / autopilot modes) —
  not duplicated here.
- Martin: senior-track .NET, junior FE, NEW to infra/DevOps.
- Code comments mark Martin's decisions ("Martin's decision 2026-09-16") — treat them as
  decided; don't silently change them.

## 1. Product in one paragraph

Privacy-first, local-first net-worth tracker (working title PortfolioTrackerApp). The
aggregate portfolio NEVER exists server-side. Client (React SPA) = the actual app: owns
an encrypted vault (IndexedDB), computes net worth. Backend = stateless gateway:
normalizes institution responses (Connectors) and, later, serves non-personal market
data (MarketData). Personal data transits per request, never stored or logged.
Only live connector: Fio banka (CZ). Only valuation: CZK cash 1:1.

## 2. Repo map (tracked files)

```
Directory.Build.props   net10.0, Nullable, ImplicitUsings, TreatWarningsAsErrors, InvariantGlobalization=false
PortfolioTrackerApp.slnx  4 src + 3 test projects
Dockerfile              node:22-alpine → dotnet/sdk:10.0 publish → dotnet/aspnet:10.0 (+ client dist→wwwroot), EXPOSE 8080
.dockerignore           node_modules, dist, bin, obj, .git
.gitignore              VS template + src/PortfolioTrackerApp.Api/wwwroot/
.editorconfig
.github/workflows/ci.yml, .github/dependabot.yml
infra/main.bicep, infra/rbac.bicep
src/  Api | Connectors | Domain | MarketData
tests/ Api.Tests | Connectors.Tests | Domain.Tests
client/ React+Vite+TS
docs/ STATUS.md, observability.md, runbook.md, adr/0000–0011, uml/
README.md               architecture overview (deploy section stale — see §9)
```
Untracked (as of 2026-10-09): `docs/uml/02-connectors-classes.puml`,
`docs/uml/02-read-domain-classes.puml`. `.idea/`, `*.DotSettings.user` gitignored.

## 3. Backend (`src/`)

Architecture (ADR-0002/0003): modular monolith, vertical capability modules,
ports-and-adapters inside each module (public `Contracts/`, rest `internal`).
Dependency rule: `Api → Connectors|MarketData → Domain`. `Api` has NO Domain reference and
sets `DisableTransitiveProjectReferences=true` → `Api → Domain` is a compile error by design.

### Api (`PortfolioTrackerApp.Api`)
- `Program.cs` pipeline order (matters, ADR-0011):
  `UseHttpLogging` (outermost) → inline middleware setting `X-Request-Id` =
  `TraceIdentifier` via `Response.OnStarting` (survives exception handler's
  `Response.Clear()`) → `UseExceptionHandler` (+`AddProblemDetails`) → OpenAPI (Dev only)
  → `GET /health` → `UseDefaultFiles` + `UseStaticFiles(spaFiles)` →
  `MapFallbackToFile("index.html", spaFiles)` → `MapGroup("/api").MapSyncEndpoints()`.
- `spaFiles` cache policy: `index.html` → `no-cache`; everything else →
  `public, max-age=31536000, immutable`. Same options passed to fallback (ADR-0006 amend).
- Global `JsonStringEnumConverter` (ADR-0010).
- HttpLogging fields allow-list: RequestMethod|RequestPath|ResponseStatusCode|Duration,
  `CombineLogs=true`. NEVER add bodies/headers (sync body = credential, response = IBAN).
- `// builder.Services.AddMarketDataModule(builder.Configuration);` — commented out.
- `public partial class Program;` for `WebApplicationFactory`.
- `Sync/SyncEndpoints.cs`: `POST /api/sync` body `SyncRequest(Source?, Credential?)`.
  400 ProblemDetails for missing source / missing credential / unknown source; else
  resolve `IEnumerable<IConnector>` by `SourceId` (case-insensitive) → 200 `ConnectorSyncResult`.
- `appsettings.json`: JSON console formatter, `IncludeScopes`, UTC ISO timestamps,
  `Microsoft.AspNetCore: Warning` BUT `Microsoft.AspNetCore.HttpLogging: Information`
  (required or request line is filtered). `appsettings.Development.json` lacks the
  HttpLogging override → no request line in Dev by default.
- `launchSettings.json`: http :5018, https :7101.
- Packages: Microsoft.AspNetCore.OpenApi 10.0.12, Microsoft.OpenApi 2.12.2 (pinned <3 —
  see dependabot ignore).

### Connectors (`PortfolioTrackerApp.Connectors`)
- `Contracts/IConnector`: `SourceId`, `FetchHoldingsAsync(credential, ct)`. Laws (doc
  comment): outcomes are data, never thrown; idempotent (Fio uses `periods`, never
  `last`); source-tagged; credentials never persisted/logged incl. URLs and EXCEPTION
  MESSAGES; unsupported instruments → `Warnings` (skip loudly).
- `ConnectorSyncResult` record: Source, Status, AccountLabel?, AsOf?, Holdings[], Warnings[];
  `Failed(source, status)` factory.
- `SyncStatus` enum: Ok=1, InvalidCredential=2, Unavailable=3, RateLimited=4 (no 0 on purpose).
- `SyncedHolding`: Kind ("cash"|"security"|"crypto"|"other"), Symbol, Quantity, Currency, Name?, Isin?.
- `Fio/FioConnector` (internal): GET
  `v1/rest/periods/{token}/{today}/{today}/transactions.json` on `https://fioapi.fio.cz/`,
  timeout 30 s. Maps: HTTP 500 → InvalidCredential (Fio spec!), 409 → RateLimited,
  other non-2xx / HttpRequestException / timeout / bad JSON / domain ArgumentException →
  Unavailable. Caller cancellation propagates. Ok → one cash holding from
  `accountStatement.info.closingBalance`+`currency`, AccountLabel = IBAN. Promotes through
  Domain (`Money`, `Position.OfCash`) to enforce invariants at the boundary.
- `Logging/LoggingConnector` (internal decorator): single log line
  `Sync {Source} finished with {Status} in {ElapsedMs} ms` — Information if Ok else
  Warning. Only place connector activity is logged.
- `ServiceCollectionExtensions.AddConnectorsModule()`: registers `TimeProvider.System`;
  private `AddConnector<T>` = the ONLY way to register a connector:
  `AddHttpClient<T>(...).RemoveAllLoggers()` + `IConnector` transient wrapped in `LoggingConnector`.
- `InternalsVisibleTo`: Connectors.Tests, Api.Tests.

### Domain (`PortfolioTrackerApp.Domain`) — references nothing
- `Monetary/Money` record: decimal Amount, normalized Currency; `+ - unary- *`; never
  crosses currencies (`CurrencyMismatchException : InvalidOperationException`), never rounds;
  negatives legal.
- `Monetary/CurrencyCode` (internal static): structural ISO-4217 check (3 ASCII letters, upper-cased).
- `Assets/Asset` record + `AssetType {Cash=1, Security=2, Crypto=3, Other=4}`; `Asset.Cash(ccy)`.
- `Holdings/Position` record: Asset, Quantity (negatives legal), Source (required), AsOf;
  `OfCash`, `ValueAt(unitPrice)` (throws on quote-currency mismatch).
- No ManualAsset / Snapshot / Transaction types exist yet.

### MarketData (`PortfolioTrackerApp.MarketData`)
- Only the `.csproj` (refs Domain). No code, not wired. Referenced by Api.

## 4. Client (`client/`)

- Stack: React 19.3, react-router 8.4, @tanstack/react-query 5.x, idb 8, Vite 8,
  TypeScript ~7.0.2, oxlint, prettier (no semi, single quotes, width 100).
  Scripts: `dev`, `build` (`tsc -b && vite build`), `lint`, `preview`, `format`.
  NO `test` script, no test framework installed.
- Vite dev proxy: `/api` → `http://localhost:5018` (no CORS anywhere; prod = same origin).
- `main.tsx`: `QueryClientProvider` around `<App/>` in StrictMode.
- `App.tsx`: state `vault: UnlockedVault|null` (null = locked) + `mode: loading|create|unlock`.
  `updateVaultData` = persist (encrypt+save) FIRST, then `setVault` (memory never drifts
  from disk; immutable replace). Router exists only when unlocked:
  `<AppLayout>` layout route → `/` DashboardPage, `/connections` ConnectionsPage,
  `*` "Page not found." (inside frame).
- `app/AppLayout.tsx`: `Sidebar` + `<main><Outlet/></main>`.
- `app/Sidebar.tsx`: sections Overview{Dashboard /}, Wealth{Holdings /holdings, Manual
  assets soon, Wealth graph soon}, Plan{Goals & DCA soon, Insights soon},
  Setup{Connections /connections, Settings soon}. `soon` items = disabled buttons.
- `lib/crypto.ts`: PBKDF2-SHA256 600k iters → AES-256-GCM non-extractable key; 12-byte IV
  per encryption; wrong passphrase = GCM tag failure (ADR-0009). Argon2id deferred ("M3").
- `lib/db.ts`: IndexedDB `portfolio-tracker` v1, store `vault`, single record
  `{id:'vault', version:1, salt, iv, ciphertext}`.
- `lib/vault.ts`: `VaultData = { syncResults: ConnectorSyncResult[]; credentials: StoredCredential[] }`;
  `StoredCredential = {source, credential, accountLabel?}`; `createVault`, `unlockVault`,
  `persistVault` (reuses salt, fresh IV), `vaultExists`. Stale "TODO (Martin)" comments
  remain above already-implemented `unlockVault`/`persistVault`.
- `lib/api.ts`: wire types mirroring backend contracts (string-union `SyncStatus`);
  `postSync()` returns `{result, requestId}` (from `X-Request-Id`); non-2xx → `SyncError`
  carrying `requestId` and ProblemDetails `title`.
- `features/vault/UnlockPage.tsx`: create/unlock form; "No recovery" notice.
- `features/dashboard/DashboardPage.tsx`: derives holdings from `data.syncResults` every
  render (no own state); CZK cash valued 1:1, else `valueCzk` undefined → "—" + banner
  "N holding(s) not valued yet". `onDataChange` prop accepted but unused (kept for future cards).
  `NetWorthCard` supports optional `deltaCzk` (unused yet).
- `features/holdings/HoldingsTable.tsx` + `types.ts` (`Holding` = temporary view type,
  to be replaced by a client domain — "Martin's code").
- `features/connections/ConnectionsPage.tsx`: one `ConnectionCard` per stored credential,
  joined at render to `syncResults` by `source`; Remove = `window.confirm` → drops the
  credential AND all syncResults of that source (Martin, 2026-09-16). Hosts `SyncForm`
  prefilled with the first stored fio credential.
- `features/connections/ConnectionCard.tsx`: status chip per `SyncStatus`, account label,
  last sync (cs-CZ), warnings, Remove button. `sourceNames = {fio: 'Fio banka'}`.
- `features/sync/SyncForm.tsx`: `useMutation(postSync('fio', token))`; on Ok →
  `onSynced(result, token)` (credential saved only after successful sync, ADR-0009);
  per-status messages; "Reference for support" click-to-copy chip on any failure/non-Ok.
- `index.html` `<title>` is still `client`.

## 5. Tests

- Last verified: 43 green on 2026-09-15 (`dotnet test` at repo root). Not re-run
  2026-10-09 (no `dotnet` in that sandbox); only change since = commit `46c35d2`
  (moved logging tests into `LoggingTests/` folders + namespaces, no logic change).
- Stack: xUnit 2.9.3, Microsoft.NET.Test.Sdk 18.10.1, coverlet, Mvc.Testing 10.0.12. No mocking lib.
- Domain.Tests: `MoneyTests` (equality, arithmetic, exact decimals, mismatch throws,
  normalization), `PositionTests` (ValueAt, currency mismatch, cash, negatives, source required).
- Connectors.Tests: `FioConnectorTests` (valid→cash+IBAN, 500→InvalidCredential,
  409→RateLimited, other→Unavailable, garbage body, invalid currency, empty credential);
  `StubHttpMessageHandler`; `LoggingTests/` = `LoggingConnectorTests`,
  `ConnectorsModuleLoggingTests` (real DI + stubbed Fio; every connector wrapped and its
  HttpClient has no logging handler; control test proving that without
  `RemoveAllLoggers` the token URL WOULD be logged), `CapturingLoggerProvider`.
- Api.Tests: `HttpLoggingTests` (real app in-process as `Production`, own temp web root,
  captures scopes): request line fields, no secrets in any category, /health logged,
  connector exception → 500 ProblemDetails logged once at Error, control test (credential
  in exception message DOES leak → why the IConnector law exists), X-Request-Id equals
  RequestId scope on both lines incl. 500 path, index.html no-cache vs immutable assets.
  `LoggingTests/CapturingLoggerProvider`.
- Logging law guarded at three layers (decorator unit, real DI, real app). Mutating
  either guard fails CI with a message naming the leak.
- Client: ZERO automated tests.

## 6. CI/CD + deployment (ADR-0006/0007/0008)

- `ci.yml` on push to main + PRs: `backend` (setup-dotnet 10.0.x, restore, build Release,
  test) ∥ `client` (Node 22, npm ci, build — no lint/test step) → `deploy`.
- **DEPLOY IS PAUSED** (commit `0c5a586`, 2026-10-08): `if: false && github.event_name == 'push' && …`.
  Re-enable by removing `false && `. Reason not recorded in repo — ask Martin before
  touching. Whether the Azure app/RG still exists is unknown from the repo.
- Deploy job (when enabled): `concurrency: {group: deploy-production, cancel-in-progress: false}`;
  OIDC `azure/login@v3` with repo VARIABLES `AZURE_CLIENT_ID/TENANT_ID/SUBSCRIPTION_ID`
  (no secrets anywhere) → `az acr login` → `docker build/push`
  `acrportfoliotrackerapp.azurecr.io/portfoliotrackerapp:<sha>` →
  `az deployment group create -g rg-portfoliotracker --name deploy-<run_id> --template-file infra/main.bicep --parameters image=$IMAGE`. ~5 min.
- Public URL (last known): https://ca-portfoliotracker.graymoss-a8833994.germanywestcentral.azurecontainerapps.io/
- Azure (RG `rg-portfoliotracker`, `germanywestcentral` — West Europe refused new subs):
  ACR `acrportfoliotrackerapp` (Basic); Log Analytics `workspace-rgportfoliotrackerk171`
  (30-day retention); env `cae-portfoliotracker` (Consumption); app `ca-portfoliotracker`
  (0.5 vCPU/1Gi, scale 0–1, ingress 8080, single-revision, system identity → AcrPull);
  pipeline identity `id-github-deploy` + federated credential `github-main`.
- `infra/main.bicep`: acr, logAnalytics, env, app, deployIdentity, githubFederation,
  `alert-sync-unavailable`. Params: `location`, `image` (default pinned to old sha
  `daef300…`), `githubSubject`, `unavailableAlertThreshold`=5. Outputs `appUrl`,
  `deployClientId`. Deployed ONLY by pipeline.
- `infra/rbac.bicep`: 3 role assignments via `existing` refs, `guid(scope,principal,role)`
  names: app→AcrPull; pipeline→AcrPush + Contributor(RG). Deployed ONLY by a human:
  `az deployment group create -g rg-portfoliotracker --template-file infra/rbac.bicep`.
- Always `what-if` first. Noise: `-` on unmentioned defaults, `"x" => "[reference(...)]"`.
  Signal: `+`, whole-resource `-`, `"old" => "new"` with two concrete values.
- Gotchas: OIDC subject embeds numeric IDs
  (`repo:martintvarog@63610399/private-portfolio-tracker-app@1329731821:ref:refs/heads/main`)
  and changes on repo rename → update `githubSubject`. Provider registration
  (`Microsoft.ContainerRegistry`, `Microsoft.App`, `Microsoft.OperationalInsights`) one-time
  per subscription. `az role assignment list` needs `--all`; `revision list` needs `--all`.
  Free trial = $200 ≈ €171.
- Fresh-subscription bootstrap (manual, in order): `az login` → register providers →
  `az group create` → first `main.bicep` deploy AS A HUMAN (creates pipeline identity; app
  needs an image in ACR) → `rbac.bicep` → set GitHub variables from outputs → pipeline
  owns `main.bicep` thereafter.
- Cost ~€5/month (ACR Basic); app scales to zero. Kill switch: `az group delete -n rg-portfoliotracker`.
- GitHub repo: `martintvarog/private-portfolio-tracker-app`. No branch ruleset on `main`
  (Martin's choice while solo; if added: block force-push+deletion, require `backend`+`client`).

## 7. Observability (ADR-0011, `docs/observability.md`, `docs/runbook.md`)

- stdout JSON → Container Apps → Log Analytics: `ContainerAppConsoleLogs_CL` (app),
  `ContainerAppSystemLogs_CL` (platform). Live tail:
  `az containerapp logs show -n ca-portfoliotracker -g rg-portfoliotracker --follow`.
- Per sync: HttpLogging request line + LoggingConnector outcome line, both carrying
  `RequestId` scope = `X-Request-Id` header = client "Reference for support".
- Law: never log credential, bodies, outbound URLs (Fio token in path), AccountLabel/IBAN.
- Alert `alert-sync-unavailable`: every 5 min over 15 min, fires when >5 outcome lines
  have Status==Unavailable; InvalidCredential excluded on purpose. No action group
  (portal-only). Never test-fired (to prove: threshold 0, 6 bad-token syncs, wait ~10 min, restore 5).
- Rollback drilled 2026-09-15 (runbook §5): `az containerapp update --image …:<good-sha>`
  → new revision (0000010/11 were the drill) → then `git revert` + push so main agrees.
- Runbook sections: up?, revisions, live logs, replicas, ROLLBACK, registry,
  pipeline/identity, drift, cost/kill switch, rules of engagement.
- Known gaps: no App Insights/OTel (its dependency tracking would bypass removed loggers
  → needs Fio outbound-URL redaction first); failure references not persisted in vault.

## 8. ADRs (`docs/adr/`, all use `**Chosen:**`/`**Why:**`/`**Risk:**` structure since `cb49e35`)

0000 template · 0001 record decisions (2026-08-07) · 0002 modular monolith, vertical
modules · 0003 contracts own their types, Domain hidden from Api · 0004 single repo ·
0005 sync = one connector per call, outcomes as data, DI registry = connector list ·
0006 API serves built client, one container (+cache-policy amendment) · 0007 Docker +
ACA + GH Actions OIDC · 0008 Bicep, RBAC in human-only file · 0009 client-side encrypted
vault, one sealed blob · 0010 enums as strings · 0011 logging architecture (+JSON logs,
X-Request-Id amendment). New ADRs follow `0000-adr-template.md`.

## 9. Known gaps / inconsistencies (verified in code 2026-10-09)

1. Sidebar `Holdings` → `/holdings`, but no such route → hits catch-all "Page not found".
   Holdings only render on `/`. Decision needed: add route or point nav to `/`.
2. Multi-account per source is half-supported: credentials are keyed by
   source+accountLabel, but `syncResults` are replaced/joined/removed by `source` only.
   Second Fio account sync overwrites the first account's holdings; both cards show the
   same result; removing one card drops all Fio holdings; `SyncForm` prefills only the
   first fio credential.
3. README "Deployment" says push to main deploys — false while deploy is paused.
   README Conventions cites `docs/adr/template.md` (actual: `0000-adr-template.md`).
4. ADR-0004 lists `docker-compose.yml` as repo infra — file does not exist (planned to
   return with Postgres).
5. `vault.ts` stale TODO comments; `index.html` title `client`; Dev appsettings lacks
   HttpLogging override.
6. Fio: 1 request/token/30 s; a second sync inside the window stalls until the 30 s
   HttpClient timeout → `Unavailable` (should arguably be RateLimited). Unknown tokens
   also stall 30 s.

## 10. Repo hygiene

- Dependabot weekly (Mon): NuGet (one `dotnet` group; Microsoft.AspNetCore.*/Extensions.*
  and Microsoft.OpenApi majors ignored), npm (`npm-minor-patch` group; majors separate),
  GitHub Actions, Docker (dotnet/* and node majors ignored).
- Runtime majors by hand as one branch (Directory.Build.props + Dockerfile + ci.yml +
  Microsoft.* packages). Plan: Node 24 ≈ spring 2027; .NET 10 LTS to Nov 2028, skip 11 (STS).
- First grouped round merged 2026-09-16 (checkout 7, setup-dotnet 6, setup-node 7,
  azure/login 3, typescript 7.0.2, npm group 8 updates, dotnet group 7 updates).

## 11. Next step

**Manual assets** (client-only vault CRUD) — NOT STARTED in code (`VaultData` has no
manual-asset field; sidebar item `soon`). Design artifacts in repo: `docs/uml/01-manual-assets-use-cases.puml`
(Add/View/Revalue/Dispose, each <<include>> Save+snapshot) and `docs/uml/01-revalue-asset.md`
(revaluation appends value + ValuationDate, history kept; future date / negative value
rejected). Planned with it: Vitest + Testing Library, `npm test`, CI test step in `client` job.
Open decisions before coding: ManualAsset fields/types and vault shape; liquidity
override as its own use case or not; whether to fix §9 gap 2 (multi-account) first.

## 12. Open decisions / parked (Martin's)

- Deploy paused — reason + when to resume (ask).
- `/holdings` route vs nav fix (§9 gap 1).
- Parked: App Insights/OTel + Fio URL redaction; Fio timeout 30 s→15 s; alert test-fire;
  second environment (`envName` param + `.bicepparam`, one deploy identity per env);
  MarketData/FX with EF Core + Postgres (+ Postgres in Bicep, docker-compose, Key Vault
  only when a real secret exists, HTTP health probes); custom domain.
- Accepted risks: no branch ruleset on `main` (solo); alert has no action group
  (portal-only); removing a connection drops its holdings with no undo (confirm dialog).

## 13. Learning

- No plan files in `docs/learning/` yet. UML exercise outputs live in `docs/uml/`
  (`01-*` committed, `02-*` untracked).

## 14. Commands

- `dotnet build` / `dotnet test` at repo root (.NET 10 SDK).
- `dotnet run --project src/PortfolioTrackerApp.Api` (:5018) + `cd client && npm install && npm run dev` (:5173).
- Client lint/format: `npm run lint` (oxlint), `npm run format` (prettier). No client tests.
- Production-style: `npm run build`, copy `client/dist/*` → `src/PortfolioTrackerApp.Api/wwwroot/`, open :5018.
- Container: `docker build -t portfoliotrackerapp . && docker run --rm -p 8080:8080 portfoliotrackerapp`.

## 15. External

- Miro board (architecture + UI v2 mockups): https://miro.com/app/board/uXjVHz8auJM=/
