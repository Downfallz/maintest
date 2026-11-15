---
paths:
  - "learning/**"
  - "docs/learning/**"
  - "models/**"
  - "benchmarks/**"
---

# Learning rules

- A win rate comes from three dataset seeds, reported with their spread; a gate reads the minimum. Never one
  seed, never the seed that scored best: the benchmark seeds are fixed, so that is selection on the test set
  (ADR 0049).
- Two agents on the same seeds are compared seed by seed (`uv run --project learning paired a b`): their two
  marginal intervals are not a test of the difference (journal, 2026-09-18).
- A proposal searched on some seeds is checked on seeds it was not searched on (`score-content --seeds`), and a
  new content leader wins on `benchmarks/confirmation-seeds.json` too (ADR 0074).
- A change that moves a number (content, engine, agents, the benchmark digest) gets a journal entry, a file of
  its own under `docs/learning/journal/`, named as `docs/learning/journal.md` says and then listed by the
  command it gives (ADR 0183): the date, what changed, the command that measured it, the numbers with their
  spread, and what it does not show.
- The benchmark digest is per content hash; regenerate it with `benchmark --write` only when the content or
  the engine changed on purpose, and say why.
- A model under `models/` is committed with its evaluation; `runs/` is never committed.
