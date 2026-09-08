# 0002. Restart from a clean slate, freeze the prototypes

Date: 2026-09-08
Status: Accepted

## Context

The repository held several overlapping generations of the engine (`DA.Core.*`, `DA.Game.*`, `Domain2`),
targeting .NET 6 through 9, none buildable as a whole and all work in progress. The design ideas in them are
valuable; the code is not a foundation.

## Decision

We will start a new solution at the repository root (`DownfallArena.slnx`, `src/`, `tests/`, `docs/`) and
move every previous project under `legacy/`, unchanged and unbuilt. Nothing in `src/` or `tests/` may
reference `legacy/`. Concepts are carried over deliberately, one at a time, through the glossary and ADRs.
The folder is deleted once nothing in it is needed; the last commit before the restart on `main` is
`185fe30`.

## Consequences

- Good: a buildable, enforced baseline from day one. Agents cannot accidentally build on abandoned code.
- Bad: the legacy tree still shows up in searches. Mitigated by `legacy/README.md`, the deny rules in
  `.claude/settings.json`, and the instruction in `AGENTS.md` to treat it as read-only.
- Neutral: the game design ideas must be re-documented before they are re-implemented, which is the point.

## Alternatives considered

- Delete the prototypes outright: loses convenient access to the design record; history alone is awkward
  to browse for agents.
- Keep building on `Domain2`: it was the most complete generation, but it dragged in the older layers and
  their inconsistencies.

## Follow-up

- `legacy/README.md` explains the rules.
- Delete `legacy/` when the glossary and game rules cover everything worth keeping.
