---
name: verify
description: Run the local quality gate as CI does - the storage emulator started, the engine built with warnings as errors, format-checked and tested, the pages' tests, and what the diff calls for (the battle bundle, the learning project, the content builder) - and report the results. Use before committing, before opening a PR, or whenever asked "does it pass".
---

# Verify

```bash
scripts/verify.sh          # the steps this branch's changes call for
scripts/verify.sh --all    # every step, whatever the diff
```

The script fetches nothing: run `git fetch origin main` first, or it compares with a stale `main` (and with no
`origin/main` at all it runs every step). It always restores, builds, format-checks and tests the engine and
runs the pages' tests; it adds the battle bundle, the learning project and the content builder when the diff
calls for them (`studio/browser/plan.js --gate`); it starts Azurite when it is installed. It stops at the first
failure, and records the tree it passed on, which quiets the `Stop` hook until the files change again.

Run it in the background (`run_in_background`): it takes three minutes or more, and longer with the learning
project.

Rules:

- If `dotnet` is not available, say so plainly. Do not report the gate as passed.
- If `dotnet format --verify-no-changes` fails, run `dotnet format`, show the resulting diff, and run the gate
  again.
- If a test fails, quote the failing test name and assertion message verbatim. Do not skip or delete it.
- The summary's `note storage tests` line means the storage tests were skipped: report them as skipped, not
  passed.
- The summary's `note browser` line is what CI will play for this diff. When it names journeys, or says
  `real-engine=true` or `release=true`, run `/browser-check` before `/ship`, or say they were left to CI.
- Report a compact summary: each step, pass, fail or skip, and the counts (errors, warnings, tests passed,
  failed, skipped).
