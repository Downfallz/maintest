# Storyboards: the base actions

The actions every creature has, whatever its packages: the base attack, Strike, and Focus, both staged by BC6. They
are seen nearly every round, so they sit in the ladder's Base action band ([README](README.md#the-ladder)): the
quietest stagings in the game, which every family spell must be able to look heavier than. A row whose **Beat** ends
in a coded beat (`` `burst at 840, ...` ``) is a beat of the spell's `staging` in `data/presentation/spell-art.json`;
the other rows are the body's movement ([movement.md](movement.md)) or the hold, and carry no code.

## Strike (`spell:heavy_strike:v1`)

### Intent

The player reads "this creature hit that one, once, plainly": who struck, who took it, and nothing more. Strike costs
2 energy and deals 3 damage to one enemy, and most creatures use it most rounds, so it takes the least attention of
any blow. It has no light, no camera move the eye can follow, nothing that outlives the contact, and the field is
empty again by 1540 ms. It stays a physical blow: the `impact` motif is its primary element.

### Today

Filmed at `15ee3b0`, after BC5, 390 by 844, 1×, a1 on b1, no health change given (so no flash and no recoil):

- 0 to 180 ms: a pale arc on a1's body, the shared charge of a cast, on a melee blow. Gone by 240 ms.
- 0 to 840 ms: BC5's draw back and strike; at 840 ms a1 covers b1's feet and lower body, b1's head clear.
- 900 to 1380 ms and on: three pale strokes and a ring on b1, upright whatever the line of the blow, with no edge,
  low against b1's grey hide; still seen at 1380 ms, and alive until 2040 ms. No particle.
- About 1320 ms: a1 nearly home.

### Beats

