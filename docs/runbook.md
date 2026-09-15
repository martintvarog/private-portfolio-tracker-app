# Incident runbook — Container App

Copy-paste commands for "the app is broken / slow / down". Log queries live in
`observability.md`; this file is about *acting*. Set these once per shell:

```bash
APP=ca-portfoliotracker; RG=rg-portfoliotracker; ACR=acrportfoliotrackerapp
URL=https://ca-portfoliotracker.graymoss-a8833994.germanywestcentral.azurecontainerapps.io
```

## 1. Is it up? (30 seconds)

```bash
curl -s -o /dev/null -w "%{http_code} in %{time_total}s\n" $URL/health      # expect 200, < 1 s (cold start: ~5 s)
curl -s -D - -o /dev/null $URL/ | grep -i "HTTP/\|cache-control"            # 200 + no-cache on index.html
az containerapp show -n $APP -g $RG --query "{image:properties.template.containers[0].image, status:properties.runningStatus, fqdn:properties.configuration.ingress.fqdn}" -o json
```

## 2. What is running, and what ran before?

```bash
az containerapp revision list -n $APP -g $RG --all \
  --query "sort_by([], &properties.createdTime)[].{name:name, active:properties.active, created:properties.createdTime, image:properties.template.containers[0].image}" -o table
git log --oneline -10            # map the SHAs in the image tags to commits
```

## 3. Live logs (no ingestion delay)

```bash
az containerapp logs show -n $APP -g $RG --follow                 # app stdout (JSON lines)
az containerapp logs show -n $APP -g $RG --type system --follow   # platform: replica start/stop, probes, image pull
```

Stored logs (2–5 min behind): portal → Container App → Logs, queries in `observability.md`.

## 4. Replicas, scaling, restarts

```bash
az containerapp replica list -n $APP -g $RG -o table                       # 0 replicas when idle is normal (min 0)
az containerapp revision restart -n $APP -g $RG --revision <active-name>   # "turn it off and on again"
az containerapp update -n $APP -g $RG --min-replicas 1                     # temporarily kill cold starts (costs ~€0.03/h); undo: 0
```
Note: `update` creates a new revision; the next pipeline deploy (Bicep) resets scale to what `main.bicep` says.

## 5. ROLLBACK (single-revision mode = deploy the old image as a new revision)

```bash
# a) pick the last good SHA from step 2, then:
az containerapp update -n $APP -g $RG --image $ACR.azurecr.io/portfoliotrackerapp:<good-sha>
az containerapp show -n $APP -g $RG --query properties.template.containers[0].image -o tsv   # confirm
# b) make it permanent — otherwise the next push rolls forward again:
git revert <bad-commit> && git push          # pipeline redeploys the reverted code as a new SHA
```
Alternative b): GitHub → Actions → the last good run → "Re-run all jobs" (rebuilds the old SHA; ~5 min; still temporary).

## 6. Registry

```bash
az acr repository show-tags -n $ACR --repository portfoliotrackerapp --orderby time_desc -o table   # every deployable version
az acr login -n $ACR && docker pull $ACR.azurecr.io/portfoliotrackerapp:<sha>                        # run any version locally: docker run --rm -p 8080:8080 <image>
```

## 7. Pipeline / identity

```bash
gh run list --limit 5                          # or the Actions tab
az identity show -n id-github-deploy -g $RG --query clientId -o tsv                                          # must equal the AZURE_CLIENT_ID GitHub variable
az identity federated-credential list --identity-name id-github-deploy -g $RG --query "[].subject" -o tsv   # must match repo:<owner>@<id>/<repo>@<id>:ref:refs/heads/main
az role assignment list --assignee $(az identity show -n id-github-deploy -g $RG --query principalId -o tsv) --all -o table   # AcrPush on ACR, Contributor on RG
```
Auth error `AADSTS700213` in the deploy job = the subject above no longer matches (repo renamed?) → fix `githubSubject` in `infra/main.bicep`.

## 8. Infra drift

```bash
az deployment group what-if -g $RG --template-file infra/main.bicep      # anything but noise = someone changed Azure by hand
az deployment group list -g $RG --query "[].{name:name, state:properties.provisioningState, when:properties.timestamp}" -o table
```
`rbac.bicep` is human-only: `az deployment group create -g $RG --template-file infra/rbac.bicep`.

## 9. Cost / kill switch

```bash
az consumption usage list --start-date $(date -d '-7 days' +%F) --end-date $(date +%F) --query "[].{r:instanceName,c:pretaxCost}" -o table
az containerapp ingress disable -n $APP -g $RG      # take it offline, keep everything (re-enable: ingress enable --type external --target-port 8080)
az group delete -n $RG --yes --no-wait              # nuclear: deletes registry, environment, app, logs
```

## Rules of engagement

- Stop the bleeding first (rollback), then make it permanent (revert). Never leave `main` disagreeing with what runs.
- Anything changed by hand via `az containerapp update` is reset by the next pipeline deploy — that is the point of Bicep, not a bug.
- Never log or paste a user's credential or IBAN anywhere while investigating; the `X-Request-Id` they quote is all you need.
