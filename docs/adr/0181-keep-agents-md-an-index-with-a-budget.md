# 0181. Keep AGENTS.md an index with a budget, and load an area's instructions with the area

Date: 2026-11-14
Status: Accepted (stage W1 of [the AI workflow plan](../engineering/ai-workflow-plan.md), settled by the owner)

## Context

`AGENTS.md` is loaded into every session and every subagent. It had grown to 43 KB, 68% of it a Commands block
whose comments were the only description of a route or a flag; one line ran past 15,000 characters. It changed in
101 of the last 300 pull requests and was the file parallel sessions most often conflicted on. Most of what it held
mattered only to a change in one area (the table host, the pages, the battle view, learning, infrastructure), yet
every session paid for all of it, and nothing guided a change to those areas.

## Decision

We will keep the root `AGENTS.md` an index under a budget: under 200 lines, under 12 KB, no line over 300
characters, held by a test. It says what the project is, one line per directory, the gate's commands, the
architecture rules, the conventions and the definition of done. Every command, flag and route is described once,
in `docs/reference/commands.md`, and the table's routes are listed in `table/README.md`. Each area with its own
commands and traps (`table/`, `battle/`, `studio/`, `learning/`, `infra/`, `src/DownfallArena.Cli/`) keeps an
`AGENTS.md`, with a one-line `CLAUDE.md` beside it that imports it, so its instructions load when an agent first
touches the area. Concerns that span directories with no common parent are path-scoped rules in
`.claude/rules/`. `docs/README.md` lists every plan, and a test checks it.

## Consequences

- Good: a session starts on a fraction of the old contract; a new flag or route edits a reference page, not the
  file every branch edits; an area's traps are read by the change that meets them.
- Good: the budget is a failing test, not a reminder that thins out in a long session.
- Bad: one fact may be wanted in two places; the rule is one home per fact and a link from the other.
- Bad: tools that only read the root `AGENTS.md` see less; the index points to where the rest lives.
- Neutral: the numbers in the budget are a choice and may move with a new ADR.

## Alternatives considered

- Trim the Commands block in place: it grows back with the next feature, and still loads everywhere.
- Rules only, no area files: other tools than Claude Code read the nearest `AGENTS.md`, not `.claude/rules/`.
- Routes generated from the host's code: the host routes by hand-written matching; a generator is a later stage.

## Follow-up

- `tests/DownfallArena.Architecture.Tests/InstructionFileTests.cs` holds the budget, checks each area's
  `CLAUDE.md` imports its `AGENTS.md`, and checks each `docs/**/*plan*.md` is listed in `docs/README.md`.
- `.claude/rules/docs.md` points new commands at `docs/reference/commands.md`.
- 2026-11-17 (#676): a row of the repository map stays under 200 characters, held by the same test. A row names
  its directory and points to where the detail lives; the `scripts/` row, which listed every subfolder, had left
  the file 50 bytes under its budget. `scripts/README.md` holds that detail now.
