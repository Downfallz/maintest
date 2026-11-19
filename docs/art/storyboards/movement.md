# Storyboards: the movement

The body and camera language every action shares, whatever its spell: how a figure loads, strikes, is struck, falls,
rests, and how the view answers a hit. It is BC5 of [the battle's craft plan](../battle-craft-plan.md). It stages no
spell: BC6 to BC8 place each spell's own beats on top of these, and [Strike](base-actions.md) already names which
of its beats are this file's. The numbers here are the defaults a staging does not repeat.

The weight of a blow is read from the host's confirmed outcome, `cue.changes`: the health each recipient lost, in
points. The scene does not know a creature's maximum health, so the scale is in absolute points (*house*). In
`data/`, a creature has 30 base health, Strike deals 3 and Pummel 2, and the damage amounts of the spells run from 1
to 11, most of them between 2 and 7.

| Weight | Health lost by the figure | Examples |
| --- | --- | --- |
| light | 0 to 3, or contact with no change | Strike (3), Pummel (2), Quill (3) |
| medium | 4 to 6 | most family spells |
| heavy | 7 or more, or a defeat | the capstones, any killing blow |

The blow's weight, which decides the anticipation and the hit stop, is the heaviest among the figures it struck. Each
struck figure's own loss decides its flash, its recoil and what it adds to the camera.

## A melee blow

### Intent

The player reads who struck whom and how hard, from the body alone: a light blow is a jab, a heavy one is wound up,
lands with a stop and throws its target back. Every melee action plays it, so it is a grammar and not a show: it
takes no more attention than the blow's weight earns, and the spell's own beats (BC6) sit on top of it.

### Today

Filmed at `7ddc6a9`, 390 by 844, 1×, a1 on b1, no health change given: Strike and Pummel, each a whole action (12
frames every 240 ms) and a contact (24 frames every 60 ms).

- Strike and Pummel give the same sheets, frame for frame: Pummel's "Hammer with a huge front paw" is staged exactly
  as the base attack. Nothing in the body says which blow is heavier.
- 0 to 180 ms: a pale arc on a1's body, the cast's gather, on a melee blow.
- 0 to 240 ms: the draw back is not seen; a1 stays on its ground ring.
- 240 to 840 ms: the strike, `Cubic.In`. At 540 ms a1 has barely left its ring, at 660 ms it is a quarter of the way,
  at 720 ms half. At 840 ms a1 covers b1's feet and lower body; b1's head stays clear.
- 900 ms: the strokes appear on b1. No flash at any frame, no hit stop: a1 starts back at 960 ms.
- 840 to 1400 ms: b1's recoil (12 px and 4°, out and back) cannot be told from b1 standing still on either sheet.
- 1380 ms: a1 is home. 1680 to 2640 ms: the same frame five times.

### Beats

The rows are the heavy blow, the one that leans hardest on the windows. A light and a medium blow keep the same
beats on shorter numbers, listed under the table.

| At | Lasts | Window | Layer | Beat | Pattern |
| --- | --- | --- | --- | --- | --- |
| 0 | 240 | windup | the caster | Draw back 32 px away from the target and lean back 12° (*house*), eased `Cubic.Out`, so it slows as it loads. | [Anticipation](../battle-craft.md#anticipation), [Easing families](../battle-craft.md#easing-families) |
| 240 | 80 | windup | the caster | Held at the far end of the draw back, 80 ms (*house*): the heavy blow is wound up before it goes. | [Anticipation](../battle-craft.md#anticipation) |
| 320 | 520 | windup | the caster | The strike to the approach point (`strikeApproach`), eased `Cubic.In`, leaning 9° into the blow. Shorter than a light strike's 600 ms over a longer way, so it arrives faster. | [Contact and follow-through](../battle-craft.md#contact-and-follow-through) |
| 720 | 120 | windup | the caster | Stretch 8% (*house*, the book's ceiling) along the main axis of the travel (up and down on a phone held upright, across on a wide screen), the other axis narrowed to keep the volume, about the feet: the image's origin is already its ground anchor. | [Squash and stretch](../battle-craft.md#squash-and-stretch) |
| 840 | 120 | impact | the caster and the target | Hit stop, 120 ms (*house*). The caster holds at contact, stretched; the target trembles ±2 px across the line of the blow, 30 ms a swing (*house*). Made of the two figures' tween delays, not the global time scale: the spell's motif and burst play through the stop. | [Hit stop](../battle-craft.md#hit-stop) |
| 840 | 80 | impact | the target | Hit flash: the target filled flat in bone (`--bone`), 80 ms, then its tint cleared. Only on a figure that lost health, and not when the spell's own staging flashes it. | [Hit flash](../battle-craft.md#hit-flash), [Flash limits](../battle-craft.md#flash-limits) |
| 840 | 264 | impact | the camera | The target's loss adds 0.55 to the trauma, which falls by 0.25 every 120 ms: still by 1104 ms. See "The camera" below. | [Shake from trauma](../battle-craft.md#shake-from-trauma) |
| 960 | 80 | impact | the caster | The stretch released to rest, eased `Quad.Out`. | [Squash and stretch](../battle-craft.md#squash-and-stretch) |
| 960 | 640 | impact | the target | Recoil after the stop: 26 px and 8° (*house*) along the line from the caster's home to the target's, 280 ms out eased `Quad.Out`, held 80 ms, 280 ms back. | [Recoil](../battle-craft.md#recoil) |
| 1020 | 720 | impact | the caster | The return home, eased `Back.Out` with an overshoot of 1.2 (*house*): past home by a few pixels, then still. | [Contact and follow-through](../battle-craft.md#contact-and-follow-through), [Easing families](../battle-craft.md#easing-families) |
| 1740 | 800 | impact | the field | Nothing new. Both figures at home and breathing again. The result is read here. | [The hold before the number](../battle-craft.md#the-hold-before-the-number), [Breath at rest](../battle-craft.md#breath-at-rest) |

The scale, light / medium / heavy (all *house*):

- Draw back: 20 px and 8° / 26 px and 10° / 32 px and 12°, over 240 ms. Hold: none / 40 ms / 80 ms. Strike: 600 ms
  from 240 / 560 ms from 280 / 520 ms from 320. The windup ends at 840 ms in all three.
- Stretch from 720 ms: 5% / 6.5% / 8%.
- Hit stop from 840 ms: 60 / 90 / 120 ms. Tremor ±2 px in all three.
- Hit flash: 80 ms in all three: it says "lost health", not how much. None when the figure lost nothing.
- Stretch released at the end of the stop: 900 / 930 / 960 ms, over 80 ms.
- Recoil from the end of the stop: 12 px and 4° / 18 px and 6° / 26 px and 8°; held 0 / 40 / 80 ms; 280 ms out and
  280 ms back. It ends at 1460 / 1530 / 1600 ms. A figure struck with no loss recoils as a light one.
- Return home 60 ms after the stop: from 960 / 990 / 1020 ms, 720 ms: home at 1680 / 1710 / 1740 ms.
- Trauma added: 0.25 / 0.40 / 0.55.

The light column is Strike's storyboard to the millisecond. A blow that strikes several figures stops once, for its
heaviest, and each figure recoils and flashes for its own loss. A blow that defeats its target plays "A death" for
that target instead of the recoil.

### Reduced motion

Nothing. The frame after the action is shown at once for 650 ms with the result text, as today: no draw back, no
stretch, no stop, no flash, no recoil, no shake.

### Cost

- Objects: none. Every beat is a tween of the two figures' position, angle, scale or tint: no draw call, no particle.
- Tweens at the peak: three on the caster (or one chain, `Phaser.Tweens.TweenManager#chain`), two on each struck
  figure (the tremor, the recoil). The flash is a tint.
- The cancel path (`clearMotion`) gains, for every figure: the scale back to the figure's own (from
  `figureGeometry`), the tint cleared, as it already sets the position home and the angle to 0. The stop is made of
  tween delays, so `killAll` already ends it: no time scale to restore, no timer to remove. A skip during the flash,
  the stop or the stretch leaves each figure home, at its scale, untinted.
- The breath (below) must not write the scale of a figure a tween holds.
- Built by BC5: the weight read from `cue.changes`, the scaled draw back and hold, the stretch, the stop, the scene's
  own hit flash (the scene's, not the kit's: it must play before the kit has loaded), the recoil after the stop, the
  `Back.Out` return. The laboratory's sample, which has no `visual`, is played by the same beats; its 14 sparks drawn
  by hand become the kit's burst. BC6 places Strike's and Pummel's own beats on top.

### On the film strip

Filmed at 390 by 844, 1×, a1 on b1, from the repository with the bundle built, the sheets written outside it:

```bash
node scripts/battle/filmstrip.mjs spell:heavy_strike:v1 --actor a1 --targets b1 --damage 3 --out <folder>/light.png
node scripts/battle/filmstrip.mjs spell:pummel:v1 --actor a1 --targets b1 --damage 5 --out <folder>/medium.png
node scripts/battle/filmstrip.mjs spell:pummel:v1 --actor a1 --targets b1 --damage 8 --out <folder>/heavy.png
node scripts/battle/filmstrip.mjs spell:pummel:v1 --actor a1 --targets b1 --damage 8 \
  --frames 24 --interval 60 --columns 6 --out <folder>/heavy-contact.png
node scripts/battle/filmstrip.mjs spell:pummel:v1 --actor a1 --targets b1 --damage 0 \
  --frames 24 --interval 60 --columns 6 --out <folder>/nothing-lost-contact.png
```

- Light sheet: the frames Strike's storyboard lists, with its gather left to BC6.
- Heavy, 240 ms: a1 plainly below its ground ring, further than on the light sheet at 240 ms, leaning away from b1.
- Heavy contact, 240 and 300 ms: a1 in the same place on both frames (the hold).
- Heavy contact, 480 ms: a1 still within a tenth of the way to b1. 720 ms: about halfway.
- Heavy contact, 840 and 900 ms: a1's silhouette touches b1's and does not cover b1's head. b1 filled flat in bone
  at 840 and 900 ms and on no other frame. a1 at the same place on both frames.
- Heavy contact, 960 ms: b1 no longer filled; a1 still touching b1 (the stop ends here).
- Heavy contact, 1080 ms: a1 has left b1. b1 off its ring, away from a1, further than on the light sheet at 1080 ms.
- Heavy contact, 1260 and 1320 ms: b1 at the same place, the far end of its recoil (the hold: out from 960 ms for
  280 ms, held from 1240 to 1320 ms).
- Heavy sheet, 1680 ms: b1 home. a1 within a few pixels of its ring, past it or on it.
- Heavy sheet, 1920 ms: both figures home. Nothing else on the field but the spell's own motif, if it still fades.
- Medium sheet, 1200 ms: b1's recoil visibly shorter than the heavy's and longer than the light's.
- Nothing lost, 840 to 960 ms: no frame with b1 filled; b1 still recoils, as on the light sheet.

The sheets cannot show the 8% stretch on a figure 130 px tall (about 10 px), the 2 px tremor, the shake, the
`Back.Out` overshoot or the feel at full speed: do not judge them from a sheet.

### Left

- The weight is read in absolute points, 0 to 3, 4 to 6, 7 and up. A creature's maximum health would make it a share,
  but the scene frame does not carry it; if it ever does, the scale should become one.
- The anticipation is scaled by the outcome the host confirmed, before the number is shown. It shows nothing the
  frame does not already hold (ADR 0131). Settled by the owner, 2026-11-19: kept.
- A heavy blow holds 80 ms at the end of its draw back and strikes in 520 ms: the windup stays 840 ms, so
  `table/battle-timing.js` does not move. The draw back and the strike are no longer `DRAW_MS` and `STRIKE_MS`
  for a medium or heavy blow; the code keeps their sum.
- A figure that loses nothing is not flashed: the flash says "lost health". This settles the question Strike's
  storyboard left. It still recoils, as a light blow: contact happened (ADR 0131).
- The recoil is in pixels, as today, whatever the figure's size on the screen. On a wide screen 26 px is a smaller
  share of a larger figure; scaling it by `home.size` is a choice for the film to make.
- The hit stop is two delays, not a frozen clock. The book's `setGlobalTimeScale` route would freeze the motif and the
  burst too, and the book itself warns against it for that.
- At 2× a light stop is 30 ms, under two frames: a film at `--speed 2` says whether it still reads.

## A cast and a projectile

### Intent

A caster who does not move still acts: its body loads during the windup and lets go at the moment its spell leaves
or lands, so the eye knows who cast before the motif arrives. A figure harmed from a distance reacts as a struck one
does, a little less, so the player sees who was hurt without reading.

### Today

Filmed at `7ddc6a9`, 390 by 844, 1×: Quill (`spell:throwing_star:v1`, a projectile, a1 on b1) and Overdrive
(`spell:adrenaline_tonic:v1`, a cast, a1 on a2), each a whole action and a contact, no health change given.

- Overdrive, 0 to 180 ms: a pale arc on a1, fading by 240 ms. From 0 to 2640 ms a1 does not move at all, nor a2.
  900 to 1380 ms: the spores rise on a2 and fade. Nothing on a1 says it cast, except its halo.
- Quill, 0 to 540 ms: the quill stands upright on a1's body, still, as if part of it. 600 ms: at a1's back. 660 to
  780 ms: across the field. 840 ms: gone, and no motif yet. 900 ms: the strokes on b1, in the biological green.
- Quill: a1 does not move. b1 does not move: only a melee target recoils today. a1's dorsal quills, the approved
  execution ("Detachable dorsal bone quills projected toward up to two foes"), are not seen to throw.

### Beats

The rows are a heavy hit from a distance; the reaction keeps the melee scale (light / medium / heavy) on a shorter
stop. "The caster (cast)" and "the caster (projectile)" are two forms of one beat: a spell plays one.

| At | Lasts | Window | Layer | Beat | Pattern |
| --- | --- | --- | --- | --- | --- |
| 0 | 600 | windup | the caster (cast) | The body loads: a squash of 4% (*house*) about the feet, the width widened to keep the volume, eased `Sine.InOut`. No move. | [Anticipation](../battle-craft.md#anticipation), [Squash and stretch](../battle-craft.md#squash-and-stretch) |
| 600 | 240 | windup | the caster (cast) | The rise: from the squash to a stretch of 4% upward (*house*), eased `Cubic.In`, at its peak on the contact frame. | [Squash and stretch](../battle-craft.md#squash-and-stretch), [Contact and follow-through](../battle-craft.md#contact-and-follow-through) |
| 840 | 360 | impact | the caster (cast) | The release: back to rest, eased `Back.Out` with an overshoot of 1.2 (*house*). | [Contact and follow-through](../battle-craft.md#contact-and-follow-through), [Easing families](../battle-craft.md#easing-families) |
| 0 | 240 | windup | the caster (projectile) | The load: 8 px away from the target and a squash of 4% (*house*), eased `Cubic.Out`. | [Anticipation](../battle-craft.md#anticipation) |
| 240 | 120 | windup | the caster (projectile) | The throw, as the projectile leaves: to 6 px past home toward the target and a stretch of 4% along the throw (*house*), eased `Quad.Out`. Never further than 6 px from home: no lunge. | [Contact and follow-through](../battle-craft.md#contact-and-follow-through), [Squash and stretch](../battle-craft.md#squash-and-stretch) |
| 360 | 480 | windup | the caster (projectile) | Back home and to rest, eased `Sine.InOut`, before the projectile lands. | [Easing families](../battle-craft.md#easing-families) |
| 840 | 80 | impact | the target | Hit flash in bone, 80 ms, on each figure that lost health. | [Hit flash](../battle-craft.md#hit-flash), [Flash limits](../battle-craft.md#flash-limits) |
| 840 | 60 | impact | the target | Hit stop on the target alone, half the melee's: 30 / 45 / 60 ms by its loss (*house*), a tremor of ±1 px. | [Hit stop](../battle-craft.md#hit-stop) |
| 840 | 264 | impact | the camera | The loss adds to the trauma as a melee loss does (0.25 / 0.40 / 0.55). | [Shake from trauma](../battle-craft.md#shake-from-trauma) |
| 900 | 640 | impact | the target | Recoil along the line from the caster's home to the target's (`Phaser.Math.Angle.Between`), on the melee scale: here 26 px and 8°, 280 ms out, held 80 ms, 280 ms back. | [Recoil](../battle-craft.md#recoil), [An impact that keeps the direction](../battle-craft.md#an-impact-that-keeps-the-direction) |
| 1540 | 1000 | impact | the field | Nothing new. Every figure home. The result is read here. | [The hold before the number](../battle-craft.md#the-hold-before-the-number) |

A figure that receives without losing health (Overdrive's ally, a heal, a ward) does not react: no flash, no
recoil, no stop. Several recipients react together, on the contact frame, each by its own loss: the host gives one
contact. A caster who pays in blood does not react to its own cost: its wound is the spell's (BC8).

### Reduced motion

Nothing. The frame after the action at once, for 650 ms: no load, no throw, no flash, no recoil.

### Cost

- Objects: none. Tweens of the caster's scale and position, the target's position and angle, and a tint.
- The cancel path: the same lines as the melee blow (scale back to the figure's own, tint cleared, position home,
  angle 0). A skip during the load leaves the caster at rest.
- Built by BC5: the load and release of a cast, the throw of a projectile caster, the hit reaction from a distance
  (today `spellImpact` recoils a target only when the delivery is melee). BC7 gave the quill its arc; its trail
  is Paralyzing Barb's.

### On the film strip

Filmed at 390 by 844, 1×, from the repository with the bundle built, the sheets written outside it:

```bash
node scripts/battle/filmstrip.mjs spell:adrenaline_tonic:v1 --actor a1 --targets a2 \
  --frames 24 --interval 60 --columns 6 --out <folder>/cast-contact.png
node scripts/battle/filmstrip.mjs spell:throwing_star:v1 --actor a1 --targets b1 --damage 3 \
  --frames 24 --interval 60 --columns 6 --out <folder>/projectile-contact.png
node scripts/battle/filmstrip.mjs spell:engulfing_flames:v1 --actor a1 --targets b1 --damage 8 \
  --out <folder>/cast-heavy.png
```

- Overdrive, 480 and 540 ms: a1 lower at its top than at 0 ms, its feet on the same line.
- Overdrive, 840 ms: a1 taller than at 0 ms, its feet on the same line, not moved sideways.
- Overdrive, 1200 to 1380 ms: a1 the same as at 0 ms. a2 does not move on any frame.
- Quill, 180 and 240 ms: a1 a few pixels below its home, away from b1. 360 and 420 ms: a few pixels above it,
  toward b1 (at 300 ms the throw has only brought it back to its ring). 840 ms: a1 at home. On no frame is a1 more
  than 8 px from home.
- Quill, 840 and 900 ms: b1 filled flat in bone (the flash is 80 ms), at home; 960 ms: not filled.
- Quill, 1080 to 1200 ms: b1 off its ring away from a1 (light: 12 px). 1500 ms: b1 home.
- Incinerate (`spell:engulfing_flames:v1`, the cast sheet with 8 lost), 1200 ms: b1 at the far end of a recoil as
  long as the heavy melee's, away from a1.

The 4% squash is about 5 px on a figure 130 px tall: the critique compares the top of a1's silhouette between
frames, and does not judge the feel.

### Left

- A cast loads in the body and gathers no particle by default: the windup's pale arc (the motif's charge in
  `spell-effects.ts`) stays until BC6 to BC8 decide each spell's windup. The two may read twice as "cast".
- The projectile caster releases at the throw (240 ms), not at contact: the body lets go when the quill leaves it.
  A cast releases at contact because nothing leaves it before.
- The distant recoil keeps the melee's distances: damage is damage. Only the stop is halved, as the book's source
  gives less hit stop to a projectile. The owner may want a smaller push from afar.
- Settled in BC7 (#909): the quill is not drawn while a1 loads, leaves just over a1's head with the toss at
  240 ms and flies at an even pace to the contact, on a low arc when thrown across the field (`FLIGHT` in
  `table/battle-motion.js`, [Quill](biological.md#quill-spellthrowing_starv1)). `THROW` did not move.

## A death

### Intent

The killing blow pays off and the body stays: the player sees which figure fell, at the moment it fell, and the field
keeps it, faded, where it stood. A death is rare and decisive, so it is the one hit that may take the camera.

### Today

Not filmed at `7ddc6a9`, before the bench had `--defeat`. From the code (`battle/src/scene.ts`, `present`): a creature
the frame gives as not alive is drawn at 20% alpha and its shadow at 20% the moment the impact frame arrives. So at
840 ms the defeated figure drops to 20% in one frame, and its recoil, if melee, plays on a figure already faded. Its
auras go at once. The settled frame, which this storyboard keeps: the figure at home, unscaled, untinted, at 20%,
its shadow at 20%.

### Beats

A defeat is a heavy blow for the stop and the camera, whatever the health lost. The rows are a melee death; from a
distance the recoil starts at 900 ms, after the target's 60 ms stop, and every later row is the same.

| At | Lasts | Window | Layer | Beat | Pattern |
| --- | --- | --- | --- | --- | --- |
| 840 | 80 | impact | the fallen | Hit flash in bone, 80 ms: its one flash. The figure is at full alpha: the scene holds the frame's 20% until the fall gives it. | [Hit flash](../battle-craft.md#hit-flash), [Flash limits](../battle-craft.md#flash-limits) |
| 840 | 120 | impact | the fallen and the caster | Hit stop, 120 ms, the heavy one, tremor ±2 px. | [Hit stop](../battle-craft.md#hit-stop) |
| 840 | 200 | impact | the camera | Punch zoom: 3% in at contact, back over 200 ms (*house*, the book's), eased `Quad.Out`. Only on a defeat, or a critical hit that took health. | [Punch zoom](../battle-craft.md#punch-zoom) |
| 960 | 320 | impact | the fallen | Recoil with no return: 26 px and 8° along the line of the blow, eased `Quad.Out`, and it stays there. | [Recoil](../battle-craft.md#recoil), [Death](../battle-craft.md#death) |
| 1280 | 160 | impact | the fallen | Held at the far end: the blow landed. | [Death](../battle-craft.md#death) |
| 1440 | 760 | impact | the fallen | The fall: it slumps about its feet (height to 90%, width to 105%, *house*), tips 6° further along the blow, and sinks back onto its own ground spot (`Sine.InOut`) while its alpha falls to 20% (`Sine.In`) and its shadow from 55% to 20%. | [Death](../battle-craft.md#death), [Easing families](../battle-craft.md#easing-families) |
| 1440 | 760 | impact | the fallen | The colour drained: a multiply tint from white to the palette's cold grey, `--brand-line` (`0x52636b`), through `Phaser.Display.Color.Interpolate.ColorWithColor` on a counter. No filter. | [Death](../battle-craft.md#death), [Colour matrix](../battle-craft.md#colour-matrix) |
| 2200 | 200 | impact | the fallen | At 20%, the slump and the tip ease out to scale 1 and 0°; the tint is cleared at 2400 ms. This is today's settled frame. | [Death](../battle-craft.md#death) |
| 2400 | 140 | impact | the field | Nothing new. The fallen does not breathe. The result is read here. | [The hold before the number](../battle-craft.md#the-hold-before-the-number), [Breath at rest](../battle-craft.md#breath-at-rest) |

The caster of a melee death plays the heavy blow's beats: draw back, hold, strike, stretch, return. A caster felled
by its own blood cost has no blow to follow: no recoil, the rest from 1440 ms.

### Reduced motion

The settled frame at once: the figure at home at 20%, its shadow at 20%, untinted, for 650 ms with the result text.
No flash, no stop, no zoom, no fall.

### Cost

- Objects: none. Tweens of the fallen's position, angle, scale and alpha, the shadow's alpha, and one counter for the
  tint. The camera's zoom is the camera's.
- Why a tint and not the colour matrix: the filter costs the fallen a framebuffer and a pass each frame for the
  760 ms of the fall, on a phone, where a tint is a vertex attribute and costs nothing. A multiply tint darkens toward
  grey rather than taking the colour out, but the figure is fading to 20% at the same time: by the time the
  difference could be seen, there is too little figure left to see it in. The book's own rule is "a tint comes first".
- `present` sets a defeated figure to 20% on every frame it receives: while the fall plays, it must leave that figure's
  alpha and its shadow's to the fall.
- The cancel path gains: the alpha and the shadow's alpha set to the frame's (20% for the fallen), the tint cleared,
  the scale back to the figure's own, the position home and the angle 0, the zoom put back as below. A skip mid-death
  leaves exactly the settled frame.
- Built by BC5: the death's beats and the punch zoom.

### On the film strip

Filmed at 390 by 844, 1×, a1 on b1, from the repository with the bundle built, the sheets written outside it:

```bash
node scripts/battle/filmstrip.mjs spell:pummel:v1 --actor a1 --targets b1 --damage 8 --defeat \
  --frames 14 --interval 240 --out <folder>/death.png
node scripts/battle/filmstrip.mjs spell:pummel:v1 --actor a1 --targets b1 --damage 8 --defeat \
  --frames 24 --interval 60 --columns 6 --out <folder>/death-contact.png
node scripts/battle/filmstrip.mjs spell:throwing_star:v1 --actor a1 --targets b1 --damage 8 --defeat \
  --frames 14 --interval 240 --out <folder>/death-ranged.png
```

- Contact, 840 and 900 ms: b1 filled flat in bone, at full alpha. 960 ms: not filled, at full alpha.
- Contact, 900 ms: every figure on the field drawn a little larger than at 780 ms (the zoom). 1080 ms: the same size
  as at 780 ms.
- Death, 1200 ms: b1 off its ring, away from a1, at full alpha and in its own colours.
- Death, 1440 ms: b1 at the same place as at 1200 ms.
- Death, 1680 and 1920 ms: b1 fainter at each frame, greyer, lower at its top, tipped away from a1, and closer to its
  ring than at 1440 ms.
- Death, 2640 ms onward (2640, 2880, 3120): b1 the same on every frame, on its ring, at 20%, upright, and its
  colour as on today's settled frame: no grey left. Its shadow faint. The tint is cleared at 2400 ms, so a frame
  there still shows the last of it, on a figure already at 20% and upright.
- Death-ranged, 1200 ms: b1 off its ring away from a1, with a1 at home; from 2400 ms, the same settled frame.

The sheets cannot show the stop's tremor or the feel of the zoom at full speed.

### Left

- The body does not stay where it fell: it sinks back onto its own spot while it fades, because the settled frame,
  which replays and reloads also show, has it at home. A body that stays where the blow sent it would need the
  settled frame to change, which reaches `arrange` and every replay. Settled by the owner, 2026-11-19: kept.
- No colour matrix: a tint, for the reason under Cost. The book's Death entry asks for the filter during the fall;
  it should say the tint comes first, and the filter only if a film shows the tint does not read.
- The punch zoom moves the labels and dice drawn in the scene for 200 ms, and the page's own labels over the canvas
  do not follow it: about 6 px at the edge of a phone. The film says whether that is seen.
- A critical hit that does not kill gets the punch zoom too, as the book allows; nothing else of it is staged here.

## The breath at rest

### Intent

A standing figure is alive between two actions: a slow rise and fall, too small to be read as movement, that keeps
the field from looking like a picture. It must never compete with an action, and it is gone when a figure falls.

### Today

Filmed at `7ddc6a9`: on every sheet, from 1680 ms to 2640 ms, the frames are the same but for the halos. Between
actions the creatures stand still (`battle/src/scene.ts`, `update`: "Creatures stand still between actions"), as
`docs/art/battle-polish.md` asked. The owner asked for the breath back (question 4 of the plan).

### Beats

The breath is not an action's: it plays in every window, on every figure no action moves.

| At | Lasts | Window | Layer | Beat | Pattern |
| --- | --- | --- | --- | --- | --- |
| 0 | 840 | windup | every standing figure | Each figure the action does not move breathes: its height rises and falls by 1.5% from top to bottom (±0.75% about its own scale, *house*), about the feet, once every 3600 ms (*house*), a sine of the scene's ambient clock. A figure a tween holds does not breathe. | [Breath at rest](../battle-craft.md#breath-at-rest) |
| 840 | 1700 | impact | every standing figure | The same. A figure starts again once the last tween that held it is done (`Phaser.Tweens.TweenManager#isTweening`), from where its sine stands: a jump under 1 px. A fallen figure never breathes. | [Breath at rest](../battle-craft.md#breath-at-rest) |
| 2540 | 500 | settle | every standing figure | Every standing figure breathes, the caster and the targets included. | [Breath at rest](../battle-craft.md#breath-at-rest) |

- Out of phase: each figure's breath starts at its own place in the cycle, the figure's place in the roster times
  0.37 of a cycle (*house*), so no two of six figures rise together. The same figures give the same breath on every
  film.
- Computed in `Phaser.Scene#update`, on the clock the halos, the motes and the auras already read (`ambient`), as the
  book asks: not a tween, so `killAll` does not end it and a skip does not stop it.
- It plays at the resting rate: since #842 a still field is drawn one frame in four, about 15 a second, and the
  breath is not counted as movement in `busy()`. On a phone it is ±1 px and moves at most 0.12 px a frame, which
  the eye cannot tell from 60 frames a second. Counting it as movement would keep every field at 60 frames a second
  and undo #842 for a pixel.

### Reduced motion

Off: every figure stands at its own scale, as today.

### Cost

- Objects: none. One sine per standing figure per drawn frame, applied to its scale. The mutation cues follow the
  image's scale, so they breathe with it.
- The resting loop: nothing added to `busy()`. A field at rest stays at 15 frames a second.
- The figure's own scale (from `figureGeometry`, set in `arrange`) is kept on the figure, and the breath multiplies
  it: a resize does not compound it.
- The cancel path: the scale back to the figure's own. The next drawn frame breathes again.
- Built by BC5, with the amendment of `docs/art/battle-polish.md`, which says creatures stand still.

### On the film strip

Filmed at 390 by 844, 1×, a1 casting on a2, so a3, b1, b2 and b3 stand apart from the action:

```bash
node scripts/battle/filmstrip.mjs spell:adrenaline_tonic:v1 --actor a1 --targets a2 \
  --frames 16 --interval 240 --columns 4 --scale 2 --out <folder>/breath.png
```

- At 0 and 3600 ms each standing figure's top is at one height (the period); between them it reaches a height
  about 1 px away (2 px on the sheet at `--scale 2`), and its feet do not move. Its height scale stays within
  0.9925 and 1.0075 of its own.
- b1 and b2 are not at the top of their breath at the same frame.
- From 2640 ms the sheet is not five identical frames.

A sheet cannot show ±1 px reliably. With `--motion` the bench writes, beside the sheet, each figure's height scale at each
frame (one line a frame, the figures by id): the critique then checks the period, the amplitude, the phases, that a1
and a2 do not breathe while their tweens play, and that a fallen figure (`--defeat`) holds still.

### Left

- The breath plays at the resting rate, for the reason above. If the owner sees it step on a phone, BC10 measures
  what counting it as movement costs.
- 1.5% of height and 3600 ms are the book's figures; on a phone that is a pixel, which may be too little to see. The
  film decides; doubling it would still be under 2 px.
- Only the height breathes, as the book says; the width does not narrow with it.
- The breath stops only while a tween holds a figure, not for the whole action: a figure the action never moves
  breathes through it.

## The camera

### Intent

The view trembles in proportion to the harm done: a jab is felt rather than seen, a killing blow is seen. One value,
trauma, replaces the two fixed shakes, so two hits in one action add up instead of fighting, and a blood cost, a heal
or a ward never moves the view.

### Today

Read in `battle/src/scene.ts` and `battle/src/fx/shake.ts`, at `7ddc6a9`, since a sheet cannot show a shake:

- A physical spell whose contact struck someone: `Phaser.Cameras.Scene2D.Camera#shake`, 240 ms at 0.0015 (about
  0.6 px across and 1.3 px up and down on a 390 by 844 view), at a fixed strength, whatever the damage.
- An action the host served no `visual` for: 320 ms at 0.003, with the hand-drawn sparks.
- The kit's `shake` beat: a camera shake of `lasts` (120 ms by default) at `strength` × 0.01 of the view, decaying
  squared. A staging that shakes replaces the physical tremor.
- Every one of them draws from `Math.random`, which the bench pins for a film.

### Beats

The rows are the heaviest case: a defeat, which reaches the ceiling.

| At | Lasts | Window | Layer | Beat | Pattern |
| --- | --- | --- | --- | --- | --- |
| 840 | 288 | impact | the camera | Trauma rises at contact by the sum of what the harmed figures add, to a ceiling of 0.6 (*house*), and falls by 0.25 every 120 ms (*house*): from 0.6, still by 1128 ms. The view's offset is 5% of its shorter side (*house*; 2.5% until the owner asked for more tremor, 2026-11-19) × trauma² × a smooth wander on two axes, two sines an axis of the action's clock (`wander`, `table/battle-motion.js`). No rotation. | [Shake from trauma](../battle-craft.md#shake-from-trauma) |
| 840 | 200 | impact | the camera | Punch zoom on a defeat or a critical hit only: 3% in, back over 200 ms. | [Punch zoom](../battle-craft.md#punch-zoom) |
| 1128 | 1412 | impact | the camera | Still. Nothing bright starts after 960 ms, and nothing bright remains after 1160 ms. | [The hold before the number](../battle-craft.md#the-hold-before-the-number) |

What each harmed figure adds, by the health it lost: light 0.25, medium 0.40, heavy 0.55; a defeat adds 0.10 more.
Squared, the shake is 6%, 16%, 30% and, at the ceiling, 36% of its full reach: on a phone 390 px wide, about 1.2 px,
3.1 px, 5.9 px and 7.0 px (`TRAUMA.reach` 0.05 in `table/battle-motion.js`). The light one is twice the old physical
tremor across. Still by 120 ms, 192 ms, 264 ms and 288 ms after
it rose.

- What adds: a recipient other than the caster that lost health, in any family and any delivery. A blood cost, a
  heal, a ward, a figure struck with no loss, and a fizzle add nothing.
- The kit's `shake` beat adds its `strength` to the same trauma at its `at`, under the same ceiling. It does not start
  a shake of its own: the decay is the trauma's, so its `lasts` no longer sets how long the view moves. A staging
  shakes more than its damage, never less: the damage's share is always there.
- The punch zoom plays once an action, however many fall.

The rule of the bright beats. [The craft book](../battle-craft.md) says the flash, the burst and the shake end within
120 ms of contact. The kit gave its burst 320 ms, because a 120 ms burst is two frames and reads as a flicker (BC4).
The kit's number stands. What the hold needs is that nothing bright starts after contact has passed, and that what is
bright fades from its first frame: the flash is 80 ms, the burst's particles fade as they fly, the trauma falls from
its peak. So every bright beat starts within 120 ms of contact and is gone within 320 ms of it, and the camera's
ceiling (288 ms) fits inside that.

### Reduced motion

Nothing. The camera does not move or zoom; trauma is never added.

### Cost

- Objects: none. One number, and an offset and a zoom on the camera.
- Computed in `Phaser.Scene#update` from the scene's time, not from `Math.random`: two films of one hit give the same
  frames without pinning anything, and the shake is no longer `Phaser.Cameras.Scene2D.Camera#shake`.
- `busy()` counts trauma above 0 and a punch of the zoom still easing back, so the camera plays
  at the full rate.
- The cancel path gains: trauma set to 0 and the camera centred again as `fitCamera` places it; the zoom set back to the
  fitted zoom, which is not 1: `fitCamera` zooms by the ratio of the canvas's pixels to its CSS size. The punch is
  laid over the fitted zoom from `update`, not a zoom effect, so there is no effect to reset.
- The offset is applied on top of the fitted scroll, never written into it: a resize during a shake does not keep it.
- Built by BC5: the trauma, its decay and noise, what each loss adds, the kit's `shake` beat changed to add to it,
  the punch zoom. The two fixed shakes in `scene.ts` go.

### On the film strip

A sheet cannot show a tremor of a few pixels, nor tell it from the figures' own motion. With `--motion` the bench writes
beside the sheet, at each frame, the trauma, the camera's offset in pixels and its zoom. Then, with the films of
"A melee blow" and "A death":

```bash
node scripts/battle/filmstrip.mjs spell:heavy_strike:v1 --actor a1 --targets b1 --damage 3 \
  --frames 24 --interval 60 --columns 6 --out <folder>/camera-light.png
node scripts/battle/filmstrip.mjs spell:pummel:v1 --actor a1 --targets b1 --damage 8 --defeat \
  --frames 24 --interval 60 --columns 6 --out <folder>/camera-defeat.png
node scripts/battle/filmstrip.mjs spell:bone_ward:v1 --actor a1 --targets a1 --wound \
  --frames 24 --interval 60 --columns 6 --out <folder>/camera-blood.png
```

- Light: trauma 0.25 at 840 ms, 0 at 960 ms; offset under 1.5 px at every frame; zoom unchanged.
- Defeat: trauma 0.6 at 840 ms, about 0.35 at 960 ms, 0 by 1140 ms; offset under 8 px (on a phone at most 7 px; on the bench, 2.6 px at 880 ms); zoom 3% above its fitted
  value at 840 ms, back by 1080 ms.
- Blood cost (Bonewall, the caster's own ward paid in blood): trauma 0 and offset 0 at every frame.
- Every sheet, from 1200 ms: the camera exactly where it was at 0 ms.

### Left

- No rotation. The book allows an angle; on a phone the page's own labels sit over the canvas and would not turn
  with it, and an angle is what the reduced-motion guidance names first. The offset alone, a few pixels, keeps them
  close enough.
- Every family shakes by damage, not only the physical one. Damage is the weight; a heavy fireball should be felt as
  a heavy paw. `docs/art/spell-vfx.md` now says that health lost by another creature, in any family, moves the
  view. Settled by the owner, 2026-11-19: kept.
- The kit's `shake` beat changes meaning: its `strength` is trauma added, its `lasts` is no longer used. ADR 0196
  names the `shake` beat and no more, so it stands; `battle/README.md` says what the beat does now.
- The scale of the offset is 5% of the shorter side (doubled from 2.5% on the owner's word, 2026-11-19, for more
  tremor; the breath is unchanged), so a desktop shakes more pixels than a phone, the same share.
- The book's rule changed: under "The hold before the number", **Here**, the bright beats now start within 120 ms
  of contact and are gone within 320 ms of it.
