# Plan: CI that fits a private repository's minutes, and a shorter wait on every pull request

Status: draft, 2026-11-16. Nothing is built.

## What the owner asked for

"With a move to a private GitHub repository in mind, where Actions minutes are limited, can you speed up the
journeys, the battle release and the rest? And I find it long anyway, in the day-to-day workflow, on every pull
request."

Two budgets, then: the minutes a month a private repository is billed, and the minutes a person waits for `gate`
on each pull request.

## What was read

- `ci.yml`, `studio-browser.yml`, `image.yml`, `nightly.yml`, the three Playwright configurations, `plan.js` and
  its test, ADR 0167 and ADR 0182.
- Every job and step of one complete pull-request run (43801390732, 2026-11-16, everything called for), and
  WEEK_SAMPLE.
- GitHub's and SonarSource's pricing pages as search results summarise them on 2026-11-16. A figure from a
  secondary source is marked as one.

Not measured: a run on a 2-vCPU runner, which is what a private repository gets; the minutes are billed only
once the repository is private, so the week's figures below are what it would have been billed, computed from
each job's start and end.

## What exists today

| Concern | What the code and the runs hold | What it means here |
| --- | --- | --- |
| The pipeline | `ci.yml` plans the run from the diff (`plan.js`, ADR 0182); `gate` is the one required check | The right shape. Nothing below changes it |
| A complete pull-request run | 27 min 24 s of wall clock, 12 jobs, 117 billed minutes (each job rounded up to the minute) | One run is 6% of GitHub Free's month |
| The critical path | `battle-release`: 27 min, of which 25 min 11 s play matches, one test at a time (`workers: 1`) over three profiles | Every other job had finished 7 minutes earlier |
| Mock-host journeys | Four jobs: small-phone 13 min, phone 12, desktop and tablet 1/2 13, 2/2 21 | The two desktop halves differ by 8 minutes |
| Real-engine journeys | table-phone 4 min 30, table-desktop 9 min 20 | |
| The complete match | `battle.loop.js` plays two complete 3v3 matches against the real engine in five projects: table-phone, table-desktop, release-desktop, release-android, release-ios | release-desktop is table-desktop's browser and viewport again; release-android is Chromium on a phone, as table-phone is. Only release-ios adds an engine |
| Build, format, test, analyse | 8 min 40: format check 1 min 21, build 1 min 06, tests 2 min 30, Sonar's end 2 min 26 | Sonar is the largest step after the tests |
| The learning job | 17 s, then `build-and-test` runs pytest again for Sonar's coverage | Billed 1 minute to run ruff |
| Set-up repeated per job | Playwright and its browsers installed in 8 jobs: 18 to 60 s each, nothing cached | 3 to 5 billed minutes a run |
| Nightly | `nightly.yml` plays the whole browser matrix every night, main changed or not | WEEK_NIGHTLY |
| The week | WEEK_TABLE | WEEK_SUMMARY |
| A private repository on GitHub Free | 2,000 minutes a month; `ubuntu-latest` is 2 vCPU and 8 GB, where a public one gets 4 vCPU and 16 GB; rulesets and required checks are not available | ADR 0182's required `gate` cannot be required |
| On GitHub Pro | 3,000 minutes a month, rulesets on private repositories, Pages from a private repository | About $4 a month |
| Beyond the included minutes | Linux 2-core minutes are billed per minute (cut on 2026-01-01). A charge on self-hosted minutes announced for 2026-03-01 was postponed, by secondary sources | Self-hosted minutes cost the machine, not GitHub |
| SonarQube Cloud | Free for public projects; private analysis free up to 50,000 lines of code. A rough count of the code files outside `legacy/` gives 144,570 non-blank lines; Sonar's own count was not read | Private means a paid plan (from $34 a month, by its pricing page) or no Sonar |
| The image | `rollout.yml` checks that `ghcr.io/.../downfall-table` pulls without a credential; the package is public | It stays public whatever the repository becomes |
| Two vCPU | Playwright's default is one worker per two cores: the mock-host journeys go from two workers to one | Their wall clock and their minutes roughly double, by that arithmetic (not measured) |

## The shape

Three kinds of change, in the order they pay.

### Play less: fewer minutes and a shorter wait

1. **The complete match once per browser engine on a pull request.** The release job's own value is WebKit
   (release-ios) and the release budgets. On a pull request it plays release-ios; release-desktop and
   release-android play on `main` before the deployment and in the nightly matrix, where a red one still stops
   production (ADR 0182). release-desktop is dropped wherever table-desktop already plays the same spec.
2. **A narrower plan.** Today a change under `src/` or `data/` calls for the release profiles. The engine and the
   content are judged by the real-engine journeys; the release profiles are called for by `battle/`,
   `table/battle-*`, the release specs and the workflows. On `main` they always play.
