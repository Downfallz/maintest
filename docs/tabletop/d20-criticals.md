# Every critical chance is a twentieth

Status: **Draft, settled, not built** (2026-09-17; the tables and the counts re-read on 2026-10-04 against
content `e6f72578`, on 2026-10-05 against `ad3e4d00`, and on 2026-10-06 against `3c9eb083`). Spells carry the names of `9419f935`, the same content renamed
([package-renaming-plan.md](../domain/package-renaming-plan.md)), also in the earlier readings: the ids did not
move, so a row is found at any of those hashes under the name the plan's table gives. Every question
this document opened has an
answer; what is left is the work. Not a numbered ADR: this branch claims no ADR number. When the rule is settled and built, this text moves into
`docs/adr/` with the next free number.

## The rule, in one sentence

A Critical chance is a whole number of twentieths — 0, 0.05, 0.10, ... 1.00 — everywhere it is authored: on a
Spell, on a Creature definition, and in the bounds a balance pass may move it between. Nothing else is
buildable.

## Why

A player rolls a die. The die the catalogue can afford is a d20 — that is measured, not assumed
([components.md](components.md) §1.6): over the 21 Spells that rolled when it was measured, a d20 moves the
fewest of them, has the smallest worst move and the smallest mean error, and it is the only grid
`data/balance/knobs.json` already declares, at a step of 0.05 on 21 Spells then and 25 now.

But the reason to make it a **rule** rather than a one-off tuning pass is not the table. It is that the
catalogue cannot stay on a grid it is not held to. Seven of the 44 Spells are off the twentieths today (ten
of 36 when this was first written), and none of them got there by a balance pass choosing an odd number:

The table below is a **reading of content `3c9eb083`** (2026-10-06), not a constant: the maintainer is
tuning, and a pass moves these values. Regenerate it rather than trusting it, with

```bash
python3 -c "
import json,glob,math
for f in sorted(glob.glob('data/Spells/**/*.json',recursive=True)):
    d=json.load(open(f)); c=d.get('criticalChance',0); s=math.floor(c*20+0.5)/20
    if abs(c-s)>1e-9: print(f\"{d['name']:<20} {c} -> {s}\")"
```

Which rows it prints changes with every pass; **what does not change is that the rows exist**, because a knob
moves a value by its step from wherever it sits. Ten Spells move at content `7e199df4`, as they did at
`938bef5e`, and only Whirlwind's numbers differ between the two readings.

**Re-read on 2026-10-04, at content `e6f72578`.** Eight Spells move. Two rows are gone: Shock
now sits on 0.50, and Wraithguard is authored at 0, so the zeroing below has landed. Toxic Mend moved
from 0.28 to 0.33 and still snaps. Ambush (0.35) and Blood Hunt (0), new since the last reading, are on the
grid; Shadowstep and Death Squad, which they replace, were too.

**Re-read on 2026-10-05, at content `ad3e4d00`.** Seven Spells move. One row is gone: Death Wail is
authored at 0 and rolls no critical, where it printed 0.38, and its critical chance knob went with it. Its
weight is in the Bleed it now leaves on each target, which a critical never reaches (ADR 0033). Blood Hunt
changed its reach, its Damage and its drain, and is still at 0.

**Re-read on 2026-10-06, at content `3c9eb083`.** The same seven Spells move. Basic Attack is gone from the
catalogue. It was authored at 0 and had no critical chance knob, so no row and no band moved with it.

| Spell | Now | Snapped | Move |
| --- | --- | --- | --- |
| Whirlwind | 0.38 | 0.40 | 0.020 |
| Toxic Mend | 0.33 | 0.35 | 0.020 |
| Revitalize | 0.22 | 0.20 | 0.020 |
| Void Pulse | 0.33 | 0.35 | 0.020 |
| Incinerate | 0.33 | 0.35 | 0.020 |
| Crash | 0.283 | 0.30 | 0.017 |
| Pummel | 0.767 | 0.75 | 0.017 |

