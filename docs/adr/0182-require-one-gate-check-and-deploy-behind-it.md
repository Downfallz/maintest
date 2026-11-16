# 0182. Require one gate check, and deploy behind it

Date: 2026-11-15
Status: Accepted. Amended by [0190](0190-play-the-release-profiles-webkit-alone-on-a-pull-request.md) (a pull request plays the release profiles' WebKit alone), [0192](0192-narrow-what-a-pull-request-plays-and-builds.md) (what a pull request builds)

## Context

Three workflows judged a change: `ci.yml` on every pull request, the browser suite and the image behind path
filters. A filtered workflow reports no check, so neither could be required, and a merge went in before the
checks finished in 5 of the 8 latest sessions read (docs/engineering/ai-workflow-plan.md). The browser suite ran
on pull requests only, so a journey red on `main` was found by the next pull request, and `deploy.yml` deployed
`main` without it. The suite takes 30 to 45 minutes whatever the diff, and a flaky journey is red or re-run by hand.

## Decision

`ci.yml` is the one pipeline. Its `changes` job decides what the run plays from the changed paths
(`studio/browser/plan.js`, tested by `studio/browser-suite.test.js`): which mock-host specs, whether the
real-engine and release jobs run, whether the image is built. The browser suite (`studio-browser.yml`), the image
(`image.yml`) and the deployment (`rollout.yml`) are workflows it calls. A `gate` job needs every job before it, always runs, and passes when each succeeded or was
skipped by the plan: it is the one check the ruleset on `main` requires. On `main` the deployment waits for the
gate and runs when the image was rebuilt. A pull request answers for its own diff; a push to `main` for
everything since `main` was last green, and its runs queue instead of being cancelled. Each journey gets one
retry in CI, and a step says in the log which tests passed only on it. A red `main` keeps one issue open
(`red-main`), closed by the next green run; the whole matrix runs every night (`nightly.yml`) and keeps `red-nightly` the same way.

## Consequences

- Good: one required check that always reports; a change no page reads plays no journey and is green in minutes.
- Good: `main` is played by the browser suite, and production never receives what the suite refused.
- Good: a change whose own run went red is deployed by the commit that repairs it, even one the image does not
  carry, because that run reads from the last green one.
- Bad: a deployment waits for the suite, 20 to 30 minutes after a merge where it took 5.
- Bad: the plan is a list of what each job reads, kept by hand, as ADR 0167's filters were. A wrong line plays
  too little, and only the nightly matrix finds it.
- Bad: a retry hides a journey that fails once in two. The flake report names it; nothing yet counts them.
- Neutral: `deploy.yml` run by hand still builds and deploys a commit without the gate: it is the way back.

## Alternatives considered

- Require the existing checks: a filtered workflow reports none, so a documentation change would wait forever.
- Play the whole suite on every pull request: 30 to 45 minutes on each of up to 22 a day, and slower than the merge.
- Have `deploy.yml` poll for the gate's check: an idle runner for the length of the suite, and a red run's change
  stays undeployed when its repair touches no deployed path.
- Trigger the deployment with `workflow_run`: it runs the default branch's file on another commit's event, and
  loses the diff.

## Follow-up

The owner changes the ruleset on `main` to require `gate` alone, once this has merged and reported once.
`.claude/rules/workflows.md`, `studio/AGENTS.md`, `infra/AGENTS.md` and `infra/README.md` describe the pipeline.
