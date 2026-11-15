# infra/ — the hosted table on Azure

The table at play.downfallinstinct.com behind Cloudflare (ADR 0080-0082, ADR 0162, ADR 0175): `main.bicep` (the
API app, the worker app, the hourly tending job), the one-time `bootstrap.sh`, and `README.md`, which is the
runbook. The image is the root `Dockerfile`, built and tried by `.github/workflows/deploy.yml`.

## Commands

```bash
docker build -t downfall-table . && docker run --rm -p 8080:8080 downfall-table   # the image locally; the token is in the log
az bicep build --file infra/main.bicep && az bicep lint --file infra/main.bicep   # what deploy.yml runs; needs the az CLI
```

## A change here runs

- The gate. `deploy.yml` compiles and lints `main.bicep` on every pull request that touches it; a warning
  fails the run. A session has no `az` and no Azure: say the Bicep was not compiled when it was not.
- A change to the `Dockerfile`: `docker build` where Docker is available, or say it was not built.

## Traps

- The app follows `main`: a merge that touches anything the image carries deploys itself.
- A new role, provider or secret needs `infra/bootstrap.sh setup` run again by the owner before the deploy
  that needs it, or that deploy fails on the role assignment (README.md, "Changing it").
- No secret in a log, a workflow output or a Bicep output. Settings that are secrets go through the platform.
- `.claude/rules/infra.md` holds the rest: what a change costs, and how a step only the owner can do is written.
