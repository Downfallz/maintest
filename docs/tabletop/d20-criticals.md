# Every critical chance is a twentieth

Status: **Draft, for discussion** (2026-09-17). Not a numbered ADR: this branch claims no ADR number. When the
rule is settled and built, this text moves into `docs/adr/` with the next free number.

## The rule, in one sentence

A Critical chance is a whole number of twentieths — 0, 0.05, 0.10, ... 1.00 — everywhere it is authored: on a
Spell, on a Creature definition, and in the bounds a balance pass may move it between. Nothing else is
buildable.

## Why

A player rolls a die. The die the catalogue can afford is a d20 — that is measured, not assumed
([components.md](components.md) §1.6): over the 21 Spells that roll, a d20 moves the fewest of them, has the
smallest worst move and the smallest mean error, and it is the only grid `data/balance/knobs.json` already
declares, at a step of 0.05 on 20 Spells.

But the reason to make it a **rule** rather than a one-off tuning pass is not the table. It is that the
catalogue cannot stay on a grid it is not held to. Ten of the 36 Spells are off the twentieths today, and
none of them got there by a balance pass choosing an odd number:

| Spell | Now | Snapped | Move |
| --- | --- | --- | --- |
| Tornado | 0.33 | 0.35 | 0.020 |
| Noxious Cure | 0.28 | 0.30 | 0.020 |
| Crazed Specter | 0.38 | 0.40 | 0.020 |
| Revenant Guards | 0.33 | 0.35 | 0.020 |
| Rejuvenate | 0.22 | 0.20 | 0.020 |
| Toxic Waves | 0.33 | 0.35 | 0.020 |
| Engulfing Flames | 0.33 | 0.35 | 0.020 |
| Protective Slam | 0.283 | 0.30 | 0.017 |
| Pummel | 0.767 | 0.75 | 0.017 |
| Lightning Bolt | 0.617 | 0.60 | 0.017 |

Read the values, not the table: 0.33, 0.667 and 0.717 are the legacy prototype's thirds, carried over by the
port (`docs/domain/spells.md`). A knob moves a value **by** its step, from wherever the value already is. So a
step of 0.05 on a start of 0.33 gives 0.28 and 0.38; on 0.717 it gives 0.767; on 0.17 it gives 0.22. **The
step did not create the offset — it preserves it, and every tuning pass carries it forward.** Nineteen of the
twenty declared bands are already on the grid; the values that walk them are not, and never will be.

That is what makes this a rule and not a chore. Snap once and the offset is gone for good, because a knob that
starts on the grid and moves in twentieths stays on it.

## What it costs

- **Ten Spells move**, by 0.02 at most and 0.0091 on average. That is a content change: a new content hash, a
  regenerated benchmark digest, a journal entry, and the four readings of `knobs.json` saying what it cost.
- **One knob band moves**: `lightning_bolt`'s floor of 0.17 — itself a legacy third — to 0.15 or 0.20. It is
  the only band off its own grid.
- Nothing else in `data/` changes, and no Spell changes by more than one twentieth, so no spell changes role.

## Where the rule is enforced — the choice to make

The rule is worth nothing if it is only written down. Three places can hold it, and they are not exclusive:

1. **The data builder** (`tools/DownfallArena.DataBuilder`, ADR 0009). A Spell or a Creature definition whose
   Critical chance is not a multiple of 0.05 fails the build with a precise error. The content is where the
   value is authored, so this is where a wrong one should die. It also covers a Creature's own chance, which
   matters the day one stops being zero: twentieth plus twentieth is a twentieth, so the sum the engine rolls
   against stays on the grid by construction.
2. **The knobs check** (`uv run --project learning check-knobs`). Every `/criticalChance` band's `min`, `max`
   and `step` must be multiples of 0.05. This closes the other door: a search that cannot leave the grid can
   never re-introduce an offset, which is exactly how the current one got in.
3. **The `CriticalChance` type** (`SharedKernel/Stats`). The strongest: the value could refuse to exist off
   the grid. **Recommended against.** ADR 0007 says the shared kernel holds no game rules, and a d20 is a game
   rule — one that belongs to how this game is played, not to what a probability is. It would also bind the
   engine to a die it does not roll: the engine draws a continuous number and compares.

Recommended: **1 and 2**. The rule lives where content is validated, and the search space is shaped so it
cannot produce what validation would reject.

## Open questions

- **Rounding.** Nearest twentieth, and a tie (an exact 0.025 step) rounds where? Up is one rule, and the ten
  moves above contain no tie, so it costs nothing to say it now.
- **Revenant Guards.** It prints 0.33 — 0.35 after the snap — and a critical multiplies a target's damage and
  a direct heal (ADR 0033). This Spell has neither: two Defense buffs and a Bleed on its caster. Its own
  `knobs.json` note says the chance is deliberately not a knob for that reason. So the snap would put a
  number on a card where the die can change nothing. Set it to zero and the card says "no critical roll",
  which is the truth, or leave it and accept a printed chance that does nothing.
- **The threshold on the card.** A chance of 0.35 is "14 or more on a d20" the way `components.md` writes it.
  Card, player aid and rulebook must state one convention and never the other.
- **Whether a Creature's chance is allowed back.** It is zero today (ADR 0042). The rule covers it either way;
  the question is whether a Creature that crits more than another is still wanted, because that is the thing
  that made the printed chance and the rolled chance differ in the first place.
