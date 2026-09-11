---
name: verify
description: Run the full local quality gate (restore, build with warnings as errors, format check, all tests) exactly as CI does and report the results. Use before committing, before opening a PR, or whenever asked "does it pass".
disable-model-invocation: false
---

# Verify

Run these commands from the repository root, in order, stopping at the first failure:

```bash
dotnet restore
dotnet build --no-restore
dotnet format --verify-no-changes --no-restore
dotnet test --no-build
node --test "studio/*.test.js"
```

Rules:

- If `dotnet` is not available, say so plainly. Do not report the gate as passed.
- `node --test` needs no install: it is Node's own runner and the studio has no dependencies (ADR 0024).
- If `dotnet format --verify-no-changes` fails, run `dotnet format` to fix it, show the resulting diff, and
  re-run the gate.
- If a test fails, quote the failing test name and assertion message verbatim. Do not skip or delete it.
- Report a compact summary: each command, pass or fail, and counts (errors, warnings, tests passed/failed).
