# Plan: the battle's craft, every spell, attack, move and the ground staged by a designer who can see

Status: accepted, 2026-11-18 (the owner settled the questions at the end). BC1 is built; BC2 is next. Each stage
below is one pull request.

## What the owner asked for

"I want the best Phaser designer in the world, one who searches every corner of the world's knowledge for the best
patterns and the most attractive solutions, for the whole game: the effects of every spell, every attack, the
background, the movements, everything. How would I do that? Is it a skill, an agent, or neither, and we redo a pass
by hand directly in Claude with the strongest model? What is the best approach?"

## The answer in short

None of the three alone. A prompt that says "you are the best Phaser designer in the world" adds no knowledge and
no eyes. Four things make the work good, and each has its own form:

| What makes it good | Its form | Why this form |
| --- | --- | --- |
| The knowledge, searched once and written down | A document, `docs/art/battle-craft.md` | A session forgets what it searched. A document is read by every later session and reviewed by the owner. |
| Eyes: the effect seen frame by frame | A bench page and a film strip script | A model that writes tweens without seeing them guesses. A strip of frames is also what the owner approves on a phone. |
| A director who stages one spell at a time | An agent, `battle-designer` | A role with its own reading list and a fresh look at a film strip, as `component-designer` is for the box. |
| The same loop every time | A skill, `/battle-pass` | Storyboard, build, film, critique, fix, show the owner: a procedure, so it is a skill. |

The building itself is done by hand: one stage, one fresh session, the strongest model, in the main session and not
in a subagent, because the build needs the gate, the browser and the owner's eye.

## What exists today

Measured on `main` at `fc07dcb`, the bundle built in this session.

| Concern | What the code and the docs hold |
| --- | --- |
| Engine | Phaser 4.2.1, TypeScript, esbuild, 42 files and 5,867 lines under `battle/src`. No watch server. |
| Spell effects | `battle/src/spell-effects.ts`, 184 lines: one `Graphics` object per effect, drawn once, tweened in scale and alpha. 44 entries in `data/presentation/spell-art.json` share 16 motifs: `impact` 8, `slash` 7, `vital` 5, `bite` 4, `spores` 3, `spirits` 3, six more used once or twice. |
| Delivery | 23 `cast`, 19 `melee`, 2 `projectile`. Only melee moves the caster (`docs/art/spell-vfx.md`). |
| What Phaser offers and the view does not use | No particle emitter, no shader, no render texture, no Phaser filter, no light. Two camera shakes, four additive blends, eight tweens. |
| Movement | A draw back and a strike (`table/battle-timing.js`: 240 ms and 600 ms), a return over 720 ms. Creatures stand still between actions: the idle bob was removed (`docs/art/battle-polish.md`). |
| Ground | One painting, `table/battle-art/eclipse-battle.webp`, tinted, with horizon plates over it (`battle/src/battle-horizon.ts`). Nothing in it moves. |
| Sound | Synthesized tones by family, off on each load (ADR 0134). Not part of this plan. |
| The boundary | The view plays the host's confirmed frames and decides nothing (ADR 0131, ADR 0141). The page names no spell: a spell's look is content, served on its card as `visual`. |
| Lifecycle | Skip, rotation, a hidden tab and a seat change cancel every effect. Reduced motion shows the settled frame with no animation. |
| Budget | `battle/budget.mjs`, ADR 0136. Live bundle 1,711,869 of 1,769,472 bytes: 57,603 left. Gzip 483,924 of 524,288: 40,364 left. Art a visit loads 1,528,370 of 2,097,152: 568,782 left. |
| The lab | `/battle-lab` plays one fictional action (`table/battle-lab-state.js`). It cannot play a chosen spell. |
| Seeing | The browser suite takes screenshots in 20 battle specs, each a still. Nothing captures an effect across time. |
| Review | `design-reviewer` reads a diff against the identity and the art docs. It does not look at a render. |

Not measured: frame time on a phone. No device is attached to a session; BC10 measures it with the browser's CPU
throttle and leaves the physical gate where ADR 0136 already holds it.

## The craft book

`docs/art/battle-craft.md`, written once from a search of the web and of the installed engine, then kept.

One entry per pattern, the same six lines each: what it is, where it comes from (a link), the Phaser 4.2.1 API that
does it, what it costs (bytes, draw calls, a phone), its reduced-motion form, where this game uses it.

The patterns it must cover:

- Timing: anticipation, contact, follow-through, hit stop, the hold before the number, easing families.
- The body: squash and stretch, recoil, a hit flash, a death, a breath at rest.
- The camera: shake driven by a decaying trauma value, a punch zoom, a slow push on a capstone.
- Particles: emitters with textures generated at boot, bursts, trails, gravity wells, additive light.
- Light and surface: Phaser 4's filters (glow, blur, displacement, colour matrix), a shader for one hero effect.
- Projectiles: arcs, ribbons, chains between targets, an impact that inherits the travel direction.
- The ground: parallax, drifting fog and embers, a light that answers a cast, a scar that fades.
- Reading: one effect one meaning, shape before colour, gold kept for the target marker (`docs/art/spell-vfx.md`).

Phaser 4 renamed and replaced parts of Phaser 3, and most examples on the web are for Phaser 3. So a test reads the
book: every engine symbol it names in backticks exists in the installed types (`battle/node_modules/phaser/types`).
A pattern the book cannot tie to a real 4.2.1 API is not in the book.

## The bench and the film strip

`/battle-lab?bench` lists every entry of `spell-art.json` and plays the chosen one on chosen creatures, from a
fixture, with no host and no rule. It is the lab's page, so the live bundle does not carry it (`battle/build.mjs`
already refuses the lab's modules in `live.js`).

