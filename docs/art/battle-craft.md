# The battle's craft book

The patterns the battle view stages its spells, attacks, moves and ground with: what each one is, where it comes
from, the Phaser API that does it, what it costs, what it becomes under reduced motion, and where this game uses
it. It is BC1 of [the battle's craft plan](battle-craft-plan.md). The `battle-designer` agent (BC3) writes
[storyboards](storyboards/README.md) in these words, and the session that builds a stage reads the entry before
it writes a tween.

Written on 2026-11-18 from the installed engine (Phaser 4.2.1, `battle/node_modules/phaser`) and a search of the
web. It is a reference, not a decision: a pattern is used when a stage of the plan builds it.

## How to read an entry

Every entry has the same six lines.

- **What**: the pattern, in one or two sentences, with the numbers its source gives.
- **Source**: where it comes from, as a link.
- **Phaser**: the Phaser 4 API that does it. An engine name is written in full, as Phaser's own documentation
  writes it: the namespace, the class, then `#` and the member, as in `Phaser.Cameras.Scene2D.Camera#shake`.
  After a configuration type's key, such as `Phaser.Types.Tweens.TweenBuilderConfig.ease`, a name that starts
  with a dot (`.hold`) is another key of the same type.
- **Cost**: bytes, draw calls, and what a phone pays.
- **Reduced motion**: what is left of it when the player asked for less motion.
- **Here**: what the view does today, and which stage of the plan uses the pattern.

A number or a claim marked *house* is ours: no source read for this book gives it, and a film strip (BC2) tunes
it.

## Rules every pattern obeys

These come from the decisions already taken. No pattern below is an exception to them.

1. The host decides. An effect shows a confirmed outcome on its confirmed recipients, and nothing else
   (ADR 0131). No effect delays a command, and the player's own cast is never held (ADR 0141).
2. One clock. Every duration is divided by the playback rate and fits inside the windows of
   `table/battle-timing.js`: 840 ms of windup, 1700 ms of impact, 500 ms to settle, at 1×. A pattern spends time
   inside a window. It never makes one longer.
3. One owner. Every object and every tween is destroyed by the path `clearMotion` runs in `battle/src/scene.ts`:
   a skip, a rotation, a hidden tab and a seat change all go through it. That path ends in
   `Phaser.Tweens.TweenManager#killAll`, `Phaser.Cameras.Scene2D.Camera#resetFX` and `SpellEffects.clear`.
   A pattern that starts something this path does not stop adds its own line to it.
4. Reduced motion shows the settled frame. Each entry says its form; for most it is "nothing".
5. Randomness is pinned where a film strip must repeat. Phaser's particles and its camera shake draw from
   `Math.random`, not from `Phaser.Math.RND`, so the bench pins `Math.random` for the length of a play
   (`battle/src/bench.ts`). Phaser's tweens keep a clock of their own, read from `Date.now` and not from the
   time the game's step hands a scene: a film pins `Date.now` too, and a pattern that reads the wall clock itself
   cannot be filmed.
6. The budgets hold (`battle/budget.mjs`, ADR 0136). The measures are in the next section.
7. Sound is another matter (ADR 0134). No entry here plays one.

## Measured on the installed engine

Measured in this session on `main` at `49efe5d`, the bundle built with `npm --prefix battle run build`.

| Fact | Measure | What it means for a pattern |
| --- | --- | --- |
| The whole engine is in the bundle | `dist/phaser.esm.js` is 1,388,640 of the 1,711,869 bytes of `live.js`. The bundle already holds the particle emitter, every filter, the shader quad, the point light and the rope. | A pattern that uses an engine feature adds its calling code, and no engine bytes. |
| What batches | Images, sprites, text, particles and render textures share one batch family; `Graphics` and shapes another; a point light a third (the engine's rendering guide). | Many particles from one emitter are one draw call. Forty `Graphics` objects are not forty draw calls: they batch too, until something breaks the batch. |
| What breaks a batch | A filter, a change of blend mode, a change of lighting, another kind of object (a `Phaser.GameObjects.Shader`, a `Phaser.GameObjects.Rope`). | Draw every additive object together, after the normal ones, and not interleaved. |
| One texture a batch on a phone | `Phaser.Types.Core.RenderConfig.autoMobileTextures` is on by default, and the view does not turn it off: on iOS and Android a batch holds one texture. | Each creature's picture is already its own draw call there. Put the kit's shapes on one texture, or each one costs one more draw call. |
| What a filter costs | The object is drawn to a framebuffer, then one pass per active filter. | A filter is a draw call or more, each frame, for as long as it is active. Remove it when the effect ends. |
| What the engine warns about | "Draw calls are expensive. Don't use more than a few hundred." "Filling pixels is expensive." | On a phone the limit is pixels filled, before objects counted. |
| Frame time on a phone | Measured for the page's thread, not for the graphics card ([the acceptance record](battle-acceptance.md), BC10). With the processor slowed four times, no step of any of the 44 stagings takes a frame: the worst is 11.8 ms. No device is attached to a session, and a session draws in software. | A staging's code does not use up a phone's frame. What its pixels cost a phone's graphics card stays with the physical gate of ADR 0136. `node scripts/battle/frametime.mjs` measures a new staging against the record. |

## Timing

### Anticipation

- **What**: a small move against the action, before it. It tells the eye where to look and makes the strike
  believable. In a game it is a negotiation: design wants few frames, animation wants many.
- **Source**: [Twelve basic principles of animation](https://en.wikipedia.org/wiki/Twelve_basic_principles_of_animation),
  after Thomas and Johnston, *The Illusion of Life*, 1981; and
  [The 12 principles of animation in video games](https://www.gamedeveloper.com/production/the-12-principles-of-animation-in-video-games),
  Jonathan Cooper.
- **Phaser**: `Phaser.Tweens.TweenManager#chain` plays the draw back and then the strike as one cancellable
  object; the draw back eases out (`Phaser.Math.Easing.Cubic.Out`), so it slows as it loads.
- **Cost**: one tween. No draw call.
- **Reduced motion**: nothing. The frame after the action is shown at once.
- **Here**: built in BC5 (`battle/src/body-motion.ts`, on the numbers of `table/battle-motion.js`). A melee caster
  draws back 20, 26 or 32 px by the weight of the blow, eased out, and holds 0, 40 or 80 ms before it strikes. A
  cast loads in the body, a squash of 4% where it stands; a projectile's caster loads 8 px away from its target.
  Neither lunges.

### Contact and follow-through

- **What**: the strike accelerates into the contact frame, the one frame the eye keeps. What trails the body
  arrives after it and settles past its rest: follow-through and overlapping action.
- **Source**: [Twelve basic principles of animation](https://en.wikipedia.org/wiki/Twelve_basic_principles_of_animation),
  "Follow through and overlapping action" and "Slow in and slow out".
- **Phaser**: the strike eases in (`Phaser.Math.Easing.Cubic.In`); the settle overshoots with
  `Phaser.Math.Easing.Back.Out`, its overshoot set by `Phaser.Types.Tweens.TweenBuilderConfig.easeParams`.
- **Cost**: tweens only.
- **Reduced motion**: nothing.
- **Here**: the strike eases in and lands when the windup ends: 600, 560 or 520 ms by the weight of the blow. The
  return is 720 ms of `Back.Out` with an overshoot of 1.2 (BC5). The kit starts an impact beat on the contact frame, not before: a beat at 840 ms has no delay.

### Hit stop

- **What**: the two figures freeze for a few frames at contact, so the hit reads as heavy. The stop grows with
  the damage. A projectile gives less of it, an electric attack more. Super Smash Bros. caps it at 20 frames in
  Melee and 30 from Brawl on. The struck figure shakes more than the attacker, sideways on the ground and up and
  down in the air, and the shake lessens gradually; the attacker keeps moving just a little.
- **Source**: [Thinking About Hitstop](https://sourcegaming.info/2015/11/11/thoughts-on-hitstop-sakurais-famitsu-column-vol-490-1/), Masahiro Sakurai's Famitsu column, translated by Source Gaming, 2015;
  [Hitlag](https://www.ssbwiki.com/Hitlag), SmashWiki; [Fine-tuning hit stop](https://nintendowire.com/news/2022/12/12/this-week-in-sakurai-12-5-12-11-fine-tuning-hit-stop-and-cheating-the-system/), Nintendo Wire, 2022, on Sakurai's eight hit stop
  techniques.
- **Phaser**: `Phaser.Tweens.TweenManager#setGlobalTimeScale` at 0 freezes every tween;
  `Phaser.Time.Clock#delayedCall` still runs and sets it back to 1. Emitters have their own
  `Phaser.GameObjects.Particles.ParticleEmitter#timeScale`. To freeze two figures only, use
  `Phaser.Tweens.Tween#pause` and `Phaser.Tweens.Tween#resume`: the global scale stops the effect's own tweens
  too, so a motif meant to arrive during the stop needs this second route.
- **Cost**: none to draw. The stop is spent inside the impact window, never added to it. The cancel path must
  set the time scale back to 1 and remove the timer (`Phaser.Time.TimerEvent#remove`): `killAll` does neither.
- **Reduced motion**: nothing. The action is not played, so there is nothing to freeze.
- **Here**: built in BC5: 60, 90 or 120 ms at 1× by the health lost (*house*), half that from a distance, divided
  by the playback rate. It is made of the two figures' tween delays, not of the global time scale: the caster's
  return and the target's recoil wait, the target trembles, and the spell's own effects play through. So the
  cancel path has no time scale and no timer to put back.

### The hold before the number

- **What**: the impact is seen first, then its result is read. The loud beats come at once and end early; the
  scene then goes quiet, so the eye can leave the body for the number.
- **Source**: [Thinking About Hitstop](https://sourcegaming.info/2015/11/11/thoughts-on-hitstop-sakurais-famitsu-column-vol-490-1/), Sakurai's Famitsu column, 2015: in the stop both sides
  "momentarily freeze, emphasizing the power of impact". No source gives a figure for the hold: the lengths are
  house rules.
- **Phaser**: one `Phaser.Time.Timeline` places the scene's own beats at their milliseconds
  (`Phaser.Types.Time.TimelineEventConfig.at`): the flash, the burst and the shake first, the slow fade after.
  The scene holds only what it draws.
- **Cost**: none.
- **Reduced motion**: the result is there at once, for `REDUCED_MS` (650 ms), as today.
- **Here**: the result text is the page's, not the scene's (`battle/src/result-card.ts`): it arrives with the
  impact frame and no effect delays it (ADR 0131, ADR 0141). So the hold is made on the scene's side: every
  bright beat starts within 120 ms of contact and is gone within 320 ms of it, fading from its first frame
  (*house*): the flash lasts 80 ms, the burst's particles fade as they fly, the trauma falls from its peak.
  Nothing bright starts after.

### Easing families

- **What**: nothing real starts or stops at once. Ease out for what arrives, ease in for what leaves, both for
  what moves between two rests. An ease takes 200 to 500 ms; a bounce or an elastic needs 800 to 1200 ms to
  settle, or it reads as aggressive.
- **Source**: [Easing Functions Cheat Sheet](https://easings.net/), Andrey Sitnik and Ivan Solovev;
  [Choosing the right easing](https://web.dev/articles/choosing-the-right-easing), Google web.dev;
  [Easing Equations by Robert Penner](https://doc.qt.io/qt/qtcore-attribution-easing.html), 2001.
- **Phaser**: `Phaser.Types.Tweens.TweenBuilderConfig.ease` takes a name or a function. The functions are under
  `Phaser.Math.Easing`: `Phaser.Math.Easing.Sine.InOut` for a breath, `Phaser.Math.Easing.Quadratic.Out` and
  `Phaser.Math.Easing.Cubic.Out` for arrivals, `Phaser.Math.Easing.Expo.Out` for a burst,
  `Phaser.Math.Easing.Back.Out` for an overshoot, `Phaser.Math.Easing.Elastic.Out` for a wobble.
- **Cost**: none.
- **Reduced motion**: nothing.
- **Here**: the view uses `Cubic.In`, `Cubic.Out`, `Quad.Out` (the short name of `Quadratic.Out`), `Sine.Out`
  and one `Back.Out`. An elastic settle
  only just fits the 840 ms windup, and does not fit a recoil: BC5 keeps it for the capstones.

### Stagger and overlap

- **What**: the parts of one effect do not move together. The main shape leads; the secondary ones follow a few
  frames later. The offset makes one event out of several objects.
- **Source**: [Twelve basic principles of animation](https://en.wikipedia.org/wiki/Twelve_basic_principles_of_animation),
  "Follow through and overlapping action"; [Juice It or Lose It](https://www.gdcvault.com/play/1016487/juice-it-or-lose),
  Martin Jonasson and Petri Purho, GDC Europe 2012.
- **Phaser**: `Phaser.Tweens.TweenManager#stagger` gives each target of one tween its own delay.
- **Cost**: none beyond the tweens.
- **Reduced motion**: nothing.
- **Here**: the three strokes of a slash and the five bones of a ward appear together today. The kit staggers
  the copies of a beat, one a target, by 40 ms (*house*) when the beat's window has room
  (`battle/src/spell-effects.ts`). The strokes of one motif still appear together.

## The body

### Squash and stretch

- **What**: a body stretches along its motion and squashes on contact, keeping its volume: what one axis gains
  the other loses. It gives weight to a figure that has one picture and no skeleton.
- **Source**: [Twelve basic principles of animation](https://en.wikipedia.org/wiki/Twelve_basic_principles_of_animation),
  "Squash and stretch"; [Juice It or Lose It](https://www.gdcvault.com/play/1016487/juice-it-or-lose), Jonasson
  and Purho, 2012.
- **Phaser**: tween `Phaser.GameObjects.Image#scaleX` and `Phaser.GameObjects.Image#scaleY` in opposition, about
  the feet: `Phaser.GameObjects.Image#setOrigin` at the ground anchor, so a squash does not lift the figure.
- **Cost**: none. A scale is part of the sprite's transform.
- **Reduced motion**: nothing.
- **Here**: built in BC5: the strike stretches 5, 6.5 or 8% (*house*) along the main axis of its travel over its
  last 120 ms, and lets go after the stop; a cast squashes 4% and stands 4% tall on the contact. The mutation cues follow the image's scale
  (`docs/art/mutation-cues.md`), so they squash with it. While an action moves a figure, its breath is stopped.

### Recoil

- **What**: the struck figure is pushed along the line of the attack and comes back. It says who was hit and how
  hard (*house*).
- **Source**: [Vlambeer co-founder shares advice on building better action games](https://www.gamedeveloper.com/design/vlambeer-co-founder-shares-advice-on-building-better-action-games), Game Developer, 2014, on Jan Willem Nijman's talk "The art of screenshake": thirty
  small tweaks, each making a dull shooter read better. The talk was not watched for this book, so the recoil
  stands as a house pattern.
- **Phaser**: one tween with `Phaser.Types.Tweens.TweenBuilderConfig.yoyo`, its direction from the caster to
  the target (`Phaser.Math.Angle.Between`); `Phaser.Types.Tweens.TweenBuilderConfig.hold` keeps the figure at the
  far end before it returns.
- **Cost**: one tween.
- **Reduced motion**: nothing.
- **Here**: built in `battle/src/body-motion.ts`: after the stop, a struck figure moves 12 px and 4°, 18 px and
  6° or 26 px and 8° by the health it lost, holds 0, 40 or 80 ms, and comes back, 280 ms each way, eased
  `Quad.Out`. A body's blow throws whoever it struck; from a distance, whoever lost health. A defeated figure
  does not come back ("Death").

### Hit flash

- **What**: the struck sprite turns one flat colour for a frame or two at contact. Its job is to say "this one
  took damage" faster than any number (*house*).
- **Source**: [Vlambeer co-founder shares advice on building better action games](https://www.gamedeveloper.com/design/vlambeer-co-founder-shares-advice-on-building-better-action-games), Game Developer, 2014, on the same thirty tweaks; and
  [Understanding SC 2.3.1](https://www.w3.org/WAI/WCAG22/Understanding/three-flashes-or-below-threshold.html),
  W3C, for what a flash may not do. No source gives a duration: ours is a house rule.
- **Phaser**: `Phaser.GameObjects.Image#setTint` with `Phaser.GameObjects.Image#setTintMode` at
  `Phaser.TintModes.FILL`, then `Phaser.GameObjects.Image#clearTint`, which also puts the mode back to
  `Phaser.TintModes.MULTIPLY`.
- **Cost**: none: a tint is a vertex attribute and does not break the batch.
- **Reduced motion**: nothing. A flash is exactly what the setting removes.
- **Here**: built in BC5, as the acting halo fills a sprite's shape (`battle/src/acting-halo.ts`): once per hit,
  80 ms (*house*), only on a figure that lost health and that the spell's own staging does not flash, in the palette's bone (`--bone`, `docs/art/branding.md`) and
  never in the target marker's gold. See "Flash limits" before using it on several figures at once.

### Death

- **What**: the last hit pays off and the body stays. A figure that vanishes leaves no history on the field; one
  that falls and remains does (*house*).
- **Source**: [The dark art of creating gorgeous death animations](https://www.pcgamer.com/the-dark-art-of-creating-gorgeous-death-animations/),
  PC Gamer: a death animation is "a splash of reward. You did something right!". No source describes a death
  for a single picture: the staging is ours.
- **Phaser**: a tween of `Phaser.GameObjects.Image#alpha` and scale about the feet, and a
  `Phaser.Filters.ColorMatrix` added with `Phaser.GameObjects.Components.FilterList#addColorMatrix`, whose
  `Phaser.Display.ColorMatrix#grayscale` drains the colour.
- **Cost**: the filter is a framebuffer and a pass for that figure, each frame, while it is on. A multiply tint
  costs nothing and comes first; the filter only if a film shows the tint does not read.
- **Reduced motion**: the settled frame: the figure at its defeated alpha, at once.
- **Here**: built in BC5, inside the impact window: the flash and the stop at full alpha, a recoil with no
  return, a hold, then the fall. The figure slumps and tips, sinks back onto its own spot, fades to 20% and drains
  toward the palette's cold grey (`--brand-line`) by a tint, with no filter: it fades as it drains, so a colour matrix would have little left to show
  on. It ends on the settled frame: at home, upright, untinted, at 20%.

### Breath at rest

- **What**: a still figure is never quite still. A moving hold, a slow rise and fall, keeps a long pose alive.
- **Source**: [Twelve basic principles of animation](https://en.wikipedia.org/wiki/Twelve_basic_principles_of_animation):
  a character sitting still can show "some sort of movement, such as breathing", so the picture does not go
  lifeless.
- **Phaser**: computed in `Phaser.Scene#update` from the scene's time, as the halos are, and applied to
  `Phaser.GameObjects.Image#scaleY`. Not a tween: the cancel path's `Phaser.Tweens.TweenManager#killAll` would
  kill it at the first skip.
- **Cost**: one sine per figure per frame.
- **Reduced motion**: off. The figure stands still.
- **Here**: removed once (`docs/art/battle-polish.md`); the owner asked for it back (question 4 of the plan), and
  BC5 built it: 1.5% of height over 3.6 s (*house*), each creature 0.37 of a cycle after the one before, stopped
  while a tween holds the figure, and never on a fallen one. It plays at the resting rate (one frame in four) and
  is not counted as movement. The mutation cues follow the image's scale, so they breathe
  with it.

## The camera

### Shake from trauma

- **What**: keep one value, trauma, between 0 and 1. A hit adds 0.2 to 0.5 to it; it falls back at a steady
  rate. The shake is trauma squared or cubed, so a small hit barely moves the view and a large one moves it much
  more: cubed, a trauma of 0.3, 0.6 and 0.9 gives 3%, 22% and 73% of the full shake. Offset is
  `maxOffset × shake × noise`, angle `maxAngle × shake × noise`. Smooth noise reads better than a random draw.
- **Source**: [Math for Game Programmers: Juicing Your Cameras With Math](https://gdcvault.com/play/1023146/Math-for-Game-Programmers-Juicing),
  Squirrel Eiserloh, GDC 2016 ([slides](https://media.gdcvault.com/gdc2016/Presentations/Eiserloh_Squirrel_JuicingYourCameras.pdf)).
- **Phaser**: `Phaser.Cameras.Scene2D.Camera#shake` draws a new random offset each frame at a fixed intensity:
  no decay, no rotation. Its callback receives the progress, and `Phaser.Cameras.Scene2D.Effects.Shake#intensity`
  can be lowered there. For the full pattern, set `Phaser.Cameras.Scene2D.Camera#setScroll` and
  `Phaser.Cameras.Scene2D.Camera#setRotation` each frame from `Phaser.Math.HashSimplex`.
- **Cost**: none to draw.
- **Reduced motion**: nothing. Camera motion is the first thing the setting removes.
- **Here**: built in BC5 (`battle/src/view-shake.ts`), in place of two fixed shakes. Each creature other than
  the caster that lost health adds 0.25, 0.40 or 0.55 by its loss, a defeat 0.10 more, to a ceiling of 0.6; the
  trauma falls by 0.25 every 120 ms. The offset is trauma squared times 5% of the view's shorter side (2.5% as BC5 built it; the owner asked for more, #910), on two
  axes, with no rotation: the page's labels sit over the canvas and would not turn with it. The noise is two
  sines an axis of the action's clock, not `Phaser.Math.HashSimplex`, whose configuration it did not need. A blood
  cost alone, a heal and a blow that took nothing do not move the view (`docs/art/spell-vfx.md`). The kit's
  `shake` beat adds its strength to the same value.

### Punch zoom

- **What**: the camera jumps a little closer on a heavy hit and eases back. With a hit stop it marks the one
  blow that matters. Super Smash Bros. Ultimate does it on a decisive hit: the background flashes, time slows
  down and the camera zooms in on the impact.
- **Source**: [Special Zoom](https://www.ssbwiki.com/Final_Zoom), SmashWiki.
- **Phaser**: `Phaser.Cameras.Scene2D.Camera#zoomTo` with an ease, or `Phaser.Cameras.Scene2D.Camera#setZoom`
  from a tween. `Phaser.Cameras.Scene2D.Camera#resetFX` does not reset a zoom: the cancel path must also call
  `Phaser.Cameras.Scene2D.Effects.Zoom#reset` on `Phaser.Cameras.Scene2D.Camera#zoomEffect` and set the zoom back
  to the fitted one, which is not 1 here: the view's camera zooms by the canvas's pixel ratio.
- **Cost**: none to draw. Everything on the camera moves, the labels and dice drawn in the scene included.
- **Reduced motion**: nothing.
- **Here**: built in BC5, on a defeat and on a critical hit only: 3% in, back over 200 ms (*house*), once an
  action. It is laid over the fitted zoom with `Phaser.Cameras.Scene2D.Camera#setZoom` from the scene's update
  (`battle/src/view-shake.ts`), beside the trauma, and not a zoom effect: the cancel path sets the fitted zoom back.

### Slow push on a capstone

- **What**: the camera moves in slowly while a great spell gathers, and releases at the impact. A slow push
  builds tension; it is the opposite of the punch.
- **Source**: [Special Zoom](https://www.ssbwiki.com/Final_Zoom), SmashWiki, for a zoom kept for the rare
  moment. No source of the craft was found for the slow form: its numbers are house rules.
- **Phaser**: `Phaser.Cameras.Scene2D.Camera#zoomTo` over the windup with `Phaser.Math.Easing.Sine.InOut`, and
  `Phaser.Cameras.Scene2D.Camera#pan` toward the caster.
- **Cost**: none to draw. The same reset as the punch zoom.
- **Reduced motion**: nothing.
- **Here**: not built. BC8 staged the Stage III spells without it: a staging has no beat for the camera's zoom.

## Particles

### Textures drawn at boot

- **What**: the kit's particles use a few small white textures made when the scene starts: a soft dot, a
  spark, a shard, a ring. Colour comes from a tint, so one texture serves every family, and nothing is downloaded.
- **Source**: [Phaser 4 Rendering Concepts](https://phaser.io/tutorials/phaser-4-rendering-concepts), Phaser
  Studio, on textures and batches.
- **Phaser**: `Phaser.GameObjects.Graphics#generateTexture` bakes a drawing, through the browser's canvas: a
  gradient fill does not come out. A soft dot is drawn on `Phaser.Textures.TextureManager#createCanvas` with the
  canvas's own radial gradient, then `Phaser.Textures.CanvasTexture#refresh`. Several shapes on one canvas, each
  a frame (`Phaser.Textures.Texture#add`), stay in one batch.
- **Cost**: no download. A 64 px square is 16 KiB of texture memory. On a phone a batch holds one texture, so
  the shapes go on one canvas, not one texture each.
- **Reduced motion**: the textures exist; nothing plays them.
- **Here**: every spell effect is a `Graphics` drawn once. The acting halo already makes its soft glow this
  way, on a canvas with a radial gradient (`battle/src/acting-halo.ts`): the kit draws its one texture the same way
  (`battle/src/fx/kit.ts`): `fx-kit`, four frames, a dot, a spark, a shard and a ring. A generated sprite sheet is for what these cannot give (question 3 of
  the plan).

### Burst

- **What**: many particles at once from one point, each on its own heading, fading as it goes. It is the
  impact's debris, and the main beat of most effects.
- **Source**: [Particle System](https://unpkg.com/phaser@4.2.1/skills/particles/SKILL.md), the notes Phaser
  4.2.1 ships; [Juice It or Lose It](https://www.gdcvault.com/play/1016487/juice-it-or-lose), Jonasson and Purho,
  2012.
- **Phaser**: one emitter made idle (`Phaser.Types.GameObjects.Particles.ParticleEmitterConfig.emitting` false),
  then `Phaser.GameObjects.Particles.ParticleEmitter#explode` with a count and a place. Shape it with
  `Phaser.Types.GameObjects.Particles.ParticleEmitterConfig.speed`, `.angle`, `.lifespan`, `.scale`, `.alpha` and
  `.gravityY`.
- **Cost**: one draw call for the emitter, whatever the count. One emitter per texture and blend mode, reused
  for every burst: not one per effect.
- **Reduced motion**: nothing.
- **Here**: when the host served no `visual` for an action, as in the battle lab's sample, `burst` in
  `battle/src/scene.ts` tweens a ring and 14 rectangles by hand; a mutation reveal uses the ring alone. The kit's
  burst (`battle/src/fx/burst.ts`) is 1 to 24 particles (*house*), 320 ms unless told, cleared with
  `Phaser.GameObjects.Particles.ParticleEmitter#killAll`. Each beat makes its own emitter, not one per texture:
  an emitter's settings move its living particles, so two beats that overlap cannot share one (ADR 0196). The
  sample's rectangles are still drawn by hand: BC5 rewrites that impact. BC6's first films showed the burst as
  BC4 built it could not be read: at a steady pace its pieces spent most of their life over the figure's own
  body, the colour of the motif. A piece now leaves fast and slows (cubic out), is clear of the body by the
  second frame, and falls a little; its size goes by the beat's `strength`, and a reach goes to 400 hundredths
  of the figure's radius, since 200 ends inside the silhouette (*house*).

### Flow

- **What**: a steady stream: a few particles every interval, for as long as something lasts. Embers above a
  fire, a channel between two figures, the air of the ground.
- **Source**: [Particle System](https://unpkg.com/phaser@4.2.1/skills/particles/SKILL.md), Phaser 4.2.1.
- **Phaser**: `Phaser.GameObjects.Particles.ParticleEmitter#flow`, or
  `Phaser.Types.GameObjects.Particles.ParticleEmitterConfig.frequency` and `.quantity`;
  `Phaser.Types.GameObjects.Particles.ParticleEmitterConfig.maxAliveParticles` caps it, and `.advance` starts it
  already full. `Phaser.GameObjects.Particles.ParticleEmitter#stop` ends the stream and lets the living finish.
- **Cost**: one draw call. The cost is the pixels the particles cover: see "A budget of pixels".
- **Reduced motion**: nothing.
- **Here**: the kit's `flow` (`battle/src/fx/flow.ts`), on a figure. BC9 uses the pattern for the ground.

### Trail

- **What**: particles left behind a moving thing. They stay where they were emitted, so they draw the path it
  took.
- **Source**: [Particle System](https://unpkg.com/phaser@4.2.1/skills/particles/SKILL.md), Phaser 4.2.1;
  [Anatomy of a League of Legends missile, part 1](https://realtimevfx.com/t/anatomy-of-a-league-of-legends-missile-part-1-creating-areas-of-focus/11614),
  Oli McDonald, Riot: the head carries the meaning, the trail is secondary.
- **Phaser**: `Phaser.GameObjects.Particles.ParticleEmitter#startFollow` on the projectile, with a short
  `Phaser.Types.GameObjects.Particles.ParticleEmitterConfig.lifespan` and a scale that falls to 0.
- **Cost**: one draw call. Keep the trail dimmer and smaller than the head.
- **Reduced motion**: nothing.
- **Here**: the kit's `trail` (`battle/src/fx/trail.ts`). Behind a projectile its head is the quill's own place
  on its arc (`flightAt`), so it leaves with the quill and never runs ahead of it; Paralyzing Barb has one, and
  Quill, a Stage I spell with no beat in its windup, flies bare. At the feet it is a crack: the head goes at an even pace
  and what it leaves stays for the whole beat, since a head that eased in crossed the field between two frames
  (#918, Groundbreaker).

### Gravity well and convergence

- **What**: particles pulled toward a point, or pushed from it. Drawn inward they read as gathering, a charge or
  a drain; pushed outward, as a release.
- **Source**: [Particle System](https://unpkg.com/phaser@4.2.1/skills/particles/SKILL.md), Phaser 4.2.1.
- **Phaser**: `Phaser.GameObjects.Particles.ParticleEmitter#createGravityWell`, its pull in
  `Phaser.Types.GameObjects.Particles.GravityWellConfig.power` (negative repels). To land each particle on a
  point at the end of its life, use `Phaser.Types.GameObjects.Particles.ParticleEmitterConfig.moveToX` and
  `.moveToY`.
- **Cost**: a little arithmetic per particle. One draw call.
- **Reduced motion**: nothing.
- **Here**: the windup draws a ring that grows away from the caster. The kit's `gather` (`battle/src/fx/gather.ts`) draws particles
  in from a ring instead, for a spell whose staging asks. BC8 uses it too, for a charge, a freeze that closes in, the void and the spirits among others; the blood cost is drops leaving the caster.
  BC6 uses it first: Focus draws six motes in, Carapace and Brace draw bone shards onto the plates, and the
  Stage III blows load their caster with it through the windup.

### Additive light

- **What**: drawn additively, a particle's colour is added to what is behind it. Overlaps sum toward white and
  read as light. On a bright ground the sum is already white and the shape is lost; on a dark one it glows.
  Premultiplied alpha blends as `c + (1 - a) × d`: where a texture's alpha is 0 it adds, where it is 1 it covers.
- **Source**: [Premultiplied alpha](https://iquilezles.org/articles/premultipliedalpha/), Inigo Quilez, for the
  blend's equation. That additive light is lost on a bright ground is arithmetic, not a citation.
- **Phaser**: `Phaser.Types.GameObjects.Particles.ParticleEmitterConfig.blendMode` at `Phaser.BlendModes.ADD`;
  `Phaser.BlendModes.SCREEN` sums more gently and does not blow out.
- **Cost**: a change of blend mode starts a new batch. Draw every additive object together.
- **Reduced motion**: nothing.
- **Here**: four kinds of additive object today: the capstone's halo and its sparks
  (`battle/src/capstone-effects.ts`), the condition's motes and its rings (`battle/src/condition-auras.ts`).
  The ground is basalt under a bright eclipse: additive light reads on the basalt and blows out against the
  disc and over a pale sprite, where `SCREEN` is used. The kit draws every lit object one depth above every plain one.

### A budget of pixels

- **What**: a phone's GPU runs out of pixels before it runs out of particles. Many blended particles on top of
  each other make it draw each pixel many times, and so do a few particles that are large on the screen. The
  count to keep low is the pixels covered, times the layers over them.
- **Source**: [Optimal Particle Rendering on PowerVR](https://docs.imgtec.com/performance-guides/graphics-recommendations/html/topics/optimal-particle-rendering-on-powervr.html),
  Imagination Technologies; [Phaser 4 Rendering Concepts](https://phaser.io/tutorials/phaser-4-rendering-concepts),
  Phaser Studio: "Filling pixels is expensive".
- **Phaser**: `Phaser.Types.GameObjects.Particles.ParticleEmitterConfig.maxAliveParticles` is the cap, and
  `Phaser.GameObjects.Particles.ParticleEmitter#getAliveParticleCount` is what a test reads.
- **Cost**: the particle cap is the cost. On the page's thread it is small: the bench's sample at the cap
  steps in 2.0 ms at the median with the processor slowed four times ([the acceptance record](battle-acceptance.md)).
  The pixels are not measured: no phone is attached to a session.
- **Reduced motion**: nothing.
- **Here**: 24 particles a beat and 150 alive on the field (*house*; BC10's measure gave no reason to move
  them): a beat gets only
  the room left under the 150, counted as what the living emitters may hold (`battle/src/fx/kit.ts`).

## Light and surface

A filter is Phaser 4's name for what Phaser 3 called an FX, and it replaces masks too. A game object must opt in
(`Phaser.GameObjects.Image#enableFilters`) before it has a `filters` list; a camera has one from the start. The
list has two halves: internal filters cover the object and are cheaper, external ones cover the whole drawing
area (the context), usually the screen. A `Graphics` has no size, so its internal filters cover the context too:
bake the drawing to a texture first, or filter a container.

### Glow

- **What**: a soft halo around a shape. It says energy, and it lifts an effect off the ground.
- **Source**: [Phaser 4 Filters and Post-FX](https://unpkg.com/phaser@4.2.1/skills/filters-and-postfx/SKILL.md),
  the notes Phaser 4.2.1 ships.
- **Phaser**: `Phaser.GameObjects.Components.FilterList#addGlow`; tween `Phaser.Filters.Glow#outerStrength` for
  a pulse. The quality and the distance are fixed when the filter is made.
- **Cost**: a framebuffer and a pass, each frame, for each glowing object. The cheap form costs little: a
  soft-dot sprite, tinted, additive, drawn behind the shape.
- **Reduced motion**: nothing. The settled frame holds no effect, a still one included.
- **Here**: the capstone halo is the cheap form already (`battle/src/capstone-effects.ts`). The kit's `glow` is the
  cheap form, and its `pulse` is the filter, for one hero effect in each family. The kit calls
  `Phaser.Filters.Controller#setPaddingOverride` with null so the filter has room around its object: without
  that call the halo is cut at the picture's edge. Under a renderer with no graphics card a frame with the
  pulse takes about 300 ms where a frame without one holds its 16.7 ms, and three pulses about 830 ms (*house*,
  [the acceptance record](battle-acceptance.md)): the page's thread pays nothing, the renderer all of it. So a
  pulse stays on one figure, and what it costs a phone's graphics card is the physical gate's to say.
  Since BC6 a `glow` with the shape of a ring is a shock ring: one image of a thin ring, flattened onto the
  ground under the figure, that opens to its reach over the whole beat and fades (*house*). Two of them 160 ms
  apart read as an aftershock. It is the strongest single beat the kit has, kept for Stage II and Stage III.
  A pulse on the acting figure was drowned out by the gold rim the scene draws around it: while one plays, the rim is
  quieted to a fifth (*house*) and given back when the beat ends or is cleared (#919).

### Blur

- **What**: softens what is behind or beside the subject, or smears a fast thing along its motion.
- **Source**: [Phaser 4 Filters and Post-FX](https://unpkg.com/phaser@4.2.1/skills/filters-and-postfx/SKILL.md),
  Phaser 4.2.1.
- **Phaser**: `Phaser.GameObjects.Components.FilterList#addBlur`, its amount in `Phaser.Filters.Blur#strength`.
- **Cost**: the most expensive common filter: several samples per pixel. On the camera it is the whole screen.
  A smear is cheaper as a stretched sprite.
- **Reduced motion**: nothing.
- **Here**: not used. BC9 did not blur the ground. BC10 says nothing about it: a full-screen filter costs the
  renderer, and a session cannot measure that for a phone.

### Displacement

- **What**: pixels are moved by a second picture: heat above a fire, a ring that bends the ground as a shock
  passes.
- **Source**: [Phaser 4 Filters and Post-FX](https://unpkg.com/phaser@4.2.1/skills/filters-and-postfx/SKILL.md),
  Phaser 4.2.1.
- **Phaser**: `Phaser.GameObjects.Components.FilterList#addDisplacement` with a texture key; the strength is
  `Phaser.Filters.Displacement#x` and `Phaser.Filters.Displacement#y`, very small numbers (0.005).
  `Phaser.GameObjects.Components.FilterList#addBarrel` is a simpler bulge, with no texture.
- **Cost**: a full pass over what it covers. On the camera, the screen.
- **Reduced motion**: nothing.
- **Here**: not used. A candidate for one hero effect: the ground wave of `tornado`, or the void aperture of
  `toxic_waves`.

### Colour matrix

- **What**: one matrix changes every colour: drain the saturation, darken, shift the hue. On the whole view it
  sets a mood for a moment; on one figure it marks a state.
- **Source**: [Phaser 4 Filters and Post-FX](https://unpkg.com/phaser@4.2.1/skills/filters-and-postfx/SKILL.md),
  Phaser 4.2.1.
- **Phaser**: `Phaser.GameObjects.Components.FilterList#addColorMatrix` returns a `Phaser.Filters.ColorMatrix`;
  its methods moved to `Phaser.Filters.ColorMatrix#colorMatrix` in Phaser 4:
  `Phaser.Display.ColorMatrix#saturate`, `Phaser.Display.ColorMatrix#brightness`,
  `Phaser.Display.ColorMatrix#reset`.
- **Cost**: one pass. Cheap per pixel, but on the camera it forces the whole scene through a framebuffer.
- **Reduced motion**: nothing during an action. The settled frame of a defeat keeps today's dimmed figure.
- **Here**: not used: the ground and the plates are tinted (`battle/src/battle-horizon.ts`). A tint is free, so
  it comes first; the matrix is for what a tint cannot do, the drained colour of a death.

### Bloom

- **What**: the bright parts of the picture bleed light around them.
- **Source**: [Phaser 4 Filters and Post-FX](https://unpkg.com/phaser@4.2.1/skills/filters-and-postfx/SKILL.md),
  Phaser 4.2.1: there is no Bloom filter in Phaser 4.
- **Phaser**: `Phaser.Actions.AddEffectBloom` builds it. By hand:
  `Phaser.GameObjects.Components.FilterList#addParallelFilters`, with a threshold and a blur on
  `Phaser.Filters.ParallelFilters#top`, blended back additively.
- **Cost**: three passes or more. The heaviest pattern in the book.
- **Reduced motion**: nothing.
- **Here**: not used, and not planned for a phone. Additive particles on the dark ground already read as light.

### One hero shader

- **What**: a fragment shader draws one effect no shape or particle can: a corona, a rift, a flowing surface.
  Layered noise gives it life: each octave doubles the frequency and halves the amplitude, and one noise bending
  another gives flame and smoke.
- **Source**: [Phaser 4 Shader Guide](https://phaser.io/tutorials/phaser-4-shader-guide), Phaser Studio;
  [Fractal Brownian Motion](https://thebookofshaders.com/13/), The Book of Shaders;
  [Domain warping](https://iquilezles.org/articles/warp/), Inigo Quilez.
- **Phaser**: `Phaser.GameObjects.GameObjectFactory#shader` with a `Phaser.Types.GameObjects.Shader.ShaderQuadConfig`:
  the code in `.fragmentSource`, the values in `.setupUniforms` or through `Phaser.GameObjects.Shader#setUniform`.
  Phaser does not pass the time to the shader; you set it. For no code at all, the engine ships two shaders:
  `Phaser.GameObjects.NoiseSimplex2D` and `Phaser.GameObjects.Gradient`.
- **Cost**: one draw call each, and it breaks the batch. Its price is the pixels of its quad: keep the quad small.
- **Reduced motion**: nothing during an action. A shader that is part of the ground shows one still frame.
- **Here**: not used. The plan allows one for a hero effect. Candidates: the eclipse's corona (BC9), and the
  void, which BC8 staged from the kit's beats and left without a dark of its own.

## Projectiles

### Arcing travel

- **What**: a thrown thing rises and falls. A curve from caster to target, with one control point above the
  middle, reads as weight; a straight line reads as a beam.
- **Source**: [Curves and Paths](https://unpkg.com/phaser@4.2.1/skills/curves-and-paths/SKILL.md), the notes
  Phaser 4.2.1 ships: a quadratic Bézier has a start, one control point and an end. That an arc reads as weight
  is a house rule.
- **Phaser**: `Phaser.Curves.QuadraticBezier`; drive a value from 0 to 1 with
  `Phaser.Tweens.TweenManager#addCounter`, place the projectile with `Phaser.Curves.QuadraticBezier#getPoint` and
  turn it with `Phaser.Curves.QuadraticBezier#getTangent`. `Phaser.Curves.Curve#getPointAt` gives an even speed
  along the curve.
- **Cost**: one counter tween.
- **Reduced motion**: nothing. No projectile is launched, as today.
- **Here**: since BC7 every projectile flies `flightAt` (`table/battle-motion.js`). It leaves just over the
  thrower's head with the toss, at 240 ms, and lands on the contact 600 ms later. The curve's control point
  stands above the middle by 16% of the flight's length (*house*), and the flight covers an even length of the
  curve a moment: the curve's own parameter slowed a quill thrown up the field into its hit. The lift is up the
  screen, so a throw across the field bows and one straight up or down it keeps to its line. The curve is
  written by hand: the effects kit holds no engine, and its trail follows the same curve. What the projectile
  knocks off its target goes on down the end of the arc. Eased in, the quill stood on the thrower's body for
  300 ms after the throw (#909).

### Ribbon

- **What**: a strip that follows the head and thins to nothing: the path drawn as one continuous shape, where
  particles would draw it in dots.
- **Source**: [Anatomy of a League of Legends missile, part 1](https://realtimevfx.com/t/anatomy-of-a-league-of-legends-missile-part-1-creating-areas-of-focus/11614),
  Oli McDonald, Riot.
- **Phaser**: `Phaser.GameObjects.Rope`: `Phaser.GameObjects.Rope#setPoints` with the last positions of the
  head, and `Phaser.GameObjects.Rope#setAlphas` falling to 0 at the tail. Or redraw
  `Phaser.GameObjects.Graphics#strokePoints` each frame.
- **Cost**: a rope has its own shader, so it starts a new batch. A `Graphics` redrawn each frame batches with
  the other vectors, and rebuilds its path each frame.
- **Reduced motion**: nothing.
- **Here**: not built. The kit's trail is particles. The first spell that needs a ribbon chooses one of the
  two forms on the bench, by measure.

### Chain between targets

- **What**: a jagged line from one target to the next. Split each segment at its middle and move the middle
  sideways; halve the move at each pass. Draw two bolts and move their points every frame, so it flickers. A
  glow under the bright line gives it a core.
- **Source**: [Lightning Bolts](https://www.drilian.com/posts/2009.02.25-lightning-bolts/), Josh Jersild, 2009.
- **Phaser**: `Phaser.GameObjects.Graphics#strokePoints` on points built with `Phaser.Math.Vector2`; the offsets
  come from a `Phaser.Math.RandomDataGenerator` with a seed, so a film strip repeats.
- **Cost**: one `Graphics`. A redraw is a new path, not a new object.
- **Reduced motion**: nothing. A flickering bolt is a flash: see "Flash limits".
- **Here**: `lightning_bolt` draws one fixed nine-point arc from the caster to each recipient. BC8 staged Shock
  around that arc and did not redraw it: the subdivision and the flicker are not built.

### An impact that keeps the direction

- **What**: what flies off an impact goes on the way the blow was going. Debris in a cone along the line of
  travel says where the hit came from; a round burst says nothing.
- **Source**: [Vlambeer co-founder shares advice on building better action games](https://www.gamedeveloper.com/design/vlambeer-co-founder-shares-advice-on-building-better-action-games), Game Developer, 2014: one of Nijman's tweaks shakes the screen
  "slightly in the opposite direction of the player's gunfire". An effect that carries the direction of the blow
  is the same idea, applied to debris.
- **Phaser**: `Phaser.Math.Angle.Between` the caster and the target gives the heading in radians. The emitter's
  `Phaser.Types.GameObjects.Particles.ParticleEmitterConfig.angle` is in degrees: convert with
  `Phaser.Math.RadToDeg`, then give a range around it.
- **Cost**: none beyond the burst.
- **Reduced motion**: nothing.
- **Here**: the recoil already follows that line. The kit's burst takes a direction (`along`, `back`, `up`,
  `out`) and a spread, and each particle is turned to its heading. Since BC6 the strokes of the `impact` motif
  run along the line of the blow too, each over a dark edge, for every spell that draws it; the `slash` strokes
  cut 60° across it (*house*), so a claw and a hammer differ when the blow is vertical (#919).

## The ground

### Parallax

- **What**: far things move less than near things. Layers that shift by different amounts when the view moves
  give one flat painting depth. Godot's own example runs from 0.1 for the sky and 0.2 and 0.3 for the clouds to
  0.5 for the hills and 0.7 for the forest.
- **Source**: [2D Parallax](https://docs.godotengine.org/en/stable/tutorials/2d/2d_parallax.html), the Godot
  documentation.
- **Phaser**: `Phaser.GameObjects.Image#setScrollFactor` on each plate: the camera's shake, punch and push then
  move the near plates more than the far ones, with no code each frame.
- **Cost**: none.
- **Reduced motion**: nothing moves, so nothing shifts.
- **Here**: built by BC9, by hand and not with a scroll factor: the fitted camera has a zoom and a scroll that is
  not zero, so a scroll factor would displace a plate for good. Each far layer is laid again while the view moves,
  shifted against the view's offset and scaled against its punch (`depthPlace` in `table/battle-ground.js`,
  `battle/src/ground.ts`): the sky's plates at 0.5, the fog at 0.75, the far embers at 0.8 (*house*). The painting
  carries the floor and stays at 1. The camera moves a few pixels for under 300 ms, so the depth is felt and not
  seen ([the ground's storyboard](storyboards/ground.md)).

### Fog and embers

- **What**: slow particles in the air say the place is alive between two actions. Few, dim, and behind the
  figures.
- **Source**: [Particle System](https://unpkg.com/phaser@4.2.1/skills/particles/SKILL.md), Phaser 4.2.1, for a
  flow that starts full; [Optimal Particle Rendering on PowerVR](https://docs.imgtec.com/performance-guides/graphics-recommendations/html/topics/optimal-particle-rendering-on-powervr.html),
  Imagination Technologies, for what a wide blended particle costs.
- **Phaser**: one flowing emitter, full from the first frame
  (`Phaser.Types.GameObjects.Particles.ParticleEmitterConfig.advance`), spread by
  `Phaser.Types.GameObjects.Particles.ParticleEmitterConfig.emitZone`. A band of fog is a
  `Phaser.GameObjects.TileSprite` whose `Phaser.GameObjects.TileSprite#tilePositionX` drifts. On
  `Phaser.Core.Events.HIDDEN` the emitter is paused.
- **Cost**: one draw call for the embers, one for the fog. A band of fog covers many pixels: keep it to one.
- **Reduced motion**: nothing. The motes are hidden, as today.
- **Here**: built by BC9 with no emitter. An emitter draws from `Math.random`, which the bench pins only for the
  length of a play: a film of the field at rest would not repeat (rule 5). So the 24 embers are images of one
  texture whose places are a function of the scene's ambient clock (`emberAt` in `table/battle-ground.js`), and
  the fog is one `Phaser.GameObjects.TileSprite` drifting on the same clock, in a slate taken from the painting.
  The embers are the faults' ember orange, behind the figures: the motes' gold was the target marker's. A hidden
  tab stops them by stopping that clock, and with less motion the fog stands still and no ember is drawn
  ([the ground's storyboard](storyboards/ground.md)).

### A light that answers a cast

- **What**: the ground takes the colour of the spell for a moment. The place reacts, so the spell feels as if it
  happened there and not on top of a picture.
- **Source**: [Special Zoom](https://www.ssbwiki.com/Final_Zoom), SmashWiki: the background flashes on the
  decisive hit; [Understanding SC 2.3.1](https://www.w3.org/WAI/WCAG22/Understanding/three-flashes-or-below-threshold.html),
  W3C, for its limit.
- **Phaser**: a `Phaser.GameObjects.PointLight` at the cast: a light with no normal map, built for flashes, with
  `Phaser.GameObjects.PointLight#intensity` and `Phaser.GameObjects.PointLight#radius` to tween. For the whole
  ground, tween the tint of the painting and the plates through `Phaser.Display.Color.Interpolate.ColorWithColor`.
- **Cost**: a point light is one small draw call; a tint is free. `Phaser.Cameras.Scene2D.Camera#flash` covers
  the whole view in one colour, which is what the flash limits forbid at full white.
- **Reduced motion**: nothing.
- **Here**: built by BC9 (`battle/src/ground.ts`, [the ground's storyboard](storyboards/ground.md)): the eclipse's
  corona brightens in the family's colour and the painting, the plates and the arch go part of the way to it,
  once: a rise of 100 ms and a fall of 200 ms (*house*), gone within 320 ms of the contact as every bright beat
  is. Its strength is the health the host confirmed lost by the others (`lightOf` in `table/battle-ground.js`):
  nothing to 3, so a base action leaves the ground as it rests, half from 4, all from 7 or on a defeat. The same
  light pools on the ground under each figure harmed, behind every figure (the owner's choice, #953). A heal, a
  ward and a fizzle get nothing. The corona is a soft ring drawn at boot, additive, around a disc left dark: a
  `Phaser.GameObjects.PointLight` on the disc was filmed first, and read as a second sun.

### A scar that fades

- **What**: a mark left where the blow landed, which fades. For a few seconds the ground remembers
  (*house*).
- **Source**: [Phaser 4 Rendering Concepts](https://phaser.io/tutorials/phaser-4-rendering-concepts), Phaser
  Studio, for what a render texture costs. No source read here describes the pattern or gives a duration.
- **Phaser**: one image under the figure, blended with `Phaser.BlendModes.MULTIPLY`, its alpha tweened to 0. For
  many marks, stamp them into a `Phaser.GameObjects.RenderTexture` with `Phaser.GameObjects.RenderTexture#stamp`,
  then `Phaser.GameObjects.RenderTexture#render`: in Phaser 4 nothing is drawn until `render` is called.
- **Cost**: an image is free. A render texture is a framebuffer.
- **Reduced motion**: nothing: the settled frame has no scar.
- **Here**: not built: BC9 staged the ground's air, its depth and its light, and no scar. A scar is decoration,
  never state: a replay and a first load show none. Whoever builds it fades it inside the settle window, so no
  frame the player can navigate to holds one.

### A beat on the ground

- **What**: a beat says where it starts on its figure: at the feet, at the centre or at the head. What starts at
  the feet lies on the ground: its travel toward or away from the eye is shortened, so dust from a stamp stays low
  and a blow struck up the field throws its debris along the ground, not up the screen. A shock ring lies there
  too, and a ring aimed at its targets opens as an arc on their side only, so it does not sweep through a
  neighbour. A trail at the feet of both figures is a crack running from the caster to its target.
- **Source**: [Particle System](https://unpkg.com/phaser@4.2.1/skills/particles/SKILL.md), Phaser 4.2.1, for an
  emitter's zone and its place. The ground's slant, 0.42 of a distance across it (*house*), is the one the `wave`
  and `roar` motifs already draw their rings with.
- **Phaser**: the emitter placed at the level's point, its pieces' vertical travel multiplied by the slant, and a
  zone flattened the same way through `Phaser.Types.GameObjects.Particles.ParticleEmitterConfig.emitZone`. The
  aimed ring is one `Phaser.GameObjects.Graphics` whose arc is drawn again on each step with
  `Phaser.GameObjects.Graphics#clear` and `Phaser.GameObjects.Graphics#strokePoints`, from
  `Phaser.Types.Tweens.TweenBuilderConfig.onUpdate`; its heading is `Phaser.Math.Angle.Between` the two figures,
  turned onto the ground.
- **Cost**: none for a level. An aimed ring is one `Graphics` of about 20 points, drawn again each frame of its
  beat, in the vectors' batch.
- **Reduced motion**: nothing.
- **Here**: the kit's `level` (`feet`, `centre`, `head`), read by `table/battle-staging.js` and checked by
  `SpellArt`; the scene gives each figure its feet and head besides its centre. The ring of a `glow` lies at the
  feet whatever its level; with a `direction` of `along` or `back` it is an arc, 120° unless its `spread` says
  (*house*). Along and back, a burst's piece is shortened at every level, since it follows a blow struck across
  the ground. The physical spells use it first: Pummel's and Body Slam's dust, Crushing Stomp's crown, Alpha
  Roar's rings and Groundbreaker's ring and crack.

## Reading

### One effect, one meaning

- **What**: the same shape always means the same thing. What an effect weighs to the eye should match what it
  weighs in the game. The part of an effect that carries the game's message is its focus; the rest carries the
  theme, and is kept lower in value and opacity (*house*).
- **Source**: [Clarity in League](https://www.leagueoflegends.com/en-us/news/dev/clarity-in-league/), Riot
  Games: "the amount of attention an ability grabs should match its level of importance", and visual noise adds
  up quickly; [Anatomy of a League of Legends missile, part 1](https://realtimevfx.com/t/anatomy-of-a-league-of-legends-missile-part-1-creating-areas-of-focus/11614),
  Oli McDonald, Riot, 2020: the area of focus "should be the element that is communicating gameplay".
- **Phaser**: the primary element is drawn last and highest (`Phaser.GameObjects.Graphics#setDepth`), and each
  object carries its motif, creature and phase (`Phaser.GameObjects.GameObject#setData`), which the browser specs
  read.
- **Cost**: none.
- **Reduced motion**: the meaning stays in the result text and the frame after.
- **Here**: 16 motifs for 44 spells, each family with its shapes (`docs/art/spell-vfx.md`). BC6 to BC8 give each
  spell its own staging; the motif stays the primary element, so a spell is still read as its family first.

### Shape before colour

- **What**: no information rests on colour alone. Shape, pattern or text says it first; colour confirms it.
- **Source**: [Ensure no essential information is conveyed by a fixed colour alone](https://gameaccessibilityguidelines.com/ensure-no-essential-information-is-conveyed-by-a-fixed-colour-alone/),
  Game Accessibility Guidelines; [Xbox Accessibility Guideline 103](https://learn.microsoft.com/en-gb/gaming/accessibility/xbox-accessibility-guidelines/103),
  Microsoft.
- **Phaser**: the white textures of the kit take their colour from `Phaser.GameObjects.Image#setTint`: the shape
  is in the texture, the family in the tint.
- **Cost**: none.
- **Reduced motion**: unchanged.
- **Here**: held today: "Shapes differ as well as colors. Warm gold remains reserved for the target/active
  marker" (`docs/art/spell-vfx.md`). A staging that tells two spells apart by hue alone is refused.

### The three parts of an effect

- **What**: an effect has an anticipation, a main beat and a dissipation: the animator's anticipation,
  action and follow-through, applied to an effect (*house*). The main beat is the brightest and the shortest. The
  end falls in value, saturation and opacity, and never competes with the next action (*house*).
- **Source**: [Twelve basic principles of animation](https://en.wikipedia.org/wiki/Twelve_basic_principles_of_animation),
  for anticipation and follow-through; [Visual Effects Bootcamp: Artistic Principles of VFX](https://www.gdcvault.com/play/1024439/Visual-Effects-Bootcamp-Artistic-Principles),
  Hadidjah Chamberlin and Jason Keyser, GDC 2017: artistic fundamentals of VFX, with cases from League of
  Legends.
- **Phaser**: one `Phaser.Time.Timeline` per effect places the three parts on the game's clock
  (`Phaser.Time.Timeline#add`), and one `Phaser.Time.Timeline#timeScale` follows the playback rate.
- **Cost**: none.
- **Reduced motion**: nothing.
- **Here**: the three parts are the game's three windows: the windup's charge, the impact's motif (100 ms to
  arrive, the rest to fade), the settle. A `staging` is a list of beats placed in the first two (ADR 0196).

### Flash limits

- **What**: nothing flashes more than three times in any one second. A flash is a pair of opposing changes in
  brightness. Small flashes pass: under a quarter of a 10° field of view, about 341 by 256 px on a 1024 by 768
  screen. A saturated red flash is judged more strictly.
- **Source**: [Understanding SC 2.3.1: Three Flashes or Below Threshold](https://www.w3.org/WAI/WCAG22/Understanding/three-flashes-or-below-threshold.html),
  W3C; [Xbox Accessibility Guideline 118: Photosensitivity](https://learn.microsoft.com/en-us/gaming/accessibility/xbox-accessibility-guidelines/118),
  Microsoft; [Avoid flickering images and repetitive patterns](https://gameaccessibilityguidelines.com/avoid-flickering-images-and-repetitive-patterns/),
  Game Accessibility Guidelines.
- **Phaser**: `Phaser.Cameras.Scene2D.Camera#flash` fills the whole view: never at full white, and never twice in
  a second. A hit flash (`Phaser.TintModes.FILL`) covers one figure.
- **Cost**: none.
- **Reduced motion**: no flash at all. The limit holds with the setting off, for everyone.
- **Here**: at 2× an action lasts about 1.5 s, and one spell can hit three figures. A staging holds at most
  one `flash` and one `shake`; the builder refuses a second. That is one flash per figure per action.
  All 44 stagings hold it (`studio/battle-frametime.test.js`).

### Reduced motion

- **What**: motion started by an interaction can be turned off unless it is essential. Scaling, panning and
  camera movement are what make people ill; a change of colour or opacity mostly is not.
- **Source**: [Understanding SC 2.3.3: Animation from Interactions](https://www.w3.org/WAI/WCAG21/Understanding/animation-from-interactions),
  W3C; [prefers-reduced-motion](https://developer.mozilla.org/en-US/docs/Web/CSS/@media/prefers-reduced-motion),
  MDN; [Xbox Accessibility Guideline 117](https://learn.microsoft.com/en-us/gaming/accessibility/xbox-accessibility-guidelines/117),
  Microsoft, on camera shake.
- **Phaser**: none is needed, and that is the point: under the setting the scene starts no tween
  (`Phaser.Tweens.TweenManager#add`) and no emitter. A kit primitive returns before it creates anything.
- **Cost**: none.
- **Reduced motion**: this entry is the rule.
- **Here**: the page reads the system setting and its own checkbox (`battle/src/live.ts`); the scene then shows
  the frame after the action for 650 ms, with no vector, projectile, shake or displacement. Every primitive of
  the kit has a test that it plays nothing (`table/battle-fx.test.js`, `studio/browser/battle-fx.spec.js`).

## Phaser 3 names that are gone

Most examples a search returns are for Phaser 3. These names are not in the installed types; the test fails if
one comes back. The left column is the Phaser 3 name, the right what Phaser 4.2.1 has.

| Phaser 3 | Phaser 4.2.1 |
| --- | --- |
| `Phaser.GameObjects.Image#preFX`, `Phaser.GameObjects.Image#postFX` | Filters: `Phaser.GameObjects.Image#enableFilters`, then `Phaser.GameObjects.Image#filters`. |
| `Phaser.FX.Glow`, and the rest of `Phaser.FX` | `Phaser.Filters.Glow`, and the rest of `Phaser.Filters`. |
| `Phaser.FX.Bloom` | `Phaser.Actions.AddEffectBloom`, or parallel filters. |
| `Phaser.Display.Masks.BitmapMask` | A mask is a filter: `Phaser.GameObjects.Components.FilterList#addMask`. |
| `Phaser.GameObjects.Particles.ParticleEmitterManager` | The emitter is the game object: `Phaser.GameObjects.GameObjectFactory#particles` returns it. |
| `Phaser.Geom.Point` | `Phaser.Math.Vector2`. |
| `Phaser.Math.PI2` | `Phaser.Math.TAU`, which is now 2π; it was π/2 in Phaser 3. |
| `Phaser.GameObjects.Mesh` | `Phaser.GameObjects.Mesh2D`, made by `Phaser.GameObjects.GameObjectFactory#mesh2d`: a flat textured mesh. |
| `Phaser.GameObjects.Plane` | Removed. |
| `Phaser.Textures.TextureManager#generate` | `Phaser.GameObjects.Graphics#generateTexture`, or a canvas texture. |

## Declared, and dead

The types cannot catch these. They compile, and do the wrong thing.

| Name | What happens |
| --- | --- |
| `Phaser.GameObjects.Image#setTintFill` | Still declared. It does nothing but log an error. Use `setTint` and `setTintMode` with `Phaser.TintModes.FILL`. |
| `Phaser.GameObjects.Image#setMask` | Canvas only. Under WebGL a mask is a filter. |
| `Phaser.Textures.DynamicTexture#draw` | Draws nothing until `Phaser.Textures.DynamicTexture#render` is called. In Phaser 3 it drew at once. |
| `Phaser.GameObjects.GameObjectFactory#noisesimplex2d` | The name is lower case. The notes the package ships write it in camel case, which does not exist. |
| `Phaser.Types.GameObjects.Particles.ParticleEmitterConfig.angle` | In degrees, where `Phaser.Math.Angle.Between`, a curve's tangent and a rotation are in radians. A radian given to an emitter sends every particle to the right. |
| A shader's texture coordinates | `y` is 0 at the bottom in Phaser 4. A Phaser 3 or Shadertoy shader comes out upside down. |

## How the book is kept

- `studio/battle-craft.test.js` reads this file in the gate. Every name in backticks that starts with `Phaser.`,
  and every key continued from one, exists in `battle/node_modules/phaser/types`; every name in the left column
  of "Phaser 3 names that are gone" does not; every entry has its six lines, a linked source and an engine name.
  So an entry that cannot name a real API is not in the book.
- The test reads a name from the first character of its backticks. It refuses a span that hides one further in,
  or that reaches the engine through an instance: write the class and the member. It does not check the right
  column of the two tables of traps, nor what a sentence says a name does: a reader checks those.
- To check a name before writing it:

  ```bash
  node scripts/battle/phaser-symbols.mjs "Phaser.Cameras.Scene2D.Camera#shake"
  ```
- A change to this file runs the full build in CI, not the light one: the light build installs no Phaser
  (`studio/browser/plan.js`).
- When Phaser is upgraded, the test says which names moved. Fix the entry, then read "Declared, and dead" again
  against the new version: the test cannot.
- Every link was opened and read against the entry that cites it, on 2026-11-18. What a page did not say was
  taken out of its entry, or kept and marked *house*. No video is cited: a talk is cited through a page that
  describes it, and for what that page says. The Phaser facts are first hand, read in the installed package and
  its source.
- Sources looked for and left out, because they could not be read: Riot's VFX style guide (gone from its
  address), Animation Mentor on the moving hold, Envato Tuts+ on 2D lightning, Arm on fog, and the talks of
  Sakurai, Nijman and Holmér as videos. An entry that would lean on one says *house* where it does.
- A storyboard names a pattern by a link to its heading here, and `studio/storyboards.test.js` follows the link:
  renaming an entry means renaming it in the storyboards that use it.
- A new pattern is added with all six lines, under the group it belongs to. A number with no source is marked
  *house*.
