# Every critical chance is a twentieth

Status: **Draft, settled, not built** (2026-09-17). Every question this document opened has an answer; what
is left is the work. Not a numbered ADR: this branch claims no ADR number. When the
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
| Rejuvenate | 0.22 | 0.20 | 0.020 |
| Toxic Waves | 0.33 | 0.35 | 0.020 |
| Engulfing Flames | 0.33 | 0.35 | 0.020 |
| Protective Slam | 0.283 | 0.30 | 0.017 |
| Pummel | 0.767 | 0.75 | 0.017 |
| Lightning Bolt | 0.617 | 0.60 | 0.017 |
| Revenant Guards | 0.33 | **0** | — (see below) |

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

## Settled

**Rounding: to the nearest twentieth, and a tie rounds up.** None of the ten moves is a tie, so the rule costs
nothing today and exists so that the next pass cannot ask. One warning for whoever builds it: `round()` in
both Python and .NET rounds a tie to even, so `round(0.025 * 20) / 20` is `0.0`, not `0.05`. The rule is
`floor(x * 20 + 0.5) / 20`.

**Revenant Guards prints no chance at all.** Not 0.35: **0**. A critical multiplies a target's Damage and a
direct Heal (ADR 0033), and this Spell has neither — two Defense buffs and a Bleed on its caster — which is
why `knobs.json` already refuses it a critical knob. Snapping it would print a number on a card where the die
cannot change anything. The Spell itself may be reworked later; until then the card tells the truth.

**The threshold on the card is the one `components.md` already writes**: a chance of 0.35 is `d20: 14+`. Card,
player aid and rulebook state that one and never its complement.

**Where the rule is enforced: the data builder and `check-knobs`** — options 1 and 2 above, not the
`CriticalChance` type. Taken as recommended; say so if you want it elsewhere.

### What the catalogue looks like afterwards

Sixteen of the 36 Spells never touch the die, and the twenty that do carry **nine distinct chances**, each a
clean threshold:

| Chance | Faces | Card |
| --- | --- | --- |
| 0.20 | 4 | `d20: 17+` |
| 0.30 | 6 | `d20: 15+` |
| 0.35 | 7 | `d20: 14+` |
| 0.40 | 8 | `d20: 13+` |
| 0.45 | 9 | `d20: 12+` |
| 0.50 | 10 | `d20: 11+` |
| 0.60 | 12 | `d20: 9+` |
| 0.75 | 15 | `d20: 6+` |
| 0.80 | 16 | `d20: 5+` |

Eleven values become nine, and every one of them is a number a player reads off the die without arithmetic.

**A Creature has no Critical chance, and it stays at zero.** ADR 0042 set it to zero and deliberately left the
mechanism standing, so a Creature that crits more than another remained possible. That door is closed: no
Creature carries a chance, and the rule keeps it authored at zero. Twentieth plus zero is a twentieth, so the
grid holds without depending on it.

This is what lets the card be read literally. The engine still sums the Creature's chance with the Spell's, so
until now every document had to carry the nuance — *the card's number is the whole chance because this
content's Creature is at zero*. It is a rule now rather than a property of one Creature definition, and three
documents can drop the clause when this is built: the rulebook's §6.7 and Part 9, the player aid's critical
paragraph, and the audit's `ActionResolution` row.

## Still open

Nothing. What is left is the work: snap the ten Spells, move `lightning_bolt`'s band floor, teach the data
builder and `check-knobs` the grid, and pay the usual price of a content change — a new hash, a regenerated
digest, a journal entry, and the four readings saying what the snap cost.
