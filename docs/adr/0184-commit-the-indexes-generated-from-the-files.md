# 0184. Commit the indexes, generated from the files

Date: 2026-11-15
Status: Accepted (settled by the owner). Amends [0183](0183-read-the-indexes-from-the-files-and-keep-one-journal-entry-per-file.md) (neither index was committed)

## Context

ADR 0183 dropped the ADR table and cut the journal into one file per entry, and committed no index: the
directory, read with a command, was the list. That removed the conflict, and it left GitHub showing file names
only. The owner reads on a phone, where a command is not at hand, and asked for committed indexes.

## Decision

We will commit both indexes and write neither by hand. The table in `docs/adr/README.md` is generated from each
record's first line and its `Status:` paragraph; `docs/learning/journal/README.md` lists the entries, newest
first, from their first lines. `AdrFileTests` and `JournalFileTests` build each index and compare it with what is
committed, and write it instead when `UPDATE_INDEXES=1`, so one implementation generates and checks. The rest of
ADR 0183 stands: the shapes, the fetch before a number, one file per entry.

## Consequences

- Good: the lists read on GitHub, from a phone, with titles and statuses.
- Good: an index cannot disagree with the files, and a status keeps one home.
- Bad: two branches that each add a record or an entry conflict again, in the generated index. The conflict has
  one answer, the command, where the hand-edited table needed a reading.
- Bad: a change that forgets the command fails the gate.
- Neutral: an entry is still its own file, so two branches never edit the same prose.

## Alternatives considered

- Keep ADR 0183 as merged: no conflict at all, and nothing to read on GitHub. Refused by the owner.
- A generator of its own (a script, a tool project): a second implementation for the test to agree with.
- A union merge driver for the index: GitHub's merge does not run it.

## Follow-up

- `docs/adr/README.md`, `docs/learning/journal/README.md`, `docs/learning/journal.md`, `.claude/skills/adr/SKILL.md`.
- `.claude/rules/docs.md`, `.claude/rules/learning.md`, `docs/README.md`, `docs/engineering/ai-workflow-plan.md`.
- `tests/DownfallArena.Architecture.Tests/`: `AdrFileTests.cs`, `JournalFileTests.cs`, `GeneratedIndexes.cs`.
