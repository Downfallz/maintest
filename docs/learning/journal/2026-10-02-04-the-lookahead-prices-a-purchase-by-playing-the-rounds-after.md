# 2026-10-02. The lookahead prices a purchase by playing the rounds after it out (ADR 0094)

- **What the bots buy, and what it is worth.** On content `0a515da0`, 800 exploring self-play matches
  (`explore:0.2`, seed 1, traced): Greedy opens Occultist on 88 % of round-1 picks. Yet every package that
  a side owned won about half its matches (Brute 0.53, Occultist 0.50, Prowler 0.51), and the rare
  Brute + Occultist opening won 0.62 of 158 sides. The bots' pick was lopsided. The packages' worth was not.
- **Forcing the opening.** A scratch agent forced the round-1 picks and then played as the agent it
  wrapped. That agent played its own unforced self, paired by seed.
  - Against Greedy, every opening beat Greedy's own Occultist twice: +0.09 to +0.43 on the 200 benchmark
    seeds, and the 400 confirmation seeds reproduced each number to within its interval.
  - Played opening against opening, Occultist twice was the worst of the six for Greedy (0.30 mean) and for
    search-19 (0.27), and the best for search-31, which won every pairing with it (0.92 mean) while opening
    Brute twice itself.
  - So what an opening is worth depends on the opponent's opening and on how the rest is played. The
    one-step reading prices neither: it reads a package's best spell on the board of the moment.
- **The change.** The lookahead buys each package on offer on a hypothetical board and plays the next 4 rounds
  out, 4 times, every decision Greedy's in both seats, the enemy's face-down picks guessed as Greedy's, on
  the same dice for every candidate (`RoundRollout`, `Advance.Buy`). A rollout started at a match's first
  Speed sub-phase on that match's dice ends where the match ends, with the same sum
  (`RoundRolloutTests`).
- **Measured** with `lookahead` (built-in weights), before (`main` at `3d5abce`) and after, each seat once a
  seed, on the 200 benchmark seeds, paired by seed:

  | Against | Before | After | Paired difference |
  | --- | --- | --- | --- |
  | Greedy | 0.554 ± 0.046 | 0.902 ± 0.029 | **+0.349 ± 0.050** |
  | search-19 | 0.328 ± 0.044 | 0.594 ± 0.049 | **+0.266 ± 0.064** |
  | search-31 | 0.505 ± 0.065 | 0.505 ± 0.048 | +0.000 ± 0.073 |

  The lookahead was the weakest of the featured bots against search-19, and now beats it. Against search-31
  it gains nothing. Search-31 opens Brute twice, the rollouts guess it opens as Greedy does, and a best
  response to the wrong guess is no better than the old pick. Guessing an opponent from the packages it has
  revealed is the next thing to try. The confirmation seeds were not run for the change: the owner took the
  benchmark reading.
- **Cost.** About ten times the old lookahead's time on the benchmark seeds (297 s against 29 s for 400
  matches against Greedy, 764 s against 73 s against search-31).
- **A horizon can mislead.** On a test catalogue where a package opens a much stronger one, four rounds
  read the opening as even and the whole match read it as losing 29 to 49 (100 forced Greedy matches). The
  defaults `PurchaseReading(4, 4)` were not swept; a sweep of the horizon is open.