| At | Lasts | Window | Layer | Beat | Pattern |
| --- | --- | --- | --- | --- | --- |
| 0 | 240 | windup | the caster | Draw back 20 px from the target and lean back 8° (*house*; today 14 px and 7°), eased `Cubic.Out`. No mark on the caster's body. | [Anticipation](../battle-craft.md#anticipation), [Easing families](../battle-craft.md#easing-families) |
| 240 | 600 | windup | the caster | The strike to the approach point, as today: eased `Cubic.In`, leaning 9° into the blow. | [Contact and follow-through](../battle-craft.md#contact-and-follow-through) |
| 720 | 120 | windup | the caster | Stretch along the line of travel, 5% (*house*), about the feet. | [Squash and stretch](../battle-craft.md#squash-and-stretch) |
| 840 | 60 | impact | the caster and the target | Hit stop, 60 ms (*house*, the low end of the book's range). Both figures hold; the target trembles 2 px across the blow (*house*). The effect layer plays on. | [Hit stop](../battle-craft.md#hit-stop) |
| 840 | 80 | impact | the target | Hit flash: the target filled flat in bone (`--bone`), 80 ms (*house*), then its tint cleared. The action's only flash. | [Hit flash](../battle-craft.md#hit-flash), [Flash limits](../battle-craft.md#flash-limits) |
| 840 | 700 | impact | the effect | The motif arrives on the target's body: three strokes and a ring, from 85% scale and 35% alpha, eased `Cubic.Out`. Each stroke over a dark edge of 1.5 px (*house*). Its whole life is 700 ms, so it is gone by 1540 ms: `motif at 840, lasts 700` | [Shape before colour](../battle-craft.md#shape-before-colour) |
| 840 | 100 | impact | the effect | The strokes lean along the line from caster to target, not at a fixed angle: the `impact` motif's own drawing, on every spell that carries it, not a beat of its own. | [An impact that keeps the direction](../battle-craft.md#an-impact-that-keeps-the-direction) |
| 840 | 240 | impact | the effect | Burst: 8 chips (*house*), bone shards, normal blend, in ±25° along the blow beyond the target, reaching 1.6 radii (*house*), gone in 120 ms: `burst at 840, count 8, shape shard, tint bone, direction along, spread 50, reach 300, lasts 240, blend normal` | [Burst](../battle-craft.md#burst), [An impact that keeps the direction](../battle-craft.md#an-impact-that-keeps-the-direction) |
| 840 | 120 | impact | the camera | Trauma rises by 0.25 (*house*), squared, back to 0 by 960 ms. A tremor felt rather than seen. | [Shake from trauma](../battle-craft.md#shake-from-trauma) |
| 900 | 80 | impact | the caster | The stretch released to rest, eased `Quad.Out`. | [Squash and stretch](../battle-craft.md#squash-and-stretch) |
| 900 | 560 | impact | the target | Recoil after the stop: 12 px and 4° along the line of the blow, 280 ms out eased `Quad.Out`, 280 ms back, as today. | [Recoil](../battle-craft.md#recoil) |
| 940 | 600 | impact | the effect | The motif fades to nothing, eased `Quad.Out`, growing to 125%. Destroyed at 1540 ms. The end of the motif beat above, not a beat of its own. | [The three parts of an effect](../battle-craft.md#the-three-parts-of-an-effect), [The hold before the number](../battle-craft.md#the-hold-before-the-number) |
| 960 | 720 | impact | the caster | The return home, eased `Back.Out` with an overshoot of 1.2 (*house*): past home by a few pixels, then still. | [Contact and follow-through](../battle-craft.md#contact-and-follow-through), [Easing families](../battle-craft.md#easing-families) |
| 1680 | 860 | impact | the field | Nothing new. Both figures at home, moved only by their breath at rest. The result is read here. | [The hold before the number](../battle-craft.md#the-hold-before-the-number), [Breath at rest](../battle-craft.md#breath-at-rest) |

Nothing is staged in the settle. One flash, on the target, and no change of light: the ground does not answer a base
action. The staging is two beats, the motif and the chips: 8 particles on the target, nothing in the windup, no
glow, no staged shake (the trauma above is the body's, by the health lost), the last beat gone by 1540 ms, inside
the Base action band.

### Reduced motion

Nothing. The frame after the action is shown at once for 650 ms with the result text, as today: no draw back, no
strike, no flash, no motif, no chip, no shake.

### Cost

- Objects at the peak (840 to 940 ms): one `Graphics` for the motif (strokes, edges and ring in one drawing), one
  emitter with 8 particles, and the tweens of the caster, the target and the motif. The windup's charge is gone, so
  the windup draws one object fewer than today.
- Draw calls: the motif batches with the other vectors; the emitter is one draw call, its own on a phone. The flash
  (a tint), the hit stop and the shake draw nothing. One draw call more than today, during 240 ms.
- Particles at the peak: 8, of the scene's 150 (*house*). Each is a few pixels: no budget of pixels is at stake.
- Bytes: not measured. The engine features are already in the bundle; the calling code is BC4's and BC5's to weigh.
- The cancel path gains: the hit stop resumes the paused tweens and removes its timer, the tint is cleared, the
  trauma is set to 0, the emitter's particles are killed. A skip during the flash must not leave a bone figure.
- Which stage builds what:
  - BC4: the burst primitive, with a direction and a spread; the chip texture drawn at boot; the `staging` field.
  - BC5: the draw back scaled to the weight of the blow, the stretch, the hit stop, the hit flash, the trauma shake,
    the recoil after the stop, the `Back.Out` return, and the breath at rest.
  - BC6: Strike's staging, the two coded beats above: no gather on the caster (a staged spell shows no shared
    charge), the `impact` motif leaning along the blow on its dark edge, the burst placed, the motif's 700 ms life.
    Each beat plays on the BC4 or BC5 primitive named above.

### On the film strip

Filmed at 390 by 844, 1×, a1 on b1, from the repository with the bundle built, the sheets written outside it. b1
loses Strike's 3 health, because the flash and the recoil below answer only a loss:

```bash
node scripts/battle/filmstrip.mjs spell:heavy_strike:v1 --actor a1 --targets b1 --damage 3 --out <folder>/strike.png
node scripts/battle/filmstrip.mjs spell:heavy_strike:v1 --actor a1 --targets b1 --damage 3 \
  --frames 24 --interval 60 --columns 6 --out <folder>/strike-contact.png
```

- 0 ms: no mark on the caster's body, only its halo.
- 240 ms: the caster at the far end of its draw back, plainly off its ground ring on the side away from b1, leaning
  back.
- 480 ms: the caster still close to its draw back, less than a tenth of the way to b1.
- 720 ms: the caster about halfway to b1.
- 840 and 900 ms: the caster's silhouette touches b1's and does not cover its head. b1 is filled flat in bone in
  these two frames and in no other frame of either sheet.
- 900 ms: the three strokes lean along the line from a1 to b1, each with its dark edge, and read against b1's hide.
  The chips are on the far side of b1 from a1, none on a1's side. Nothing in the effect is gold.
- 960 ms: b1 is no longer filled; the chips above b1's head, clear of its outline.
- 1080 ms: no chip left; the strokes still near full. The caster has left b1.
- 1200 ms: the strokes at about half their brightness.
- 1140 or 1200 ms: b1 at the far end of its recoil, off its home along the line from a1, away from a1.
- 1380 ms: the strokes all but gone.
- 1500 ms: b1 at its home.
- 1680 ms: no effect object on the field. The caster at its home.
- 1920 to 2640 ms: the same frame as 1680 ms, but for the breath.

The sheets cannot show the 5% stretch at 720 ms, the 2 px tremor, the shake, the overshoot of the return, or the
feel at full speed: do not judge them from a sheet.

### Left

- The four choices below are built as proposed; the owner approves or refuses on the sheets.
  - No gather in Strike's windup. A melee blow loads in the body, and a gather on the caster says "cast". The other
    melee spells take the same answer unless their own storyboard stages a windup beat.
  - The strokes lean along the blow for the whole `impact` and `slash` motif, not for Strike alone: every melee blow
    has a direction.
  - The dark edge under the strokes, 1.5 px (*house*); the film strip tunes it. Raising the ring's alpha instead was
    set aside, because the ring is the motif's secondary shape and should stay lower.
  - The 8 chips. They may be refused as too much for an action seen every round; without them Strike still reads,
    by the flash and the motif, and its staging is one beat.
- The trauma row is the body's (BC5) and stays out of the staging: the Base action band allows no staged shake.
- The three strokes do not appear one after another. A stagger would make one blow read as a flurry of three.
- From 840 to 960 ms the caster covers b1's lower half. That is today's approach (`table/battle-layout.js`), not
  this storyboard's; it is acceptable while b1's head and the motif stay clear.
- A Strike that defeats its target is staged by BC5's death, not here. Strike has no critical chance, so no punch
  zoom.
- An applied zero-damage outcome still records contact (ADR 0131). Whether the hit flash plays when nothing was
  lost is not settled: the flash says "this one took damage".
- At 2× the hit stop is 30 ms, under two frames at 60 frames a second. A film at 2× says whether it still reads.
- Frame time is not measured; BC10 measures it.

## Focus (`spell:wait:v1`)

### Intent

The player reads "this creature gathered itself" and nothing else: no one was struck, nothing left it. Focus costs
nothing and gives its caster 2 energy, it targets the caster alone, and it is cast nearly every round, so it is the
quietest action in the game, quieter than Strike: no flash, no light, no shake, nothing that leaves the figure, the
field empty again by 1540 ms. Its motion goes inward, into the caster, where every other spell's goes out. Its
primary element is the neutral `focus` motif, the small ellipse and its vertical stroke.

### Today

Filmed at `15ee3b0`, 390 by 844, 1×, a1 on itself:

- 0 to 180 ms: a pale arc on a1's body, the shared charge of a cast. Gone by 240 ms.
- 240 to 840 ms: nothing seen; the cast's load (4% squash, then rise) does not read on the sheets.
- 900 to about 1440 ms: the ellipse and its stroke on a1's chest, pale grey, small, arriving and then fading
  outward. Gone from the frame by 1680 ms. No particle, no flash.

### Beats

| At | Lasts | Window | Layer | Beat | Pattern |
| --- | --- | --- | --- | --- | --- |
| 0 | 840 | windup | the caster | The body loads and rises as every cast does ([movement.md](movement.md), "A cast and a projectile"): a 4% squash, then a 4% stretch upward at contact. No mark, no arc, no particle on the caster. | [Anticipation](../battle-craft.md#anticipation), [Squash and stretch](../battle-craft.md#squash-and-stretch) |
| 840 | 360 | impact | the caster | The release to rest, as every cast. The caster lost no health: no flash, no recoil, no trauma. | [Contact and follow-through](../battle-craft.md#contact-and-follow-through) |
| 840 | 300 | impact | the effect | Gather: six soft grey-blue dots (*house*) start on a ring at 1.3 radii from a1's centre (*house*) and land on it at 1140 ms, brightening as they arrive. Screen blend, so they do not blow out on the hide: `gather at 840, on caster, count 6, shape dot, tint family, reach 220, lasts 300, blend screen` | [Gravity well and convergence](../battle-craft.md#gravity-well-and-convergence), [Additive light](../battle-craft.md#additive-light) |
| 1080 | 460 | impact | the effect | The motif settles where the dots land: the ellipse and its vertical stroke on a1's body, arriving over a third of its life, then fading. Destroyed at 1540 ms: `motif at 1080, lasts 460` | [Shape before colour](../battle-craft.md#shape-before-colour), [The three parts of an effect](../battle-craft.md#the-three-parts-of-an-effect) |
| 1540 | 1000 | impact | the field | Nothing. a1 at rest, moved by its breath only. The result, the energy gained, is read here. | [The hold before the number](../battle-craft.md#the-hold-before-the-number), [Breath at rest](../battle-craft.md#breath-at-rest) |

The staging is two beats, the gather and the motif: 6 particles, nothing in the windup, no glow, no shake, no
flash, gone by 1540 ms, inside the Base action band and under Strike's 8 chips. No flash at all and no change of
light. I put the dots before the mark, not with it, so the eye follows them in and finds the mark where they land:
that order is the spell's one signature.

### Reduced motion

Nothing. The frame after the action is shown at once for 650 ms with the result text: no load, no dot, no mark.

### Cost

- Objects at the peak (1080 to 1140 ms): one emitter of 6 particles and one `Graphics` for the motif, with their
  tweens and the caster's.
- Draw calls: the emitter is one, drawn with the lit objects; the motif batches with the vectors. One more than
  today, during 300 ms.
- Particles at the peak: 6, of the scene's 150 (*house*).
- Bytes: not measured. Nothing new is drawn at boot: the dot is a frame of the kit's texture.
- The cancel path: the kit's `clear` kills the emitter and the motif's tweens; a skip during the gather leaves no
  dot and a1 at rest.
- Which stage builds what: BC4 the `gather` primitive and the kit's texture, BC5 the cast's load and release, BC6
  this staging.

### On the film strip

Filmed at 390 by 844, 1×, a1 casting on itself, the sheets written outside the repository:

```bash
node scripts/battle/filmstrip.mjs spell:wait:v1 --actor a1 --targets a1 --out <folder>/focus.png
node scripts/battle/filmstrip.mjs spell:wait:v1 --actor a1 --targets a1 \
  --frames 24 --interval 60 --columns 6 --out <folder>/focus-contact.png
```

- 0 to 780 ms: no arc, mark or particle on a1, only its halo.
- 900 and 960 ms: faint grey-blue dots around a1, none farther than about 1.3 radii from its centre, none on its
  middle; no mark yet.
- 1080 ms: the dots nearer a1's centre than at 960 ms, and brighter.
- 1140 or 1200 ms: no dot left; the ellipse and its stroke on a1's chest, at full strength.
- 1380 ms: the mark fainter than at 1200 ms.
- 1680 ms: no effect object on the field.
- Every frame: a1 never filled flat in any colour; nothing on a2, a3 or the other side; nothing in the effect gold;
  no frame brighter around a1 than Strike's 900 ms frame is around b1.

The sheets cannot show the load's squash, the feel at full speed or the sound: do not judge them from a sheet.

### Left

- The motif fades growing to 135% (the shared envelope in `battle/src/spell-effects.ts`), outward, against the
  spell's inward reading. Shrinking it as it fades (to about 80%, *house*) would finish the gesture, but the size of
  a motif's fade is not a staging parameter: a change to the `focus` motif's envelope, in its own change, if the
  owner wants it. Until then the dots carry the inward reading.
- The gather plays after contact, not in the windup: the Base action band allows no windup beat, and the body's
  load already says "about to cast".
- Six dots, not eight: Focus must stay under Strike. Screen, not add, so they stay grey-blue on the hide; the film
  strip says whether they read at all, and reach or count is tuned there.
- The craft book's "Gravity well and convergence" names only BC8's use of the kit's `gather`; Focus is the first.
  Its **Here** line could say so, in the book's own change.
- The energy gained is the result text's to say, not an effect's.
- Frame time is not measured; BC10 measures it.
