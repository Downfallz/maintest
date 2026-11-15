---
name: stage
description: Start one stage of an accepted plan in a fresh session - read the stage and the ADRs it names, branch from origin/main, write the tests first, build it, pass the gate and ship it. Use when the owner names a plan and a stage id ("/stage docs/engineering/ai-workflow-plan.md W5", "fais le W5").
argument-hint: "<plan path> <stage id>"
---

# Build a stage

The stage: $ARGUMENTS

One stage, one session, one pull request (the standing orders in `CLAUDE.md`).

1. Find the stage. With only an id ("W5", "A3b"), find its plan: `grep -rlE '\*\*<id>[,.]' docs --include='*plan*.md'`.
   Read the whole plan, then the stage's line and any `*Built*` note of the stages before it: what they left to
   this one is part of it. Read the questions the owner settled; an open question the stage depends on is asked
   with AskUserQuestion before any code.
2. Start from fresh `main`: `git fetch origin main`, then a new branch from `origin/main` (the session's own branch
   name when one is given). Never build on a merged branch.
3. Read what the stage names: its ADRs, the `AGENTS.md` of each area it touches, the glossary when it touches the
   domain. `/adr` numbers a new ADR after fetching `main`.
4. State the plan in five lines or fewer: the layer, the files, the tests, the docs it makes stale.
5. Tests first, red, then the code, then green (AGENTS.md, "How to work on a task"). Use `test-engineer` for
   tests and `domain-reviewer` after a domain change.
6. `/verify`, and `/browser-check` when it names journeys. Then `/reviewers` on the diff, and fix what it
   sorts as fix now, or say why not.
7. Mark the stage in its plan: a `*Built (<date>)*:` paragraph under the stage's line, saying what was built and
   what was left, as W0 to W3 in `docs/engineering/ai-workflow-plan.md` do. Update the plan's `Status:` line.
8. `/ship`. Once it is merged, or past 300,000 tokens, `/wrap-up`.

Rules:

- Stay in the stage. Work the stage does not describe goes to an issue (`/wrap-up` writes them) or to a later
  stage, not into this pull request.
- What the stage could not do, because a setting or a portal is the owner's, is written as numbered steps for the
  owner, one block to paste each, with the way through the portal.
