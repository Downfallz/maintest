---
name: browser-check
description: Run the browser specs the current diff calls for (studio/browser/plan.js) in the background - the mock-host journeys, the real-engine journeys and the release profiles - and report what passed, failed or flaked. Use after /verify names journeys, before shipping a change to table/, studio/, battle/ or the host, or when asked to "run the browser tests".
argument-hint: "[--all | spec files]"
---

# Browser check

CI plays what `studio/browser/plan.js` chooses from the diff (ADR 0182); this plays the same set here, so a red
journey is found before the pull request. Arguments: $ARGUMENTS (`--all` for everything, or spec file names).

1. What the diff calls for:

   ```bash
   git fetch origin main
   { git diff --name-only "$(git merge-base HEAD origin/main)"; git ls-files --others --exclude-standard; } \
     | sort -u | node studio/browser/plan.js --pull-request
   ```

   `journeys` is `none`, `all`, or spec files (`journey-jobs`, the CI jobs they play in); `real-engine` is the
   other job; `release` is `none` or `webkit`: a pull request plays release-ios alone, and not for `src/` or
   `data/` (without the flag, `all`: `main` plays the three profiles, ADR 0190, 0192); `build` is `light` when CI
   builds and runs `Architecture.Tests` alone. Nothing called for: say so and stop.
2. Prepare once: `npm --prefix studio/browser ci --ignore-scripts`, and the battle bundle
   (`npm --prefix battle ci --ignore-scripts && npm --prefix battle run build`) if `table/battle-build/` is
   missing or older than a change to `battle/` or `table/`. Chromium is preinstalled in a cloud session: never run `playwright install`; point the suite at it
   with `STUDIO_CHROMIUM=/opt/pw-browsers/chromium` when it does not find it on its own.
3. Run in the background (`run_in_background`), one command per job, and keep working while they run:
   - Journeys, from `studio/browser`:
     `node node_modules/playwright/cli.js test <specs, or nothing for all> --trace off --reporter=dot,json`
     with `PLAYWRIGHT_JSON_OUTPUT_NAME` set to a file in the scratchpad. On a change a person sees, all four
     projects; otherwise `--project=phone --project=desktop` first, and the rest before shipping.
   - Real engine, when called for: `dotnet build src/DownfallArena.Cli` and the catalogue
     (`dotnet run --project tools/DownfallArena.DataBuilder -- data data/dst`: `data/dst/` is ignored by git and
     the host will not start without it), then
     `node node_modules/playwright/cli.js test --config table.config.js --trace off`.
   - Release, when called for: `--config battle.release.config.js --project=release-ios`, only with WebKit
     installed; otherwise say it was left to CI.
4. Read the result when the task notifies, never by polling. `node studio/browser/flakes.js < <report>` names a
   test that passed only on a retry; locally nothing retries, so a failure that passes alone is a flake: say so
   and open a `flaky` issue in `/wrap-up`.
5. Report each job: run or left to CI, passed, failed (the spec, the project, the assertion verbatim), flaked.
   A failure is fixed or reported, never skipped.
