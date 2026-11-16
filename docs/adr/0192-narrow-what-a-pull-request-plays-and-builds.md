# 0192. Narrow what a pull request plays and builds

Date: 2026-11-16
Status: Accepted (settled by the owner, docs/engineering/ci-minutes-plan.md question 6 and the plan's shape, M4). Amends [0182](0182-require-one-gate-check-and-deploy-behind-it.md) (what a pull request builds) and [0190](0190-play-the-release-profiles-webkit-alone-on-a-pull-request.md) (which paths call for the release profiles on a pull request)

## Context

A pull request still played the release profiles' WebKit for any change under `src/`, `tools/` or `data/`, though
the real-engine journeys already judge the engine and the content. A pull request of Markdown alone still ran the
whole of `build-and-test`: the learning project, the storage emulator, the battle bundle, the format check, every
test project, Sonar, the content and the benchmark, about 9 minutes for files that only `Architecture.Tests` and the
pages' own tests read. And a journeys job the plan left empty, `desktop replays` most often, still set up a runner
to play nothing. A private repository is billed every one of those minutes.

## Decision

On a pull request, the release profiles are called for by what WebKit renders and by the release job itself:
`battle/`, `table/`, the release specs and fixtures, the workflows and the release job's definition. `src/`,
`tools/`, `data/` and the build files (`global.json`, `Directory.Build.props`, `Directory.Packages.props`,
`DownfallArena.slnx`) call for them on `main` alone, where the three profiles still play before the deployment.

A pull request that changes only Markdown runs the light build, when no test outside `Architecture.Tests` and the
pages' own tests reads those files (`data/README.md` is read by the learning tests). `build-and-test` then builds,
runs `Architecture.Tests` and the pages' tests, and skips the rest, Sonar included. `main` always runs the whole
build.

The plan (`studio/browser/plan.js`) says both (`release`, `build`), and holds
the journeys jobs with their Playwright arguments (`JOURNEY_JOBS`): CI starts only the jobs the planned specs play
in (`journey-jobs`). `gate` is still the one required check.

## Consequences

- Good: an engine or content pull request no longer waits for a WebKit release job; a Markdown pull request waits
  for a build and two test steps, not the whole job; a journeys job with nothing to play is not started.
- Bad: a change under `src/` or `data/` that breaks only a release profile is found by `main`'s run, which holds
  the deployment and opens `red-main`, as ADR 0190 already accepted for the Chromium profiles.
- Bad: a Markdown pull request is not analysed by Sonar; Sonar reads no Markdown, so nothing it would report is
  lost, but the pull request shows no Sonar result.
- Bad: `build-and-test` now waits for the `changes` job (a checkout with history, about half a minute) before it
  starts.
- Neutral: when a test outside the light build starts to read a Markdown file, that file joins `plan.js`'s
  list, and `studio/browser-suite.test.js` holds it, as for any path a job starts to read (ADR 0167).

## Alternatives considered

- Keep `src/` and `data/` calling for WebKit: the real-engine journeys already play the complete match on the same
  engine build, and WebKit's own risk lies in the pages it renders.
- Skip `build-and-test` altogether for Markdown: `Architecture.Tests` holds the instruction files' budget, the
  ADRs and the generated indexes, and the pages' tests hold the prompts and the rulebook, all Markdown.
- A path filter on the workflow: a filtered workflow reports no check, and `gate` must always report (ADR 0182).

## Follow-up

`studio/browser/plan.js`, `studio/browser-suite.test.js`, `.github/workflows/ci.yml` and
`.github/workflows/studio-browser.yml` change with this decision; `.claude/skills/browser-check/SKILL.md` names the
new outputs.
