# The battle's craft book

The patterns the battle view stages its spells, attacks, moves and ground with: what each one is, where it comes
from, the Phaser API that does it, what it costs, what it becomes under reduced motion, and where this game uses
it. It is BC1 of [the battle's craft plan](battle-craft-plan.md). The `battle-designer` agent (BC3) writes
storyboards in these words, and the session that builds a stage reads the entry before it writes a tween.

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

A number marked *house* is ours: no source gives it, and a film strip (BC2) tunes it. A number marked *to check*
was read in a search excerpt and not on the page (see "How the book is kept").

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
   `Math.random`, not from `Phaser.Math.RND`, so BC2 pins `Math.random` in the bench.
6. The budgets hold (`battle/budget.mjs`, ADR 0136). See what is measured below.
7. Sound is another matter (ADR 0134). No entry here plays one.

## Measured on the installed engine

Measured in this session on `main` at `49efe5d`, the bundle built with `npm --prefix battle run build`.

| Fact | Measure | What it means for a pattern |
| --- | --- | --- |
| The whole engine is in the bundle | `dist/phaser.esm.js` is 1,388,640 of the 1,711,869 bytes of `live.js`. The bundle already holds the particle emitter, every filter, the shader quad, the point light and the rope. | A pattern that uses an engine feature adds its calling code, and no engine bytes. |
| What batches | Images, sprites, text, particles and render textures share one batch family; `Graphics` and shapes another; a point light a third (the engine's rendering guide). | Many particles from one emitter are one draw call. Forty `Graphics` objects are not forty: they batch too, until something breaks the batch. |
| What breaks a batch | A filter, a change of blend mode, a change of lighting, another kind of object (a `Phaser.GameObjects.Shader`, a `Phaser.GameObjects.Rope`). | Draw every additive object together, after the normal ones, and not interleaved. |
| One texture a batch on a phone | `Phaser.Types.Core.RenderConfig.autoMobileTextures` is on by default, and the view does not turn it off: on iOS and Android a batch holds one texture. | Each creature's picture is already its own draw call there. The kit's shapes share one texture, or each costs one more. |
| What a filter costs | The object is drawn to a framebuffer, then one pass per active filter. | A filter is a draw call or more, each frame, for as long as it is active. Remove it when the effect ends. |
| What the engine warns about | "Draw calls are expensive. Don't use more than a few hundred." "Filling pixels is expensive." | On a phone the limit is pixels filled, before objects counted. |
| Frame time on a phone | Not measured. No device is attached to a session. | BC10 measures every staging under the browser's CPU throttle. The physical gate stays with ADR 0136. |

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
- **Here**: the caster draws back 14 px and leans 7° over `DRAW_MS` (240 ms) in `battle/src/scene.ts`, melee only.
  BC5 scales the draw back to the weight of the strike and gives a cast its own: a gather, with no lunge.

### Contact and follow-through

- **What**: the strike accelerates into the contact frame, the one frame the eye keeps. What trails the body
  arrives after it and settles past its rest: follow-through and overlapping action.
- **Source**: [Twelve basic principles of animation](https://en.wikipedia.org/wiki/Twelve_basic_principles_of_animation),
  "Follow through and overlapping action" and "Slow in and slow out".
- **Phaser**: the strike eases in (`Phaser.Math.Easing.Cubic.In`); the settle overshoots with
  `Phaser.Math.Easing.Back.Out`, its overshoot set by `Phaser.Types.Tweens.TweenBuilderConfig.easeParams`.
- **Cost**: tweens only.
- **Reduced motion**: nothing.
- **Here**: the strike is 600 ms of `Cubic.In` and the return 720 ms of `Cubic.Out`, with no overshoot. BC5 adds
  the settle, and BC4's kit starts its impact beat on the contact frame, not before.

### Hit stop

- **What**: the two figures freeze for a few frames at contact, so the hit reads as heavy. The stop grows with
  the damage. A projectile freezes its target only. The struck figure shakes during the stop, and the shake dies
  down. SmashWiki gives a cap of 20 frames in Melee and 30 from Brawl on (*to check*).
- **Source**: [Stop for Big Moments!](https://www.youtube.com/watch?v=OdVkEOzdCPw), Masahiro Sakurai, 2022;
  [Thinking About Hitstop](https://sourcegaming.info/2015/11/11/thoughts-on-hitstop-sakurais-famitsu-column-vol-490-1/),
  Sakurai's Famitsu column, translated by Source Gaming, 2015; [Hitlag](https://www.ssbwiki.com/Hitlag), SmashWiki.
- **Phaser**: `Phaser.Tweens.TweenManager#setGlobalTimeScale` at 0 freezes every tween;
  `Phaser.Time.Clock#delayedCall` still runs and sets it back to 1. Emitters have their own
  `Phaser.GameObjects.Particles.ParticleEmitter#timeScale`. To freeze two figures only, use
  `Phaser.Tweens.Tween#pause` and `Phaser.Tweens.Tween#resume`.
- **Cost**: none to draw. The stop is spent inside the impact window, never added to it. The cancel path must
  set the time scale back to 1 and remove the timer (`Phaser.Time.TimerEvent#remove`): `killAll` does neither.
- **Reduced motion**: nothing. There is no motion to stop.
- **Here**: not built. BC5 builds it: 60 to 120 ms at 1× by damage (*house*), divided by the playback rate.

### The hold before the number

- **What**: the impact is seen first, then its result is read. A short hold between the flash and the number
  gives the eye time to move from the body to the text.
- **Source**: [Stop for Big Moments!](https://www.youtube.com/watch?v=OdVkEOzdCPw), Masahiro Sakurai, 2022, for
  the idea that a pause makes room to read. No source gives a figure: the length is a house rule.
- **Phaser**: `Phaser.Types.Tweens.TweenBuilderConfig.delay` on the number's own tween, or one
  `Phaser.Time.Timeline` that places the flash and the number at their milliseconds
  (`Phaser.Types.Time.TimelineEventConfig.at`).
- **Cost**: none.
- **Reduced motion**: the number is there at once, for `REDUCED_MS` (650 ms), as today.
- **Here**: the result text is the host's, shown by the page. BC5 holds its entrance 80 to 120 ms after contact
  (*house*). The text itself is never withheld from a skip.

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
- **Here**: the view uses `Cubic.In`, `Cubic.Out`, `Quad.Out`, `Sine.Out` and one `Back.Out`. An elastic settle
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
- **Here**: the three strokes of a slash and the five bones of a ward appear together today. BC4's kit staggers
  the copies of a beat by 30 to 60 ms (*house*).

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
- **Here**: not built: a lunge rotates the figure and nothing else. BC5 stretches the strike and squashes the
  landing by at most 8% (*house*); the mutation cues ride the same transform (`battle/src/mutation-cues.ts`).

### Recoil

- **What**: the struck figure is pushed along the line of the attack and comes back. Knockback is feedback: it
  says who was hit and how hard.
- **Source**: [The art of screenshake](https://www.youtube.com/watch?v=AJdEqssNZ-U), Jan Willem Nijman, Vlambeer,
  2013, which lists knockback of the enemy among its tweaks.
- **Phaser**: one tween with `Phaser.Types.Tweens.TweenBuilderConfig.yoyo`, its direction from the caster to
  the target (`Phaser.Math.Angle.Between`); `Phaser.Types.Tweens.TweenBuilderConfig.hold` keeps the figure at the
  far end before it returns.
- **Cost**: one tween.
- **Reduced motion**: nothing.
- **Here**: built: 18 px and 5° over 280 ms, eased `Quad.Out`, in `battle/src/scene.ts`; 12 px for a magical hit.
  BC5 scales it by damage and lets a defeated figure not come back.

### Hit flash

- **What**: the struck sprite turns one flat colour for a frame or two at contact. Its job is to say "this one
  took damage" faster than any number.
- **Source**: [The art of screenshake](https://www.youtube.com/watch?v=AJdEqssNZ-U), Nijman, 2013, which lists
  the hit animation. No source gives a duration: ours is a house rule.
- **Phaser**: `Phaser.GameObjects.Image#setTint` with `Phaser.GameObjects.Image#setTintMode` at
  `Phaser.TintModes.FILL`, then `Phaser.GameObjects.Image#clearTint`, which also puts the mode back to
  `Phaser.TintModes.MULTIPLY`.
- **Cost**: none: a tint is a vertex attribute and does not break the batch.
- **Reduced motion**: nothing. A flash is exactly what the setting removes.
- **Here**: not built. The acting halo already fills a sprite's shape this way (`battle/src/acting-halo.ts`).
  BC5 flashes once per hit, 60 to 80 ms (*house*), in bone and never in the target marker's gold. See "Flash
  limits" before using it on several figures at once.

### Death

- **What**: the last hit pays off and the body stays. A figure that vanishes leaves no history on the field; one
  that falls and remains does.
- **Source**: [The dark art of creating gorgeous death animations](https://www.pcgamer.com/the-dark-art-of-creating-gorgeous-death-animations/),
  PC Gamer; [The art of screenshake](https://www.youtube.com/watch?v=AJdEqssNZ-U), Nijman, 2013, on permanence.
  No source describes a death for a single picture: the staging is ours.
- **Phaser**: a tween of `Phaser.GameObjects.Image#alpha` and scale about the feet, and a
  `Phaser.Filters.ColorMatrix` added with `Phaser.GameObjects.Components.FilterList#addColorMatrix`, whose
  `Phaser.Display.ColorMatrix#grayscale` drains the colour.
- **Cost**: the filter is a framebuffer and a pass for that figure, each frame, while it is on. Keep it for
  the fall, then remove it and leave a tint.
- **Reduced motion**: the settled frame: the figure at its defeated alpha, at once.
- **Here**: a defeated creature is drawn at 20% alpha with no transition. BC5 stages the fall inside the impact
  window: a recoil with no return, a sink, the colour drained, then today's settled frame.

### Breath at rest

- **What**: a still figure is never quite still. A moving hold, a slow rise and fall, keeps a long pose alive.
- **Source**: [Why all animators need to master the moving hold](https://animationmentor.com/blog/why-all-animators-need-to-master-the-moving-hold),
  Animation Mentor; [Twelve basic principles of animation](https://en.wikipedia.org/wiki/Twelve_basic_principles_of_animation),
  on a character at rest.
- **Phaser**: computed in `Phaser.Scene#update` from the scene's time, as the halos are, and applied to
  `Phaser.GameObjects.Image#scaleY`. Not a tween: the cancel path's `Phaser.Tweens.TweenManager#killAll` would
  kill it at the first skip.
- **Cost**: one sine per figure per frame.
- **Reduced motion**: off. The figure stands still.
- **Here**: removed once (`docs/art/battle-polish.md`); the owner asked for it back. BC5 builds it: about 1.5% of
  height over 3 to 4 s (*house*), each creature out of phase, stopped while an action moves the figure.

## The camera

### Shake from trauma

- **What**: keep one value, trauma, between 0 and 1. A hit adds to it; it falls back linearly. The shake is
  trauma squared or cubed, so a small hit barely moves the view and a large one moves it much more. Offset is
  `maxOffset × shake × noise`, angle `maxAngle × shake × noise`. Smooth noise reads better than a random draw.
- **Source**: [Math for Game Programmers: Juicing Your Cameras With Math](https://gdcvault.com/play/1023146/Math-for-Game-Programmers-Juicing),
  Squirrel Eiserloh, GDC 2016 ([slides](https://media.gdcvault.com/gdc2016/Presentations/Eiserloh_Squirrel_JuicingYourCameras.pdf)).
- **Phaser**: `Phaser.Cameras.Scene2D.Camera#shake` draws a new random offset each frame at a fixed intensity:
  no decay, no rotation. Its callback receives the progress, and `Phaser.Cameras.Scene2D.Effects.Shake#intensity`
  can be lowered there. For the full pattern, set `Phaser.Cameras.Scene2D.Camera#setScroll` and
  `Phaser.Cameras.Scene2D.Camera#setRotation` each frame from `Phaser.Math.HashSimplex`.
- **Cost**: none to draw.
- **Reduced motion**: nothing. Camera motion is the first thing the setting removes.
- **Here**: two fixed shakes, 320 ms at 0.003 for a physical hit and 240 ms at 0.0015 for a magical one. BC5
  replaces them with one trauma value: damage adds to it, and a blood cost alone still shakes nothing
  (`docs/art/spell-vfx.md`).

### Punch zoom

- **What**: the camera jumps a little closer on a heavy hit and eases back. With a hit stop it marks the one
  blow that matters. Smash keeps it for slow, hard-to-land hits.
- **Source**: [Special Zoom](https://www.ssbwiki.com/Final_Zoom), SmashWiki.
- **Phaser**: `Phaser.Cameras.Scene2D.Camera#zoomTo` with an ease, or `Phaser.Cameras.Scene2D.Camera#setZoom`
  from a tween. `Phaser.Cameras.Scene2D.Camera#resetFX` does not reset a zoom: the cancel path must also call
  `Phaser.Cameras.Scene2D.Effects.Zoom#reset` on `Phaser.Cameras.Scene2D.Camera#zoomEffect` and set the zoom to 1.
- **Cost**: none to draw. Everything on the camera moves, the labels and dice drawn in the scene included.
- **Reduced motion**: nothing.
- **Here**: not built. BC5 allows it on a defeat and on a critical hit only: 3% in, back over 200 ms (*house*).

### Slow push on a capstone

- **What**: the camera moves in slowly while a great spell gathers, and releases at the impact. A slow push
  builds tension; it is the opposite of the punch.
- **Source**: [Special Zoom](https://www.ssbwiki.com/Final_Zoom), SmashWiki, for a zoom kept for the rare
  moment. No source of the craft was found for the slow form: its numbers are house rules.
- **Phaser**: `Phaser.Cameras.Scene2D.Camera#zoomTo` over the windup with `Phaser.Math.Easing.Sine.InOut`, and
  `Phaser.Cameras.Scene2D.Camera#pan` toward the caster.
- **Cost**: none to draw. The same reset as the punch zoom.
- **Reduced motion**: nothing.
- **Here**: not built. BC8 may use it for a capstone: 4% over the 840 ms windup (*house*), released on contact.

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
- **Here**: not built: every effect is a `Graphics` drawn once. BC4 makes the textures in the kit. A generated
  sprite sheet is for what these cannot give (question 3 of the plan).

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
- **Here**: the lab's impact tweens ten `Graphics` sparks by hand (`battle/src/scene.ts`). BC4's burst primitive
  replaces that: 8 to 24 particles (*house*), cleared with `Phaser.GameObjects.Particles.ParticleEmitter#killAll`.

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
- **Here**: not built. BC4 makes it a primitive; BC9 uses it for the ground.

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
- **Here**: the quill flies bare. BC4 makes the trail a primitive; BC7 gives it to the two projectile spells.

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
- **Here**: the windup draws a ring that grows away from the caster. BC4's gather primitive draws particles in
  instead; BC8 uses it for the blood cost, from the caster's wound to the ward.

### Additive light

- **What**: drawn additively, overlapping particles sum toward white and read as light. On a bright ground they
  blow out and lose shape and colour; on a dark one they glow.
- **Source**: [Technical Artist Bootcamp: The VFX of Diablo](https://gdcvault.com/play/1017660/Technical-Artist-Bootcamp-The-VFX),
  Julian Love, Blizzard, GDC 2013; [Premultiplied alpha](https://iquilezles.org/articles/premultipliedalpha),
  Inigo Quilez.
- **Phaser**: `Phaser.Types.GameObjects.Particles.ParticleEmitterConfig.blendMode` at `Phaser.BlendModes.ADD`;
  `Phaser.BlendModes.SCREEN` sums more gently and does not blow out.
- **Cost**: a change of blend mode starts a new batch. Draw every additive object together.
- **Reduced motion**: nothing.
- **Here**: four additive objects today: the capstone halos and the condition motes. The eclipse ground is dark,
  so additive light reads on it; over a pale sprite, use `SCREEN`. BC4 gives the kit one additive layer.

### A budget of pixels

- **What**: a phone's GPU runs out of pixels before it runs out of particles. Many blended particles on top of
  each other are drawn over and over. Prefer fewer and larger; keep the transparent margin of a texture small.
- **Source**: [Optimal Particle Rendering on PowerVR](https://docs.imgtec.com/performance-guides/graphics-recommendations/html/topics/optimal-particle-rendering-on-powervr.html),
  Imagination Technologies; [Phaser 4 Rendering Concepts](https://phaser.io/tutorials/phaser-4-rendering-concepts),
  Phaser Studio: "Filling pixels is expensive".
- **Phaser**: `Phaser.Types.GameObjects.Particles.ParticleEmitterConfig.maxAliveParticles` is the cap, and
  `Phaser.GameObjects.Particles.ParticleEmitter#getAliveParticleCount` is what a test reads.
- **Cost**: this entry is the cost. Not measured on a phone; BC10 measures it.
- **Reduced motion**: nothing.
- **Here**: BC4 sets a cap per primitive and one for the scene: 150 particles alive at once (*house*), until
  BC10 measures.

## Light and surface

A filter is Phaser 4's name for what Phaser 3 called an FX, and it replaces masks too. A game object must opt in
(`Phaser.GameObjects.Image#enableFilters`) before it has a `filters` list; a camera has one from the start. The
list has two halves: internal filters cover the object and are cheaper, external ones cover the context, usually
the screen. A `Graphics` has no size, so its internal filters cover the context too: bake the drawing to a
texture first, or filter a container.

### Glow

- **What**: a soft halo around a shape. It says energy, and it lifts an effect off the ground.
- **Source**: [Phaser 4 Filters and Post-FX](https://unpkg.com/phaser@4.2.1/skills/filters-and-postfx/SKILL.md),
  the notes Phaser 4.2.1 ships.
- **Phaser**: `Phaser.GameObjects.Components.FilterList#addGlow`; tween `Phaser.Filters.Glow#outerStrength` for
  a pulse. The quality and the distance are fixed when the filter is made.
- **Cost**: a framebuffer and a pass, each frame, for each glowing object. The cheap form costs nothing: a
  soft-dot sprite, tinted, additive, drawn behind the shape.
- **Reduced motion**: a glow that does not move may stay. A pulse is removed.
- **Here**: the capstone halo is the cheap form already (`battle/src/capstone-effects.ts`). BC4 keeps the cheap
  form as the default, and the filter for one hero effect a family.

### Blur

- **What**: softens what is behind or beside the subject, or smears a fast thing along its motion.
- **Source**: [Phaser 4 Filters and Post-FX](https://unpkg.com/phaser@4.2.1/skills/filters-and-postfx/SKILL.md),
  Phaser 4.2.1.
- **Phaser**: `Phaser.GameObjects.Components.FilterList#addBlur`, its amount in `Phaser.Filters.Blur#strength`.
- **Cost**: the most expensive common filter: several samples a pixel. On the camera it is the whole screen.
  A smear is cheaper as a stretched sprite.
- **Reduced motion**: nothing.
- **Here**: not used. BC9 may blur the ground during a capstone's push, and only if BC10's measure allows it.

### Displacement

- **What**: pixels are moved by a second picture: heat above a fire, a ring that bends the ground as a shock
  passes.
- **Source**: [Phaser 4 Filters and Post-FX](https://unpkg.com/phaser@4.2.1/skills/filters-and-postfx/SKILL.md),
  Phaser 4.2.1; [Stylized VFX in RiME](https://simonschreibt.de/gat/stylized-vfx-in-rime/), Simon Trümpler, for
  distortion used as a shape and not as noise.
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
- **Reduced motion**: a change of colour that does not flash may stay. See "Flash limits".
- **Here**: not used: the ground and the plates are tinted (`battle/src/battle-horizon.ts`). A tint is free, so
  it comes first; the matrix is for what a tint cannot do, the drained colour of a death.

### Bloom

- **What**: the bright parts of the picture bleed light around them.
- **Source**: [Phaser 4 Filters and Post-FX](https://unpkg.com/phaser@4.2.1/skills/filters-and-postfx/SKILL.md),
  Phaser 4.2.1: there is no Bloom filter in Phaser 4.
- **Phaser**: `Phaser.Actions.AddEffectBloom` builds it, or by hand:
  `Phaser.GameObjects.Components.FilterList#addParallelFilters`, a threshold and a blur on
  `Phaser.Filters.ParallelFilters#top`, blended back additively.
- **Cost**: three passes or more. The heaviest pattern in the book.
- **Reduced motion**: static, so it may stay; it is the cost that decides.
- **Here**: not used, and not planned for a phone. Additive particles on the dark ground already read as light.

### One hero shader

- **What**: a fragment shader draws one effect no shape or particle can: a corona, a rift, a flowing surface.
  Layered noise gives it life: each octave doubles the frequency and halves the amplitude, and one noise bending
  another gives flame and smoke.
- **Source**: [Phaser 4 Shader Guide](https://phaser.io/tutorials/phaser-4-shader-guide), Phaser Studio;
  [Fractal Brownian Motion](https://thebookofshaders.com/13/), The Book of Shaders;
  [Domain warping](https://iquilezles.org/articles/warp), Inigo Quilez.
- **Phaser**: `Phaser.GameObjects.GameObjectFactory#shader` with a `Phaser.Types.GameObjects.Shader.ShaderQuadConfig`:
  the code in `.fragmentSource`, the values in `.setupUniforms` or through `Phaser.GameObjects.Shader#setUniform`.
  Time is not passed for you. With no code to write: `Phaser.GameObjects.NoiseSimplex2D` and
  `Phaser.GameObjects.Gradient` are shaders the engine ships.
- **Cost**: one draw call each, and it breaks the batch. Its price is the pixels of its quad: keep the quad small.
- **Reduced motion**: one still frame of it, or nothing.
- **Here**: not used. The plan allows one for a hero effect. Candidates: the eclipse's corona (BC9), the void
  (BC8).

## Projectiles

### Arcing travel

- **What**: a thrown thing rises and falls. A curve from caster to target, with one control point above the
  middle, reads as weight; a straight line reads as a beam.
- **Source**: [The Beauty of Bézier Curves](https://youtu.be/aVwxzDHniEw), Freya Holmér, 2021.
- **Phaser**: `Phaser.Curves.QuadraticBezier`; drive a value from 0 to 1 with
  `Phaser.Tweens.TweenManager#addCounter`, place the projectile with `Phaser.Curves.QuadraticBezier#getPoint` and
  turn it with `Phaser.Curves.QuadraticBezier#getTangent`. `Phaser.Curves.Curve#getPointAt` gives an even speed
  along the curve.
- **Cost**: one counter tween.
- **Reduced motion**: nothing. No projectile is launched, as today.
- **Here**: the quill flies straight, 600 ms of `Cubic.In` after the draw back (`battle/src/spell-effects.ts`).
  BC7 gives the two projectile spells a low arc.

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
- **Here**: not built. BC4 chooses one of the two forms on the bench, by measure.

### Chain between targets

- **What**: a jagged line from one target to the next. Split each segment at its middle and move the middle
  sideways; halve the move at each pass. Redraw it two or three times so it flickers. A wide dim line under a thin
  bright one gives it a core and a glow.
- **Source**: [Lightning Bolts](https://www.drilian.com/posts/2009.02.25-lightning-bolts/), Josh Jersild, 2009;
  [How to Generate Shockingly Good 2D Lightning Effects](https://code.tutsplus.com/how-to-generate-shockingly-good-2d-lightning-effects--gamedev-2681t),
  Michael Hoffman, 2013.
- **Phaser**: `Phaser.GameObjects.Graphics#strokePoints` on points built with `Phaser.Math.Vector2`; the offsets
  come from a `Phaser.Math.RandomDataGenerator` with a seed, so a film strip repeats.
- **Cost**: one `Graphics`. A redraw is a new path, not a new object.
- **Reduced motion**: nothing. A flickering bolt is a flash: see "Flash limits".
- **Here**: `lightning_bolt` draws one fixed nine-point arc from the caster to each recipient. BC8 gives it the
  subdivision and one redraw (*house*), still pinned to both ends.

### An impact that keeps the direction

- **What**: what flies off an impact goes on the way the blow was going. Debris in a cone along the line of
  travel says where the hit came from; a round burst says nothing.
- **Source**: [The art of screenshake](https://www.youtube.com/watch?v=AJdEqssNZ-U), Nijman, 2013, for effects
  that carry the direction of the shot. No source was found for this pattern by name.
- **Phaser**: `Phaser.Math.Angle.Between` the caster and the target, then
  `Phaser.Types.GameObjects.Particles.ParticleEmitterConfig.angle` as a range around it.
- **Cost**: none beyond the burst.
- **Reduced motion**: nothing.
- **Here**: the recoil already follows that line; the motifs do not. BC4's burst takes a direction and a spread.

## The ground

### Parallax

- **What**: far things move less than near things. Layers that shift by different amounts when the view moves
  give one flat painting depth. Godot's own example runs from 0.1 for the sky to 0.7 for the nearest layer
  (*to check*).
- **Source**: [2D Parallax](https://docs.godotengine.org/en/stable/tutorials/2d/2d_parallax.html), the Godot
  documentation.
- **Phaser**: `Phaser.GameObjects.Image#setScrollFactor` on each plate: the camera's shake, punch and push then
  move the near plates more than the far ones, with no code a frame.
- **Cost**: none.
- **Reduced motion**: nothing moves, so nothing shifts.
- **Here**: the horizon plates sit over the painting at fixed places (`battle/src/battle-horizon.ts`). BC9 gives
  them scroll factors, and the camera work of BC5 makes them show.

### Fog and embers

- **What**: slow particles in the air say the place is alive between two actions. Few, large, dim, and behind
  the figures.
- **Source**: [Special effects graphic techniques: fog effects](https://support.arm.com/documentation/102259/0100/Fog-effects),
  Arm, which prefers few large particles; [Particle System](https://unpkg.com/phaser@4.2.1/skills/particles/SKILL.md),
  Phaser 4.2.1.
- **Phaser**: one flowing emitter, full from the first frame
  (`Phaser.Types.GameObjects.Particles.ParticleEmitterConfig.advance`), spread by
  `Phaser.Types.GameObjects.Particles.ParticleEmitterConfig.emitZone`. A band of fog is a
  `Phaser.GameObjects.TileSprite` whose `Phaser.GameObjects.TileSprite#tilePositionX` drifts. On
  `Phaser.Core.Events.HIDDEN` the emitter is paused.
- **Cost**: one draw call for the embers, one for the fog. A band of fog covers many pixels: keep it to one.
- **Reduced motion**: nothing. The motes are hidden, as today.
- **Here**: 24 gold motes, each a shape moved by hand each frame, hidden under reduced motion
  (`battle/src/scene.ts`). BC9 makes them one emitter and adds the fog.

### A light that answers a cast

- **What**: the ground takes the colour of the spell for a moment. The place reacts, so the spell feels as if it
  happened there and not on top of a picture.
- **Source**: [Special Zoom](https://www.ssbwiki.com/Final_Zoom), SmashWiki: the background answers the great
  hit; [Understanding SC 2.3.1](https://www.w3.org/WAI/WCAG22/Understanding/three-flashes-or-below-threshold.html),
  W3C, for its limit.
- **Phaser**: a `Phaser.GameObjects.PointLight` at the cast: a light with no normal map, built for flashes, with
  `Phaser.GameObjects.PointLight#intensity` and `Phaser.GameObjects.PointLight#radius` to tween. For the whole
  ground, tween the tint of the painting and the plates through `Phaser.Display.Color.Interpolate.ColorWithColor`.
- **Cost**: a point light is one small draw call; a tint is free. `Phaser.Cameras.Scene2D.Camera#flash` covers
  the whole view in one colour, which is what the flash limits forbid at full white.
- **Reduced motion**: nothing.
- **Here**: the ground never changes. BC9 builds the answer: a rise of 100 ms and a fall of 400 ms (*house*),
  one change of light per cast, never a strobe.

### A scar that fades

- **What**: a mark left where the blow landed, which fades. For a few seconds the ground remembers.
- **Source**: [The art of screenshake](https://www.youtube.com/watch?v=AJdEqssNZ-U), Nijman, 2013, on
  permanence. No source gives a duration.
- **Phaser**: one image under the figure, blended with `Phaser.BlendModes.MULTIPLY`, its alpha tweened to 0. For
  many marks, stamp them into a `Phaser.GameObjects.RenderTexture` with `Phaser.GameObjects.RenderTexture#stamp`,
  then `Phaser.GameObjects.RenderTexture#render`: in Phaser 4 nothing is drawn until `render` is called.
- **Cost**: an image is free. A render texture is a framebuffer.
- **Reduced motion**: nothing: the settled frame has no scar.
- **Here**: not built. A scar is decoration, never state: a replay and a first load show none. BC9 fades it
  inside the settle window, so no frame the player can navigate to holds one.

## Reading

### One effect, one meaning

- **What**: the same shape always means the same thing. An effect has one primary element that carries the
  game's message, and secondary ones that carry the theme, lower in value and opacity. What an effect weighs to
  the eye should match what it weighs in the game.
- **Source**: [League's VFX Style Guide](https://nexus.leagueoflegends.com/en-us/2017/10/dev-leagues-vfx-style-guide/),
  Riot Games, 2017; [Clarity in League](https://www.leagueoflegends.com/en-us/news/dev/clarity-in-league/), Riot
  Games.
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

- **What**: an effect has an anticipation, a main beat and a dissipation. The main beat is the brightest and the
  shortest. The end falls in value, saturation and opacity (*to check*), and never competes with the next action.
- **Source**: [Visual Effects Bootcamp: Artistic Principles of VFX](https://www.gdcvault.com/play/1024439/Visual-Effects-Bootcamp-Artistic-Principles),
  Hadidjah Chamberlin and Jason Keyser, GDC 2017; [League's VFX Style Guide](https://nexus.leagueoflegends.com/en-us/2017/10/dev-leagues-vfx-style-guide/),
  Riot Games, 2017.
- **Phaser**: one `Phaser.Time.Timeline` per effect places the three parts on the game's clock
  (`Phaser.Time.Timeline#add`), and one `Phaser.Time.Timeline#timeScale` follows the playback rate.
- **Cost**: none.
- **Reduced motion**: nothing.
- **Here**: the three parts are the game's three windows: the windup's charge, the impact's motif (100 ms to
  arrive, the rest to fade), the settle. BC4's `staging` is a list of beats placed in those windows.

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
- **Here**: at 2× an action lasts about 1.5 s, and one spell can hit three figures. BC4's flash and light beats
  count: one flash per action for each figure, one change of light per cast. BC10 checks all 44 stagings.

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
  BC4 has a test that it plays nothing.

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
| `Phaser.GameObjects.Mesh`, `Phaser.GameObjects.Plane` | Removed. |
| `Phaser.Textures.TextureManager#generate` | `Phaser.GameObjects.Graphics#generateTexture`, or a canvas texture. |

## Declared, and dead

The types cannot catch these. They compile, and do the wrong thing.

| Name | What happens |
| --- | --- |
| `Phaser.GameObjects.Image#setTintFill` | Still declared. It does nothing but log an error. Use `setTint` and `setTintMode` with `Phaser.TintModes.FILL`. |
| `Phaser.GameObjects.Image#setMask` | Canvas only. Under WebGL a mask is a filter. |
| `Phaser.Textures.DynamicTexture#draw` | Draws nothing until `Phaser.Textures.DynamicTexture#render` is called. In Phaser 3 it drew at once. |
| `Phaser.GameObjects.GameObjectFactory#noisesimplex2d` | The name is lower case. The notes the package ships write it in camel case, which does not exist. |
| A shader's texture coordinates | `y` is 0 at the bottom in Phaser 4. A Phaser 3 or Shadertoy shader comes out upside down. |

## How the book is kept

- `studio/battle-craft.test.js` reads this file in the gate. Every engine name written in backticks exists in
  `battle/node_modules/phaser/types`; every name in the left column of "Phaser 3 names that are gone" does not;
  every entry has its six lines, a linked source and an engine name. So an entry that cannot name a real API is
  not in the book.
- To check a name before writing it:
  `node scripts/battle/phaser-symbols.mjs "Phaser.Cameras.Scene2D.Camera#shake"`.
- A change to this file runs the full build in CI, not the light one: the light build installs no Phaser
  (`studio/browser/plan.js`).
- When Phaser is upgraded, the test says which names moved. Fix the entry, then read "Declared, and dead" again
  against the new version: the test cannot.
- The links were found by a web search on 2026-11-18. The session's network could not open them, so no page was
  read here: each source is cited for what it is known to say, and a figure taken from a search excerpt is marked
  *to check*. The Phaser facts are first hand, read in the installed package and its source. A session that can
  open the links removes the marks it has checked.
- A new pattern is added with all six lines, under the group it belongs to. A number with no source is marked
  *house*.
