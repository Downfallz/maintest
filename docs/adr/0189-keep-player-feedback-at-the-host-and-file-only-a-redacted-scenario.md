# 0189. Keep player feedback at the host and file only a redacted scenario

Date: 2026-11-16
Status: Accepted

## Context

A player has no way to report a bug with its context or to say what they would like: a note belongs to one
session's record, and the page's own errors are lost (docs/tabletop/feedback-plan.md, "What exists today"). The
owner wants a report to reach a reproducible position and a regression test, and an idea to reach a plan. Three
forces pull against that. The repository is public (ADR 0186), so an issue or a file under `scenarios/` is read
by anyone, and a scenario file names its source's session, run and match and the time of every move (ADR 0179).
The host holds no GitHub credential and deploys by federation (ADR 0082). And a door any guest can open can be
flooded. The owner settled the shape on 2026-11-16 (the eleven questions in docs/tabletop/feedback-plan.md).

## Decision

We will keep **feedback** (a **bug report** or an **idea**) at the host, behind one entry, **Report or suggest**,
open to anyone holding a seat and to any signed-in account. The host never writes to GitHub.

- **Stored**: one record each, in a store of its own beside the recordings.
- **A bug report holds** the player's words, a category, the page's last errors, and what the host adds: the
  table, seat, round, sub-phase and journal length, the content, rules and engine, and the seat's own view, which
  shows nothing that seat may not see.
- **Read by** the operator only. Kept 180 days after it is closed, then the tending pass deletes it. When an
  account is deleted, its feedback keeps its text and loses the account.
- **Against spam**, one budget for both kinds: two minutes apart and five a day per sender, five a day per
  network address for guests, a repeat on the same position merged, 100 a day for the host. The operator can mark
  feedback as spam and mute its sender.
- **An idea never leaves the host.** The operator groups duplicates, marks an idea planned with a plan's path,
  and copies a `/new-plan` prompt written in their own words.
- **A bug report leaves only as a redacted scenario.** The operator cuts the scenario at the reported round.
  Every source id is replaced by the report's id, every time is set to the epoch, and the operator writes the
  title and goal. A check refuses a file that holds an account id, a table id or a player name. The operator
  downloads it as a zip and attaches it by hand to an issue they file through GitHub's form, pre-filled from the
  panel.
- **In the repository** it becomes a **bug scenario** under `scenarios/bugs/`, read by one C# test that fails
  until the rule is fixed.

## Consequences

- Good: no player's text and no session id reaches the public repository unless the operator wrote it or read it
  first; what is filed is checked, not trusted.
- Good: the host gains no credential and no write path to GitHub. The issue's state is read from the public API.
- Good: a bug becomes a position a session replays and a test that is red before its fix, as the testing
  conventions ask.
- Good: the spam guards cost the operator one look at a flood, not a row per message.
- Bad: attaching the zip and pasting the issue's number back are hand steps for the operator.
- Bad: feedback is personal data the host now keeps; the *What we keep* panel has to say so.
- Bad: a guest's network address is counted in memory only, so a restart resets that budget.
- Neutral: ideas have no public board and no votes. With tens to hundreds of accounts, a board would stay empty.

## Alternatives considered

- The host files the issue with a GitHub token: a credential on the host and player text published unread.
- A link to the position served by the host instead of a file: a public address into production data.
- The expectation written in the scenario file: a second assertion language beside the C# tests.
- Ideas as public issues or a voting board: player text made public, and moderation at the host's size.
- Notes extended with a status: a note feeds the learning loop and lives with one session; feedback has its own
  life and outlives the session.

## Follow-up

- docs/tabletop/feedback-plan.md stages R1 to R8 and I1: the store and `POST /api/feedback`, the sheet, the
  Feedback tab, grouped ideas, Reproduce, the redacted scenario and the issue form, the bug scenarios and their
  tests, the `/repro` skill, the notices.
- Glossary: Feedback, Bug report, Idea, Redacted scenario, Bug scenario.