3. **The nightly only when `main` moved.** It reads the commit of the last green nightly run, and stops at once
   when `main` is the same.
4. **Sonar on `main` only, or not at all.** A pull request then pays neither the begin nor the end step (about
   2 min 40). The question is the owner's: Sonar is paid once the repository is private.
5. **A Markdown-only diff** builds and runs `Architecture.Tests` (the instruction files and the generated
   indexes) and the pages' own tests, not the whole suite.

### The same work, side by side: a shorter wait at the same minutes

6. **`battle-release` as a matrix, one job per profile.** 25 minutes of matches become three jobs of about 9;
   the critical path drops from 27 minutes to the slowest journey job.
7. **Desktop in three shards**, or the heaviest spec moved, so that no half takes 21 minutes while the other takes 13.
8. **Playwright's browsers cached** by Playwright's version (`~/.cache/ms-playwright`), and the system packages
   still installed: 20 to 40 s off each of eight jobs.
9. **The learning job folded into `build-and-test`**, which already installs uv and runs pytest; ruff joins it.

### Minutes from elsewhere

10. **A self-hosted runner for the browser jobs**, on a machine the owner keeps on or a small VM, with the
    hosted runner as the fallback label. GitHub bills none of its minutes today. A self-hosted runner on a
    private repository runs only the owner's code; it is never offered to a fork.

### The budget, after each kind

BUDGET_TABLE

## Where the code goes

| Change | Files |
| --- | --- |
| The meter | `scripts/workflow-metrics.py` gains `--minutes`: billed minutes per workflow and per CI job group over the last days, from the Actions API |
| The release split and the narrower plan | `studio/browser/plan.js` (`release` becomes `none`, `webkit` or `all`), `studio/browser-suite.test.js`, `studio-browser.yml`, `battle.release.config.js`, `ci.yml` |
| Shards, cache, learning | `studio-browser.yml`, `ci.yml`, `studio/browser-suite.test.js` (the gate's `needs`) |
| Nightly | `nightly.yml`, `.github/scripts/` |
| Sonar, Markdown-only | `ci.yml`, `plan.js` and its test |
| The move | `docs/engineering/private-repository.md`: the owner's steps, numbered, through the portal |
| Self-hosted | `infra/` or `docs/engineering/`, by the owner's answer; `studio-browser.yml` runner labels |

## Stages

1. **M1, the meter.** `workflow-metrics.py --minutes` reports billed minutes per workflow and per CI job group,
   and `/retro` quotes them each week, so every stage after this one shows what it saved. Tested in
   `learning/tests` with a recorded API answer.
2. **M2, the release profiles split and deduplicated.** ADR 0188 amends ADR 0182: a pull request plays the
   release profiles' WebKit alone; `main` and the nightly play all three. `battle-release` becomes one job per
   profile. `plan.js` and `browser-suite.test.js` hold it.
3. **M3, the wait evened out.** Desktop in three shards, Playwright's browsers cached, the learning job folded
   into `build-and-test` (`browser-suite.test.js` holds the gate's `needs`).
4. **M4, the narrower plan.** `src/` and `data/` no longer call for the release profiles on a pull request; a
   Markdown-only diff runs the light build. Tests in `browser-suite.test.js`.
5. **M5, the nightly when `main` moved, and Sonar where the owner settles it.** `nightly.yml` stops on an
   unchanged `main`; the Sonar steps follow the owner's answer.
6. **M6, the move.** `docs/engineering/private-repository.md`: the plan to buy, the ruleset to recreate, the
   package that stays public, Pages, Sonar, Codex's access, Dependabot, and what the sessions need. The owner
   performs it; a session checks it from the next run's minutes.
7. **M7, a self-hosted runner**, if the owner chooses one: an ADR (who can run on it, the fallback, the way
   back), the runner's set-up, and the browser jobs' labels. Reviewed by `security-reviewer` and `infra-reviewer`.

## Questions for the owner

1. Which GitHub plan for the private repository: Pro (3,000 minutes, rulesets, so `gate` stays required), or Free
   (2,000 minutes, and nothing refuses a merge on a red gate)?
2. Where the minutes beyond the plan come from: a self-hosted runner for the browser jobs, paying GitHub's
   overage, or keeping the repository public until the cuts are measured?
3. What a pull request plays of the release profiles: WebKit alone, the three on `main` and every night; the
   three on every pull request, as today; or none on a pull request?
4. SonarQube Cloud once private: drop it and keep the analyzers (warnings as errors, ruff); pay for it; or run it
   on `main` only?
5. The nightly matrix: only when `main` moved since the last green one; once a week; or every night, as today?
6. A Markdown-only pull request: the light build (`Architecture.Tests` and the pages' tests), or the whole
   build-and-test job, as today?
