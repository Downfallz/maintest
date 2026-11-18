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
| [base-actions.md](base-actions.md) | The base attack, Strike. Focus joins it in BC6. | BC6 |

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

At 1× the windup runs from 0 to 840 ms, the impact from 840 to 2540 ms and the settle from 2540 to 3040 ms. Every
duration is divided by the playback rate when it is played; a storyboard writes them at 1× only.

`studio/storyboards.test.js` holds the format in the gate: the seven parts in order, each spell an entry of
`data/presentation/spell-art.json`, each beat inside its window on the timings of `table/battle-timing.js`, each
pattern a heading of the craft book, each file listed in the table above.

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
