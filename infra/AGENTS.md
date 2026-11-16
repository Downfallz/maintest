# infra/ — the hosted table on Azure

The table at play.downfallinstinct.com behind Cloudflare (ADR 0080-0082, ADR 0162, ADR 0175): `main.bicep` (the
API app, the worker app, the hourly tending job, the alerts), the one-time `bootstrap.sh`, `README.md`, and
`runbooks/`, the owner's steps written for a phone. Production is read by `ops-report.yml` as an identity that
cannot write (ADR 0186); `/ops` reads its log. The image is the root `Dockerfile`, built and tried by `.github/workflows/image.yml`
and deployed by `rollout.yml`, which CI calls on `main` once its gate is green (ADR 0182); `deploy.yml` is the
same two by hand.

## Commands

```bash
docker build -t downfall-table . && docker run --rm -p 8080:8080 downfall-table   # the image locally; the token is in the log
az bicep build --file infra/main.bicep && az bicep lint --file infra/main.bicep   # what image.yml runs; needs the az CLI
```

## A change here runs

- The gate. `image.yml` compiles and lints `main.bicep` on every pull request that touches it; a warning
  fails the run. A session has no `az` and no Azure: say the Bicep was not compiled when it was not.
- A change to the `Dockerfile`: `docker build` where Docker is available, or say it was not built.

## Traps

- The app follows `main`: a merge that touches anything the image carries deploys itself.
- A new role, provider or secret needs `infra/bootstrap.sh setup` run again by the owner before the deploy
  that needs it, or that deploy fails on the role assignment (README.md, "Changing it").
- No secret in a log, a workflow output or a Bicep output. Settings that are secrets go through the platform.
- The report's log is public: `scripts/ops/report.mjs` prints counts and masked lines. A new reading is masked
  and tested in `studio/ops.test.js` before it is printed.
- `.claude/rules/infra.md` holds the rest: what a change costs, and how a step only the owner can do is written.
