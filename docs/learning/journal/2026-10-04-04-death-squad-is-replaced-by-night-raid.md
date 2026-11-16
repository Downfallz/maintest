# 2026-10-04. Death Squad is replaced by Night Raid

- **Why.** Death Squad (+2 initiative to up to three allies for a round) was declared zero times with the
  Assassin line forced to Deathstalker against Greedy on the 400 confirmation seeds, and no reshaping of it
  changed that (+3 for 1 energy: 27; two rounds: 8). The owner asked for a second spell the rest of the
  catalogue does not already have.
- **What was tried in its place**, the same forced path, declarations of the new spell next to Mortal Wound:

  | Spell | Shape | Declared | Mortal Wound |
  | --- | --- | --- | --- |
  | Death Squad | +2 initiative, up to 3 allies | 0 | 589 |
  | Assassinate | 6 damage and a stun, cost 4 | 48 | 542 |
  | Fan of Knives | 2 damage and bleed 2 for 2 rounds, up to 3 | 143 | 473 |
  | Smoke Screen | +3 defense and +2 initiative, up to 3 allies | 19 | 587 |
  | Death Mark | 3 damage and -3 defense for 2 rounds | 18 | 582 |
  | Smoke Bomb | -3 initiative, up to 3 enemies | 0 | 589 |
  | Garrote | bleed 2 for 2 rounds and a drain of 2 | 24 | 583 |
  | Night Raid 1 / 1 / 2 | damage / drain / cost, up to 3 enemies | 0 | 589 |
  | Night Raid 2 / 1 / 2 | | 8 | 588 |
  | Night Raid 3 / 1 / 2 | | 30 | 594 |
  | Night Raid 2 / 1 / 1 | | 19 | 587 |
  | Night Raid 1 / 2 / 1 | | 52 | 584 |
  | Night Raid 2 / 2 / 2 | | 169 (821 energy drained) | 538 |
  | **Night Raid 2 / 2 / 3 (kept)** | | **102** (547 drained) | 537 |
  | Night Raid 2 / 2 / 2, two targets | | 78 (268 drained) | 564 |

  The owner asked whether a raid cast every round could lock a whole team, as Soul Devourer did one creature
  (ADR 0093). It could: at a cost of 2, the round's income, the enemy team's fizzles went from 1363 to 1742.
  At 3 they are 1617, at 2 targets 1497. It costs 3, as Soul Devourer does, and its keep says why.

  Assassinate and Fan of Knives were cast but would have been a fourth stun and a second Toxic Waves. A
  support spell beside Mortal Wound and Momentum is never the best single action, which is what the agents
  read, so only a shape with enough of a hit or a drain that locks (ADR 0093) is ever declared. Night Raid
  is the one team-wide drain in the catalogue; at 2 / 2 / 3 it moves the forced Deathstalker path +0.013 ±
  0.009 against Death Squad. It takes Death Squad's place in the
  package and the talent tree under a new id, `spell:night_raid:v1`; its knobs are the cost (3 to 4), the
  damage (1 to 3) and the drain (1 to 2, never more than a round's income).
- **The tuner's reading** on content `5708132d`, the benchmark seeds: the objective stays at **0.000**. On the
  exploring run Night Raid landed 11 times to Mortal Wound's 9, the first time Deathstalker's casts are split
  at all (its usage bound 0.34); Greedy cast Mortal Wound 62 times and Night Raid none. Matches 10.8 rounds. Content `5708132d`, benchmark digest
  regenerated.
