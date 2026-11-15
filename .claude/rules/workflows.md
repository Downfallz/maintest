---
paths:
  - ".github/**"
---

# Workflow rules

- Least permission: `permissions:` at the top is `contents: read`, and a job asks for more only for what it
  writes. `persist-credentials: false` on checkout unless the job pushes.
- Actions are pinned to a full commit SHA, with the version in a comment, as Dependabot keeps them.
- A required check always reports: a path filter skips steps inside the job, never the job itself, or a pull
  request it filters out waits for a check that never comes.
- A workflow runs on what it reads (ADR 0167): its path filters list the files its steps read, and nothing else.
- No secret is echoed, written to a step summary or passed on a command line.
- A workflow that pushes pushes a branch and proposes it; it never pushes to `main`.
