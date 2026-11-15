# 2026-10-08. Toxic Mend heals 6 and shreds 3 defense

Content `cedcfb61` to `da8d744b`. One spell: Toxic Mend (`spell:noxious_cure:v1`) heals 6 where it healed 7, and
the defense it strips from the cured goes from 2 to 3 for the round. Both inside the knobs' bounds.

- **Why.** Played by the lookahead on its strongest weights (lookahead-36) on both sides, a match lasted 15.2
  rounds and the first creature died at round 11. Heals came to 8.1 a round against 10.3 damage, and Toxic Mend
  was the largest of them: about ten casts a match, three allies healed for 7 each, 14 on its 35 % critical,
  for 2 energy.
- **The lookahead-36 mirror**, 60 matches from seed 300, the same seeds before and after:

  | | before | after |
  |---|---|---|
  | rounds | 15.20 (median 14) | 13.27 (median 12) |
  | matches over 20 rounds | 9 | 0 |
  | in the 10 to 15 band | 36 | 44 |
  | first death | round 11.0 | round 10.6 |
  | heal a round | 8.1 | 5.4 |
  | damage a round | 10.3 | 10.9 |
  | Toxic Mend casts a match | 9.9 | 6.8 |
  | Guard casts a match | 13.1 | 16.7 |
  | player 1 wins | 34 | 29 |

  Shorter by 1.93 rounds, seed by seed (+0.50 to +3.36), clear of zero. The heal it no longer gives is not
  replaced by another: Guard is cast more, Restorative Gush less.
- **The Greedy mirror** barely moves (15.04 to 15.09 rounds over the same 100 matches): Greedy rarely casts it.
- **Digest regenerated** on `da8d744b`.
