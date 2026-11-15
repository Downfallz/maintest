---
paths:
  - "infra/**"
  - "Dockerfile"
  - ".github/workflows/deploy.yml"
  - ".github/workflows/image.yml"
  - ".github/workflows/rollout.yml"
---

# Infrastructure rules

- No secret in a log, a workflow output, a Bicep output or a committed file. A secret is a GitHub secret or a
  platform setting, named in `infra/README.md`, never valued there.
- Say what a change costs: a new resource, a larger SKU, a new always-on replica, more egress. Prefer what the
  hosted table already pays for.
- `main` deploys itself. A change that needs the owner first (a role, a provider, a secret, a DNS record) is
  written as a numbered runbook step in `infra/README.md`: one block to paste each, and the way through the
  portal, so it can be done from a phone.
- `main.bicep` must build and lint without a warning (`az bicep build`, `az bicep lint`). A session has no `az`:
  when it could not run them, say so.
- The image carries only what the host serves: the bundle and its notices, the pages, the content.