Read the values, not the table: 0.33, 0.667 and 0.717 are the legacy prototype's thirds, carried over by the
port (`docs/domain/spells.md`). A knob moves a value **by** its step, from wherever the value already is. So a
step of 0.05 on a start of 0.33 gives 0.28 and 0.38; on 0.717 it gives 0.767; on 0.17 it gives 0.22. **The
step did not create the offset — it preserves it, and every tuning pass carries it forward.** Nineteen of the
twenty declared bands were on the grid at the first reading, and twenty-four of the twenty-five are at
`ad3e4d00` and `3c9eb083` (twenty-five of twenty-six at `e6f72578`, before Death Wail's band left); the values that walk
them are not, and never will be.

That is what makes this a rule and not a chore. Snap once and the offset is gone for good, because a knob that
starts on the grid and moves in twentieths stays on it.

## What it costs

- **Seven Spells are snapped** (nine at the first reading, eight at `e6f72578`), by 0.02 at most and 0.019
  on average over the seven. Death Wail was one of the eight until 2026-10-05; it left the list by going
  to 0, not by a snap. **Wraithguard was the tenth and is not a snap**: it went from 0.33 to 0, a move
  of 0.33, because a critical cannot reach anything it does. The content of 2026-10-04 made that move; the rest of this bullet
  is what it cost. Do not average the two together — the snap's cost and the zeroing's cost are different
  decisions and the journal entry has to price them apart. The zeroing also moves what the engine *records*, even though
  it moves no board: `CombatResolution.IsCritical` is false where it used to be true one cast in three, so the
  critical counts of every evaluation change for that Spell.
- Both are a content change: a new content hash, a regenerated benchmark digest, a journal entry, and the four
  readings of `knobs.json` saying what it cost.
- **One knob band moves**: `lightning_bolt`'s floor of 0.17 — itself a legacy third — to 0.15 or 0.20. It is
  the only band off its own grid.
- Nothing else in `data/` changes, and no Spell changes by more than one twentieth, so no spell changes role.

## Where the rule is enforced — the choice to make

The rule is worth nothing if it is only written down. Three places can hold it, and they are not exclusive:

1. **The data builder** (`tools/DownfallArena.DataBuilder`, ADR 0009). A Spell whose Critical chance is not a
   multiple of 0.05 fails the build with a precise error. The content is where the value is authored, so this
   is where a wrong one should die.

   **A Creature definition is held to more than the grid: its Critical chance must be exactly zero.** The grid
   alone does not carry the rule this document rests on. `ResolutionRules` sums the Creature's chance with the
   Spell's, so a Creature authored at 0.05 — a legal twentieth — would pass validation while every card, the
   player aid and the app's catalogue projection print the Spell's threshold alone. The table and the app
   would then roll against a number nobody printed. Requiring zero is what makes the printed chance the whole
   chance, and it is the rule ADR 0042 left standing as a possibility rather than closing (see the settled
   section below). Should a Creature ever want a chance of its own, that is an ADR, and it moves the threshold
   out of the shared catalogue projection into the per-seat payload — the consequence is written down in
   `playtest-app.md` §1.3.
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

**Rounding: to the nearest twentieth, and a tie rounds up.** None of the seven moves is a tie, so the rule costs
nothing today and exists so that the next pass cannot ask. One warning for whoever builds it: `round()` in
both Python and .NET rounds a tie to even, so `round(0.025 * 20) / 20` is `0.0`, not `0.05`. The rule is
`floor(x * 20 + 0.5) / 20`.

**Wraithguard prints no chance at all.** Not 0.35: **0**. A critical multiplies a target's Damage and a
direct Heal (ADR 0033), and this Spell has neither — two Defense buffs and a Bleed on its caster — which is
why `knobs.json` already refuses it a critical knob. Snapping it would print a number on a card where the die
cannot change anything. The Spell itself may be reworked later; until then the card tells the truth. The
content of 2026-10-04 authors it at 0.

**The threshold on the card is the one `components.md` already writes**: a chance of 0.35 is `d20: 14+`. Card,
player aid and rulebook state that one and never its complement.

**Where the rule is enforced: the data builder and `check-knobs`** — options 1 and 2 above, not the
`CriticalChance` type. Taken as recommended; say so if you want it elsewhere.

**The app is already on both sides of the pass.** `CatalogueProjection` computes `21 - 20 x chance` and prints
`Crit 35% · d20 14+` when the chance is a twentieth; when it is not, the card prints the percentage and no die
line — the same fallback `components.md`:778 gives the print generator. So today Pummel's 0.767 prints as
`Crit 76.7%`, and the day this pass lands it prints a die face with **no change to the app**: the rule is read
off the content, not written into the client (stage 3 of [app-roadmap.md](app-roadmap.md)).

### What the catalogue looks like afterwards

Re-read on 2026-10-06 at content `3c9eb083`. Twenty of the 44 Spells never touch the die, and the
twenty-four that do carry **nine distinct chances**, each a clean threshold (twenty-one of 45 never touched it
at `ad3e4d00`, before Basic Attack, which never rolled, left; twenty and twenty-five at `e6f72578`; Death
Wail's 0.38 would have snapped to 0.40, a row Whirlwind still fills):

| Chance | Faces | Card |
| --- | --- | --- |
| 0.20 | 4 | `d20: 17+` |
| 0.30 | 6 | `d20: 15+` |
| 0.35 | 7 | `d20: 14+` |
| 0.40 | 8 | `d20: 13+` |
| 0.45 | 9 | `d20: 12+` |
| 0.50 | 10 | `d20: 11+` |
| 0.55 | 11 | `d20: 10+` |
| 0.75 | 15 | `d20: 6+` |
| 0.80 | 16 | `d20: 5+` |

Thirteen values become nine, and every one of them is a number a player reads off the die without
arithmetic. At the first reading it was eleven, with a 0.60 row for Shock and no 0.55; Shock
is at 0.50 now, and Vital Echo at 0.55.

**A Creature has no Critical chance, and it stays at zero.** ADR 0042 set it to zero and deliberately left the
mechanism standing, so a Creature that crits more than another remained possible. That door is closed: no
Creature carries a chance, and the rule keeps it authored at zero. Twentieth plus zero is a twentieth, so the
grid holds without depending on it.

This is what lets the card be read literally. The engine still sums the Creature's chance with the Spell's, so
until now every document had to carry the nuance — *the card's number is the whole chance because this
content's Creature is at zero*. It is a rule now rather than a property of one Creature definition, and three
documents can drop the clause when this is built: the rulebook's §6.7 and Part 9, the player aid's critical
paragraph, and the audit's `ActionResolution` row (translation.md 1.9; that sub-phase is half of `Activation`
since ADR 0083, and the audit keeps its old name).

## Still open

Nothing. What is left is the work: snap the seven Spells (Wraithguard's zeroing landed with the content
of 2026-10-04, and Death Wail's with that of 2026-10-05), move `lightning_bolt`'s band floor, teach the data
builder and `check-knobs` the grid, and pay the usual price of a content change — a new hash, a regenerated
digest, a journal entry, and the four readings saying what the snap cost.
