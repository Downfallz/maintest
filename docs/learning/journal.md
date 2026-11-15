# Learning journal

One entry per change that moves a number: content, engine, agents, or the benchmark seeds. Each entry names
the run stamps involved so that any two results can be compared on one axis at a time (ADR 0013).

An entry is a file of its own under [journal/](journal/) (ADR 0183), and [journal/README.md](journal/README.md)
lists them all, newest first. That list is generated (ADR 0184): `JournalFileTests` holds it, the names and
the first lines.

## Writing an entry

- The file is `docs/learning/journal/YYYY-MM-DD-NN-slug.md`: the date, the entry's order in its day (`01` for
  the first), and a few words of the title in lower case with hyphens.
- Its first line is `# YYYY-MM-DD. Title`, the title saying what was found.
- Take the next order of the day as you see it. Two branches that take the same one still have two files: the
  tie only orders them by slug.
- Rebuild the list, here and to settle a conflict in it. Never edit it:

  ```bash
  UPDATE_INDEXES=1 dotnet test --project tests/DownfallArena.Architecture.Tests
  ```
- What an entry says is in `.claude/rules/learning.md`: what changed, the command that measured it, the numbers
  with their spread, and what it does not show.

## Reading it

Start from [the list](journal/README.md).

```bash
ls docs/learning/journal/2026-10-*                   # a month
grep -l 'search-51' docs/learning/journal/*.md       # the entries about a subject
```

The entries from 2026-09-08 to 2026-11-02 were one file until ADR 0183; they were cut as they stood.
