# 0190. Play the release profiles' WebKit alone on a pull request

Date: 2026-11-16
Status: Accepted (settled by the owner, docs/engineering/ci-minutes-plan.md question 3). Amends [0182](0182-require-one-gate-check-and-deploy-behind-it.md) (what a pull request plays)

## Context

One job played two complete 3v3 matches in each of three profiles, one test at a time. It took 27 minutes, 25
of them in those matches. It was the longest job of a complete run: every other job finished 7 minutes before
it. Two of the three profiles repeat the engine the real-engine journeys already play, Chromium:
release-desktop is table-desktop's browser at the same viewport, and release-android is Chromium on an emulated
phone, as table-phone is. Only release-ios brings an
engine no other job plays, WebKit. A private repository is billed these minutes (the plan's budget), and every
pull request waits for them.

## Decision

The plan (`studio/browser/plan.js`) says which release profiles a run plays: `none`, `webkit` or `all`. A pull
request whose diff calls for the release profiles plays `webkit`, release-ios alone (`plan.js --pull-request`, from
`ci.yml`); a push to `main`, the nightly matrix, a run asked by hand, and a pull request that changes the
release job's own definition (`studio-browser.yml`, `battle.release.config.js`, `plan.js`) play `all`. The release job is a matrix of
one job per profile, side by side, each installing only its own engine and keeping its own artifact
(`battle-release-<profile>`). Everything else in ADR 0182 stands: `gate` is still the one required check, and on
`main` the deployment still waits for the three profiles.

## Consequences

- Good: a pull request plays one release profile where it played three: about 9 of the 25 minutes by the plan's
  estimate, not yet measured (M1's meter reads it). On `main` the three play side by side, so the release job
  stops being the critical path.
- Good: production still never receives a change the three profiles refused, since `main` plays them before the
  deployment.
- Bad: a change that breaks only the Chromium release profiles (their budgets in Chromium, the 1440 px release
  screenshots) is found after its merge, by `main`'s run, which then holds the deployment and opens `red-main`.
- Bad: the three jobs on `main` each install, build the engine and the bundle: the same play minutes as one
  job, plus two more set-ups, each a few billed minutes (not yet measured).
- Bad: each job's timeout drops from 35 to 25 minutes on the same estimate; the first runs on `main` say
  whether WebKit's job, the slowest, fits with its retry.
- Neutral: the profiles the matrix names are held against `battle.release.config.js` by
  `studio/browser-suite.test.js`; a profile added there and not here fails that test.

## Alternatives considered

- Drop release-desktop and release-android everywhere: they are the only Chromium runs of the release budgets
  and screenshots, and the owner keeps them before the deployment.
- Keep one job and play WebKit alone on a pull request: shorter on a pull request, but `main` keeps a 25-minute
  job in front of the deployment.
- Shard the one job with Playwright's `--shard`: shards split by test, not by profile, so each would still
  install both engines.

## Follow-up

`studio/browser/plan.js`, `studio/browser-suite.test.js`, `.github/workflows/ci.yml` and
`.github/workflows/studio-browser.yml` change with this decision; `.claude/skills/browser-check/SKILL.md` and
`docs/tabletop/battle-release-acceptance.md` name the new outputs and artifacts.
