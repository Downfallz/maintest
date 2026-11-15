# Architecture Decision Records

An ADR captures one decision that is hard to reverse: an architecture choice, a dependency, a convention,
or a settled game-design question. Copy `0000-template.md`, take the next number, keep it under a page.
Accepted ADRs are immutable. To change a decision, write a new ADR that supersedes the old one, and say so on
the old one's `Status:` line.

## The index is the directory

No table is kept here (ADR 0183): a list every pull request edits is a list two branches edit at once. Each
file carries what a row carried, on its first line (`# NNNN. Title`) and on its `Status:` line, and
`AdrFileTests` holds every file to that shape.

```bash
head -qn 1 docs/adr/0*.md                    # every number and title
grep -m 1 -H '^Status: ' docs/adr/0*.md      # every status, with what amends or supersedes it
grep -li 'web push' docs/adr/0*.md           # what is decided about a subject
```

## Taking a number

Fetch `main` first, then take the highest number on either side plus one: `/adr` does it. A number two files
share fails `AdrFileTests`; the later pull request takes the next one.
