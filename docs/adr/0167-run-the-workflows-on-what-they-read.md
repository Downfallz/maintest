# 0167. Run the workflows on what they read

Date: 2026-11-11

Status: Accepted

## Context

Of the last hundred workflow runs, over six hours, ten were the learning loop and ten the studio's publication on
a push to `main`, about twenty were the full CI on pull requests and as many the browser suite. Nearly every one of
those merges changed only the table's host, `src/DownfallArena.Cli/Table/`. The learning loop reads none of it and
spent about twenty runner minutes a seed replaying numbers nothing had moved; the studio's site is its export,
which reads none of it either, and was republished identical. A pull request of documentation alone still built and
tested everything.

## Decision

- **The learning loop no longer runs on a push to `main`.** It runs on Monday's schedule, on a pull request that
  changes `learning/experiments/next.json`, and on demand.
- **The studio's publication leaves out the table's host** (`src/DownfallArena.Cli/Table/`, `Hosting/`): every other
  change under `src/` still republishes it, since the weights and the audit it publishes are the engine's own.
- **The CI skips its build and its tests for a pull request of Markdown alone.** A first job reads the pull
  request's files; the two others depend on it and are skipped when only `.md` files changed, which a required check
  counts as passed. No test reads a Markdown file. A push to `main` and a dispatch always run them.
- **The browser suite leaves out the console commands no page reaches** (`ConsoleAgent`, `ConsoleMatchLog`,
  `EvaluationConsole`) and Markdown.

## Consequences

- Good: a merge to the table's host runs the CI, the browser suite and the deployment, and nothing else.
- Bad: a change to the engine has its learning report on Monday, or when somebody asks for one, not at its merge.
- Bad: a path filter is a list of what each workflow reads, kept by hand: a page that starts to read the console
  commands, or a test that starts to read Markdown, has to say so here.
- Neutral: the browser suite still runs on almost every change under `src/`: it plays the real engine, so most of
  `src/` is what it reads.
