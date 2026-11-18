# Storyboards: the base actions

The actions every creature has, whatever its packages: the base attack, Strike, and Focus, which BC6 adds here. They
are seen nearly every round, so they are the quietest stagings in the game, and a family spell must be able to look
heavier than them.

## Strike (`spell:heavy_strike:v1`)

### Intent

The player reads "this creature hit that one, once, plainly": who struck, who took it, and nothing more. Strike costs
2 energy and deals 3 damage to one enemy, and most creatures use it most rounds, so it takes the least attention of
any blow. It has no light, no camera move the eye can follow, nothing that outlives the contact, and the field is
empty again by 1540 ms. It stays a physical blow: the `impact` motif is its primary element.

### Today

Filmed at `4d51935`, 390 by 844, 1×, a1 on b1: one sheet of 12 frames every 240 ms, one of 24 frames every 60 ms
from 0 to 1380 ms.

- 0 to 180 ms: two pale arcs on the caster's body, the windup's charge. They grow and fade, and are gone by 240 ms.
  A melee blow shows the gather a cast uses.
- 0 to 240 ms: the draw back, 14 px and 7°, not eased. Barely visible: the caster hardly leaves its ground ring.
- 240 to 840 ms: the strike, eased `Cubic.In`. At 540 ms the caster has covered about an eighth of the way, at
  720 ms about half. So half the travel happens in the last 120 ms, not nearly all of it after 720 ms.
- 840 ms: the caster at its closest. Its silhouette covers b1's feet and lower body; b1's head stays clear. No motif
  is on the frame yet.
- 900 to 1020 ms: three pale strokes and a thin ring over b1's body, in the physical family's pale taupe. Low in
  contrast on b1's grey hide. The strokes lean the same way whatever the direction of the blow.
- 960 ms: the caster still sits over b1's lower half. Its return starts here.
- 840 to 1400 ms: b1's recoil, 12 px and 4°, and a 240 ms camera shake at 0.0015. Neither reads on the sheets.
- About 1320 ms: the caster nearly home; it arrives at 1680 ms. The strokes fade from 1040 ms and are no longer seen
  by about 1500 ms, although their object lives until 2040 ms.
- There is no flash, no hit stop and no particle.
- 1680 to 2640 ms: nothing moves. The frames at 1680, 1920, 2160, 2400 and 2640 ms are the same.

### Beats

