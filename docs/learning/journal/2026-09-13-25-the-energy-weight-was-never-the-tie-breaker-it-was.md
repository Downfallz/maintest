# 2026-09-13. The energy weight was never the tie-breaker it was documented as

- **What changed**: `energy` 0.2 to **0.3** (ADR 0037), `initiative` kept at 2.1. Weights fingerprint
  `93f3683c` to **`1933f3ae`**, benchmark digest regenerated on content `91da955c`, which does not move.
- **Why it was read at all**: it is the last weight that was hand-set and never re-measured. Phase L5 set 0.2
  when it priced one thing, energy *kept*, and `agents.md` still called it "enough to break a tie towards the
  cheaper spell". ADR 0020, 0026 and 0035 then gave it energy handed out, energy regenerated, the uncovered
  part of an unlock cost, and energy drained — four jobs on a number chosen for one.
- **The sweep says the description was wrong, not just stale.** At `energy` 0.0 the first mover wins **0.720**
  of the mirror and the objective reads **100.09**, the worst point of the sweep below 0.5. A term that moves
  `player1WinShare` by a fifth between 0.0 and 0.3 was never a tie-breaker.

  | `energy` | 0.0 | 0.1 | **0.2** | **0.3** | 0.4 | 0.5 | 0.6 | 1.0 |
  | --- | --- | --- | --- | --- | --- | --- | --- | --- |
  | objective | 100.09 | 61.40 | 54.34 | **49.32** | 50.12 | 108.33 | 73.12 | 81.40 |
  | `player1WinShare` | 0.720 | 0.620 | 0.575 | **0.510** | 0.525 | 0.535 | 0.520 | 0.530 |
  | `spellEntropyA` | 2.619 | 3.249 | 3.437 | **3.530** | 2.955 | 2.588 | 2.626 | 2.428 |
  | `skill` | 0.985 | 0.988 | 0.990 | 0.985 | 0.943 | 0.905 | 0.915 | 0.802 |
  | `exploit` | 0.335 | 0.212 | 0.195 | 0.198 | 0.233 | 0.790 | 0.708 | 0.710 |

  0.2, 0.3 and 0.4 sit within 5.1 of each other and 0.5 breaks hard, so 0.3 is the middle of the step rather
  than an end of it — the test ADR 0028 and ADR 0032 each had to pass.
- **What it buys, stated exactly**: the mirror's first-mover share, **not a stronger agent**. `heuristic:` at
  0.3 against the compiled `greedy` at 0.2, each seed played from both sides, is a dead heat — **0.505, CI
  [0.481, 0.529]**, against a same-weights control reading 0.495 — and `exploit` is flat (0.195 to 0.198).
  ADR 0032 could show its baseline getting harder to exploit; this one cannot, and the ADR says so in its
  consequences rather than in a footnote.
- **Initiative was re-swept on the new content and does not move.** Eleven points: 2.1 is still the best
  (54.34) after six classes were rewritten under it. But it is a **narrow minimum, not a flat step** — 1.95
  reads 62.51 and 2.25 reads 60.01. What makes keeping it safe is the wider plateau 2.1..2.55 (54 to 60,
  against 62 to 70 outside), where 2.1 is best on the objective, on `skill` and on `exploit` at once. Weaker
  evidence than ADR 0032 had, and recorded as such.
- **Above 2.4 the bot stops playing well**, which the old sweep could not see: `skill` goes 0.990, 0.978,
  0.925, **0.720** at 2.1, 2.4, 2.7, 3.0, where the `74f02621` catalogue held 0.998 across the range. The new
  content has choices that overpaying for tempo makes the argmax miss.
- **Cost**: `spellsNeverCast` 1 to 2, at its target's limit. `check-knobs` stays at **ten findings** — none
  cleared, none added, two ceilings up (`momentum` 1.60 to 2.40, `restorative_burst` 3.80 to 4.10), neither
  enough to make either spell a choice.
- **The method is now a script**, `scripts/sweep-weight.py`. The first run of it was thrown away: two sweeps
  were started at once, they patch the same two files, and each restored the other's patch mid-flight, so the
  table was measuring weights it had not set. The script now refuses to start unless both files are clean in
  git, which is what that collision looks like from outside.
- **Verified**: build, 756 .NET tests, 304 pytest, `dotnet format`, ruff check and format, `check-knobs`,
  benchmark digest regenerated and re-verified.
