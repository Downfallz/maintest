# 0183. Read the indexes from the files, and keep one journal entry per file

Date: 2026-11-15
Status: Accepted (stage W3 of [the AI workflow plan](../engineering/ai-workflow-plan.md)). Amends [0001](0001-record-architecture-decisions.md) (the index). Amended by [0184](0184-commit-the-indexes-generated-from-the-files.md) (the indexes are committed)

## Context

Sessions work in parallel, and they met in the same files. Of the last 300 pull requests, 86 edited the ADR
table in `docs/adr/README.md` and 54 added to the top of `docs/learning/journal.md`; sessions conflicted with
each other in 5 of the 8 latest. An ADR number was already taken four times (#297, #512, #516, #569), because a
branch reads the highest number in a tree cut before another branch merged. The table also drifted from the
files: 59 statuses differed, and three amendments were recorded only in the table.

## Decision

We will keep no list that every change edits. `docs/adr/README.md` holds no table: an ADR's first line is
`# NNNN. Title`, its status is a `Status:` line above `## Context`, and the directory read with one command is
the index. `/adr` fetches `main` and takes the highest number on either side plus one. A journal entry is a file
under `docs/learning/journal/`, named `YYYY-MM-DD-NN-slug.md` (the date, its order in the day, a slug) and
starting `# YYYY-MM-DD. Title`; `docs/learning/journal.md` stays as the page that says so. Neither index is
committed. `AdrFileTests` and `JournalFileTests` hold the shapes, and refuse an ADR number two files share.

## Consequences

- Good: two branches that each add an ADR or a journal entry no longer touch a common file.
- Good: a status has one home, so it cannot disagree with a table.
- Good: a session reads one entry, not a 9,233-line file.
- Bad: GitHub shows file names, not titles and statuses; the full list takes a command.
- Bad: a number taken twice is still possible between two open pull requests. The test refuses the second
  only once the first is on `main` and merged in, and the fix is a rename.
- Bad: a tie in a day's order is not refused; the two entries are then ordered by slug.
- Neutral: 20 ADR headings and two status lines were rewritten to the template's shape, and the three statuses
  only the table knew (0034, 0080, 0082) moved into their files. No decision changed.
- Neutral: the 233 entries were cut as they stood; only their relative links changed.

## Alternatives considered

- A generated index, committed and checked by a test: two branches regenerate the same file, so it conflicts
  like the one edited by hand, and each conflict costs a session's turn.
- A union merge driver for the index: GitHub's merge does not run it.
- An identifier that is not sequential (a date, a hash): 182 numbers are cited across the code and the docs.
- A journal file per month: the two branches of one afternoon still meet at its top.

## Follow-up

- `docs/adr/README.md`, `docs/learning/journal.md`, `docs/learning/journal/`, `.claude/skills/adr/SKILL.md`.
- `.claude/rules/docs.md`, `.claude/rules/learning.md`, `learning/AGENTS.md`, `docs/README.md`.
- `tests/DownfallArena.Architecture.Tests/AdrFileTests.cs` and `JournalFileTests.cs`.
