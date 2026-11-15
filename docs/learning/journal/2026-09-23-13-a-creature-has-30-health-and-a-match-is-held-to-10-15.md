# 2026-09-23. A creature has 30 health and a match is held to 10-15 rounds, and the objective reads 8.35 on content `4ab506fa`

- **What changed.** The game is designed for matches of ten to fifteen rounds, and under ADR 0066 the greedy
  mirror lasted 7.8. Base health goes from 20 to 30 and the objective's bands follow the design: the mirror is
  held to 10-15 rounds (from 8-16) and the best exploiter to at least 10 (from 8) (ADR 0068). Nothing else in
  the content moves; its hash goes from `4d7a841c` to `4ab506fa`, and the digest is written for it.
- **The sweep the value was chosen from**, content `4d7a841c` with only `baseHealth` changed, benchmark seeds,
  ADR 0065's objective and ADR 0066's rule:

  | base health | mirror rounds | exploring rounds | exploiter rounds | never cast | objective (8-16 band) |
  | --- | --- | --- | --- | --- | --- |
  | 20 | 7.79 | 7.17 | 9.60 | 4 | 5.30 |
  | 25 | 8.95 | 9.34 | 8.63 | 1 | 4.03 |
  | 30 | 10.05 | 10.03 | 14.61 | 2 | 8.35 |
  | 35 | 12.07 | 11.86 | 19.48 | 1 | 6.64 |
  | 40 | 14.03 | 13.44 | 17.69 | 0 | 9.52 |

  Length grows about a round for every 2.5 health, and at 40 the first matches reach the round cap (1 %). The
  objective moved for other reasons: `tierUsageShare` grows with length (1.89 at 20, 5.81 at 30), because the
  packages a long match reaches sell spells one of which is cast far more than the other.
- **The objective on the new content**, under the new bands: **8.35**, the four terms left all content findings
  a tuning pass reads -- `tierUsageShare` 0.885 (5.81), `tierDamageSpread` 2.568 (1.29), `spellUsageShare`
  0.343 (0.87), `tierWinSpread` 0.212 (0.38). `mirror.averageRounds` reads 10.05, inside its band by 0.05: a
  move that shortens matches is penalised at once.
- **What it invalidates.** The content hash moved, so every score, weight set and tuning run before this is on
  other content. The tuning pass that was running on `4d7a841c` was stopped and restarts on this one.
