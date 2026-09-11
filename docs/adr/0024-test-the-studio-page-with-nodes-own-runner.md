# 0024. Test the studio page with Node's own test runner

Date: 2026-09-11

Status: Accepted

## Context

[ADR 0023](0023-a-hosted-studio-with-github-as-its-backend.md) gave the studio a hosted mode and left writing
as its next step: a save becomes a Git Trees commit, a branch and a pull request, built by a page in a
browser. That is the first logic in this repository that lives only in the browser **and** can do damage. A
wrong parent on a commit request, a tree entry with the wrong mode, a branch created from the wrong ref — none
of it fails loudly. It writes.

What covers the studio's JavaScript today is `tests/DownfallArena.Cli.Tests/Studio/StudioFilesTests.cs`, which
asserts that the served files *contain* certain strings: that `backend.js` contains
`export function hostedBackend()`, that `studio.js` imports from `./backend.js`. That is a rename detector. It
cannot say whether a commit request carries the right parent, because it never runs the code.

The repository has no JavaScript tooling at all: no `package.json` anywhere, no npm, no bundler, no lockfile.
`AGENTS.md` says not to add a framework without an ADR, which is why this is one.

Two things were measured before deciding, not assumed:

- `node --test studio/*.test.js` runs a test that imports `studio/backend.js` and prints nothing on stderr:
  no experimental warning to explain away, no configuration. The glob is left for the shell to expand, because
  `node --test` only expands one itself on Node 22+ and a quoted pattern is taken as a literal path on Node 20.
- Node 22 will import that file with no `package.json` at all, because it detects module syntax on its own.
  That is a heuristic and a Node 22 behaviour, and the runners' Node version is not ours to pin without adding
  an action, so this decision does not rely on it.

## Decision

We will test the studio's browser modules with **Node's built-in test runner**: `node:test` for the harness and
`node:assert/strict` for the assertions. Nothing is installed: no dependency, no lockfile, no framework, no
bundler. The one file added is `studio/package.json` containing `{"type": "module"}` and nothing else, which
declares what the browser already assumes about these files and makes the tests work on any Node with the
runner rather than only on one that guesses right. The tests are `studio/*.test.js`, and the command is:

```bash
node --test studio/*.test.js
```

Two things follow from choosing it, and they are part of the decision rather than side effects:

- **The transport is injected.** The GitHub backend takes its `fetch` as an argument instead of reaching for
  the global one, so a test drives it with a stub and asserts on the requests it would have made. This is the
  same discipline the domain already has for `TimeProvider` and `IRandomSource`: the untestable thing is a
  parameter.
- **The C# text contracts stay.** They pin what the *served page* contains, which a Node test cannot see —
  the local host serving `backend.js` at all is a fact about the host, not about the module.

## Consequences

- Good: the write path is tested before it can write. The tree, the commit, the branch and the pull request are
  assertable with no network, no token and no repository.
- Good: nothing to install. No lockfile, no supply chain, no Dependabot lane, and the runner is the same Node
  the runners already have. The cost of this decision is one CI step.
- Good: it forces the injected-transport shape, which is the design worth having anyway.
- Bad: **a fourth test runner** — xUnit, pytest, NetArchTest, now `node:test`. Four commands, four reports.
  `AGENTS.md` and the `/verify` skill both have to name it, or it becomes the one nobody runs, which is worse
  than not having it.
- Bad: `node:assert` is thin next to Shouldly. These tests will read less like behaviour sentences than the C#
  ones, and no assertion library will fix that without becoming a dependency.
- Bad: a `package.json` in `studio/` reads as an npm project to anyone who sees it, and to tooling that looks
  for one. It declares a module type and nothing more: there are no dependencies and no lockfile, Dependabot
  has no npm ecosystem configured, and `pages.yml` copies the page's files by name so it is never published.
- Bad: this coverage does not reach SonarCloud. Node can emit lcov and wiring it is deliberately not part of
  this decision, so the quality gate keeps measuring C# and Python only and the studio's JS is covered by
  tests nobody counts.
- Neutral: the tests run in Node, not a browser. They cover logic, not the DOM and not a real `fetch`.

## Alternatives considered

- **Keep only the C# text contracts.** Free, and it is what exists. It cannot test the one thing that can go
  wrong, so it answers a different question than the one ADR 0023 raised.
- **Vitest or Jest.** A real framework with a real assertion library, and a `package.json`, a lockfile, a
  Dependabot lane and a supply chain, for a test surface of one file. The built-in runner covers this surface;
  revisit if the studio's JS grows enough to need fixtures, mocks and a watch mode.
- **Serve a fake GitHub from the .NET side and test through it.** It tests the wrong seam and puts knowledge of
  GitHub in the local host, which has no business having it (ADR 0015 and ADR 0023 keep the two modes apart).
- **Playwright against the real page.** The container already has Chromium, and it would cover the DOM too. It
  is slower, it is a dependency, and it still would not remove the need to assert on the requests. Worth
  revisiting when the studio has interaction bugs rather than transport bugs.
- **Do not test it; a bad commit is one revert.** True, and it misses that the studio would already have
  written to the repository, from a phone, with the maintainer's token. The cheap revert is not the cost.

## Follow-up

- `.github/workflows/ci.yml`: a `node --test` step in the build job.
- `AGENTS.md`: the command, in the Commands block and in the sentence about what to run before declaring a task
  done.
- `.claude/skills/verify/SKILL.md`: run it with the rest of the gate.
- `studio/README.md`: how to run the studio's tests.
- The GitHub backend of ADR 0023 lands with its tests, not after them.
