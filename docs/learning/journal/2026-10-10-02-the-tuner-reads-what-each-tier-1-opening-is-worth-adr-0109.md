# 2026-10-10. The tuner reads what each tier-1 opening is worth (ADR 0109)

Content `95a78999`, the Greedy of ADR 0108, the 200 benchmark seeds played from both seats. Agent A is
`opening:<package>+<package>`, Greedy forced to buy that opening with its two round-1 picks; agent B is Greedy,
which opens Brute + Warped.

| opening | win rate of the forced side (95 % interval) | rounds |
| --- | --- | --- |
| Brute + Warped | 0.500 (0.50 to 0.50) | 9.83 |
| Predator + Predator | 0.435 (0.37 to 0.50) | 9.97 |
| Warped + Predator | 0.390 (0.34 to 0.44) | 9.82 |
| Brute + Brute | 0.350 (0.30 to 0.40) | 11.57 |
| Warped + Warped | 0.282 (0.24 to 0.33) | 10.37 |
| Brute + Predator | 0.220 (0.17 to 0.27) | 9.50 |

- Each is a target of the objective, held between 0.40 and 0.60 at scale 0.05 and weight 1: four of the six sit
  below their band today, the furthest Brute + Predator, which against the Greedy before ADR 0108 read 0.494.
  Every score from here on is incomparable with every score before it.
- Brute + Brute fell from 0.500 to 0.350 and Warped + Warped rose from 0.099 to 0.282 against the new Greedy:
  what an opening is worth depends on what it meets, which is why the readings are against the mirror's player.
