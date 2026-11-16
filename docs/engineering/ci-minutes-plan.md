# Plan: CI that fits a private repository's minutes, and a shorter wait on every pull request

Status: accepted in part, 2026-11-16. The owner settled questions 1, 2, 3, 5 and 6; question 4 (Sonar) is open.
M1 (the meter) and M2 (the release profiles split) are built.

## What the owner asked for

"With a move to a private GitHub repository in mind, where Actions minutes are limited, can you speed up the
journeys, the battle release and the rest? And I find it long anyway, in the day-to-day workflow, on every pull
request."

Two budgets, then: the minutes a month a private repository is billed, and the minutes a person waits for `gate`
on each pull request.

## What was read

- `ci.yml`, `studio-browser.yml`, `image.yml`, `nightly.yml`, the three Playwright configurations, `plan.js` and
  its test, ADR 0167 and ADR 0182.
- Every job and step of one complete pull-request run (43801390732, 2026-11-16, everything called for).
- The 959 runs completed from 2026-11-09 to 2026-11-16, and the jobs of 26 of them: 15 of CI's current layout
  (ADR 0182 merged on 2026-11-15, so about 27 hours of the week ran it), the rest of the old one, the nightly,
  Pages, the learning loop and the ops report.
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
| Nightly | `nightly.yml` plays the whole browser matrix every night, main changed or not | 81 billed minutes a night, about 2,450 a month |
| The week | 959 runs: CI 397 (269 on pull requests, 128 on `main`), 181 cancelled. 63 of the 115 runs on the current layout played the browser jobs, 19 of them on `main` | About 49.6 billed minutes a CI run on the current layout; a complete one costs 97 to 113 |
| Where CI's minutes go | Journeys 41%, battle-release 20%, real engine 17%, build-and-test 14%, changes, gate and main-health 4%, the rest 4% | Most of the cost is the browser |
| Cancelled runs | 35 of the 115 current-layout runs were cancelled, by a newer push or by a newer merge, after spending 22% of the minutes | A push while a run plays throws away up to 113 minutes |
| At this pace, private | About 20,500 billed minutes a week, 88,000 a month (estimated from the sample) | 29 times GitHub Pro's 3,000 |
| A private repository on GitHub Free | 2,000 minutes a month; `ubuntu-latest` is 2 vCPU and 8 GB, where a public one gets 4 vCPU and 16 GB; rulesets and required checks are not available | ADR 0182's required `gate` cannot be required |
| On GitHub Pro | 3,000 minutes a month, rulesets on private repositories, Pages from a private repository | About $4 a month |
| Beyond the included minutes | Linux 2-core minutes are billed per minute (cut on 2026-01-01). A charge on self-hosted minutes announced for 2026-03-01 was postponed, by secondary sources | Self-hosted minutes cost the machine, not GitHub |
| SonarQube Cloud | Free for public projects; private analysis free up to 50,000 lines of code. A rough count of the code files outside `legacy/` gives 144,570 non-blank lines; Sonar's own count was not read | Private means a paid plan (from $34 a month, by its pricing page) or no Sonar |
| The image | `rollout.yml` checks that `ghcr.io/.../downfall-table` pulls without a credential; the package is public | It stays public whatever the repository becomes |
| Two vCPU | Playwright's default is one worker per two cores: the mock-host journeys go from two workers to one | Their wall clock and their minutes roughly double, by that arithmetic (not measured) |

## The shape

The reading settles the first question before any lever is pulled: no cut of the work brings 88,000 minutes a
month under 3,000. Cutting what CI plays shortens the wait and is worth doing for itself; the minutes of a
private repository are answered by where the jobs run.

Four kinds of change, in the order they pay.

### Play less: fewer minutes and a shorter wait

1. **The complete match once per browser engine on a pull request.** The release job's own value is WebKit
   (release-ios) and the release budgets. On a pull request it plays release-ios; release-desktop and
   release-android play on `main` before the deployment and in the nightly matrix, where a red one still stops
   production (ADR 0182). release-desktop is dropped wherever table-desktop already plays the same spec.
2. **A narrower plan.** Today a change under `src/` or `data/` calls for the release profiles. The engine and the
   content are judged by the real-engine journeys; the release profiles are called for by `battle/`,
   `table/battle-*`, the release specs and the workflows. On `main` they always play.
3. **One push per round of review.** `/ship` answers every thread, then pushes once; a push while a run plays
   cancels it, and 22% of the week's minutes went to runs cancelled that way.
