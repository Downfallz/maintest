# 2026-10-09. Guard's first point lasts three rounds

Content `da8d744b` to `f2ae0013`: one effect of `guard` (Brace), its first `DefenseBuff 1` from permanent to 3
rounds. All at the engine's round cap of 30.

- **Why.** The lookahead-39 mirror ran 18.5 rounds on average, past the 8-to-14 band of ADR 0086, and Guard was
  its most cast spell, 16.4 casts a match: a point of defense for good on every cast of a 1-energy spell,
  which only the ceiling of 10 (ADR 0076) bounds.
- **Three variants, one at a time**, each on the lookahead-39 mirror against itself, 60 matches from seed 300:

  | | today | A: first point 3 rounds | B: ceiling 6 | C: cost 2 |
  |---|---|---|---|---|
  | rounds, mean (median) | 18.5 (18) | **16.4 (14)** | 17.5 (17) | 15.3 (14.5) |
  | at the round cap | 3 | 4 | 3 | 0 |
  | first death, round | 8.1 | 7.0 | 7.8 | 11.6 |
  | Guard casts a match | 16.4 | 15.8 | 16.3 | 2.7 |
  | damage / heals a round, raw | 7.8 / 5.5 | 9.7 / 5.5 | 8.9 / 5.1 | 9.2 / 12.0 |

  At 60 matches a round of difference is near the noise. B moves nothing: the ceiling is not what holds the
  mirror up. A keeps Guard in play and stops it accumulating, so damage lands. C all but removes Guard and
  hands its place to the heals (Rejuvenate 10.3 casts a match, Toxic Mend 7.2, more raw healing than damage
  a round, the first death three rounds later): a Guard stand-off traded for a healing one. **A is kept.**
- **`score-content` on the benchmark seeds**: 0.000 before and after, every measurement inside its range. The
  exploiting panel's best wins 0.570 where it won 0.667, and the tier win spreads fall (exploit 0.391 to 0.208,
  variety 0.108 to 0.058). The mirror reads 14.9 rounds where it read 15.1, with 4.0 % at the cap where it had
  3.0 %; the exploring run 10.25 rounds where it read 10.46. The Greedy benchmark mirror is the same match in
  348 of 400: Greedy casts little Guard.
- **Knobs.** The keep that read "half of it is permanent" now reads that nothing of it is; the first point's
  duration is a knob from 2 to 3 rounds, 3 being the longest a condition may last.
- **At the table** a cast places two +1 Defense buff tokens and moves no rail: the Condition tokens go from 174
  to 180 (`docs/tabletop/components.md`).
- **Not settled**: 16.4 rounds is still past 14 for the lookahead mirror, and a quarter of its matches go past
  round 20, the table's cap, which the engine did not play (ADR 0105, the same day).
