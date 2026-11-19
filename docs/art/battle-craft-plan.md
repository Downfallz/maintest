# Plan: the battle's craft, every spell, attack, move and the ground staged by a designer who can see

Status: accepted, 2026-11-18 (the owner settled the questions at the end). BC1 to BC8 are built; BC9 is next.
Each stage below is one pull request.

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
   through `scripts/battle/phaser-symbols.mjs`, which also answers for one name from the command line. A change
   to the book runs CI's full build, since the light one installs no Phaser. Found on the way, and written in
   the book: the whole engine is already in `live.js` (1,388,640 of its 1,711,869 bytes). So an engine feature
   costs only its calling code. On a phone a batch holds one texture. `resetFX` does not reset a zoom. `killAll`
   would kill a breath made of a tween. The particles and the camera shake draw from `Math.random`, which BC2's
   bench must pin for two film strips to agree. Every one of the book's 36 links was opened and read against the entry that cites it;
   what no page says is marked *house*, and the sources that could not be read are left out and listed. Left:
   no frame time is measured; that is BC10's.

2. **BC2, the bench and the film strip.** `/battle-lab?bench`, `scripts/battle/filmstrip.mjs`, the clock stepping.
   Tests: the bench lists all 44 entries; two runs of one effect give the same sheet; `live.js` holds no bench code.

   *Built (2026-11-18)*: the bench is the laboratory's page with `?bench` (`battle/src/bench.ts`, and
   `table/battle-bench.js` for what it lists and presents): the 44 entries, a caster and any of the fixture's six
   creatures as targets, the speed and less motion. It holds no rule: a target across the field is struck, one
   on the caster's side receives. `scripts/battle/filmstrip.mjs` serves `table/` itself, films at 390 by 844 and
   writes one sheet an effect, or all 44 with `--all` (about three minutes in a session). The film clock had one surprise:
   Phaser's tweens do not run on the time the game's step is given, they read `Date.now`. So a film stops the
   loop, steps the game in ticks of 20 ms, and pins `Date.now` as well as `Math.random`; the craft book's rule 5
   says so now. Two sheets of one effect are the same file byte for byte on one machine
   (`studio/browser/battle-bench.spec.js`); across machines the renderer may differ, so a sheet is compared with
   one filmed on the same machine. `build.mjs` refuses the bench and the spell art in `live.js`, and
   `table/battle-bench.test.js` reads the built bundle for it. Left: the bench plays a spell's look, not yet a
   move alone, a death or the ground. BC5 and BC9 add those when they build them. The sheets are not published
   anywhere: `/battle-pass` (BC3) and BC10 do that.

   *Built later (2026-11-18, #843)*: the bench offers a fizzle and a blood cost, each a choice of the fixture and
   no rule. **Outcome** plays the spell or fizzles it; a fizzle is drawn as the live page draws one
   (`actionCue`): the fizzle's mark on the caster, and no recipient. **The caster pays in blood** adds the
   caster as a recipient with a wound, as a blood cost's harm to its caster is drawn. It is off under a fizzle,
   which draws no outcome. The film strip takes `--fizzle` and `--wound` (refused together), and names their
   sheets `pummel-fizzled.png` and `pummel-wound.png`, beside the spell's own.
3. **BC3, the designer and the pass.** The `battle-designer` agent, the `/battle-pass` skill, and the first
   storyboard, for the base attack, as the worked example. Tests: `studio/prompts.test.js` on the paths they name.

   *Built (2026-11-18)*: the agent (`.claude/agents/battle-designer.md`) with its two jobs, a storyboard and a
   critique, and the skill (`.claude/skills/battle-pass/SKILL.md`): film what plays today, the storyboard, the
   build, the film, the critique, at most three rounds, the sheets on one page for the owner, then the gate.
   [The storyboards](storyboards/README.md) have a format of seven parts, and
   `studio/storyboards.test.js` holds it: each spell is an entry of the spell art, each beat stays inside its
   window on the timings of `table/battle-timing.js`, each pattern is a heading of the craft book. The first one,
   [Strike](storyboards/base-actions.md), was written by the designer's prompt from two film strips of `4d51935`:
   the whole action, and the contact at a frame every 60 ms, since the contact falls between two frames of the
   first. What it found on the film: the draw back has no ease and barely shows, a melee blow opens with the
   gather a cast uses, the strokes lean the same way whatever the blow's direction, and nothing moves after
   1680 ms. Writing it corrected the format (two patterns a beat, the film's caster and targets named) and
   two entries of the craft book. Left: the storyboard is a proposal, and its choices under **Left** wait for
   the owner: no gather on a melee blow, the leaning strokes, the 8 chips. Nothing of it is built: BC4 and BC5
   make the primitives, BC6 places them. The loop's build, critique and published page have not been run yet;
   BC5 is the first stage to run them. The book's rule that the bright beats end within 120 ms of contact
   leaves a burst about one frame on a sheet: BC4 looks at it again on its first burst. The bench and a sheet's
   title still name a spell by its id's old slug ("Heavy Strike" for Strike), with no mutation: the designer is
   told to stage from the content's name and the approved execution in `creature-art-direction.md`, and BC4,
   which changes what the builder publishes for an effect, gives the bench the real names.

   *Built later (2026-11-18, #867)*: the bench lists each spell, and a film strip is titled, with the mutation
   that casts it and its Stage: "Pummel · Brute, Stage I · physical · impact · melee". Strike and Focus are
   starting spells. `battle/bench-spells.mjs` reads the spells, the mutations (`data/Tiers`), the creatures and
   the aliases when the bundle is built; the image's battle stage copies all four, and `live.js` holds none.
4. **BC4, the effects kit.** `battle/src/fx/`, built as a second bundle loaded after the first frame with its own
   limit, the `staging` field, the builder's validation. Needs an ADR extending ADR 0131 and ADR 0136. Tests: each
   primitive is destroyed on cancel and plays nothing under reduced motion; an unknown beat falls back to the
   motif; the budgets hold.

   *Built (2026-11-18)*: [ADR 0196](../adr/0196-stage-a-spell-in-beats-an-effects-kit-plays.md) extends ADR 0131
   and ADR 0136, and holds the decision: the `staging`, its nine kinds of beat, what the builder refuses and
   when the page falls back to the motif. What follows is what the build found.

   - Sizes. The kit, `battle/src/fx/`, builds as `fx.js`: under 7 KiB, about 2.6 KiB gzip, against limits of
     48 KiB and 16 KiB in `battle/budget.mjs`. The live bundle grew by about 5 KiB and stays under its limits.
   - No spell has a staging yet. The bench lists one sample for each primitive (`kit:burst` to `kit:pulse`), and
     each was filmed and looked at.
   - Faults the films found, fixed here. A burst's chips stayed under the motif: its reach is now twice the
     figure's radius unless told. The trail ran ahead of the quill. The pulse's halo was cut at the picture's
     edge.
   - Faults the reviewers found, fixed here. The image's battle stage did not copy `data/Spells`, which the
     bench's names are read from. The builder let through a null beat, a beat with no `at`, a `lasts` large
     enough to wrap around, and a misspelt `staging`. The 150 counted particles alive, so streams that start
     together passed it. A staged shake and the shared tremor of a physical blow both played.
   - The bench names a spell as its file does: Strike, not Heavy Strike.
   - *Later (#882)*: a test reaches the cap. The bench's `kit:budget` sample starts seven streams of 24 together
     on each target, and `studio/browser/battle-fx.spec.js` steps its film and holds the field to 150 particles
     at every step. Counting particles alive, as the first version did, it reached 196.
   - The 120 ms BC3 left. A 120 ms burst is two or three frames at 60 a second and reads as a flicker, so the
     kit defaults to 320 ms. A staging that wants 120 ms says `lasts`. BC5, which builds the hold, decides
     whether the book's rule keeps the burst.

   Left for later stages:

   - Each beat makes its own emitter, not one per texture as the book asked. An emitter's settings move its
     living particles.
   - The `shake` is a decaying tremor, not yet the one trauma value BC5 builds. The `flash` is the tint BC5's
     hit flash will call. The laboratory's sample still draws its 14 sparks by hand: BC5 rewrites that impact.
   - No ribbon is built.
   - An action that plays before the kit has arrived is drawn as its motif.
   - The `pulse` is costly: under a renderer with no graphics card, a frame with it takes about ten times a
     frame without. BC10 measures it on a phone before a staging uses it on more than one figure.
   - Frame time is not measured: BC10.

5. **BC5, the movement.** Anticipation, strike, recoil, the hit reaction, the death, the return, and a slow breath
   at rest. Since #842 a still field is drawn one frame in four (`battle/README.md`, "The resting loop"): a breath
   at rest plays at that rate, or is counted as movement and costs the full rate, measured in BC10. Tests: no breath under reduced motion; the timings still agree with `table/battle-timing.js` at every
   playback rate; a skip settles the figures.

   *Built (2026-11-19)*: [the movement's storyboard](storyboards/movement.md), five parts, and the first run of
   the whole `/battle-pass` loop. The numbers are in `table/battle-motion.js`, the body's moves in
   `battle/src/body-motion.ts`, the view's in `battle/src/view-shake.ts` (`battle/README.md`, "The movement").

   - The weight of a blow is the health the host confirmed lost: light to 3, medium to 6, heavy from 7 or on a
     defeat. In absolute points: the scene's frame does not carry a creature's maximum health.
   - A melee blow: the draw back and its hold by the weight, the strike that lands when the windup ends, the
     stretch, the hit stop, the hit flash on a figure that lost health, the recoil after the stop, the return a
     little past home. The windup stays 840 ms: `table/battle-timing.js` did not move.
   - A cast loads where it stands; a projectile's caster throws. A figure that lost health from afar now answers,
     on half the stop.
   - A death is seen: thrown back with no return, then a fall that ends on the settled frame, inside the impact.
     A tint drains the colour, not the colour matrix: the figure fades as it drains.
   - The breath at rest is back (question 4): 1.5% of height every 3.6 s, off with less motion. It is ambience,
     so it plays at the resting rate and #842's saving stands; `docs/art/battle-polish.md` is amended.
   - One trauma value replaces the two fixed camera shakes, and the kit's `shake` beat adds to it. A defeat or a
     critical hit punches the zoom by 3%.
   - The bench tells a blow's weight and a death (`--damage`, `--defeat`), and a film writes beside its sheet
     what no sheet shows (`--motion`): each figure's place, lean, scale and alpha, and the view's trauma.
   - The laboratory's sample hit lost its 14 sparks drawn by hand: it is a light blow with the kit's burst.
   - The book's rule of the bright beats is settled: each starts within 120 ms of contact and is gone within
     320 ms of it. The kit's 320 ms burst stands.
   - Found by the specs: at 2× a tremor's swing is shorter than a frame, and a recoil that started from the
     tremor's far end returned to that end instead of home. The recoil now starts from home.

   Left:

   - The choices under each storyboard's **Left** wait for the owner. The ones a player would notice: the wind-up
     is scaled by the confirmed outcome before the number shows; every family moves the view, not only the
     physical one (built so, and `docs/art/spell-vfx.md` says so: the owner may still refuse it); the fallen body sinks back onto its own spot.
   - The motif's pale arc still plays in the windup beside the body's own load: BC6 to BC8 decide each spell's.
   - A projectile's caster throws at 240 ms, when the quill's flight starts, but the quill eases in and is seen to
     leave only near 600 ms. BC7, which gives the quill its arc, makes it leave with the throw.
   - The critique's one fault of the build is fixed: an impact that arrived a frame before the strike's end left
     the caster short of its contact pose. Its other finding is the projectile's late leave, above.
   - Frame time is not measured, and nobody has seen the breath on a phone: BC10.
6. **BC6, the physical spells and the two base actions.** 17 stagings. Tests: the film strips, the browser specs
   the change touches, one staging per spell in the data.

   *Built (2026-11-19)*: 16 stagings, not 17: the data holds 15 physical spells, Strike among them, and Focus.
   Each has its storyboard ([the base actions](storyboards/base-actions.md), [the physical family](storyboards/physical.md))
   and its `staging` in `data/presentation/spell-art.json`, filmed before and after and read by the designer.

   - The owner's rule for this stage: the higher the Stage of the mutation that teaches a spell, the more
     spectacular its staging. It is written as [the ladder](storyboards/README.md#the-ladder), four bands (a base
     action, Stage I, II, III) on what a staging may ask for: beats, particles, a beat in the windup, lights, the
     halo, the staged shake, when it is gone. `studio/staging-ladder.test.js` holds each staged spell to its
     band, and the bands to the README. BC7 and BC8 stage inside the same bands.
   - Each spell has one signature a player could name: Pummel's dust collar, Brace's rings and plates, Fury's
     explosion, Wild Swing's fan and blood, Seize's pull back to the caster, Crash's hard ring, Carapace's
     shards closing in, Body Slam's ground ring and dust, Dominate's two blows, Alpha Roar's three rings,
     Crushing Stomp's crown, Vital Surge's wave, Deranged Charge's cone, Groundbreaker's travelling shock.
   - What the first films found, fixed in the kit. A burst as BC4 built it could not be read: its pieces spent
     their life over the figure's own body. A piece now leaves fast, slows and falls a little; a `strength` on a
     particle beat is its pieces' size; a `reach` goes up to 4 radii (400 hundredths), since 200 ends inside
     the silhouette. A `glow` with the shape of a ring is a shock ring lying on the ground.
   - The choices BC3 left on Strike are built as proposed, for every spell that draws those motifs: a staged
     melee blow shows no gather on its caster unless its beats say so, and the strokes of the `impact` motif run
     along the line of the blow, each over a dark edge. Since #919 the `slash` strokes cut 60° across it.
   - `studio/browser/battle-stagings.spec.js` plays every staged spell on the bench, on as many figures as the
     content lets it reach: every beat is seen, the field stays inside its 150 particles, nothing is left when
     the action has resolved, a stop at the height of the richest one clears it, and less motion plays none.

   Left:

   - The choices under each storyboard's **Left** wait for the owner, with the sheets.
   - Settled in BC6's follow-ups (#917 to #919): a beat's `level` starts it at the feet, the centre or the head,
     a shock ring can be aimed, and a trail at the feet is a crack. The caster's own wound or mark goes with its
     body. A windup beat on a melee caster still stays where the caster stood (`storyboards/physical.md`).
   - A staging cannot tell a critical hit: only the body's punch of the zoom does.
   - Frame time is not measured: BC10. The `pulse` is used four times, each on one figure.
7. **BC7, the biological spells.** 14 stagings. Same tests.

   *Built (2026-11-19)*: the 14 stagings, each with its storyboard in [the biological family](storyboards/biological.md)
   and its `staging` in `data/presentation/spell-art.json`, filmed before and after and read by the designer.

   - On the ladder as BC6 left it: 2 spells at Stage I (4 beats), 6 at Stage II (6 beats), 6 at Stage III (9 or
     10). `studio/staging-ladder.test.js` now holds the biological family to its bands and to its storyboards.
   - Each spell has one signature a player could name: Venom Claw's weeping scratch, Quill's splinters going
     through, Overdrive's double jolt, Toxic Mend's mend that sheds, Ambush's burst, Pursuit's streak, Latch's
     clamp, Bite's gulp, Contagion's cloud that hangs, Paralyzing Barb's pin, Eviscerate's pool, Blood Hunt's
     theft, Blood Price's price paid first, Devour's siphon and the family's one halo.
   - The family keeps one meaning a look, written at the head of its file: a round green drop is what a gland
     makes, green sparks are energy (rising: gained; drawn off toward the foe: lost), bone is a quill, a claw or a
     flake of hide, blood on the struck figure is its Bleed and blood carried to the caster is what it drank. So
     Ambush, Blood Hunt and Blood Price's target show no blood.
   - The quill leaves with the throw (#909). A projectile is not drawn while its caster loads; at 240 ms it
     appears just over the caster's head and flies 600 ms to the contact, at an even pace whichever way it is
     thrown (`FLIGHT` and `flightAt` in `table/battle-motion.js`, the craft book's "Arcing travel"). Thrown across
     the field it bows on a low arc; straight up or down it, its height lies along the line. Its debris and its
     strokes go on down the end of the arc. `THROW` did not move. A `trail` on a projectile follows the same
     curve: Paralyzing Barb has the family's one; Quill, a Stage I spell with no beat in its windup, flies bare.
   - What the designer's reading of the first films found, in nearly every spell: pieces thrown `along` or `back`
     under a reach of 300, or flowing under a reach of 120, never left the struck body; dots under a strength of
     0.4 piled into one soft cloud; bone pieces cannot be seen on a figure filled in bone by its hit flash, at
     840 and 900 ms. About 50 beats and lines were changed and the films made again; a second reading asked for 22 more,
     made too. The session checked the third films; the designer did not read them.

   Left:

   - The choices under each storyboard's **Left** wait for the owner, with the sheets: blood kept for a Bleed or
     a drink, green sparks for energy, the halo for a heal only, Blood Price's cost shown in its windup.
   - A beat on a caster stays at the caster's home while its body crouches or lunges (Blood Hunt's ring, Blood
     Price's light); a gather lands on a figure's centre, not its outline (Latch's hooks); a flow cannot fall;
     Blood Hunt's chain from one victim to the next has no beat. Each is a change to the kit.
   - Frame time is not measured: BC10. The `pulse` is used once in this family, on Devour's caster.
8. **BC8, the elemental, astral and necromantic spells.** 14 stagings, the blood cost and the wards among them.
   Same tests.

   *Built (2026-11-19)*: the 14 stagings, each with its storyboard ([elemental](storyboards/elemental.md),
   [astral](storyboards/astral.md), [necromantic](storyboards/necromantic.md)) and its `staging` in
   `data/presentation/spell-art.json`, filmed before and after and read by the designer. All 44 entries are staged.

   - On the ladder as BC6 left it: 2 spells at Stage I (4 beats), 6 at Stage II (6 or 7), 6 at Stage III (9 or
     10). The necromantic family has no Stage I spell. `studio/staging-ladder.test.js` now holds every family.
   - Each spell has one signature a player could name: Shock's crackle, Ice Grip's shards at the legs, Emberstorm's
     three flares, Incinerate's charge and burst, Deep Freeze's shards closing in and its ring; Revitalize's rise,
     Vital Echo's second wave, Whisper's low mist, Renewal Burst's burst and the family's one halo, Void Pulse's
     pieces drawn in; Bonewall's bone rising from the feet, Infestation's swarm that settles, Wraithguard's
     standing rings, Death Wail's arcs going through.
   - The blood cost has one look in the four necromantic spells: red drops leaving the caster in the windup, 6, 8
     or 14 by the price, before the scene's own wound. A Stage III spell lights its caster red first, and again at
     home. Wraithguard carries the price along the ground to each ally.
   - A ward does not shake the view and has no halo: bone for Bonewall, the family's blue-green ring standing at
     the feet for Wraithguard. The tight ring that lingers at the feet stays the stun's in every family (Deep
     Freeze), a slow has none (Ice Grip), and blood stays a Bleed, even from fire or the void.
   - The bench gave a caster who pays in blood the wound alone, so a ward cast on oneself was filmed without its
     ward. A caster among its own recipients now receives the spell too (`benchFrame`, `table/battle-bench.js`).
   - The pass ran two rounds. The designer's first reading asked for 37 changes (8, 14 and 15 by family), nearly all
     of three causes: a beat in the windup nobody could see, soft dots piled in one cloud, a trail too short to
     show. They were made and the spells filmed again. The designer read the second films and the data was not
     changed after them: what they still found is under each storyboard's **Left**.

   Left:

   - The choices under each storyboard's **Left** wait for the owner, with the sheets.
   - A trail that is not a projectile's eases in and its pieces live half its length at most, so the line from a
     caster to an ally hardly shows (Revitalize, Vital Echo, Renewal Burst, Incinerate). A change to the kit.
   - Still short on the second films: Ice Grip's shards clump under the crystal, Bonewall's stay inside the
     figure, Infestation's Bleed is lost under its own light, Renewal Burst's sparks of energy and its halo are
     hard to pick out, the column that says regeneration does not climb.
   - Not built, though the craft book had named them for this stage: Shock's arc redrawn with a flicker, a slow
     push of the camera on a Stage III spell, a dark of its own for the void, a chain from one target to the next.
   - Frame time is not measured: BC10. The `pulse` is used once in these families, on Renewal Burst's target.
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