4. **The nightly only when `main` moved.** It reads the commit of the last green nightly run, and stops at once
   when `main` is the same.
5. **Sonar on `main` only, or not at all.** A pull request then pays neither the begin nor the end step (about
   2 min 40). The question is the owner's: Sonar is paid once the repository is private.
6. **A Markdown-only diff** builds and runs `Architecture.Tests` (the instruction files and the generated
   indexes) and the pages' own tests, not the whole suite.

### The same work, side by side: a shorter wait at the same minutes

7. **`battle-release` as a matrix, one job per profile.** 25 minutes of matches become three jobs of about 9;
   the critical path drops from 27 minutes to the slowest journey job.
8. **Desktop in three shards**, or the heaviest spec moved, so that no half takes 21 minutes while the other
   takes 13.
9. **Playwright's browsers cached** by Playwright's version (`~/.cache/ms-playwright`), the system packages still
   installed: 20 to 40 s off each of eight jobs.
10. **The learning job folded into `build-and-test`**, which already installs uv and runs pytest; ruff joins it.

### Minutes from elsewhere

11. **A runner of the project's own** for the heavy jobs (build-and-test, the journeys, the real engine, the
    release profiles), and GitHub's hosted runner for the small ones (changes, gate, main-health). GitHub bills
    no self-hosted minute today; the machine is the cost. One machine runs one job per runner instance, so an
    8-core machine with four instances plays the eight browser jobs in two waves. A self-hosted runner on a
    private repository runs only the owner's branches; it is never offered to a fork.

### The budget, after each kind

Estimated from the week's sample, on the current layout, per week. The private runner's 2 vCPU would make the
browser rows larger; that is not in these numbers.

| After | Billed hosted minutes a week | A month | Wait for `gate`, complete run |
| --- | --- | --- | --- |
| Today | ~20,500 | ~88,000 | 27 min |
| 1 and 2 (release on pull requests) | ~18,000 | ~77,000 | 27 min |
| 3 (one push per review round) | ~15,000 | ~65,000 | 27 min |
| 4 to 6 and 9, 10 (Sonar off pull requests, cache, learning) | ~13,500 | ~58,000 | 25 min |
| 7 and 8 (side by side) | ~13,500 | ~58,000 | 13 to 15 min |
| 11 (own runner for the heavy jobs) | ~800 | ~3,500, or under 3,000 with the small jobs folded | depends on the machine |

On GitHub's hosted runners alone, the month after every cut is about 55,000 billable minutes: roughly $330 at
$0.006 a minute (secondary sources). Under GitHub Pro's 3,000 means about 700 hosted minutes a week: seven
complete runs.

## Where the code goes

