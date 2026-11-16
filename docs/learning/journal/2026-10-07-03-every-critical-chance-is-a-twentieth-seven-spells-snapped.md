# 2026-10-07. Every critical chance is a twentieth: seven Spells snapped, and the grid is a rule

- **What changed.** The seven Spells off the d20's grid are on it, the pass
  [d20-criticals.md](../../tabletop/d20-criticals.md) priced and [ADR 0100](../../adr/0100-every-critical-chance-is-a-twentieth.md)
  decides: Whirlwind 0.38 to 0.40, Toxic Mend, Void Pulse and Incinerate 0.33 to 0.35, Revitalize 0.22 to
  0.20, Crash 0.283 to 0.30, Pummel 0.767 to 0.75. To the nearest twentieth, by 0.02 at most and 0.019 on
  average. `lightning_bolt`'s critical chance band floor moves from 0.17 to 0.15, the one band off its own
  grid; its value, 0.5, does not move. No other number in `data/` changes.
- **What holds it.** The data builder refuses a Spell whose chance is not a whole number of twentieths,
  naming the nearest, and a Creature definition with a chance of its own; `check-knobs` refuses a
  `/criticalChance` band whose bounds or step are off 0.05. A knob that starts on the grid and moves in
  twentieths stays on it, which is why this is a rule and not a chore: the thirds the legacy port carried
  (0.33, 0.667, 0.717) survived every tuning pass because a knob moves a value by its step from wherever it
  sits.
- **The reading.** Content `9419f935` to `b41ba55e`, benchmark digest regenerated, both on the engine of
  ADR 0099 (the digest of `9419f935` was regenerated for it the same day). Greedy against itself on the 400
  benchmark matches:

  | | `9419f935` | `b41ba55e` |
  |---|---|---|
  | Player 1 wins | 192 (48.0 %) | 182 (45.5 %) |
  | rounds, mean | 11.27 | 11.16 |
  | health left, mean | 36.6 | 37.3 |
  | draws, round cap | 0, 0 | 0, 0 |

  94 of the 400 outcomes move, 26 of them in the winner and 76 in the length, across 47 of the 200 seeds: a
  chance moved by 0.02 flips the roll that lands between the old threshold and the new one, and a flipped roll
  replays the rest of the match. The moves are inside what a seed block moves on its own (2.6 on the
  objective at 200 seeds, `knobs.json`), which is what seven moves of 0.02 should read as. Nothing here is a
  balance result, and no Spell changes role.
- **The four readings of the objective** (`score-content` on the benchmark seeds, `knobs.json`'s four
  evaluations, both sides on the engine before ADR 0099 (`7b4c5ed`), so that the content alone is between
  them; before on a checkout of `9419f935` with the `check-knobs` of its day, since the new one refuses the
  old band):

  | reading | metric | `9419f935` | `b41ba55e` |
  |---|---|---|---|
  | mirror (Greedy, Greedy) | Player 1 share / rounds / spells never cast | 0.490 / 11.44 / 8 | 0.470 / 11.34 / 5 |
  | skill (Greedy, Random) | Greedy's win rate / rounds | 1.000 / 9.26 | 1.000 / 9.26 |
  | variety (explore:0.2:lookahead, both) | rounds / spell entropy / tier usage share | 11.09 / 4.511 / 0.823 | 11.28 / 4.560 / 0.829 |
  | exploit (five weights files, Greedy) | win rate / rounds / cap share | 0.975 / 12.92 / 0.033 | 0.973 / 12.81 / 0.033 |

  Every other metric of the four moves in the third decimal or not at all. The objective's score goes from
  0.418 to 0.664, all of it the one target already outside its band before the snap: `variety.tierUsageShare`
  reads 0.829 against a ceiling of 0.8, where it read 0.823. That is the content review's F8 (late picks),
  not this pass, and the lookahead's variety run is the noisiest of the four.
- **What the table gets.** Every card that rolls prints its threshold: nine distinct chances, 0.20 to 0.80,
  each a face (`Crit 35% · d20 14+`), read off the content by the app's catalogue projection and the print
  generator with no change to either. Twenty of the 44 Spells never roll. The rulebook's §6.7 loses its "not
  yet built" clause.
- **The test content moves with it.** The host and studio tests' creature carried a chance of 0.05 of its
  own, which the builder now refuses; its two cards carry the 0.05 instead. The sum the engine rolls against
  is the same, so the matches those tests play are the same.
