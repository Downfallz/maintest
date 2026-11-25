# Storyboards

How one spell, one move or the ground is staged in the battle view, written before it is built. A storyboard is
what the session builds from and what the `battle-designer` agent reads a film strip against
([the battle's craft plan](../battle-craft-plan.md), BC3). It names its patterns in the words of
[the craft book](../battle-craft.md) and obeys [its rules](../battle-craft.md#rules-every-pattern-obeys).

A storyboard is a proposal until its stage builds it and the owner has approved its film strips. What plays today
is in `battle/src`, not here.

## The files

One file a family. A family's stage of the plan (BC5 to BC9) adds its file here, and the storyboards in it.

| File | What it stages | Built by |
| --- | --- | --- |
| [astral.md](astral.md) | The astral family, the Shaman's line of the Sorcerer: 5 spells, by the ladder. | BC8 |
| [base-actions.md](base-actions.md) | The base attack, Strike. Focus joins it in BC6. | BC6 |
| [biological.md](biological.md) | The biological family, the Scoundrel's 14 spells, by the ladder: Stage I, then Stage II, then Stage III. | BC7 |
| [necromantic.md](necromantic.md) | The necromantic family, the Necromancer's line of the Sorcerer: 4 spells, Stage II then Stage III, each with its blood cost. | BC8 |
| [physical.md](physical.md) | The physical family, the Brute's 14 spells, by the ladder: Stage I, then Stage II, then Stage III. | BC6 |
| [ground.md](ground.md) | The ground, and no spell's look: the air at rest (fog and embers), the depth (parallax), the light that answers a cast. | BC9 |
| [elemental.md](elemental.md) | The elemental family, the Wizard's line of the Sorcerer: 5 spells, by the ladder. | BC8 |
| [movement.md](movement.md) | What every action shares, and no spell's look: a melee blow, a cast and a projectile, a death, the breath at rest, the camera. | BC5 |

## The format

A file opens with a `#` title and a line on its family. Each storyboard is a `##` heading: the name a player
reads (the `name` of the spell's file, not its id's slug), then the content id in backticks when it stages a
spell, as in ``## Strike (`spell:heavy_strike:v1`)``. A spell has one storyboard. Under it, seven `###` parts,
in this order:

- **Intent**: what the player must read from the effect, in one or two sentences, and how much attention it may
  take for what it does in the game.
- **Today**: what the film strip of the current view shows, frame by frame where it matters, with the commit
  filmed.
- **Beats**: one table, a row a beat. A stretch where nothing new plays is a beat too: it says what the figures
  do at rest, and that the result is read there. The columns are fixed:

  | Column | What it holds |
  | --- | --- |
  | At | When the beat starts, in whole milliseconds from the start of the action, at 1×. |
  | Lasts | How long it lasts, in whole milliseconds, at 1×. |
  | Window | `windup`, `impact` or `settle` (`table/battle-timing.js`). The beat starts and ends inside it. |
  | Layer | What carries it: the caster, the target, the effect, the camera, the ground. |
  | Beat | What is seen, with its numbers. A number no source gives is marked *house*. |
  | Pattern | A link to the pattern's entry in the craft book, two at most. A beat that leans on more is split into rows. |

- **Reduced motion**: what is left when the player asked for less motion. For most, nothing.
- **Cost**: objects, draw calls, particles at the peak, and the stage of the plan that builds each primitive
  the beats need. No byte count that was not measured.
- **On the film strip**: what a frame must show, by its millisecond, and the command that films it, with its
  caster and its targets (`--actor`, `--targets`): the bench's defaults may change. The critique checks these
  lines.
- **Left**: what the storyboard does not settle, a choice the owner may refuse, a pattern the book lacks.

At 1× the windup runs from 0 to 840 ms, the impact runs from 840 to 2540 ms and the settle runs from 2540 to
3040 ms. Every duration is divided by the playback rate when it is played; a storyboard writes them at 1× only.
A change to `table/battle-timing.js` moves the windows: the test then says which beats and which of these
figures to move.

A `trail` runs from the caster to each recipient other than itself. A caster among its own recipients, a ward or a
heal it casts on itself, draws no trail: one from a figure to itself would have no length (#949).

`studio/storyboards.test.js` holds the format in the gate: the seven parts in order, each spell an entry of
`data/presentation/spell-art.json`, each beat inside its window on the timings of `table/battle-timing.js`, each
pattern a heading of the craft book, each file listed in the table above.

## The ladder

The owner's rule (2026-11-19): the higher the Stage of the mutation that teaches a spell, the more spectacular its
staging. A base action is seen every round and a Stage III spell a few times a game, so the eye is spent where the
game spends its rarity. A spell is staged inside the band of its Stage, and `studio/staging-ladder.test.js` holds
the bands on what `data/presentation/spell-art.json` carries. A spell two mutations teach takes the lower Stage.

| | Base action | Stage I | Stage II | Stage III |
| --- | --- | --- | --- | --- |
| Beats in the staging | 1 to 3 | 4 to 5 | 6 to 8 | 9 to 12 |
| Particles asked for on one target, all beats together | up to 8 | up to 24 | up to 60 | up to 120 |
| Beats in the windup | none | none | one at most | one at least: the charge is seen before the blow |
| Light (`glow`) | none | none | one at least | two at least |
| The halo on a figure's outline (`pulse`) | never | never | never | one at most, on one figure |
| A staged `shake`, over the tremor of the health lost | none | none | up to 0.15 | up to 0.35 |
| The slow push of the camera (`push`), in the windup | never | never | never | one at most: a family's capstone |
| The last beat is gone by | 1540 ms | 1700 ms | 2000 ms | 2400 ms |

All numbers are *house*. What the rule does not move: the windows of `table/battle-timing.js`, one flash a figure
an action, the field's 150 particles (a spell that strikes three figures asks each for a third), gold kept for the
target marker, and the body's own movement, which answers the health the host confirmed lost and not the Stage
([movement.md](movement.md)). A Stage III staging still ends before the result is read: the hold from 2400 ms to
the end of the impact stays empty.

## The loop

`/battle-pass <family or target>` (`.claude/skills/battle-pass/SKILL.md`) runs it: the film of what plays today,
the storyboard, the build, the film, the critique, the fix, at most three rounds, then the sheets shown to the
owner.

To film one effect (`battle/README.md`, "The effects bench"):

```bash
npm --prefix battle run build
node scripts/battle/filmstrip.mjs spell:heavy_strike:v1 --actor a1 --targets b1      # the whole action
node scripts/battle/filmstrip.mjs spell:heavy_strike:v1 --actor a1 --targets b1 \
  --frames 24 --interval 60 --columns 6                                             # the contact
```
