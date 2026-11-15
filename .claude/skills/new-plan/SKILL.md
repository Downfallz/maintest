---
name: new-plan
description: Turn an idea into a plan in docs/, in the shape the repository's plans share (what was asked, what exists, the shape, stages of one pull request each, questions for the owner), list it in docs/README.md, and ask its open questions as choices. Use when the owner gives an idea, a feature or a problem to plan rather than a stage to build. Named new-plan because /plan is Claude Code's own plan mode.
argument-hint: "<the idea, in the owner's words>"
---

# Write a plan

The idea: $ARGUMENTS

A plan is read on a phone and settled by the owner one question at a time. It is the decision record of a
feature: a stage cites it, and `/wrap-up` marks its stages built. Model it on `docs/tabletop/admin-plan.md`
and `docs/engineering/ai-workflow-plan.md`.

1. Start from fresh `main`, on a new branch: `git fetch origin main && git switch -c <branch> origin/main`.
2. Read before writing: the area's `AGENTS.md`, `docs/README.md` for a plan that already covers the idea (then
   amend it instead, and say so), the ADRs the idea touches (`docs/adr/README.md` is generated from them), and the
   glossary when the idea touches the game. Measure what can be measured; say what was not, and why.
3. Write `docs/<area>/<slug>-plan.md`, where `<area>` is the docs folder it belongs to (`tabletop`, `domain`,
   `engineering`, `learning`, `art`). The file name must contain `plan`. Its sections, in this order:
   - `Status: draft, <date>.` on the second line. Nothing is built.
   - `## What the owner asked for`: the idea in their words, translated to English if it came in French.
   - `## What exists today`: a table, concern by concern, of what the code and the docs hold. Facts, with paths.
   - The shape: one or more sections on what is proposed, with its routes, its model, where the code goes.
   - `## Stages`: a numbered list. Each stage is one pull request that leaves the gate green, with an id and a
     title in bold (`1. **A1, the shell.** ...`), the ADR it needs if any, and what it tests.
   - `## Questions for the owner`: numbered, each answerable by a choice. A question the code answers is not one.
4. List it in `docs/README.md`, a row in the table with a one-line description; `InstructionFileTests` refuses
   a plan the index does not list.
5. Ask the questions with AskUserQuestion, four at most per call, the recommended option first and marked
   "(Recommended)". Write the answers under `## Questions the owner settled (<date>)` and change the status to
   `accepted` or `accepted in part`. An unanswered question stays where it was.
6. Run `/verify`, then `/ship`: a plan is a pull request like any other.
7. End with the first stage's one-line prompt: `/stage docs/<area>/<slug>-plan.md <id>`.

Rules:

- English, in the voice of `.claude/rules/docs.md`: short declaratives, the reader's words, no hedging.
- A stage is small enough for one session under 300,000 tokens. Split one that is not.
- A decision that is hard to reverse is named as an ADR a stage writes, not taken in the plan.
