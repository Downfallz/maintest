# 2026-09-10. `tune-content` on the nine Spells: the first-mover edge finally moves, and it costs length

- **What this is**: the entry the one below promised. No content changed — this is what the search proposes,
  measured, and nothing from it is applied. 40 candidates at `--seed 1 --iterations 10 --neighbours 4`, 82
  evaluations, about a quarter of an hour on content `c0ec6984`.
- **Score 104.05 to 88.76** over five moves (ADR 0021 scores the distance outside every band, zero being on
  target):

  | Move | From | To |
  | --- | --- | --- |
  | `pummel` Spell initiative | 1 | 0 |
  | `pummel` damage | 2 | 1 |
  | `lightning_bolt` damage | 3 | 4 |
  | `lightning_bolt` critical chance | 0.667 | 0.617 |
  | `poison_slash` energy cost | 2 | 3 |

- **The number that moved is the one nothing had moved**: `player1WinShare` **0.665 to 0.535**, inside the
  0.45 to 0.55 band for the first time since the first digest. Making matches half again as long did not
  touch it (the entry below); taking a point of Spell initiative off the cheapest unlock did. That is ADR
  0017 read backwards: unlocking is how a Creature gets faster, so the cheapest unlock decides who acts
  first for the rest of the match, and `pummel` at one energy is the cheapest there is.
- **It paid for that in length**: `averageRounds` **8.135 to 5.865**, back outside the 8 to 16 band we had
  just entered, and `fizzleRateA` 0.180 to 0.209. The objective weighs the first-mover share at 3 with a
  scale of 0.05 and length at 2 with a scale of 3, so a tenth of the share is worth more to it than two
  rounds. That is a choice written in `data/balance/knobs.json`, not a fact about the game, and this run is
  the first evidence about whether it is the right one.
- **The dominant Spell is untouched**: `spellUsageShare` 0.855 to 0.846, still 71 of the remaining 88.8
  points. A hill climb moving one number one step cannot close a gap that wide, and it raised
  `lightning_bolt`'s damage rather than lowering it, because damage barely moves its share while it does
  move the length the objective is also chasing. `spellsNeverCast` stayed at 5 of 9.
- **Decision**: apply nothing. The proposal names the right lever and the wrong price for it. What
  `lightning_bolt` should cost is a design question, and answering it first is what would let a search
  spend its budget on the rest.
