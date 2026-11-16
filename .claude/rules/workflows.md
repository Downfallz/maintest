---
paths:
  - ".github/**"
---

# Workflow rules

- Least permission: `permissions:` at the top is `contents: read`, and a job asks for more only for what it
  writes. `persist-credentials: false` on checkout unless the job pushes.
- Actions are pinned to a full commit SHA, with the version in a comment, as Dependabot keeps them.
- The one required check is `gate`, in `ci.yml` (ADR 0182), and it always reports. A job that judges a change
  joins `ci.yml` and the gate's `needs`; what a run plays is decided from the diff by `studio/browser/plan.js`,
  inside the run, never by a path filter on a workflow the gate depends on.
- A workflow runs on what it reads (ADR 0167): its path filters, or its lines in the plan, list the files its
  steps read, and nothing else. A job that starts to read another path says so in `plan.js` and its test.
- A CI job named or renamed is checked against the groups of `scripts/workflow-metrics.py` (`JOB_GROUPS`),
  or its minutes fall in `other`.
- `main` deploys behind the gate. Its runs queue and are never cancelled half-way.
- No secret is echoed, written to a step summary or passed on a command line.
- A workflow that pushes pushes a branch and proposes it; it never pushes to `main`.
