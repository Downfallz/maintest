# Storyboards: the elemental family

The Wizard's line of the Sorcerer, drawn on the Warped body (fractured skull, occult fissures) and its elemental
branch, Stormborn then Cataclysm: heat and cold, steam and pale-blue crystal ([the vocabulary of the
family](../spell-vfx.md), [the approved executions](../creature-art-direction.md)), on the family's pale ice blue
(0x8dcfe5); the `fire` motif and the `ember` tint draw in its warm 0xd58b75. Its five spells climb [the
ladder](README.md#the-ladder): Stage I (Shock), Stage II (Ice Grip, Emberstorm), Stage III (Incinerate, Deep
Freeze). Every one is a cast: the caster stays home, and its body loads and lets go as
[movement.md](movement.md) says; a storyboard names that in one row per window and spends its rows on the spell.

The family keeps one meaning per look ([One effect, one meaning](../battle-craft.md#one-effect-one-meaning)), and the
motif stays the primary element. Pointed sparks in the family's blue, quick and short-lived, are electricity: only
Shock throws them. Anything in `ember` is fire, on whatever figure it is: sparks thrown out are flame bursting,
motes rising slowly are heat and cinders. Angular shards in the family's blue are ice. How the ice moves says what
the cold does: shards that spring up from the ground around the legs, with a pale frost clinging low on the body,
are a grip that slows (Ice Grip, initiative lowered); shards that close in from all around onto the body are a
freeze, and the tight blue ring that then lingers at the figure's feet is the stun's pin, the same pin as Paralyzing
Barb's in [biological.md](biological.md): a tight ring at the feet means a stun in every family. A spell that does
not stun draws no ring at the feet. Deep red beads (`blood`) are a Bleed, as in every family, even when the fire
caused it (Emberstorm). No halo (`pulse`): a halo says "healed", and no spell here heals.

The two spells of Cataclysm are told apart by shape before colour: Incinerate is fire that bursts outward from its
target, Deep Freeze is cold that closes in on it and holds. Stormborn's two are told apart the same way: Ice Grip
grasps from the ground, Emberstorm flares on three figures at once.

## Shock (`spell:lightning_bolt:v1`)

### Intent

The player reads "a bolt jumped from the caster's cracked skull to that one": one jagged blue arc and a crackle on
the struck body. Shock costs little and deals 4 damage to one enemy; Stage I, cast often, so it stays short and
light: the arc is the whole signature, a spray of sparks at both of its ends dresses it, nothing in the windup.

### Today

Filmed at `fc3ec94`, 1×, a1 on b1, 4 health lost: a whole action and a contact.

- 0 to 240 ms (contact): a pale blue charge circle on a1's body, gone by 300 ms. a1 does not move.
- 840 and 900 ms: b1 filled flat in bone; the arc already drawn from a1 to b1, a small circle at b1's head.
- 960 to 1380 ms: a jagged pale blue arc from a1's centre to b1's head, steady. 1440 ms (whole): thinner; 1680 ms: a
  faint ghost of it; 1920 ms: gone.
- No particle on either end. Nothing comes from a1's skull; b1 does not visibly flinch.

### Beats

| At | Lasts | Window | Layer | Beat | Pattern |
| --- | --- | --- | --- | --- | --- |
| 0 | 840 | windup | the caster | The cast load ([movement.md](movement.md)): a1 squashes and rises where it stands. No staged beat: Stage I has none in the windup. | [Anticipation](../battle-craft.md#anticipation) |
| 840 | 700 | impact | the effect | The `arc` motif from a1 to b1, gone by 1540 ms (shortened from today's 1680 ms to end inside the Stage I band). `motif at 840, lasts 700` | [Chain between targets](../battle-craft.md#chain-between-targets), [Shape before colour](../battle-craft.md#shape-before-colour) |
| 840 | 590 | impact | the caster and the target | a1's release, b1's medium hit from a distance ([movement.md](movement.md)): its bone flash for 80 ms, its stop, its recoil away from a1. | [Hit flash](../battle-craft.md#hit-flash), [Recoil](../battle-craft.md#recoil) |
| 840 | 240 | impact | the effect | The bolt leaves the skull: 6 blue sparks (*house*) thrown up off a1's head. `burst at 840, on caster, count 6, shape spark, tint family, direction up, spread 90, reach 160, strength 0.2, level head, lasts 240, blend add` | [Burst](../battle-craft.md#burst), [Additive light](../battle-craft.md#additive-light) |
| 840 | 280 | impact | the effect | The discharge: 12 blue sparks (*house*) sprayed off b1 all around, clearing its body. `burst at 840, count 12, shape spark, tint family, direction out, spread 360, reach 260, strength 0.25, lasts 280, blend add` | [Burst](../battle-craft.md#burst), [Additive light](../battle-craft.md#additive-light) |
| 960 | 600 | impact | the effect | The crackle: 6 small sparks (*house*) flicker out over b1's body, a few at a time, gone by 1560 ms. `flow at 960, count 6, shape spark, tint family, direction out, spread 360, reach 130, strength 0.2, lasts 600, blend add` | [Flow](../battle-craft.md#flow), [The three parts of an effect](../battle-craft.md#the-three-parts-of-an-effect) |
| 1560 | 980 | impact | the field | Nothing new. Both figures home, breathing. The result is read here. | [The hold before the number](../battle-craft.md#the-hold-before-the-number), [Breath at rest](../battle-craft.md#breath-at-rest) |

Four staged beats (the motif, two bursts, the flow), none in the windup; 18 particles asked for on b1 (12, 6) and 6
on a1; no glow, no pulse, no shake, the last gone by 1560 ms: inside Stage I. One flash, the body's, on b1; the arc
is the cast's one change of light.

### Reduced motion

Nothing. The frame after the action is shown at once with the result text: no arc, no spark.

### Cost

- Objects at the peak (840 to 1080 ms): the motif's `Graphics` and two emitters; about three draw calls. From
  1080 ms the motif and one emitter.
- Particles at the peak: about 24 at 900 ms (6, 12, the crackle's first); of the field's 150.
- Bytes: not measured. Built by BC4 (motif, burst, flow, the `staging` field) and BC5 (the cast load, the hit from a
  distance); the `level` of a beat is in the kit (`table/battle-staging.js`); BC8 places the beats. The cancel path
  is the kit's.

### On the film strip

```bash
node scripts/battle/filmstrip.mjs spell:lightning_bolt:v1 --actor a1 --targets b1 --damage 4 --out <folder>/shock.png
node scripts/battle/filmstrip.mjs spell:lightning_bolt:v1 --actor a1 --targets b1 --damage 4 \
  --frames 24 --interval 60 --columns 6 --out <folder>/shock-contact.png
```

- 0 to 780 ms (contact): no staged particle or light anywhere; only the scene's charge circle on a1 early.
- 840 and 900 ms: the arc from a1 to b1; b1 filled flat in bone; blue sparks above a1's head, short.
- 960 ms: blue sparks around b1, some past its outline, none reaching b2; the arc jagged, not a smooth curve.
- 1080 to 1380 ms: the arc still drawn; a few small sparks on b1's body only.
- 1440 ms (whole): the arc faint. 1680 ms (whole) and every frame after: no effect object on the field.
- b2 and b3 are not lit, moved or marked on any frame. Nothing in the effect is gold.

The sheets cannot show the stop, the flicker at full speed, or the sound.

### Left

- The arc's own `trail` is not used: Stage I allows no windup beat, and a trail in the impact would only draw the arc
  twice. The arc is the motif.
- The sparks off a1's head stand for "the fractured skull": a beat on a caster sits at its home ([biological.md](biological.md)'s
  open point), so the sparks may show over the back rather than the skull. A point on the skull is a kit change.
- The craft book's chain entry is about an arc between targets; Shock uses its drawing for one jump only. No chain
  to a second figure exists in the kit, and Shock strikes one.

## Ice Grip (`spell:frostbite:v1`)

### Intent

The player reads "ice took hold of that one's legs: it is slowed": shards spring up from the ground around b1's legs
and a pale frost clings low on its body for a second. Ice Grip deals 3 damage and lowers one enemy's initiative by 3
for 2 rounds; Stage II, so a light hit with one cold light and a grip that stays a moment. Its signature is the
grip from below; Deep Freeze closes in from all around and pins with a ring, Ice Grip has neither.

### Today

Filmed at `fc3ec94`, 1×, a1 on b1, 3 health lost: a whole action and a contact.

- 0 to 240 ms: the pale blue charge circle on a1, gone by 300 ms. a1 does not move.
- 840 and 900 ms (contact): b1 filled flat in bone.
- 960 to 1200 ms: a six-armed pale blue crystal over b1's chest, bright. 1440 ms (whole): faded; 1680 ms: gone.
- No particle, no light, nothing at the feet. The sheet is Deep Freeze's, frame for frame.

### Beats

| At | Lasts | Window | Layer | Beat | Pattern |
| --- | --- | --- | --- | --- | --- |
| 0 | 840 | windup | the caster | The cast load ([movement.md](movement.md)): a1 squashes and rises where it stands. No staged beat in the windup: the grip comes from the ground, not from a1. | [Anticipation](../battle-craft.md#anticipation) |
| 840 | 800 | impact | the effect | The `ice` motif, the crystal, on b1, gone by 1640 ms. `motif at 840, lasts 800` | [Shape before colour](../battle-craft.md#shape-before-colour), [The three parts of an effect](../battle-craft.md#the-three-parts-of-an-effect) |
| 840 | 590 | impact | the caster and the target | a1's release, b1's light hit from a distance ([movement.md](movement.md)): its bone flash for 80 ms, its stop, its recoil away from a1. | [Hit flash](../battle-craft.md#hit-flash), [Recoil](../battle-craft.md#recoil) |
| 840 | 260 | impact | the effect | The bite of the cold: a hard blue light on b1, gone by 1100 ms. `glow at 840, tint family, reach 170, strength 0.6, lasts 260, blend add` | [Glow](../battle-craft.md#glow), [Additive light](../battle-craft.md#additive-light) |
| 840 | 120 | impact | the camera | A small staged tremor over the one the health lost gives. `shake at 840, strength 0.1` | [Shake from trauma](../battle-craft.md#shake-from-trauma) |
| 900 | 360 | impact | the effect | The grip, the signature: 10 blue shards (*house*) spring up from the ground around b1's legs in a narrow fan, like fingers closing. `burst at 900, count 10, shape shard, tint family, direction up, spread 50, reach 140, strength 0.25, level feet, lasts 360, blend normal` | [Burst](../battle-craft.md#burst), [A beat on the ground](../battle-craft.md#a-beat-on-the-ground) |
| 1000 | 900 | impact | the effect | The frost that slows: 10 pale blue dots (*house*) cling low on b1, rising barely and slowly off its legs, gone by 1900 ms. `flow at 1000, count 10, shape dot, tint family, direction up, spread 60, reach 60, strength 0.45, level feet, lasts 900, blend screen` | [Flow](../battle-craft.md#flow), [One effect, one meaning](../battle-craft.md#one-effect-one-meaning) |
| 1000 | 900 | impact | the effect | The cold held in the legs: a soft blue light low on b1, dimming slowly, gone by 1900 ms. Not a ring: it lies on the body, not the ground. `glow at 1000, tint family, reach 140, strength 0.35, level feet, lasts 900, blend screen` | [Glow](../battle-craft.md#glow), [A beat on the ground](../battle-craft.md#a-beat-on-the-ground) |
| 1900 | 640 | impact | the field | Nothing new. Both figures home, breathing. The result is read here. | [The hold before the number](../battle-craft.md#the-hold-before-the-number), [Breath at rest](../battle-craft.md#breath-at-rest) |

Six staged beats (the motif, two glows, the shake, the burst, the flow), none in the windup; 20 particles asked for
on b1 (10, 10); two glows, no pulse, a shake of 0.1, the last gone by 1900 ms: inside Stage II. One flash, the
body's, on b1; one change of light on b1, the bite, its low light a dim remainder.

### Reduced motion

Nothing. The frame after the action is shown at once with the result text. The slow is the status chip's to say.

### Cost

- Objects at the peak (900 to 1100 ms): the motif's `Graphics`, the bite's image, one emitter; about three draw calls.
  From 1000 ms the low light's image and the frost's emitter.
- Particles at the peak: about 16 at 1040 ms (the grip's last and the frost's first); of the field's 150.
- Bytes: not measured. Built by BC4 (motif, burst, flow, glow, the `staging` field), BC5 (the cast load, the hit from
  a distance), BC6 (the `shake` beat); the `level` of a beat is in the kit; BC8 places the beats. The cancel path is
  the kit's.

### On the film strip

```bash
node scripts/battle/filmstrip.mjs spell:frostbite:v1 --actor a1 --targets b1 --damage 3 --out <folder>/ice-grip.png
node scripts/battle/filmstrip.mjs spell:frostbite:v1 --actor a1 --targets b1 --damage 3 \
  --frames 24 --interval 60 --columns 6 --out <folder>/ice-grip-contact.png
```

- 0 to 780 ms (contact): no staged particle or light; only the scene's charge circle on a1 early.
- 840 and 900 ms: b1 filled flat in bone, a blue light over it.
- 960 to 1140 ms: blue shards standing up from the ground at b1's legs, pointing up, none above its back; the
  crystal on b1's chest above them, not covered.
- 1080 ms: no hard light left on b1; a soft blue light low on its legs.
- 1200 to 1380 ms: pale blue dots on b1's legs and belly, not over its head; no ring on the ground at its feet.
- 1440 ms (whole): the crystal faded, the frost still low. 1920 ms (whole) and every frame after: no effect object.
- Set beside Deep Freeze's sheet at 1200 ms: Ice Grip has dots low on b1 and no ring; Deep Freeze a ring at the feet.
- b2 and b3 are not lit, moved or marked on any frame. Nothing in the effect is gold.

The sheets cannot show the stop, the feel at full speed, or the sound.

### Left

- The slow is drawn as frost clinging low, chosen over a ring at the feet: the ring is the stun's in every family, and
  initiative lowered is not a stun. The owner may refuse; a slow of its own (a look that drags) would be a kit change.
- The shards spring up from the ground (*house*, `level feet`, `direction up`). If the film shows them inside the
  body rather than around the legs, the reach goes up before the count.
- No windup beat: "a cold grasp" happens at the victim, and the Stage II band allows none.

## Emberstorm (`spell:meteor:v1`)

### Intent

The player reads "a storm of embers fell on all three, and they burn": the caster throws embers up, then each struck
figure flares with ember sparks and a few deep red beads. Emberstorm deals 3 damage and a Bleed of 2 for a round to
up to three enemies; Stage II, three figures at once, so each gets a third of the field's particles and the same
small beats. Its signature is the same flare on every figure at once; Incinerate bursts on one, much harder.

### Today

Filmed at `fc3ec94`, 1×, a1 on b1, b2 and b3, 3 health lost each: a whole action and a contact.

- 0 to 240 ms: the pale blue charge circle on a1, gone by 300 ms. a1 does not move.
- 840 and 900 ms (contact): b1, b2 and b3 filled flat in bone.
- 960 to 1200 ms: upright pale red strokes, the ember tongues, across each of the three bodies. 1440 ms (whole):
  faint; 1680 ms: gone.
- No particle, no light, no bleed. The sheet is Incinerate's on three figures.

### Beats

| At | Lasts | Window | Layer | Beat | Pattern |
| --- | --- | --- | --- | --- | --- |
| 0 | 840 | windup | the caster | The cast load ([movement.md](movement.md)): a1 squashes and rises where it stands. | [Anticipation](../battle-craft.md#anticipation) |
| 360 | 420 | windup | the effect | The storm raised: 12 ember sparks (*house*) thrown up off a1, gone by 780 ms. `flow at 360, on caster, count 12, shape spark, tint ember, direction up, spread 50, reach 200, strength 0.25, lasts 420, blend add` | [Flow](../battle-craft.md#flow), [Anticipation](../battle-craft.md#anticipation) |
| 840 | 800 | impact | the effect | The `fire` motif, the ember tongues, on each of b1, b2 and b3, gone by 1640 ms. `motif at 840, lasts 800` | [Shape before colour](../battle-craft.md#shape-before-colour), [The three parts of an effect](../battle-craft.md#the-three-parts-of-an-effect) |
| 840 | 590 | impact | the caster and the targets | a1's release, each target's light hit from a distance ([movement.md](movement.md)): its bone flash for 80 ms, its stop, its recoil away from a1. | [Hit flash](../battle-craft.md#hit-flash), [Recoil](../battle-craft.md#recoil) |
| 840 | 240 | impact | the effect | The flare: a warm light on each target, gone by 1080 ms. `glow at 840, tint ember, reach 160, strength 0.6, lasts 240, blend add` | [Glow](../battle-craft.md#glow), [Additive light](../battle-craft.md#additive-light) |
| 840 | 120 | impact | the camera | A small staged tremor, once for the three. `shake at 840, strength 0.12` | [Shake from trauma](../battle-craft.md#shake-from-trauma) |
| 860 | 360 | impact | the effect | The fragments: 12 ember sparks (*house*) burst off each target all around. `burst at 860, count 12, shape spark, tint ember, direction out, spread 360, reach 220, strength 0.25, lasts 360, blend add` | [Burst](../battle-craft.md#burst), [Stagger and overlap](../battle-craft.md#stagger-and-overlap) |
| 960 | 700 | impact | the effect | The cinders: 8 ember motes (*house*) rise slowly off each target, gone by 1660 ms. `flow at 960, count 8, shape dot, tint ember, direction up, spread 40, reach 120, strength 0.4, lasts 700, blend screen` | [Flow](../battle-craft.md#flow), [The three parts of an effect](../battle-craft.md#the-three-parts-of-an-effect) |
| 1000 | 700 | impact | the effect | The Bleed: 4 deep red beads (*house*) well up on each target, gone by 1700 ms. `flow at 1000, count 4, shape dot, tint blood, direction out, spread 360, reach 50, strength 0.4, lasts 700, blend normal` | [Flow](../battle-craft.md#flow), [One effect, one meaning](../battle-craft.md#one-effect-one-meaning) |
| 1700 | 840 | impact | the field | Nothing new. All four figures home, breathing. The result is read here. | [The hold before the number](../battle-craft.md#the-hold-before-the-number), [Breath at rest](../battle-craft.md#breath-at-rest) |

Seven staged beats (the flow on a1, the motif, the glow, the shake, the burst, two flows), one in the windup; 24
particles asked for on each target (12, 8, 4) and 12 on a1; at most 24 × 3 + 12 = 84 alive, under 150; one glow, no
pulse, a shake of 0.12, the last gone by 1700 ms: inside Stage II. One flash per figure, the body's; one change of
light, the flare, on the three at once.

### Reduced motion

Nothing. The frame after the action is shown at once with the result text. The Bleed is the status chip's to say.

### Cost

- In the windup: one emitter on a1, gone by 780 ms.
- Objects at the peak (860 to 1080 ms): three motif `Graphics`, three glow images, three emitters; about nine draw
  calls. From 1000 ms two emitters a target.
- Particles at the peak: about 50 at 1000 ms (the fragments' last, the cinders, the beads, on three figures); of the
  field's 150.
- Bytes: not measured. Built by BC4 (motif, burst, flow, glow, the `staging` field, a beat on each target), BC5 (the
  cast load, the hit from a distance), BC6 (the `shake` beat); BC8 places the beats. The cancel path is the kit's.

### On the film strip

```bash
node scripts/battle/filmstrip.mjs spell:meteor:v1 --actor a1 --targets b1,b2,b3 --damage 3 --out <folder>/emberstorm.png
node scripts/battle/filmstrip.mjs spell:meteor:v1 --actor a1 --targets b1,b2,b3 --damage 3 \
  --frames 24 --interval 60 --columns 6 --out <folder>/emberstorm-contact.png
```

- 360 to 720 ms (contact): warm sparks rising off a1, above its back; none past the middle of the field. 840 ms: none.
- 840 and 900 ms: b1, b2 and b3 filled flat in bone, a warm light on each.
- 960 to 1140 ms: warm sparks around each of the three, the same on each; none reaching a neighbour's body; the
  tongues upright on each, not covered.
- 1200 to 1440 ms: warm motes rising slowly over each; a few deep red beads on each body, not pink, not orange.
- 1680 ms (whole): at most a bead or a mote. 1920 ms (whole) and every frame after: no effect object on the field.
- Set beside Incinerate's sheet at 960 ms: Emberstorm's sparks are short and alike on three; Incinerate's one burst
  is larger and clears b1's body.
- a2 and a3 are not lit, moved or marked on any frame. Nothing in the effect is gold.

The sheets cannot show the stop, the feel at full speed, or the sound.

### Left

- The Bleed shows `blood`, not `ember`: the game names it Bleed and its chip is Bleed's, and deep red beads mean a
  Bleed in every family. Ember motes stay the fire itself, so lingering embers would read as more burning damage. The
  owner may want a burn to look like a burn; then it is a new meaning to settle for every family, not this staging's.
- The storm does not fall from above: a burst cannot fall and no beat comes down from the sky. A falling storm is a
  kit change (a direction `down`, or a source above the field).
- The windup flow on a1 is ember sparks rising: in the biological family rising sparks are energy gained, but in its
  own green; here the tint, ember, says fire.

## Incinerate (`spell:engulfing_flames:v1`)

### Intent

The player reads "the caster gathered fire and burst it on that one": the heat builds on a1, runs to b1, and b1
explodes in flame outward and keeps smouldering. Incinerate deals 10 damage to one enemy, the family's hardest hit;
Stage III, a few times a game, so it takes the eye: a charge in the windup, the largest burst of the family, the
strongest staged shake. Its signature is the outward explosion; Deep Freeze, the same mutation's other spell, closes
in on its target instead.

### Today

Filmed at `fc3ec94`, 1×, a1 on b1, 10 health lost: a whole action and a contact.

- 0 to 240 ms: the pale blue charge circle on a1, gone by 300 ms. a1 does not move.
- 840 and 900 ms (contact): b1 filled flat in bone.
- 960 to 1440 ms: upright pale red strokes, the ember tongues, on b1's lower body; faint at 1440 ms; gone by 1680 ms.
- No particle, no light, no shake beyond the health lost's. The sheet is Emberstorm's on one figure: the family's
  hardest hit reads like its three-way spread.

### Beats

| At | Lasts | Window | Layer | Beat | Pattern |
| --- | --- | --- | --- | --- | --- |
| 0 | 840 | windup | the caster | The cast load ([movement.md](movement.md)): a1 squashes and rises where it stands. | [Anticipation](../battle-craft.md#anticipation) |
| 240 | 480 | windup | the effect | The heat drawn in: 14 ember sparks (*house*) on a ring around a1 pulled onto it, gone by 720 ms. `gather at 240, on caster, count 14, shape spark, tint ember, reach 260, strength 0.3, lasts 480, blend add` | [Gravity well and convergence](../battle-craft.md#gravity-well-and-convergence), [Anticipation](../battle-craft.md#anticipation) |
| 480 | 300 | windup | the effect | The charge: a warm light on a1, gone by 780 ms. `glow at 480, on caster, tint ember, reach 180, strength 0.5, lasts 300, blend screen` | [Glow](../battle-craft.md#glow), [Anticipation](../battle-craft.md#anticipation) |
| 600 | 240 | windup | the effect | The fire sent: 16 ember dots (*house*) run from a1 to b1, the first arriving at the contact. `trail at 600, count 16, shape dot, tint ember, strength 0.4, lasts 240, blend add` | [Trail](../battle-craft.md#trail) |
| 840 | 900 | impact | the effect | The `fire` motif, the tongues, on b1, gone by 1740 ms. `motif at 840, lasts 900` | [Shape before colour](../battle-craft.md#shape-before-colour), [The three parts of an effect](../battle-craft.md#the-three-parts-of-an-effect) |
| 840 | 700 | impact | the caster and the target | a1's release, b1's heavy hit from a distance ([movement.md](movement.md)): its bone flash for 80 ms, its stop, its long recoil away from a1. | [Hit flash](../battle-craft.md#hit-flash), [Recoil](../battle-craft.md#recoil) |
| 840 | 280 | impact | the effect | The detonation, the action's brightest part: a hard warm light on b1, wider than its body, gone by 1120 ms. `glow at 840, tint ember, reach 240, strength 0.9, lasts 280, blend add` | [Glow](../battle-craft.md#glow), [Additive light](../battle-craft.md#additive-light) |
| 840 | 160 | impact | the camera | The staged shake of the hardest hit, over the health lost's. `shake at 840, strength 0.3` | [Shake from trauma](../battle-craft.md#shake-from-trauma) |
| 860 | 420 | impact | the effect | The explosion, the signature: 24 ember sparks (*house*) thrown out all around b1, well past its body. `burst at 860, count 24, shape spark, tint ember, direction out, spread 360, reach 320, strength 0.3, lasts 420, blend add` | [Burst](../battle-craft.md#burst), [Stagger and overlap](../battle-craft.md#stagger-and-overlap) |
| 1000 | 900 | impact | the effect | The smoke of it: 14 ember motes (*house*) rise slowly off b1, gone by 1900 ms. `flow at 1000, count 14, shape dot, tint ember, direction up, spread 40, reach 160, strength 0.45, lasts 900, blend screen` | [Flow](../battle-craft.md#flow), [The three parts of an effect](../battle-craft.md#the-three-parts-of-an-effect) |
| 1100 | 700 | impact | the effect | The smoulder: a dim warm light stays on b1, fading, gone by 1800 ms. `glow at 1100, tint ember, reach 160, strength 0.35, lasts 700, blend screen` | [Glow](../battle-craft.md#glow) |
| 1900 | 640 | impact | the field | Nothing new. Both figures home, breathing. The result is read here. | [The hold before the number](../battle-craft.md#the-hold-before-the-number), [Breath at rest](../battle-craft.md#breath-at-rest) |

Ten staged beats, three in the windup (the gather, the glow, the trail); 54 particles asked for on b1 (16, 24, 14) and
14 on a1; three glows, no pulse, a shake of 0.3, the last gone by 1900 ms: inside Stage III. One flash, the body's, on
b1; two changes of light, the charge on a1 and the detonation on b1; the smoulder is its dim remainder.

### Reduced motion

Nothing. The frame after the action is shown at once with the result text: no charge, no trail, no burst.

### Cost

- In the windup: one emitter and one glow image on a1, gone by 780 ms; the trail's emitter from 600 ms.
- Objects at the peak (860 to 1120 ms): the motif's `Graphics`, the detonation's image, two emitters (the explosion,
  the trail's last); about four draw calls. From 1100 ms the smoulder's image and the smoke's emitter.
- Particles at the peak: about 32 at 900 ms (24 and the trail's last); of the field's 150.
- Bytes: not measured. Built by BC4 (motif, burst, flow, gather, trail, glow, the `staging` field), BC5 (the cast load,
  the hit from a distance), BC6 (the `shake` beat); BC8 places the beats. The cancel path is the kit's.

### On the film strip

```bash
node scripts/battle/filmstrip.mjs spell:engulfing_flames:v1 --actor a1 --targets b1 --damage 10 --out <folder>/incinerate.png
node scripts/battle/filmstrip.mjs spell:engulfing_flames:v1 --actor a1 --targets b1 --damage 10 \
  --frames 24 --interval 60 --columns 6 --out <folder>/incinerate-contact.png
```

- 240 to 660 ms (contact): warm sparks on a ring around a1, closing on it; none left by 720 ms.
- 480 to 720 ms: a warm light on a1. 660 to 780 ms: warm dots between a1 and b1, the leading ones nearest b1.
- 840 and 900 ms: b1 filled flat in bone under a hard warm light wider than its body; no dot left in the gap.
- 960 to 1140 ms: warm sparks all around b1, the outer ones clearly past its outline, toward b2 but not on it; the
  tongues on b1, not hidden.
- 1200 to 1680 ms: warm motes rising slowly over b1; a dim warm light on it, gone by 1800 ms. No blood: Incinerate
  does not bleed.
- 1920 ms (whole) and every frame after: no effect object on the field.
- Set beside Deep Freeze's sheet at 960 ms: Incinerate's pieces are going out from b1, Deep Freeze's coming in.
- b2, b3, a2 and a3 are not lit, moved or marked on any frame. Nothing in the effect is gold.

The sheets cannot show the stop, the weight of the shake, or the sound.

### Left

- No ring at the feet, though a blast ring would suit an explosion: a ring at the feet means a stun in every family,
  and Incinerate does not stun. The owner may want a shock ring for blasts; then the two need different shapes, a kit
  change.
- No `pulse`: the one halo means "healed". A burning outline would be a halo of another kind, a kit change.
- The trail runs through the air from a1 to b1; Deep Freeze's runs on the ground. Chosen so that the windups of the
  mutation's two spells differ by shape as well as colour.
- The gather sits at a1's home while its body rises ([biological.md](biological.md)'s open point).

## Deep Freeze (`spell:ice_spear:v1`)

### Intent

The player reads "the cold ran along the ground, closed on that one and froze it in place": a crack of frost to b1,
shards closing in from all around, then a tight blue ring that holds at its feet. Deep Freeze deals 4 damage and
stuns one enemy for a round; Stage III, a few times a game, so it gets a windup and a hold, but a medium hit: the
spectacle is in the closing and in what stays. Its signature is the closing-in and the pin; Incinerate, the same
mutation's other spell, bursts outward.

### Today

Filmed at `fc3ec94`, 1×, a1 on b1, 4 health lost: a whole action and a contact.

- 0 to 240 ms (contact): the pale blue charge circle on a1, gone by 300 ms. a1 does not move.
- 840 and 900 ms: b1 filled flat in bone.
- 960 to 1380 ms: a six-armed pale blue crystal over b1's chest, steady, slowly fading; faint at 1440 ms; gone by 1680 ms.
- No particle, no light, nothing at the feet: the sheet is Ice Grip's. Nothing says the stun.

### Beats

| At | Lasts | Window | Layer | Beat | Pattern |
| --- | --- | --- | --- | --- | --- |
| 0 | 840 | windup | the caster | The cast load ([movement.md](movement.md)): a1 squashes and rises where it stands. | [Anticipation](../battle-craft.md#anticipation) |
| 240 | 360 | windup | the effect | The cold drawn up: a soft blue light on a1, gone by 600 ms. `glow at 240, on caster, tint family, reach 170, strength 0.5, lasts 360, blend screen` | [Glow](../battle-craft.md#glow), [Anticipation](../battle-craft.md#anticipation) |
| 520 | 320 | windup | the ground | The frost runs: 14 blue shards (*house*) laid as a crack on the ground from a1 to b1, reaching b1's feet at the contact. `trail at 520, count 14, shape shard, tint family, strength 0.2, level feet, lasts 320, blend add` | [Trail](../battle-craft.md#trail), [A beat on the ground](../battle-craft.md#a-beat-on-the-ground) |
| 840 | 1000 | impact | the effect | The `ice` motif, the crystal, on b1, held a little longer than Ice Grip's, gone by 1840 ms. `motif at 840, lasts 1000` | [Shape before colour](../battle-craft.md#shape-before-colour), [The three parts of an effect](../battle-craft.md#the-three-parts-of-an-effect) |
| 840 | 590 | impact | the caster and the target | a1's release, b1's medium hit from a distance ([movement.md](movement.md)): its bone flash for 80 ms, its stop, its recoil away from a1. | [Hit flash](../battle-craft.md#hit-flash), [Recoil](../battle-craft.md#recoil) |
| 840 | 300 | impact | the effect | The freeze pulse: a hard blue light on b1, gone by 1140 ms. `glow at 840, tint family, reach 200, strength 0.8, lasts 300, blend add` | [Glow](../battle-craft.md#glow), [Additive light](../battle-craft.md#additive-light) |
| 840 | 120 | impact | the camera | A staged tremor as the ice takes. `shake at 840, strength 0.15` | [Shake from trauma](../battle-craft.md#shake-from-trauma) |
| 840 | 360 | impact | the effect | The closing, the signature: 20 blue shards (*house*) on a wide ring around b1 pulled in onto it. `gather at 840, on target, count 20, shape shard, tint family, reach 320, strength 0.3, lasts 360, blend add` | [Gravity well and convergence](../battle-craft.md#gravity-well-and-convergence) |
| 1000 | 1000 | impact | the ground | The pin, the stun: a tight blue ring on the ground at b1's feet opens to just past its outline and holds, fading slowly, gone by 2000 ms. `glow at 1000, shape ring, tint family, reach 130, strength 0.6, lasts 1000, blend screen` | [Glow](../battle-craft.md#glow), [A beat on the ground](../battle-craft.md#a-beat-on-the-ground) |
| 1200 | 400 | impact | the effect | The ice locks: 8 small shards (*house*) close once more, tighter, onto b1, gone by 1600 ms. `gather at 1200, on target, count 8, shape shard, tint family, reach 170, strength 0.2, lasts 400, blend screen` | [Gravity well and convergence](../battle-craft.md#gravity-well-and-convergence), [Stagger and overlap](../battle-craft.md#stagger-and-overlap) |
| 2000 | 540 | impact | the field | Nothing new. Both figures home, breathing. The result is read here. | [The hold before the number](../battle-craft.md#the-hold-before-the-number), [Breath at rest](../battle-craft.md#breath-at-rest) |

Nine staged beats, two in the windup (the glow, the trail); 42 particles asked for on b1 (14, 20, 8) and none on a1;
three glows, no pulse, a shake of 0.15, the last gone by 2000 ms: inside Stage III. One flash, the body's, on b1; two
changes of light, the cold on a1 and the freeze pulse on b1. The ring is a mark on the ground, faint, not a light on
a figure.

### Reduced motion

Nothing. The frame after the action is shown at once with the result text: no crack, no shard, no ring. The stun is
the status chip's to say.

### Cost

- In the windup: one glow image on a1, gone by 600 ms; the trail's emitter from 520 ms.
- Objects at the peak (840 to 1140 ms): the motif's `Graphics`, the pulse's image, two emitters (the closing, the
  trail's last); about four draw calls. From 1000 ms the ring's image; from 1200 ms one emitter.
- Particles at the peak: about 26 at 900 ms (20 and the trail's last); of the field's 150.
- Bytes: not measured. Built by BC4 (motif, gather, trail, glow, the `staging` field), BC5 (the cast load, the hit from
  a distance), BC6 (the `shake` beat, the ring of `glow`); the trail at the feet is in the kit; BC8 places the beats.
  The cancel path is the kit's.

### On the film strip

```bash
node scripts/battle/filmstrip.mjs spell:ice_spear:v1 --actor a1 --targets b1 --damage 4 --out <folder>/deep-freeze.png
node scripts/battle/filmstrip.mjs spell:ice_spear:v1 --actor a1 --targets b1 --damage 4 \
  --frames 24 --interval 60 --columns 6 --out <folder>/deep-freeze-contact.png
```

- 240 to 540 ms (contact): a soft blue light on a1; nothing else staged.
- 540 to 780 ms: blue shards lying on the ground, a line from a1 toward b1, its head a step nearer b1 on each frame;
  nothing in the air above the line.
- 840 and 900 ms: b1 filled flat in bone under a hard blue light; blue shards on a wide ring around b1.
- 960 to 1140 ms: the shards nearer b1 on each frame, none going outward; the crystal on b1's chest, not hidden.
- 1080 to 1380 ms: a tight blue ring on the ground at b1's feet, about as wide as its ground ring, never past it; a
  few small shards closing on b1 from 1200 ms.
- 1440 to 1680 ms (whole): the ring faint at b1's feet; the crystal fading. 2160 ms (whole) and every frame after: no
  effect object on the field.
- Set beside Ice Grip's at 1200 ms: Deep Freeze a ring at the feet, Ice Grip none; beside Incinerate's at 960 ms, the
  pieces come in.
- b2, b3, a2 and a3 are not lit, moved or marked on any frame. Nothing in the effect is gold.

The sheets cannot show the stop, the feel of the closing at full speed, or the sound.

### Left

- The stun is the ring at the feet, the pin Paralyzing Barb set in the biological family, kept so a stun reads the
  same in every family; a halo would read as a heal.
- The two gathers say one thing twice (the cold closing in), the second tighter, to reach the Stage III band without a
  new look. If the film reads them as one, the second goes before a new look comes.
- "Freeze pulse" is the hard light at the contact, not a wave going out: a pulse going out would read as Incinerate's
  burst in blue.
- The pale blue shards may not show on b1 at 840 and 900 ms, filled in bone: the closing is checked from 960 ms.
