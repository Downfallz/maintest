# 2026-09-14. The weight that priced nothing, removed

- **What changed**: the `fizzle` scoring weight is gone (ADR 0040). Eight weights, not nine. Fingerprint
  **`1933f3ae` to `362b0496`** — the first move caused by the list getting *shorter* rather than a number
  changing. Benchmark digest regenerated on content `91da955c`.
- **Why, in one line**: four measurements across three ADRs, and it priced nothing in all four.
  - ADR 0037 swept it 0 to 100: every value played the 400 seeds identically, column for column.
  - ADR 0038 renamed it and found a third reader nobody had noticed. Still nothing.
  - ADR 0039 gave it a real decision to reach. A fresh sweep at 0, 1, 2, 3, 5 read 78.44, 77.84, 77.25,
    79.52, 77.25 — spread 2.3 and not monotonic.
- **What removing it costs**: the objective reads **85.68 against 83.77** with the weight kept at 2.0. That
  is 1.9 points, *inside* the 2.3 spread the weight itself showed across 0 to 5 — so indistinguishable from
  any value it could have had, which is the whole point. `spellsNeverCast` 2 to 1 and `spellsBarelyCast` 3 to
  2; every other column identical to three decimals.
- **One site where removal is not neutral, and is better.** `HeuristicAgent.DecideIntent` scored a castable
  spell with no legal target at `-fizzle`. At zero, the bot now prefers doing nothing to an action whose
  expected score is negative — before, a spell that hurt an ally could beat a spell with nothing to hit.
  Argued for rather than inherited.
- **The double-count that justified the rest.** ADR 0039 already prices a wasted target as the value the
  action no longer earns. Charging a penalty on top priced the same loss twice, which is why the
  wasted-share term goes rather than being kept at a smaller number.
- **The sweep script refused to measure this one until it was committed**, because the guard it grew after
  the first collision compares both patched files against `HEAD` and these differed. First time it fired on
  a legitimate change rather than a mistake, and it was right to: measuring an uncommitted tree is how the
  first table of this whole arc got thrown away.
- **A rule that fell out of the review, and is worth keeping**: regenerate a provenance-bearing artifact
  *after* the tree is clean, never before. The first digest here recorded `962cd78f4c6a-dirty` because it was
  written while the docs and tests of the same change were still uncommitted, so the one field kept as
  experiment provenance named a base commit plus an unknown working tree. Nothing local would have caught it:
  `BenchmarkDigest.DifferencesFrom` ignores `EngineVersion` on purpose, so `benchmark` verified green either
  way. Regenerated clean it reads `48779f902d7b`, and exactly one line of the file differs — all 400 outcomes
  identical, which is what says it was a traceability defect and not a measurement one.
- **Verified**: build, 761 .NET tests, 304 pytest, format, ruff, studio tests, digest regenerated and
  re-verified.