| Change | Files |
| --- | --- |
| The meter | `scripts/workflow-metrics.py` gains `--minutes`: billed minutes per workflow and per CI job group over the last days, from the Actions API |
| The release split and the narrower plan | `studio/browser/plan.js` (`release` becomes `none`, `webkit` or `all`), `studio/browser-suite.test.js`, `studio-browser.yml`, `battle.release.config.js`, `ci.yml` |
| Shards, cache, learning | `studio-browser.yml`, `ci.yml`, `studio/browser-suite.test.js` (the gate's `needs`) |
| Nightly | `nightly.yml`, `.github/scripts/` |
| Sonar, Markdown-only, one push | `ci.yml`, `plan.js` and its test, `.claude/skills/ship/SKILL.md` |
| The move | `docs/engineering/private-repository.md`: the owner's steps, numbered, through the portal |
| Own runner | `infra/` or `docs/engineering/`, by the owner's answer; `runs-on` in `ci.yml`, `studio-browser.yml`, `image.yml` |

## Stages

1. **M1, the meter.** `workflow-metrics.py --minutes` reports billed minutes per workflow and per CI job group,
   and `/retro` quotes them each week, so every stage after this one shows what it saved. Tested in
   `learning/tests` with a recorded API answer.

   *Built (2026-11-16)*: `--minutes`, and `--days` (7 by default, whole UTC days ending yesterday). Each job is
   billed from its start to its end, rounded up to the minute. A skipped job bills nothing, every attempt of a
   re-run is read, and a job on a runner of the project's own is counted apart, ready for M6.

   It reports the hosted minutes and a month's estimate, the minutes per workflow, per CI run and per CI job
   group (the groups of "Where CI's minutes go"), and the cancelled jobs' share. The runs are read one day at a
   time, because the API lists at most 1,000 runs a query. `learning/tests/test_workflow_minutes.py` holds it,
   with run 43801390732 recorded: the meter bills it at the plan's 117 minutes, over its 12 jobs that ran.
   `/retro` step 2 reports it, and `docs/reference/commands.md` names the flags.

   The first reading, `--minutes --days 7` on 2026-11-16, for 2026-11-09 to 2026-11-15: 917 runs, 19,453 hosted
   minutes (about 83,400 a month), 3,631 of them in cancelled jobs. It is one week, and most of it ran CI's old
   layout, where the browser jobs were the "Studio browser" workflow's (11,848 minutes) and not CI's: CI's own
   groups and its 16.7 minutes a run describe that layout, not today's. The first `/retro` with minutes is the
   baseline.

   Left: the deploy and showcase jobs fall in `other`. They get a group of their own when a stage needs it.
2. **M2, the release profiles split and deduplicated.** ADR 0190 amends ADR 0182: a pull request plays the
   release profiles' WebKit alone; `main` and the nightly play all three. `battle-release` becomes one job per
   profile. `plan.js` and `browser-suite.test.js` hold it.

   *Built (2026-11-16)*: the number became ADR 0190, 0188 being the rulebook's. `plan.js` returns `release` as
   `none`, `webkit` or `all`; `--pull-request`, which `ci.yml` passes on a pull request, narrows `all` to
   `webkit`, and a push, the nightly and a run by hand play `all`. `studio-browser.yml` takes `release` as one
   of those three values (a choice list when the run is started by hand), and `battle-release` is a matrix of
   one job per profile, each installing its own engine and keeping its artifact `battle-release-<profile>`.
   `browser-suite.test.js` holds the plan on a pull request and on `main`, the matrix's profiles against
   `battle.release.config.js`, and `ci.yml`'s flag. The jobs are named `battle-release (<profile>)`, which the
   meter's `battle-release` group reads (`test_workflow_minutes.py` holds it). `/browser-check` and
   `scripts/verify.sh` read the pull request's plan.

   Not done: release-desktop is not dropped on `main` where table-desktop plays the same spec, since the owner
   keeps the three profiles before the deployment (question 3). Not measured: the saving; the first `/retro`
   after the merge reads it with M1's meter. M4 narrows which paths call for the release profiles at all.
3. **M3, the wait evened out.** Desktop in three shards, Playwright's browsers cached, the learning job folded
   into `build-and-test` (`browser-suite.test.js` holds the gate's `needs`).
4. **M4, the narrower plan and one push per review round.** `src/` and `data/` no longer call for the release
   profiles on a pull request; a Markdown-only diff runs the light build; `/ship` pushes once per round. Tests in
   `browser-suite.test.js`, and `prompts.test.js` for the skill.
5. **M5, the nightly when `main` moved, and Sonar where the owner settles it.** `nightly.yml` stops on an
   unchanged `main`; the Sonar steps follow the owner's answer.
6. **M6, a runner of the project's own.** An ADR (which jobs, which machine, who can run on it, the fallback to
   the hosted runner, the way back), the runner's set-up as a script and a runbook, and the jobs' `runs-on`
   labels. Reviewed by `security-reviewer` and `infra-reviewer`. Needed before the move, by the budget above.
7. **M7, the move.** `docs/engineering/private-repository.md`: the plan to buy, the ruleset to recreate, the
   package that stays public, Pages, Sonar, Codex's access, Dependabot, and what the sessions need. The owner
   performs it; M1's meter reads the first week after it.

## Questions for the owner

4. SonarQube Cloud once private: drop it and keep the analyzers (warnings as errors, ruff); pay for it; or run it
   on `main` only?

## Questions the owner settled (2026-11-16)

1. **The plan: GitHub Pro.** 3,000 minutes, rulesets on a private repository, so `gate` stays the required check
   (ADR 0182), and Pages from a private repository. M7 names it first.
2. **The heavy jobs run on a small cloud machine paid by the month**, a self-hosted runner always on, with
   GitHub's hosted runner as the fallback. M6's ADR chooses the machine, its size and its provider.
3. **A pull request plays the release profiles' WebKit alone**; release-desktop and release-android play on
   `main` before the deployment and every night. M2.
5. **The nightly matrix plays only when `main` moved** since the last green one. M5.
6. **A Markdown-only pull request runs the light build**: the build, `Architecture.Tests` and the pages' own
   tests. M4.