`scripts/battle/filmstrip.mjs` opens the bench in Chromium, steps the scene's clock by a fixed interval, and writes
one contact sheet: twelve frames of one effect at phone size, with the millisecond under each. A fixed clock makes
two runs give the same sheet, so a sheet can be compared before and after a change.

The sheet is what the designer critiques and what the owner approves.

## The designer and the pass

`.claude/agents/battle-designer.md`. It reads the craft book, `docs/art/creature-art-direction.md`,
`docs/art/spell-vfx.md`, ADR 0131, 0134 and 0141, and `battle/AGENTS.md`. It does two things:

- It writes a storyboard for one spell, one move or the ground: the beats in milliseconds, the layers, each pattern
  by its name in the book, the reduced-motion form, the cost. Storyboards live in `docs/art/storyboards/`, one file
  a family.
- It reads a film strip against its storyboard and says what is wrong, frame by frame.

It writes no TypeScript. The session builds.

`/battle-pass <family or target>` is the loop: the storyboard, the build, the film strip, the critique, the fix, at
most three rounds, then the sheet published for the owner, then `/verify` and `/reviewers`.

## The model of an effect

Today an entry is `family`, `motif`, `delivery`. Every spell gets its own staging, and the page still names no
spell: the entry gains a `staging`, a list of beats the effects kit knows how to play (a burst, a trail, a flash, a
shake, a filter pulse), each with its parameters. The builder validates it. The 16 motifs stay as the fallback.

The kit goes in `battle/src/fx/`: one small file a primitive, each cancellable through the path `SpellEffects.clear`
already owns, each with a reduced-motion answer of "nothing". ADR 0131 said shared vector motifs; BC4 writes the ADR
that extends it.

## Stages

1. **BC1, the craft book.** The search and `docs/art/battle-craft.md`. Tests: every engine symbol the book names
   exists in the installed Phaser types; the book is in `docs/README.md`.

   *Built (2026-11-18)*: [the craft book](battle-craft.md), 40 entries in the eight groups, each with its six
   lines, and two tables of traps: the Phaser 3 names that are gone, and the names still declared that do the
   wrong thing. `studio/battle-craft.test.js` holds every engine name the book writes to the installed types
   through `scripts/battle/phaser-symbols.mjs`, which also answers for one name from the command line; a change to
   the book runs CI's full build, since the light one installs no Phaser. Found on the way, and written in the
   book: the whole engine is already in `live.js` (1,388,640 of its 1,711,869 bytes), so an engine feature costs
   only its calling code; `resetFX` does not reset a zoom; `killAll` would kill a breath made of a tween; the
   particles and the camera shake draw from `Math.random`, which BC2's bench must pin for two film strips to
   agree. Left: the session's network opened none of the links, so each source was found by search and not
   read, and the figures taken from a search excerpt are marked *to check*; and no frame time is measured,
   which is BC10's.

2. **BC2, the bench and the film strip.** `/battle-lab?bench`, `scripts/battle/filmstrip.mjs`, the clock stepping.
   Tests: the bench lists all 44 entries; two runs of one effect give the same sheet; `live.js` holds no bench code.
3. **BC3, the designer and the pass.** The `battle-designer` agent, the `/battle-pass` skill, and the first
   storyboard, for the base attack, as the worked example. Tests: `studio/prompts.test.js` on the paths they name.
4. **BC4, the effects kit.** `battle/src/fx/`, built as a second bundle loaded after the first frame with its own
   limit, the `staging` field, the builder's validation. Needs an ADR extending ADR 0131 and ADR 0136. Tests: each
   primitive is destroyed on cancel and plays nothing under reduced motion; an unknown beat falls back to the
   motif; the budgets hold.
5. **BC5, the movement.** Anticipation, strike, recoil, the hit reaction, the death, the return, and a slow breath
   at rest. Tests: no breath under reduced motion; the timings still agree with `table/battle-timing.js` at every
   playback rate; a skip settles the figures.
6. **BC6, the physical spells and the two base actions.** 17 stagings. Tests: the film strips, the browser specs
   the change touches, one staging per spell in the data.
7. **BC7, the biological spells.** 14 stagings. Same tests.
8. **BC8, the elemental, astral and necromantic spells.** 14 stagings, the blood cost and the wards among them.
   Same tests.
9. **BC9, the ground.** Parallax on the horizon plates, fog and embers, the eclipse's light answering a cast.
   Tests: nothing moves under reduced motion; a hidden tab stops the emitters; the art budget holds.
10. **BC10, acceptance.** Frame time under the browser's CPU throttle at phone size for every staging, the budgets,
    reduced motion across all 44, the gallery of film strips published. The record goes in `docs/art/`.

BC6 to BC8 each end with the owner approving the family's sheets before the pull request is reported ready.

## Questions the owner settled (2026-11-18)

1. The approach: the four pieces. The craft book, the bench and the film strip, the `battle-designer` agent and the
   `/battle-pass` skill, then the passes by hand, one stage a session.
2. The budget: a second file. The kit loads after the first frame, as its own bundle with its own limit in
   `battle/budget.mjs`. The live bundle's limits do not move. BC4 sets the new limit and records it in its ADR.
3. The pictures: code, and small generated sprite sheets too, inside the 568 KiB of art a visit has left. A sheet
   passes the intake of `docs/art/asset-intake.md` and loads with the kit, not with the first frame. Code comes
   first: a sheet is for what textures drawn at boot cannot give.
4. Rest: a slow breath comes back, off under reduced motion. BC5 builds it and amends `docs/art/battle-polish.md`,
   which says creatures stand still.