| At | Lasts | Window | Layer | Beat | Pattern |
| --- | --- | --- | --- | --- | --- |
| 0 | 240 | windup | the caster | Draw back 20 px from the target and lean back 8° (*house*; today 14 px and 7°), eased `Cubic.Out`. No mark on the caster's body. | [Anticipation](../battle-craft.md#anticipation), [Easing families](../battle-craft.md#easing-families) |
| 240 | 600 | windup | the caster | The strike to the approach point, as today: eased `Cubic.In`, leaning 9° into the blow. | [Contact and follow-through](../battle-craft.md#contact-and-follow-through) |
| 720 | 120 | windup | the caster | Stretch along the line of travel, 5% (*house*), about the feet. | [Squash and stretch](../battle-craft.md#squash-and-stretch) |
| 840 | 60 | impact | the caster and the target | Hit stop, 60 ms (*house*, the low end of the book's range). Both figures hold; the target trembles 2 px across the blow (*house*). The effect layer plays on. | [Hit stop](../battle-craft.md#hit-stop) |
| 840 | 80 | impact | the target | Hit flash: the target filled flat in bone (`--bone`), 80 ms (*house*), then its tint cleared. The action's only flash. | [Hit flash](../battle-craft.md#hit-flash), [Flash limits](../battle-craft.md#flash-limits) |
| 840 | 100 | impact | the effect | The motif arrives on the target's body: three strokes and a ring, from 85% scale and 35% alpha, eased `Cubic.Out`. Each stroke over a dark edge of 1.5 px (*house*). | [Shape before colour](../battle-craft.md#shape-before-colour) |
| 840 | 100 | impact | the effect | The strokes lean along the line from caster to target, not at a fixed angle. | [An impact that keeps the direction](../battle-craft.md#an-impact-that-keeps-the-direction) |
| 840 | 120 | impact | the effect | Burst: 8 chips (*house*), tinted bone, normal blend, in ±25° along the blow beyond the target, gone in 120 ms. | [Burst](../battle-craft.md#burst), [An impact that keeps the direction](../battle-craft.md#an-impact-that-keeps-the-direction) |
| 840 | 120 | impact | the camera | Trauma rises by 0.25 (*house*), squared, back to 0 by 960 ms. A tremor felt rather than seen. | [Shake from trauma](../battle-craft.md#shake-from-trauma) |
| 900 | 80 | impact | the caster | The stretch released to rest, eased `Quad.Out`. | [Squash and stretch](../battle-craft.md#squash-and-stretch) |
| 900 | 560 | impact | the target | Recoil after the stop: 12 px and 4° along the line of the blow, 280 ms out eased `Quad.Out`, 280 ms back, as today. | [Recoil](../battle-craft.md#recoil) |
| 940 | 600 | impact | the effect | The motif fades to nothing, eased `Quad.Out`, growing to 125%. Destroyed at 1540 ms. | [The three parts of an effect](../battle-craft.md#the-three-parts-of-an-effect), [The hold before the number](../battle-craft.md#the-hold-before-the-number) |
| 960 | 720 | impact | the caster | The return home, eased `Back.Out` with an overshoot of 1.2 (*house*): past home by a few pixels, then still. | [Contact and follow-through](../battle-craft.md#contact-and-follow-through), [Easing families](../battle-craft.md#easing-families) |
| 1680 | 860 | impact | the field | Nothing new. Both figures at home, moved only by their breath at rest. The result is read here. | [The hold before the number](../battle-craft.md#the-hold-before-the-number), [Breath at rest](../battle-craft.md#breath-at-rest) |

Nothing is staged in the settle. One flash, on the target, and no change of light: the ground does not answer a base
action.

### Reduced motion

Nothing. The frame after the action is shown at once for 650 ms with the result text, as today: no draw back, no
strike, no flash, no motif, no chip, no shake.

### Cost

- Objects at the peak (840 to 940 ms): one `Graphics` for the motif (strokes, edges and ring in one drawing), one
  emitter with 8 particles, and the tweens of the caster, the target and the motif. The windup's charge is gone, so
  the windup draws one object fewer than today.
- Draw calls: the motif batches with the other vectors; the emitter is one draw call, its own on a phone. The flash
  (a tint), the hit stop and the shake draw nothing. One draw call more than today, during 120 ms.
- Particles at the peak: 8, of the scene's 150 (*house*). Each is a few pixels: no budget of pixels is at stake.
- Bytes: not measured. The engine features are already in the bundle; the calling code is BC4's and BC5's to weigh.
- The cancel path gains: the hit stop resumes the paused tweens and removes its timer, the tint is cleared, the
  trauma is set to 0, the emitter's particles are killed. A skip during the flash must not leave a bone figure.
- Which stage builds what:
  - BC4: the burst primitive, with a direction and a spread; the chip texture drawn at boot; the `staging` field.
  - BC5: the draw back scaled to the weight of the blow, the stretch, the hit stop, the hit flash, the trauma shake,
    the recoil after the stop, the `Back.Out` return, and the breath at rest.
  - BC6: Strike's staging: no gather on the caster, the motif leaning along the blow with its dark edge, the burst
    placed, the motif's shorter fade. Every beat waits for BC6 to be placed; the beats above wait for BC4 or BC5 to
    exist.

### On the film strip

Filmed at 390 by 844, 1×, a1 on b1, from the repository with the bundle built, the sheets written outside it:

```bash
node scripts/battle/filmstrip.mjs spell:heavy_strike:v1 --actor a1 --targets b1 --out <folder>/strike.png
node scripts/battle/filmstrip.mjs spell:heavy_strike:v1 --actor a1 --targets b1 \
  --frames 24 --interval 60 --columns 6 --out <folder>/strike-contact.png
```

- 0 ms: no mark on the caster's body, only its halo.
- 240 ms: the caster at the far end of its draw back, plainly off its ground ring on the side away from b1, leaning
  back.
- 480 ms: the caster still close to its draw back, less than a tenth of the way to b1.
- 720 ms: the caster about halfway to b1.
- 840 or 900 ms: the caster's silhouette touches b1's and does not cover its head. b1 is filled flat in bone in one
  of these two frames and in no other frame of either sheet.
- 900 ms: the three strokes lean along the line from a1 to b1, each with its dark edge, and read against b1's hide.
  The chips are on the far side of b1 from a1, none on a1's side. Nothing in the effect is gold.
- 960 ms: b1 is no longer filled and no chip is left.
- 1080 ms: the strokes at about half their brightness. The caster has left b1.
- 1140 or 1200 ms: b1 at the far end of its recoil, off its home along the line from a1, away from a1.
- 1380 ms: the strokes all but gone.
- 1440 ms: b1 at its home.
- 1680 ms: no effect object on the field. The caster at its home.
- 1920 to 2640 ms: the same frame as 1680 ms, but for the breath.

The sheets cannot show the 5% stretch at 720 ms, the 2 px tremor, the shake, the overshoot of the return, or the
feel at full speed: do not judge them from a sheet.

### Left

- The windup's gather is removed from Strike. A melee blow loads in the body, and a gather on the caster says
  "cast". The owner may want it kept; the other 18 melee spells decide the same question in BC6.
- The strokes lean along the blow. The `impact` motif is shared with 7 other spells, so BC6 decides whether this is
  Strike's staging or the motif's. I propose the motif's: every melee blow has a direction.
- The dark edge under the strokes is a house number; the film strip tunes it. Raising the ring's alpha instead was
  set aside, because the ring is the motif's secondary shape and should stay lower.
- The 8 chips may be refused as too much for an action seen every round. Without them Strike still reads, by the
  flash and the motif.
- The three strokes do not appear one after another. A stagger would make one blow read as a flurry of three.
- From 840 to 960 ms the caster covers b1's lower half. That is today's approach (`table/battle-layout.js`), not
  this storyboard's; it is acceptable while b1's head and the motif stay clear.
- A Strike that defeats its target is staged by BC5's death, not here. Strike has no critical chance, so no punch
  zoom.
- An applied zero-damage outcome still records contact (ADR 0131). Whether the hit flash plays when nothing was
  lost is not settled: the flash says "this one took damage".
- At 2× the hit stop is 30 ms, under two frames at 60 frames a second. A film at 2× says whether it still reads.
- Frame time is not measured; BC10 measures it.
