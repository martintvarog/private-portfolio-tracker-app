# ADR-0007: Deployment — Docker image, Azure Container Apps, GitHub Actions with OIDC

- Status: accepted
- Date: 2026-09-03

## Context

The app (ADR-0006) had to go from a laptop to a public URL that others can use, with an automated path for new versions. Decisions needed: packaging, hosting platform, registry, CI system, and how the pipeline authenticates to Azure. Learning goals (Docker, CI/CD, Azure, later Kubernetes) were an explicit input.

## Decision

- **Packaging:** multi-stage `Dockerfile` — `node:22-alpine` builds the client, `dotnet/sdk:10.0` publishes the API, `dotnet/aspnet:10.0` runtime image receives only the publish output and `dist`→`wwwroot`. `.dockerignore` excludes `node_modules`, `dist`, `bin`, `obj`, `.git`.
- **Hosting:** Azure Container Apps, Consumption profile, 0.5 vCPU / 1 GiB, scale 0–1, external ingress on 8080, single-revision mode. Region `germanywestcentral` (West Europe refused new subscriptions).
- **Registry:** Azure Container Registry (Basic). The app pulls with its system-assigned managed identity (`AcrPull`) — no registry password anywhere.
- **CI/CD:** GitHub Actions, extending the existing `ci.yml` with a `deploy` job (`needs: [backend, client]`, `if: push to main`). Images are tagged with the git commit SHA.
- **Pipeline identity:** a user-assigned managed identity (`id-github-deploy`) with a federated credential trusting GitHub's OIDC issuer for exactly `repo:<owner>/<repo>:ref:refs/heads/main`. No service-principal password; the three IDs it needs are non-secret repository variables.

## Consequences

`git push` to `main` → tests → image → registry → live, in a few minutes, with nothing secret stored in GitHub. Every deploy is addressable by SHA, so rollback is "deploy the previous tag". Blast radius of a leaked pipeline token: one hour, scoped roles only.

Rejected: **Azure DevOps** — second CI system for no gain; Actions is where the repo lives and the Azure concepts (OIDC, `az`, Bicep) are identical. **GHCR** — fine for non-Azure hosts, but pulling from it would require storing a GitHub token in Azure. **Service-principal password in a GitHub secret** — long-lived bearer secret that must be stored and rotated; a copied log or fork could use it. **`latest` tag** — destroys the ability to roll back or know what is running.

Gotchas recorded: renaming the GitHub repo changes the OIDC subject (and it now embeds numeric owner/repo IDs), so the federated credential must be updated. Provider registration (`Microsoft.ContainerRegistry`, `Microsoft.App`, `Microsoft.OperationalInsights`) is a one-time manual step per subscription. Fio's API enforces one request per token per 30 s; a second sync inside that window stalls until the 30 s `HttpClient` timeout.
