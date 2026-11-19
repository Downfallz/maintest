# Storyboards: the physical family

The Brute's spells: mass and contact, read as bone, dust, shards and the weight of a body ([the vocabulary of the
family](../spell-vfx.md)), on the family's pale taupe, with ember kept for the Frenzied branch and blood for what
tears flesh. Its 15 spells climb [the ladder](README.md#the-ladder): Strike, the base attack, is staged in
[base-actions.md](base-actions.md); this file holds Stage I (Pummel, Brace), then Stage II, then Stage III, which
later passes append. Each step must look heavier than the one below it, and each spell must keep one signature a
player could name. The body's own movement is [movement.md](movement.md)'s: a storyboard here names it in one row per
window and spends its rows on the spell.

The pass (2026-11-19) ran four rounds. The designer read the first films of all sixteen against these
storyboards, and the second round applied what it asked for. The kit then gained a level for each beat
([A beat on the ground](../battle-craft.md#a-beat-on-the-ground)): dust and rings lie at the feet, a ring aimed
`along` opens as an arc, `along` and `back` travel along the ground, and a trail at the feet is a crack. The
designer read the third films (`be36ed2`) frame by frame and asked for five changes. The fourth round made them:
Crash's daze, Dominate's windup, Vital Surge's white blot, and Groundbreaker's crack, whose trail at the feet the
kit now keeps at an even pace. The designer then read those four films again. The scene changed too: the slash
cuts across the blow, a pulse quiets the caster's gold rim, blood is a deep red, the caster's wound goes with its
body, and ember light is additive. The coded rows below are the stagings as built, and each **On the film strip**
describes the last films of its spell. What is still open, for the kit or the scene, each its own change:

- A windup beat on a melee caster stays where the caster stood: when the body leaves its ring at about 720 ms,
  the last of a gather or a glow is left behind on the ground. The wound now goes with the body; a windup beat
  does not yet. Dominate's windup ends by 720 ms to stay clear of it; Deranged Charge leaves a faint warm haze at
  780 ms.
- On the effects bench every blow is struck up the field, so `along` and `back` are shortened to 0.42 of their reach: what
  is thrown along the blow stays on the target's body or just over it (Pummel's chips, Deranged Charge's cone),
  and what is drawn back does not cross the gap to the caster (Seize). A blow struck across the field throws
  them at full reach. The sheets show only the first case.
- `ember` was 0xd58b75 and read salmon. Settled by the owner, 2026-11-19: warmer, 0xe0763f (`MOTIF_COLOR.fire` in
  `battle/src/spell-effects.ts`, which the `ember` tint and the `fire` motif share).
- A ring that lies at the feet and is not aimed opens through the neighbour's legs when its reach passes 300
  (Crash at 1080 ms, Crushing Stomp at 1200 ms; Body Slam's is now aimed). Faint by then. Settled by the owner,
  2026-11-19: kept.

## Pummel (`spell:pummel:v1`)

### Intent

The player reads "the Brute brought its huge front paw down on that one": a hammer, not a jab. Pummel costs 1 energy,
deals 2 damage to one enemy in melee with a 75% critical chance, and is the Brute's first spell, cast most rounds it
has energy: it takes a little more attention than Strike and no light, no windup mark and no camera of its own. Its
signature is the dust collar: the paw drives a ring of dust out of the ground around the target, and bone chips fly
on beyond it.

### Today

Filmed at `15ee3b0`, 390 by 844, 1×, a1 on b1, no health change given: a whole action and a contact.

- 0 to 120 ms: a pale arc on a1's body, the shared charge. A melee blow shows a cast's gather.
- 540 to 840 ms: a1 leaves its ring and strikes; at 840 ms it covers b1's lower half, b1's head clear.
- 900 to 1380 ms: three pale strokes and a ring on b1, upright whatever the blow's direction, low in contrast on
  b1's hide. No particle. Strike's sheets are the same frame for frame: nothing says "paw" or "hammer".

### Beats

| At | Lasts | Window | Layer | Beat | Pattern |
| --- | --- | --- | --- | --- | --- |
| 0 | 840 | windup | the caster | A light melee blow's draw back and strike ([movement.md](movement.md)), scaled by the health b1 lost (2, or more on a critical). No mark on a1's body: the staging has no windup beat, so the shared arc is not drawn. | [Anticipation](../battle-craft.md#anticipation), [Contact and follow-through](../battle-craft.md#contact-and-follow-through) |
| 840 | 760 | impact | the effect | The `impact` motif on b1: its strokes run along the line from a1 to b1, on their dark edge. Ends at 1600 ms, shorter than its default, inside the band. `motif at 840, lasts 760` | [An impact that keeps the direction](../battle-craft.md#an-impact-that-keeps-the-direction), [Shape before colour](../battle-craft.md#shape-before-colour) |
| 840 | 240 | impact | the effect | Bone chips knocked off b1 and thrown on beyond it, in a 50° fan along the blow, to twice its radius. 10 (*house*), two more than Strike's 8. `burst at 840, count 10, shape shard, tint bone, direction along, spread 50, reach 320, lasts 240, blend normal` | [Burst](../battle-craft.md#burst), [An impact that keeps the direction](../battle-craft.md#an-impact-that-keeps-the-direction) |
| 840 | 400 | impact | the effect | The dust collar, the signature: 8 grains of dust (*house*) pushed out all round b1's feet, flat on the ground, to just past its ground ring, slower than the chips, in the family's taupe. `burst at 840, count 8, shape dot, tint family, direction out, reach 220, strength 0.4, lasts 400, blend normal, level feet` | [Burst](../battle-craft.md#burst), [A beat on the ground](../battle-craft.md#a-beat-on-the-ground) |
| 840 | 840 | impact | the caster and the target | The light blow's hit stop, b1's bone flash when it lost health, its recoil, a1's return, home by 1680 ms ([movement.md](movement.md)). A critical hit adds the punch zoom there, not here. | [Hit stop](../battle-craft.md#hit-stop), [Recoil](../battle-craft.md#recoil) |
| 900 | 600 | impact | the effect | The dust settling: 6 grains (*house*) alive at once rise slowly off the ground at b1's feet and fade, gone by 1500 ms. `flow at 900, count 6, shape dot, tint family, direction up, spread 40, reach 160, lasts 600, blend normal, level feet` | [Flow](../battle-craft.md#flow), [A beat on the ground](../battle-craft.md#a-beat-on-the-ground) |
| 1680 | 860 | impact | the field | Nothing new. Both figures home, breathing. The result is read here. | [The hold before the number](../battle-craft.md#the-hold-before-the-number), [Breath at rest](../battle-craft.md#breath-at-rest) |

Four staged beats, 24 particles asked for on b1 (10, 8 and 6), no glow, no shake, the last gone by 1600 ms: inside
Stage I. One flash, the body's, on b1; no change of light. Nothing in the settle.

### Reduced motion

Nothing. The frame after the action is shown at once with the result text: no strike, no motif, no chip, no dust.

### Cost

- Objects at the peak (840 to 1100 ms): one `Graphics` for the motif, three emitters (chips, collar, settling dust),
  the body's tweens. Draw calls: the motif batches with the vectors; each emitter is one, three during 240 ms.
- Particles at the peak: 18 at 900 ms (10 chips, 8 grains), 24 asked for in all; of the field's 150.
- Bytes: not measured. Built by BC4 (motif, burst, flow, the `staging` field) and BC5 (the body); BC6 places the
  beats. The cancel path is the kit's: every emitter killed with the action.

### On the film strip

```bash
node scripts/battle/filmstrip.mjs spell:pummel:v1 --actor a1 --targets b1 --damage 2 --out <folder>/pummel.png
node scripts/battle/filmstrip.mjs spell:pummel:v1 --actor a1 --targets b1 --damage 2 \
  --frames 24 --interval 60 --columns 6 --out <folder>/pummel-contact.png
```

- 0 to 780 ms (contact): no mark, arc or particle on a1's body; a1 leaves its ring at 660 ms.
- 840 and 900 ms: a1's silhouette covers b1's feet and lower body, not its head. b1, which lost 2, is filled flat in
  bone in these two frames and in no other.
- 900 ms: a puff of dust at b1's feet, drawn over a1's back, which stands in front of them.
- 960 and 1020 ms: the strokes upright, along the blow struck up the field, each on a dark edge; the dust grains
  spread flat along b1's ground ring on both sides, none above b1's back. The chips are short and over b1's head,
  on its far side from a1, none on a1's side: shortened, they do not clear b1's outline.
- 1080 to 1260 ms: no chip left; a few grains low round b1's ring, rising a little. Nothing in the effect is gold.
- 1200 ms (whole): the strokes at about half their brightness. 1380 ms: the strokes all but gone, no grain left.
- 1440 ms (whole) and every frame after: no effect object on the field; a1 home.
- Set beside Strike's sheet at 960 ms: Pummel shows the dust on the ground at b1's feet and nothing brighter.

The sheets cannot show the stop, the shake, the punch zoom of a critical or the feel at full speed.

### Left

- The signature is the dust collar, not a double hammer. The host confirms one hit; two hammer marks would read as
  two blows. The owner may want the paw itself seen coming down, which is the body's (a downward stretch on a Brute's
  sprite) and not a staging's.
- The dust lies at b1's feet (`level feet`). On a blow struck up the field a1 stands in front of b1's feet at
  contact, so the collar's first frame is drawn over a1's back (900 ms); it reads as b1's from 960 ms.
- No windup beat and no flash of its own, by the band. A critical hit is told by the body's punch zoom alone.
- Dust and chips are two bursts at one moment with two meanings, kept apart by shape and tint (shard bone, dot taupe).
  If the film reads them as one, drop the collar's count to 6 before changing either colour.

## Brace (`spell:guard:v1`)

### Intent

The player reads "the Brute roared at its ally, and the ally's bone plates closed over it": who cast, who is
protected, and that the protection is armour. Brace costs 1 energy and raises one ally's defense for 3 rounds; it is a
Stage I support, cast often, so it takes about Pummel's attention and none of a blow's. Its signature is the plates
closing: bone shards drawn in from around the ally onto its body, then the `plates` motif set with a puff of dust.

### Today

Filmed at `15ee3b0`, 390 by 844, 1×, a1 on a2: a whole action and a contact.

- 0 to 120 ms: a pale arc on a1's body. a1 and a2 do not move on any frame.
- 840 ms: faint plates on a2; 900 ms: three shields read on a2's body, fading slowly, still seen at 1380 ms.
- Nothing goes from a1 to a2: no roar, and nothing hardens. The shields could be any family's ward.

### Beats

| At | Lasts | Window | Layer | Beat | Pattern |
| --- | --- | --- | --- | --- | --- |
| 0 | 840 | windup | the caster | A cast's load and rise ([movement.md](movement.md)): a1 squats and rises where it stands. No mark on a1's body, no arc. | [Anticipation](../battle-craft.md#anticipation), [Squash and stretch](../battle-craft.md#squash-and-stretch) |
| 840 | 360 | impact | the caster | The release, as a cast lets go ([movement.md](movement.md)): the roar is in the body. a2 does not react: it lost nothing. | [Contact and follow-through](../battle-craft.md#contact-and-follow-through) |
| 840 | 320 | impact | the effect | The roar: 6 small rings (*house*) leave a1's centre in a 50° fan toward a2, out to twice a1's radius, and fade. `burst at 840, on caster, count 6, shape ring, tint family, direction along, spread 50, reach 380, lasts 320, blend normal` | [Burst](../battle-craft.md#burst), [An impact that keeps the direction](../battle-craft.md#an-impact-that-keeps-the-direction) |
| 880 | 280 | impact | the effect | The plates closing, the signature: 10 bone shards (*house*) start on a ring around a2 and land on its centre as the beat ends. `gather at 880, on target, count 10, shape shard, tint bone, reach 260, strength 1, lasts 280, blend normal` | [Gravity well and convergence](../battle-craft.md#gravity-well-and-convergence), [Shape before colour](../battle-craft.md#shape-before-colour) |
| 1120 | 560 | impact | the effect | The `plates` motif on a2, as the shards land: it arrives and fades, gone by 1680 ms. `motif at 1120, lasts 560` | [The three parts of an effect](../battle-craft.md#the-three-parts-of-an-effect), [Shape before colour](../battle-craft.md#shape-before-colour) |
| 1120 | 280 | impact | the effect | The plates set: 8 grains of bone dust (*house*) puffed out all round a2, just past its outline. `burst at 1120, count 8, shape dot, tint family, direction out, reach 180, strength 0.25, lasts 280, blend normal` | [Burst](../battle-craft.md#burst), [One effect, one meaning](../battle-craft.md#one-effect-one-meaning) |
| 1680 | 860 | impact | the field | Nothing new. Both figures home, breathing. The result is read here. | [The hold before the number](../battle-craft.md#the-hold-before-the-number), [Breath at rest](../battle-craft.md#breath-at-rest) |

Four staged beats, 18 particles asked for on a2 and 6 on a1, no glow, no shake, the last gone by 1680 ms: inside
Stage I. No flash: a2 lost nothing, and a flat fill says "struck". No change of light. Nothing in the settle.

### Reduced motion

Nothing. The frame after the action is shown at once with the result text: no load, no rings, no shards, no plates.

### Cost

- Objects at the peak (1120 to 1160 ms): one `Graphics` for the motif, up to three emitters (the rings fading out,
  the last shards, the dust). Draw calls: at most three emitters at once, for about 40 ms; two otherwise.
- Particles at the peak: 16 at 900 ms (6 rings, 10 shards); 24 asked for in all; of the field's 150.
- Bytes: not measured. Built by BC4 (motif, burst, gather, the `staging` field) and BC5 (the cast's body); BC6 places
  the beats. The cancel path is the kit's: every emitter killed with the action.

### On the film strip

```bash
node scripts/battle/filmstrip.mjs spell:guard:v1 --actor a1 --targets a2 --out <folder>/brace.png
node scripts/battle/filmstrip.mjs spell:guard:v1 --actor a1 --targets a2 \
  --frames 24 --interval 60 --columns 6 --out <folder>/brace-contact.png
```

- 0 to 780 ms (contact): no mark, arc or particle on a1's body.
- 900 ms: a cluster of small rings on a1's front, on a2's side. No shard yet.
- 960 ms: the rings in the gap between a1 and a2, none on a1's far side; shards on a ring around a2, outside its
  outline. No motif yet.
- 1020 ms: the rings at a2's near side, the shards closer. 1080 ms: one or two faint rings, the shards on a2's body,
  the plates appearing. 1140 ms: no ring left.
- 1140 to 1320 ms: the three plates on a2's middle, with dust grains just outside a2's outline below and beside it.
- 1380 ms: no particle; the plates still read.
- 1680 ms (whole) and every frame after: no effect object on the field. a2 has not moved or been filled on any frame.
- Nothing in the effect is gold; a2's target marker stays the only gold on it.

The sheets cannot show the feel of the roar at full speed, nor its sound.

### Left

- The roar is 6 small rings from a1, not a shock ring: a `glow` ring is light, which Stage I does not have. Alpha
  Roar (Stage III) owns the shock ring, so Brace's roar stays small.
- The motif arrives at 1120 ms, 280 ms after contact, so the plates are seen to close before they show. The owner may
  prefer the motif at contact; then the gather would run in the windup, which Stage I refuses.
- The rings and the shards both head somewhere: rings away from a1, shards into a2. If the film reads them as one
  movement, shorten the rings' reach before cutting either.
- No flash on a2. A tint in the family's taupe could say "hardened", but one flash an action is the hit's language.
- Brace gives two defense buffs (3 rounds and 2); the staging shows one protection. The rounds are the status
  chip's to say, not the effect's.

## Fury (`spell:enraged_charge:v1`)

### Intent

The player reads "the Frenzied Brute blew up on that one": an explosive, uncontrolled blow, not an aimed one. Fury
costs 3 energy and deals 6 damage to one enemy in melee with an 80% critical chance; a Stage II spell, cast a few
times a game, so it takes clearly more than Pummel. Its signature is the explosion: ember sparks thrown out on every
side of the target at once under one ember light. Pummel's chips go along the blow; Fury's go everywhere, because
rage does not aim.

### Today

Filmed at `15ee3b0`, 390 by 844, 1×, a1 on b1, 6 health lost: a whole action and a contact.

- 0 to 60 ms: a pale arc on a1's body, the shared charge. 600 to 780 ms: a1 leaves its ring and climbs to b1.
- 840 and 900 ms: b1 filled flat in bone; a1 covers b1's lower half. 960 to 1380 ms: three pale upright strokes on
  b1, fading. No particle, no light: the sheet is Pummel's with more health lost.

### Beats

| At | Lasts | Window | Layer | Beat | Pattern |
| --- | --- | --- | --- | --- | --- |
| 0 | 840 | windup | the caster | A medium melee blow's draw back and strike ([movement.md](movement.md)); heavy on a critical that brings b1 to 7 or more, as the body decides. | [Anticipation](../battle-craft.md#anticipation), [Contact and follow-through](../battle-craft.md#contact-and-follow-through) |
| 360 | 480 | windup | the effect | The rage rising: 8 small ember motes (*house*) alive at once leave a1's body upward, the "dim ember fissures" of the branch. Ends at contact. `flow at 360, on caster, count 8, shape dot, tint ember, direction up, spread 40, reach 200, strength 0.5, lasts 480, blend add` | [Anticipation](../battle-craft.md#anticipation), [Fog and embers](../battle-craft.md#fog-and-embers) |
| 840 | 900 | impact | the effect | The `impact` motif on b1, leaning along the blow on its dark edge, gone by 1740 ms. `motif at 840, lasts 900` | [An impact that keeps the direction](../battle-craft.md#an-impact-that-keeps-the-direction), [Shape before colour](../battle-craft.md#shape-before-colour) |
| 900 | 360 | impact | the effect | The one ember light, the explosion's core: it swells over b1 and goes. `glow at 900, tint ember, reach 260, strength 0.8, lasts 360, blend add` | [Glow](../battle-craft.md#glow), [Additive light](../battle-craft.md#additive-light) |
| 840 | 360 | impact | the effect | The explosion, the signature: 20 ember sparks (*house*) thrown out on every side of b1, clear of its body. `burst at 840, count 20, shape spark, tint ember, direction out, spread 360, reach 340, lasts 360, blend normal` | [Burst](../battle-craft.md#burst), [One effect, one meaning](../battle-craft.md#one-effect-one-meaning) |
| 840 | 400 | impact | the effect | Bone torn off by the blast: 14 shards (*house*) out on every side, a little shorter than the sparks. `burst at 840, count 14, shape shard, tint bone, direction out, spread 360, reach 300, lasts 400, blend normal` | [Burst](../battle-craft.md#burst), [Shape before colour](../battle-craft.md#shape-before-colour) |
| 840 | 240 | impact | the field | A small staged shake on the body's tremor of 6 health lost, decaying over about 240 ms (*house*). `shake at 840, strength 0.12` | [Shake from trauma](../battle-craft.md#shake-from-trauma) |
| 840 | 560 | impact | the caster and the target | The medium blow's hit stop, b1's bone flash, its recoil, a1's return ([movement.md](movement.md)); a critical adds the punch zoom. | [Hit stop](../battle-craft.md#hit-stop), [Recoil](../battle-craft.md#recoil) |
| 920 | 700 | impact | the effect | Embers smouldering: 8 small ember motes alive at once rise off b1 and fade, gone by 1620 ms; additive, as the owner chose for ember light. `flow at 920, count 8, shape dot, tint ember, direction up, spread 60, reach 120, strength 0.5, lasts 700, blend add` | [Flow](../battle-craft.md#flow), [The three parts of an effect](../battle-craft.md#the-three-parts-of-an-effect) |
| 1740 | 800 | impact | the field | Nothing new. Both figures home. The result is read here. | [The hold before the number](../battle-craft.md#the-hold-before-the-number), [Breath at rest](../battle-craft.md#breath-at-rest) |

Seven staged beats, one in the windup; 42 particles asked for on b1 (20, 14, 8) and 8 on a1; one glow, a shake of
0.12, the last gone by 1740 ms: inside Stage II. One flash, the body's, on b1; one change of light, the ember glow.

### Reduced motion

Nothing. The frame after the action is shown at once with the result text: no motes, no strike, no blast, no shake.

### Cost

- Objects at the peak (840 to 1200 ms): one `Graphics` for the motif, the glow, three emitters (sparks, shards, the
  smoulder from 920 ms), the body's tweens. Draw calls: about five during 360 ms, two after.
- Particles at the peak: 42 at 960 ms (20, 14, 8); of the field's 150.
- Bytes: not measured. Built by BC4 and BC5 (the kit, the body) and the tuned kit (reach, strength, the leaning
  strokes); BC6 places the beats. The cancel path is the kit's: every emitter and the glow killed with the action.

### On the film strip

```bash
node scripts/battle/filmstrip.mjs spell:enraged_charge:v1 --actor a1 --targets b1 --damage 6 --out <folder>/fury.png
node scripts/battle/filmstrip.mjs spell:enraged_charge:v1 --actor a1 --targets b1 --damage 6 \
  --frames 24 --interval 60 --columns 6 --out <folder>/fury-contact.png
```

- 0 to 360 ms: no arc and no particle on a1. 420 to 660 ms: a few faint warm motes on a1's back, barely above it;
  none on b1. They read only on the zoomed sheet: the heat is a hint, as chosen.
- 840 ms: a1 covers b1's lower half, not its head; b1 filled in bone, ember-orange sparks already over it.
- 900 ms: sparks and shards on every side of b1, above its head, beside it and down over a1's back.
- 960 to 1140 ms: b1 washed warm by the additive ember light; the strokes upright, along the blow; shards and
  sparks thinning above and beside b1. 1200 ms: the light fading, a few motes over b1's back.
- 1260 to 1380 ms: no light; the strokes fading; at most a mote or two.
- 1440 ms (whole) and every frame after: no effect object on the field. Nothing in the effect is gold.
- Set beside Pummel's sheet at 960 ms: Fury's debris rings b1 and its light warms the whole figure; Pummel's dust
  lies at the feet.

The sheets cannot show the stop, the shake, the punch zoom of a critical, or the feel at full speed.

### Left

- The sparks are on a normal blend and read ember-orange (0xe0763f) from the contact frame (840 ms), not white: they stay at 840.
  The additive ember light warms b1 for 300 ms; it is the one change of light, and no frame is blown out.
- The windup motes are Fury's only windup beat; they stay small (strength 0.5, the least the first film could show) so the charge reads as heat, not as
  Deranged Charge's ember gather (Stage III).
- An 80% critical chance means most Furies are critical: the punch zoom is the body's, and the staging does not
  change with it. A staging keyed on a critical needs the kit to read it (its own change).

## Wild Swing (`spell:reckless_swing:v1`)

### Intent

The player reads "the Brute swung so hard it tore itself": a violent swipe across the target, and the price paid. Wild
Swing costs 2 energy, deals 6 damage to one enemy in melee and 2 to its caster. Stage II, like Fury, and it must not
read as Fury: its signature is the swipe, a wide fan of bone shards thrown across b1 along the blow, with blood
behind it. Fury bursts out on every side; Wild Swing throws to one side, wide.

### Today

Filmed at `15ee3b0`, 390 by 844, 1×, a1 on b1, 6 health lost, the caster wounded: a whole action and a contact.

- 0 to 60 ms: the shared pale arc on a1. 840 to 900 ms: b1 filled in bone, a1 over its lower half.
- 960 to 1380 ms: three pale slash strokes on b1. No particle, no light.
- 840 to 1140 ms: the scene's wound (three red strokes) is drawn at a1's home ring while a1 is still up at b1; it
  lands on a1's body only from 1200 ms.

### Beats

| At | Lasts | Window | Layer | Beat | Pattern |
| --- | --- | --- | --- | --- | --- |
| 0 | 840 | windup | the caster | A medium melee blow's draw back and strike ([movement.md](movement.md)). No windup beat: the swing is sudden. | [Anticipation](../battle-craft.md#anticipation), [Contact and follow-through](../battle-craft.md#contact-and-follow-through) |
| 840 | 800 | impact | the effect | The `slash` motif on b1, its strokes cutting 60° across the blow on their dark edge (the `impact` ones run along it), gone by 1640 ms. `motif at 840, lasts 800` | [An impact that keeps the direction](../battle-craft.md#an-impact-that-keeps-the-direction), [Shape before colour](../battle-craft.md#shape-before-colour) |
| 840 | 240 | impact | the effect | The swipe's flare: a short pale light, gentler than Fury's. `glow at 840, tint bone, reach 200, strength 0.5, lasts 240, blend screen` | [Glow](../battle-craft.md#glow) |
| 840 | 360 | impact | the effect | The swipe, the signature: 18 bone shards (*house*) thrown across b1 in a wide 110° fan along the blow, past its outline. `burst at 840, count 18, shape shard, tint bone, direction along, spread 110, reach 340, lasts 360, blend normal` | [Burst](../battle-craft.md#burst), [An impact that keeps the direction](../battle-craft.md#an-impact-that-keeps-the-direction) |
| 860 | 420 | impact | the effect | The tear: 12 drops of blood (*house*, the deep red 0x9a3b34) follow the shards in a narrower fan, shorter, and fall. `burst at 860, count 12, shape dot, tint blood, direction along, spread 70, reach 260, strength 0.25, lasts 420, blend normal` | [Burst](../battle-craft.md#burst), [One effect, one meaning](../battle-craft.md#one-effect-one-meaning) |
| 840 | 240 | impact | the field | A small staged shake on the body's tremor, decaying over about 240 ms (*house*). `shake at 840, strength 0.1` | [Shake from trauma](../battle-craft.md#shake-from-trauma) |
| 840 | 560 | impact | the caster and the target | The medium blow's hit stop, b1's bone flash, its recoil, a1's return ([movement.md](movement.md)); a1's own wound is the scene's. | [Hit stop](../battle-craft.md#hit-stop), [Recoil](../battle-craft.md#recoil) |
| 960 | 600 | impact | the effect | Dust settling: 6 grains alive at once rise off b1, gone by 1560 ms. `flow at 960, count 6, shape dot, tint family, direction up, spread 50, reach 140, strength 0.5, lasts 600, blend normal` | [Flow](../battle-craft.md#flow), [The three parts of an effect](../battle-craft.md#the-three-parts-of-an-effect) |
| 1640 | 900 | impact | the field | Nothing new. Both figures home. The result is read here. | [The hold before the number](../battle-craft.md#the-hold-before-the-number), [Breath at rest](../battle-craft.md#breath-at-rest) |

Six staged beats, none in the windup; 36 particles asked for on b1 (18, 12, 6); one glow, a shake of 0.1, the last
gone by 1640 ms: inside Stage II. One flash, the body's, on b1; a1's wound is a mark, not a flash. One change of light.

### Reduced motion

Nothing. The frame after the action is shown at once with the result text and a1's wound mark settled.

### Cost

- Objects at the peak (860 to 1200 ms): one `Graphics` for the motif, the glow until 1080 ms, two emitters (shards,
  blood), then the dust. Draw calls: about four during 240 ms, two after.
- Particles at the peak: 30 at 900 ms (18, 12); 36 asked for in all; of the field's 150.
- Bytes: not measured. Built by BC4, BC5 and the tuned kit; BC6 places the beats. Cancel path: the kit's.

### On the film strip

```bash
node scripts/battle/filmstrip.mjs spell:reckless_swing:v1 --actor a1 --targets b1 --damage 6 --wound --out <folder>/wild-swing.png
node scripts/battle/filmstrip.mjs spell:reckless_swing:v1 --actor a1 --targets b1 --damage 6 --wound \
  --frames 24 --interval 60 --columns 6 --out <folder>/wild-swing-contact.png
```

- 0 to 780 ms: no arc, mark or particle on a1.
- 840 ms: a1 covers b1's lower half, b1's head clear and filled in bone; the slash strokes already cut across b1,
  diagonal to the blow. a1's wound, three red strokes, is on a1's own body and goes up with it.
- 900 ms: shards in a wide, flat fan across b1's back and head, on its far side from a1, none on a1's side; deep red
  drops among them over b1's back, not pink.
- 960 to 1080 ms: the red drops on b1 fading; the slash strokes diagonal, on their dark edge. The light gone.
- 1140 ms: a1 nearly home with its wound; on b1 only the strokes and a grain or two.
- 1680 ms (whole) and every frame after: no effect object on the field; the wound mark on a1 is the scene's.
- Set beside Fury's sheet at 960 ms: Wild Swing's debris is a flat fan with red in it and its strokes cross the
  blow; Fury's is a ring of ember-orange sparks under a warm light, its strokes along it.

The sheets cannot show the stop, the shake, or the feel at full speed.

### Left

- The caster's wound now goes with a1's body (840 to 1140 ms on the sheet): settled.
- Along the blow struck up the field the fan is shortened: the shards and the drops land on b1's back rather than
  past it. Across the field they fly at full reach.
- Wild Swing is the only Stage II physical spell with blood: it says "this one tears". If the owner wants the
  self-tear told on a1 as well, it is the scene's wound, not a second blood burst.
- The 110° fan is wider than the kit's aimed default (50°): chosen so the swipe reads across, not through. *house*.

## Seize (`spell:extort:v1`)

### Intent

The player reads "the Brute bit that one and took its strength for itself": a dominating bite, and something going
from the target into the caster. Seize costs 2 energy, deals 4 damage to one enemy in melee, drains 1 energy from it
and gives 1 to the caster. Stage II. Its signature is the pull: what the bite tears off goes back toward a1, never
beyond b1, and lands on a1 as it returns home, under one light on a1.

### Today

Filmed at `15ee3b0`, 390 by 844, 1×, a1 on b1, 4 health lost: a whole action and a contact.

- 0 to 60 ms: the shared pale arc on a1. 840 to 900 ms: b1 filled in bone, a1 over its lower half.
- 960 to 1140 ms: a faint pale bite motif on b1, barely seen against its hide. Nothing goes to a1.

### Beats

| At | Lasts | Window | Layer | Beat | Pattern |
| --- | --- | --- | --- | --- | --- |
| 0 | 840 | windup | the caster | A medium melee blow's draw back and strike ([movement.md](movement.md)). No windup beat. | [Anticipation](../battle-craft.md#anticipation), [Contact and follow-through](../battle-craft.md#contact-and-follow-through) |
| 840 | 760 | impact | the effect | The `bite` motif on b1, gone by 1600 ms. `motif at 840, lasts 760` | [Shape before colour](../battle-craft.md#shape-before-colour), [The three parts of an effect](../battle-craft.md#the-three-parts-of-an-effect) |
| 840 | 320 | impact | the effect | Torn off and pulled: 12 bone shards (*house*) thrown back from b1 toward a1, none beyond b1. `burst at 840, count 12, shape shard, tint bone, direction back, spread 50, reach 220, lasts 320, blend normal` | [Burst](../battle-craft.md#burst), [Gravity well and convergence](../battle-craft.md#gravity-well-and-convergence) |
| 840 | 240 | impact | the field | A light staged shake: a bite, not a blast, decaying over about 240 ms (*house*). `shake at 840, strength 0.08` | [Shake from trauma](../battle-craft.md#shake-from-trauma) |
| 840 | 560 | impact | the caster and the target | The medium blow's hit stop, b1's bone flash, its recoil, a1's return ([movement.md](movement.md)). | [Hit stop](../battle-craft.md#hit-stop), [Recoil](../battle-craft.md#recoil) |
| 900 | 400 | impact | the effect | The strength leaving: 10 pale motes alive at once drawn out of b1 toward a1. `flow at 900, count 10, shape dot, tint family, direction back, spread 30, reach 340, strength 0.4, lasts 400, blend add` | [Flow](../battle-craft.md#flow), [Gravity well and convergence](../battle-craft.md#gravity-well-and-convergence) |
| 1080 | 360 | impact | the effect | The strength arriving: 14 pale motes (*house*) on a ring around a1 land on its centre as it reaches home. `gather at 1080, on caster, count 14, shape dot, tint family, reach 320, strength 0.4, lasts 360, blend add` | [Gravity well and convergence](../battle-craft.md#gravity-well-and-convergence) |
| 1440 | 280 | impact | the effect | The one light, on a1, as the gather lands: what was taken is now a1's. `glow at 1440, on caster, tint family, reach 160, strength 0.6, lasts 280, blend screen` | [Glow](../battle-craft.md#glow), [A light that answers a cast](../battle-craft.md#a-light-that-answers-a-cast) |
| 1720 | 820 | impact | the field | Nothing new. Both figures home. The result is read here. | [The hold before the number](../battle-craft.md#the-hold-before-the-number), [Breath at rest](../battle-craft.md#breath-at-rest) |

Six staged beats, none in the windup; 22 particles asked for on b1 (12, 10) and 14 on a1; one glow, on a1, a shake of
0.08, the last gone by 1720 ms: inside Stage II. One flash, the body's, on b1; one change of light, on a1.

### Reduced motion

Nothing. The frame after the action is shown at once with the result text: no bite, no motes, no light on a1.

### Cost

- Objects at the peak (1080 to 1160 ms): one `Graphics` for the motif, two emitters (the flow's last motes, the
  gather). Then the glow alone. Draw calls: at most three at once.
- Particles at the peak: 24 at 1080 ms (10 and 14); 36 asked for in all; of the field's 150.
- Bytes: not measured. Built by BC4, BC5 and the tuned kit; BC6 places the beats. Cancel path: the kit's.

### On the film strip

```bash
node scripts/battle/filmstrip.mjs spell:extort:v1 --actor a1 --targets b1 --damage 4 --out <folder>/seize.png
node scripts/battle/filmstrip.mjs spell:extort:v1 --actor a1 --targets b1 --damage 4 \
  --frames 24 --interval 60 --columns 6 --out <folder>/seize-contact.png
```

- 0 to 780 ms: no arc, mark or particle on a1.
- 960 to 1080 ms: shards on b1's lower body, pointing down toward a1; none above b1. Shortened (`back` on a blow
  struck up the field), they do not leave b1's outline.
- 960 to 1140 ms: pale motes at b1's chest and feet, a bright one at 960 ms; the bite motif faint on b1. None in the
  gap between b1 and a1.
- 1140 to 1320 ms: motes on a1's back and beside it, closing on it as a1 reaches home.
- 1440 ms (whole): a1 a shade paler, the light soft; none on b1.
- 1680 ms (whole) and every frame after: no effect object on the field. Nothing in the effect is gold.

The sheets cannot show the stop, the shake, or the feel at full speed.

### Left

- The gather is placed on a1 by the kit at a1's centre: if it follows the figure's home rather than the moving
  body, it lands where a1 will be. Chosen at 1080 ms so a1 is all but home (the sheet shows a1 home by 1200 ms).
- The energy moved is told by the motes and the light, not by a number: the number is the result text's.
- `back` is read from the line a1 to b1 and points the right way. On the bench it is shortened to 0.42, so the pull
  is told by the motes leaving b1 and the gather landing on a1, not by anything crossing the gap: the two halves
  read, the line between them does not. A blow struck across the field shows the whole pull.

## Crash (`spell:protective_slam:v1`)

### Intent

The player reads "the Brute rammed that one with its skull and left it dazed": a collision, then a stunned figure.
Crash costs 3 energy, deals 5 damage and stuns one enemy in melee for a round, with a 30% critical chance; a Stage II
blow, seen a few times a game, so it earns one light, a small staged shake and no windup beat. Its signature is the
stun: one hard, fast shock ring at contact, then slow bone motes rising over the stunned figure until the hold.

### Today

Filmed at `15ee3b0`, 390 by 844, 1×, a1 on b1, 5 health lost: a whole action and a contact.

- 0 to 60 ms: a pale arc on a1's body, the shared charge. 600 to 780 ms: a1 leaves its ring toward b1.
- 840 and 900 ms: b1 filled flat in bone; a1 covers b1's lower half, b1's head clear.
- 960 to 1380 ms: three pale upright strokes on b1, fading. No particle, no ring. The same sheet as Body Slam's,
  frame for frame: nothing says "stun".

### Beats

| At | Lasts | Window | Layer | Beat | Pattern |
| --- | --- | --- | --- | --- | --- |
| 0 | 840 | windup | the caster | A medium or heavy melee blow's draw back and strike by the 5 health b1 lost ([movement.md](movement.md)). No mark on a1: no windup beat, so no shared arc. | [Anticipation](../battle-craft.md#anticipation), [Contact and follow-through](../battle-craft.md#contact-and-follow-through) |
| 840 | 900 | impact | the caster and the target | The hit stop, b1's bone flash, its tremor and recoil, a1's return, home by about 1740 ms ([movement.md](movement.md)). | [Hit stop](../battle-craft.md#hit-stop), [Recoil](../battle-craft.md#recoil) |
| 840 | 700 | impact | the effect | The `impact` motif on b1, leaning along the blow on its dark edge, gone by 1540 ms. `motif at 840, lasts 700` | [An impact that keeps the direction](../battle-craft.md#an-impact-that-keeps-the-direction), [Shape before colour](../battle-craft.md#shape-before-colour) |
| 840 | 440 | impact | the effect | The signature's first half: one hard shock ring opens fast on the ground at b1's feet to well past its silhouette, bright, and fades by 1280 ms. *house* reach and time, shorter and harder than Body Slam's. `glow at 840, shape ring, tint bone, reach 320, strength 0.9, lasts 440, blend screen` | [Glow](../battle-craft.md#glow), [One effect, one meaning](../battle-craft.md#one-effect-one-meaning) |
| 840 | 320 | impact | the effect | Bone chips knocked off b1 and thrown on beyond it in a 60° fan along the blow, clear of the body. 16 (*house*). `burst at 840, count 16, shape shard, tint bone, direction along, spread 60, reach 320, lasts 320, blend normal` | [Burst](../battle-craft.md#burst), [An impact that keeps the direction](../battle-craft.md#an-impact-that-keeps-the-direction) |
| 840 | 200 | impact | the camera | A small staged shake over the tremor of the health lost: the skull's crack. `shake at 840, strength 0.1` | [Shake from trauma](../battle-craft.md#shake-from-trauma) |
| 880 | 360 | impact | the effect | Dust kicked up from the ground at b1's feet, small grains thrown upward in a narrow fan, not out all round (Pummel's collar). 8 (*house*). `burst at 880, count 8, shape dot, tint family, direction up, spread 50, reach 240, strength 0.25, lasts 360, blend normal, level feet` | [Burst](../battle-craft.md#burst), [A beat on the ground](../battle-craft.md#a-beat-on-the-ground) |
| 1000 | 900 | impact | the effect | The signature's second half, the daze: 10 small bone motes (*house*) rise slowly from b1's head, few at once, until 1900 ms. `flow at 1000, count 10, shape dot, tint bone, direction up, spread 30, reach 260, strength 0.6, lasts 900, blend screen, level head` | [Flow](../battle-craft.md#flow), [A beat on the ground](../battle-craft.md#a-beat-on-the-ground) |
| 1900 | 640 | impact | the field | Nothing new. Both figures home, breathing. The result is read here. | [The hold before the number](../battle-craft.md#the-hold-before-the-number), [Breath at rest](../battle-craft.md#breath-at-rest) |

Six staged beats, 34 particles asked for on b1 (16, 8, 10), one glow (the ring), a shake of 0.1, no windup beat, the
last gone by 1900 ms: inside Stage II. One flash, the body's, on b1; one change of light, the ring.

### Reduced motion

Nothing. The frame after the action is shown at once with the result text: no blow, no ring, no chip, no mote.

### Cost

- Objects at the peak (880 to 1160 ms): one `Graphics` for the motif, one ring sprite, three emitters (chips, dust,
  motes from 1000 ms), the body's tweens. Draw calls: about five during 280 ms, then two.
- Particles at the peak: 24 at 900 ms (16 chips, 8 grains); 34 asked for in all; of the field's 150.
- Bytes: not measured. Built by BC4 (motif, burst, flow, glow, shake, the `staging` field), BC5 (the body), BC6 (the
  ring shape of `glow`, the dark edge); BC6 places the beats. The cancel path is the kit's.

### On the film strip

```bash
node scripts/battle/filmstrip.mjs spell:protective_slam:v1 --actor a1 --targets b1 --damage 5 --out <folder>/crash.png
node scripts/battle/filmstrip.mjs spell:protective_slam:v1 --actor a1 --targets b1 --damage 5 \
  --frames 24 --interval 60 --columns 6 --out <folder>/crash-contact.png
```

- 0 to 780 ms: no mark, arc or particle on a1's body.
- 900 ms: a small bright ring at b1's feet, lying on the ground; the strokes upright on b1.
- 960 ms: the ring as wide as b1's body, flat, drawn over a1's back; a puff of dust at b1's middle; chips over b1's
  head, short, none on a1's side.
- 1020 and 1080 ms: the ring wider than b1 and fainter, at 1080 ms reaching the next figure's legs. 1140 ms: no chip.
- 1140 to 1380 ms: the ring fading, gone by 1320 ms; soft pale motes on b1's head and just above it, rising.
- 1440 ms (whole): two or three motes above b1's head, the strokes gone. 1680 ms (whole): the last mote or two.
- 1680 ms (whole) and every frame after: no effect object on the field; a1 home. Nothing gold in the effect.
- Beside Body Slam at 1080 ms: Crash's ring is brighter and gone sooner, and no dust band lies along its feet.

The sheets cannot show the stop, the shake, the punch zoom of a critical or the feel at full speed.

### Left

- The stun is told by the motes for about a second; the round-long stun is the status chip's to say, not the effect's.
- The motes rise; a daze could circle the head, which needs an orbit the kit has not (a book entry first).
- The ring lies at b1's feet now, and the daze rises from its head (`level`).
- The daze's strength went from 0.35 to 0.6 after the third films, where it was barely seen; on the fourth it is
  read on b1's head from 1140 ms.
- No windup beat, by choice: the skull is lowered by the body's draw back; Body Slam takes the windup dust instead.

## Carapace (`spell:full_plate:v1`)

### Intent

The player reads "the Brute pulled its plates in and locked them": its own armour thickening, nothing thrown at
anyone. Carapace costs 1 energy and gives the caster +7 defense for 2 rounds; Stage II, a self-buff cast at need, so
it earns one soft light and one windup beat, no shake and no flash. Its signature is the plates closing: bone shards
drawn in from far around a1 through the windup, then the `plates` motif under a bone light, then a second, tighter
layer landing.

### Today

Filmed at `15ee3b0`, 390 by 844, 1×, a1 on itself: a whole action and a contact.

- 0 to 60 ms: a pale arc on a1's body. a1 squats a little from 480 ms and rises at 840 ms.
- 900 to 1380 ms: three pale shields on a1, fading slowly. Nothing comes in or locks: any family's ward.

### Beats

| At | Lasts | Window | Layer | Beat | Pattern |
| --- | --- | --- | --- | --- | --- |
| 0 | 840 | windup | the caster | A cast's load where it stands ([movement.md](movement.md)): a1 squats. | [Anticipation](../battle-craft.md#anticipation), [Squash and stretch](../battle-craft.md#squash-and-stretch) |
| 480 | 360 | windup | the effect | The signature's first half: 16 bone shards (*house*) start on a wide ring well outside a1's silhouette and land on its centre at 840 ms. `gather at 480, on caster, count 16, shape shard, tint bone, reach 340, lasts 360, blend normal` | [Gravity well and convergence](../battle-craft.md#gravity-well-and-convergence), [Shape before colour](../battle-craft.md#shape-before-colour) |
| 840 | 360 | impact | the caster | The release, as a cast lets go ([movement.md](movement.md)). a1 lost nothing: no flash. | [Contact and follow-through](../battle-craft.md#contact-and-follow-through) |
| 840 | 900 | impact | the effect | The `plates` motif on a1, set as the shards land, gone by 1740 ms. `motif at 840, lasts 900` | [The three parts of an effect](../battle-craft.md#the-three-parts-of-an-effect), [Shape before colour](../battle-craft.md#shape-before-colour) |
| 840 | 420 | impact | the effect | The bone light under the plates: one soft glow, not a ring, close to the body. `glow at 840, on target, tint bone, reach 170, strength 0.6, lasts 420, blend screen` | [Glow](../battle-craft.md#glow), [Additive light](../battle-craft.md#additive-light) |
| 840 | 360 | impact | the effect | The plates set: 12 grains of dust (*house*) pushed off a1 all round, clear of its outline. `burst at 840, on target, count 12, shape dot, tint family, direction out, spread 360, reach 300, strength 0.4, lasts 360, blend normal` | [Burst](../battle-craft.md#burst), [One effect, one meaning](../battle-craft.md#one-effect-one-meaning) |
| 940 | 280 | impact | the effect | The signature's second half, the thickening: 10 shards (*house*) on a tighter ring land on a1 by 1220 ms. `gather at 940, on target, count 10, shape shard, tint bone, reach 280, lasts 280, blend normal` | [Gravity well and convergence](../battle-craft.md#gravity-well-and-convergence), [Stagger and overlap](../battle-craft.md#stagger-and-overlap) |
| 1220 | 240 | impact | the effect | The lock: 8 small bone chips (*house*) knocked off as the second layer lands, just past the outline. `burst at 1220, on target, count 8, shape shard, tint bone, direction out, spread 360, reach 240, strength 0.3, lasts 240, blend normal` | [Burst](../battle-craft.md#burst) |
| 1740 | 800 | impact | the field | Nothing new. a1 home, breathing. The result is read here. | [The hold before the number](../battle-craft.md#the-hold-before-the-number), [Breath at rest](../battle-craft.md#breath-at-rest) |

Six staged beats, 46 particles asked for on a1 (16, 12, 10, 8), one glow, one windup beat, no shake, the last gone by
1740 ms: inside Stage II. No flash; one change of light.

### Reduced motion

Nothing. The frame after the action is shown at once with the result text: no load, no shards, no plates, no light.

### Cost

- Objects at the peak (940 to 1200 ms): one `Graphics` for the motif, one glow sprite, two or three emitters.
  Draw calls: about four for 260 ms, then two.
- Particles at the peak: 22 at 960 ms (12 grains, 10 shards); 46 asked for in all; of the field's 150.
- Bytes: not measured. Built by BC4 (motif, burst, gather, glow, the `staging` field) and BC5 (the cast's body); BC6
  places the beats. The cancel path is the kit's: the windup gather dies with the action.

### On the film strip

```bash
node scripts/battle/filmstrip.mjs spell:full_plate:v1 --actor a1 --targets a1 --out <folder>/carapace.png
node scripts/battle/filmstrip.mjs spell:full_plate:v1 --actor a1 --targets a1 \
  --frames 24 --interval 60 --columns 6 --out <folder>/carapace-contact.png
```

- 0 to 480 ms: no mark, arc or particle on a1.
- 540 ms: a few faint shards far around a1; 660 ms: shards just above a1's back and beside it, closing. 780 ms: landed.
- 840 ms: the plates faint on a1. 900 ms: the plates on a1's middle under a soft pale light, a puff of dust on them.
- 1020 ms: dust grains outside a1's outline on every side; a second ring of shards above a1's back.
- 1140 and 1260 ms: the plates, fading; no light. 1380 ms: three chips just above a1's back, the lock.
- 1740 ms (whole) and every frame after: no effect object. No other figure has moved or been lit on any frame.
- Nothing in the effect is gold; beside Brace at 1080 ms, Carapace's shards came from twice as far, and twice.

The sheets cannot show the feel of the lock at full speed, nor its sound.

### Left

- The shards come in before contact, in the windup, so the plates are set at 840 ms; Brace's come in after. The owner
  may prefer the gather after contact; then the windup would hold nothing, and the step above Brace would shrink.
- No shake: nothing was struck. A small one at the lock (0.05) could say "heavy"; the book ties shake to loss.
- The two rounds and the +7 are the status chip's to say; the motif fades by 1740 ms like any other.
- `on caster` and `on target` are the same figure here; the rows say `on target` after contact so a future ally form
  of the spell would not change its reading.

## Body Slam (`spell:shield_bash:v1`)

### Intent

The player reads "the Brute threw its whole weight into that one": mass, not a point of impact. Body Slam costs 3
energy, deals 5 damage to one enemy in melee with a 35% critical chance, and gives the caster +2 defense; a Stage II
blow, so one windup beat, one light and the largest staged shake of the three. Its signature is the mass: a wide,
slow ground ring and a low wave of dust pushed along the blow and beyond the target. No sparks.

### Today

Filmed at `15ee3b0`, 390 by 844, 1×, a1 on b1, 5 health lost: a whole action and a contact.

- 0 to 60 ms: a pale arc on a1. 600 to 780 ms: a1 leaves its ring toward b1.
- 840 and 900 ms: b1 filled flat in bone; a1 covers b1's lower half.
- 960 to 1380 ms: three pale upright strokes on b1. Crash's sheet, frame for frame: no weight, no ground.

### Beats

| At | Lasts | Window | Layer | Beat | Pattern |
| --- | --- | --- | --- | --- | --- |
| 0 | 840 | windup | the caster | A medium or heavy melee blow's draw back and strike by the 5 health b1 lost ([movement.md](movement.md)). | [Anticipation](../battle-craft.md#anticipation), [Contact and follow-through](../battle-craft.md#contact-and-follow-through) |
| 600 | 240 | windup | the effect | The charge: 8 grains of dust (*house*) kicked back off a1 as it drives forward. `flow at 600, on caster, count 8, shape dot, tint family, direction back, spread 40, reach 120, strength 0.4, lasts 240, blend normal` | [Flow](../battle-craft.md#flow), [Anticipation](../battle-craft.md#anticipation) |
| 840 | 900 | impact | the caster and the target | The hit stop, b1's bone flash, its tremor and recoil, a1's return, home by about 1740 ms ([movement.md](movement.md)). | [Hit stop](../battle-craft.md#hit-stop), [Recoil](../battle-craft.md#recoil) |
| 840 | 700 | impact | the effect | The `impact` motif on b1, along the blow, on its dark edge. `motif at 840, lasts 700` | [An impact that keeps the direction](../battle-craft.md#an-impact-that-keeps-the-direction), [Shape before colour](../battle-craft.md#shape-before-colour) |
| 840 | 480 | impact | the effect | The signature's wave: 18 dust grains (*house*) pushed along the blow in a wide fan that lies on the ground at b1's feet. `burst at 840, count 18, shape dot, tint family, direction along, spread 120, reach 360, strength 0.35, lasts 480, blend normal, level feet` | [Burst](../battle-craft.md#burst), [A beat on the ground](../battle-craft.md#a-beat-on-the-ground) |
| 840 | 240 | impact | the camera | The weight landing: the largest staged shake of Stage II. `shake at 840, strength 0.15` | [Shake from trauma](../battle-craft.md#shake-from-trauma) |
| 860 | 320 | impact | the effect | Clods of bone and earth, fewer and heavier than the dust, along the blow. `burst at 860, count 10, shape shard, tint bone, direction along, spread 50, reach 300, lasts 320, blend normal` | [Burst](../battle-craft.md#burst) |
| 880 | 600 | impact | the effect | The signature's ground ring, aimed: a wide, slow, dull arc of 120° opening on the ground at b1's feet on the far side of the blow, past its silhouette, after the hit, gone by 1480 ms. *house*. Aimed on the owner's word (2026-11-19), so it is not Crash's full ring. `glow at 880, shape ring, direction along, reach 380, strength 0.6, lasts 600, tint family, blend normal` | [Glow](../battle-craft.md#glow), [One effect, one meaning](../battle-craft.md#one-effect-one-meaning) |
| 1000 | 600 | impact | the effect | The dust hanging: 8 grains (*house*) drifting on along the ground at b1's feet, gone by 1600 ms. `flow at 1000, count 8, shape dot, tint family, direction along, spread 60, reach 160, strength 0.4, lasts 600, blend normal, level feet` | [Flow](../battle-craft.md#flow), [A beat on the ground](../battle-craft.md#a-beat-on-the-ground) |
| 1740 | 800 | impact | the field | Nothing new. Both figures home, breathing. The result is read here. | [The hold before the number](../battle-craft.md#the-hold-before-the-number), [Breath at rest](../battle-craft.md#breath-at-rest) |

Seven staged beats, 36 particles asked for on b1 (18, 10, 8) and 8 on a1, one glow, one windup beat, a shake of 0.15,
the last gone by 1600 ms: inside Stage II. One flash, the body's, on b1; one change of light, the ring.

### Reduced motion

Nothing. The frame after the action is shown at once with the result text: no charge, no dust, no ring.

### Cost

- Objects at the peak (880 to 1160 ms): one `Graphics` for the motif, one ring sprite, two or three emitters, the
  body's tweens. Draw calls: about five during 280 ms, then two or three.
- Particles at the peak: 28 at 900 ms (18 grains, 10 clods); 44 asked for in all; of the field's 150.
- Bytes: not measured. Built by BC4 (motif, burst, flow, glow, shake, the `staging` field), BC5 (the body), BC6 (the
  ring shape of `glow`); BC6 places the beats. The cancel path is the kit's: the windup flow dies with the action.

### On the film strip

```bash
node scripts/battle/filmstrip.mjs spell:shield_bash:v1 --actor a1 --targets b1 --damage 5 --out <folder>/body-slam.png
node scripts/battle/filmstrip.mjs spell:shield_bash:v1 --actor a1 --targets b1 --damage 5 \
  --frames 24 --interval 60 --columns 6 --out <folder>/body-slam-contact.png
```

- 0 to 600 ms: no mark, arc or particle on a1. 660 to 780 ms: dust kicked off below a1 as it drives up the field.
- 900 ms: a band of dust at b1's feet, drawn over a1's back.
- 960 ms: the dust a flat band across b1's feet, wider than b1; clods short over b1's back and head, none on a1's
  side. 1020 ms: the ring forming at b1's feet.
- 1080 to 1260 ms: a dull arc wider than b1, flat, beyond b1 on the far side of the blow and none on a1's side,
  widening toward the next figure's legs; grains low at b1's feet. Not a closed ellipse.
- 1200 ms (whole): the arc still seen. 1440 ms (whole) and every frame after: no effect object on the field; a1
  home. No spark, nothing gold.
- Beside Crash at 1080 ms: Body Slam lays a dust band along b1's feet and its arc is duller and open; at 1200 ms Body Slam's
  arc is still seen and Crash's is all but gone.

The sheets cannot show the stop, the shake, the punch zoom of a critical or the feel at full speed.

### Left

- "Plates tighten as defense rises" is the caster's +2 defense: the scene's small neutral mark, not the staging's.
  The owner may want a plates beat on a1; it would borrow Carapace's signature, so it is left out.
- The ring and the wave now lie at b1's feet. Settled by the owner, 2026-11-19: Body Slam's ring is aimed
  (`direction along`), an arc beyond b1, so it is told from Crash's full ring by shape as well as brightness and time.

## Dominate (`spell:chain_slash:v1`)

### Intent

The player reads "the Tyrant brought both its immense front paws down, one on each of them": two enemies struck,
each one twice in quick succession. Dominate costs 3 energy and deals 7 damage to up to two enemies with a 50%
critical chance; it is a Stage III spell, cast a few times a game, so it may take the whole impact. Its signature is
the double hammer: on each target a first blow and, 160 ms later, a second, each with its own burst and its own shock
ring, so every target shows two rings, one inside the other.

### Today

Filmed at `15ee3b0`, 390 by 844, 1×, a1 on b1 and b2, 7 health lost: a whole action and a contact.

- 0 to 120 ms: a pale arc on a1, the shared charge. 540 to 780 ms: a1 draws back and leaps toward b1.
- 840 to 900 ms: b1 and b2 both filled flat in bone by the body; a1 covers b1's lower half, b1's head clear.
- 960 to 1380 ms: the same pale upright strokes and ring on each target as on any blow. One hit, one mark: nothing
  says "two paws".

### Beats

| At | Lasts | Window | Layer | Beat | Pattern |
| --- | --- | --- | --- | --- | --- |
| 0 | 840 | windup | the caster | A heavy melee blow's draw back, hold and strike on b1, the nearer target ([movement.md](movement.md)), scaled by the health lost. | [Anticipation](../battle-craft.md#anticipation), [Contact and follow-through](../battle-craft.md#contact-and-follow-through) |
| 360 | 360 | windup | the effect | The paws loaded: 12 bone shards (*house*) start on a ring around a1 and land on its body before it leaps. `gather at 360, on caster, count 12, shape shard, tint bone, reach 200, lasts 360, blend normal` | [Gravity well and convergence](../battle-craft.md#gravity-well-and-convergence), [Anticipation](../battle-craft.md#anticipation) |
| 480 | 240 | windup | the effect | The crown's resonance: a dim bone light swells on a1 and goes by 720 ms, before a1 leaves its ring. `glow at 480, on caster, tint bone, reach 160, strength 0.6, lasts 240, blend screen` | [Glow](../battle-craft.md#glow), [Additive light](../battle-craft.md#additive-light) |
| 840 | 1000 | impact | the effect | The `impact` motif on b1 and b2, its strokes leaning along the blow on their dark edge, gone by 1840 ms. `motif at 840, lasts 1000` | [An impact that keeps the direction](../battle-craft.md#an-impact-that-keeps-the-direction), [Shape before colour](../battle-craft.md#shape-before-colour) |
| 840 | 900 | impact | the caster and the targets | The heavy blow's hit stop, the body's bone flash on each target that lost health, the tremor, the recoil, a1's return; the punch zoom on a critical ([movement.md](movement.md)). | [Hit stop](../battle-craft.md#hit-stop), [Recoil](../battle-craft.md#recoil) |
| 840 | 320 | impact | the effect | The first paw: 20 bone shards (*house*) knocked off each target and thrown along the blow, clear of its body. `burst at 840, count 20, shape shard, tint bone, direction along, spread 60, reach 320, lasts 320, blend normal` | [Burst](../battle-craft.md#burst), [An impact that keeps the direction](../battle-craft.md#an-impact-that-keeps-the-direction) |
| 840 | 480 | impact | the effect | The first ring: a bone shock ring opens from each target to just past its outline. `glow at 840, shape ring, tint bone, reach 320, strength 0.7, lasts 480, blend screen` | [Glow](../battle-craft.md#glow), [Stagger and overlap](../battle-craft.md#stagger-and-overlap) |
| 1000 | 320 | impact | the effect | The second paw, the signature: 20 more shards (*house*), thrown out on every side this time, so the two blows read apart. `burst at 1000, count 20, shape shard, tint bone, direction out, spread 360, reach 360, lasts 320, blend normal` | [Burst](../battle-craft.md#burst), [One effect, one meaning](../battle-craft.md#one-effect-one-meaning) |
| 1000 | 560 | impact | the effect | The second ring, in the family's taupe, wider and later: inside the first as it starts, past it as it ends. `glow at 1000, shape ring, tint family, reach 380, strength 0.8, lasts 560, blend screen` | [Glow](../battle-craft.md#glow), [Stagger and overlap](../battle-craft.md#stagger-and-overlap) |
| 1000 | 300 | impact | the camera | The second paw is felt: a staged shake over what is left of the blow's trauma. `shake at 1000, strength 0.25` | [Shake from trauma](../battle-craft.md#shake-from-trauma) |
| 1000 | 480 | impact | the effect | Dust driven out of the ground by the second paw: 12 small taupe grains (*house*), kept small so they do not cloud the target. `burst at 1000, count 12, shape dot, tint family, direction out, spread 360, reach 260, strength 0.4, lasts 480, blend normal` | [Burst](../battle-craft.md#burst), [Shape before colour](../battle-craft.md#shape-before-colour) |
| 1960 | 580 | impact | the field | Nothing new. Both targets breathing, a1 home. The result is read here; the hold from 2400 ms is empty. | [The hold before the number](../battle-craft.md#the-hold-before-the-number), [Breath at rest](../battle-craft.md#breath-at-rest) |

Nine staged beats, two in the windup. 52 particles asked for on each target (20, 20, 12), at most 52 at once
(1000 to 1160 ms), and 12 on a1: 116 on the field at the peak. Four glows (two rings a target), no pulse, a shake
of 0.25, the last gone by 1960 ms: inside Stage III. One flash a target, the body's. Nothing in the settle.

### Reduced motion

Nothing. The frame after the action is shown at once with the result text: no leap, no ring, no shard, no dust.

### Cost

- Objects at the peak (1000 to 1160 ms): two motif `Graphics`, on each target two ring glows and three emitters
  (first shards fading, second shards, dust): about 12 objects. Draw calls: the screen glows batch together, each
  emitter is one; about 8 for 160 ms.
- Particles at the peak: 104 on the targets at 1000 to 1160 ms, of the field's 150; the gather is gone by 800 ms.
- Bytes: not measured. Built by BC4 (motif, burst, gather, flow, glow, shake, `staging`), BC5 (the body) and BC6
  (the ring shape, `reach` to 400, `strength`, the leaning strokes); BC6 places the beats. The cancel path is the
  kit's: every emitter and glow killed with the action.

### On the film strip

```bash
node scripts/battle/filmstrip.mjs spell:chain_slash:v1 --actor a1 --targets b1,b2 --damage 7 --out <folder>/dominate.png
node scripts/battle/filmstrip.mjs spell:chain_slash:v1 --actor a1 --targets b1,b2 --damage 7 \
  --frames 24 --interval 60 --columns 6 --out <folder>/dominate-contact.png
```

- 0 to 480 ms (contact): no arc, mark or particle on a1.
- 480 and 540 ms: bone shards around a1's back, closing on it; 600 and 660 ms: a dim light on a1's middle.
- 720 ms: a1 rising off its ring, a trace of the light at its feet. 780 ms: nothing left where it stood.
- 840 ms: b1 and b2 filled bone; a1 covers b1's lower half, not its head; a small ring already at b2's feet.
- 900 and 960 ms: a ring at each target's feet, wider at 960 ms; short shards over b1's head; b1's strokes upright,
  b2's leaning along the line from a1.
- 1020 ms: the second paw: large shards on b1's body. 1080 and 1140 ms: two rings at each target's feet, the taupe
  one inside the bone one, shards and grains on every side of both.
- 1200 to 1320 ms: the rings wide and fainter, b1's and b2's crossing between them; grains low; no shard by 1320 ms.
- 1440 ms (whole): the outer rings faint. 1680 ms (whole) and every frame after: no effect object; a1 home.
  Nothing in the effect is gold.

The sheets cannot show the stop, the two shakes as two, the punch zoom of a critical, or the feel at full speed.

### Left

- The host confirms one hit of 7 a target; the second paw is staged, not a second hit. The body strikes once: a
  second downward stretch of a1 between 960 and 1100 ms would show the paw itself, and is the body's (BC5), not a
  staging's. The owner may find a staged second blow on one confirmed hit misleading; then drop the second burst and
  keep the two rings as an aftershock.
- a1 strikes b1 and only b1 is touched by the body; b2 takes the effect 40 ms later at a distance. A paw that
  reaches b2 needs a melee onto two figures, which the body does not have.
- The staged shake rides on the blow's trauma (ceiling 0.6): on a heavy hit it may add little. The film cannot say.
- No pulse: the band allows one, but a halo on a1 is Crushing Stomp's signature, and none goes on the targets.
- The first ring is seen at 900 ms (#917 asked whether it was 960): it lies at the feet now and nothing covers it.
- The gather starts at 360 and the glow at 480, so both end by 720 ms, before a1 leaves its ring: on the third
  films they were left on the ground where a1 stood (780 ms). A windup beat that goes with the body is the kit's.

## Alpha Roar (`spell:thundering_seal:v1`)

### Intent

The player reads "the Tyrant roared, and its ally stood up stronger": the sound seen leaving the caster, reaching
the ally, and the ally lit. Alpha Roar costs 2 energy and gives one ally +4 defense and +2 damage for 2 rounds; it is
Stage III, cast a few times a game, and may take the whole impact. Its signature is the roar's rings: three shock
rings leave a1 one after another, the last reaching the ally, which then takes the light, the one pulse and a
rising flow.

### Today

Filmed at `15ee3b0`, 390 by 844, 1×, a1 on a2: a whole action and a contact.

- 0 to 60 ms: a pale arc on a1. a1 barely moves on any frame of the windup.
- 900 to 1380 ms: faint concentric rings at a2's feet, the `roar` motif, low in contrast on the ground.
- Nothing leaves a1, and a2 is not lit: the roar is not heard, and the gift could be anyone's.

### Beats

| At | Lasts | Window | Layer | Beat | Pattern |
| --- | --- | --- | --- | --- | --- |
| 0 | 840 | windup | the caster | A cast's load: a1 squats where it stands ([movement.md](movement.md)). | [Anticipation](../battle-craft.md#anticipation), [Squash and stretch](../battle-craft.md#squash-and-stretch) |
| 360 | 440 | windup | the effect | The breath drawn in: 16 small taupe grains (*house*) start on a ring around a1 and land on its chest. `gather at 360, on caster, count 16, shape dot, tint family, reach 260, strength 0.6, lasts 440, blend screen` | [Gravity well and convergence](../battle-craft.md#gravity-well-and-convergence), [Anticipation](../battle-craft.md#anticipation) |
| 840 | 360 | impact | the caster | The release, as a cast lets go ([movement.md](movement.md)): the roar is in the body. a2 does not react: it lost nothing. | [Contact and follow-through](../battle-craft.md#contact-and-follow-through) |
| 840 | 480 | impact | the effect | The first ring of the roar leaves a1: an arc of 120° on the ground at a1's feet, on a2's side only. `glow at 840, on caster, shape ring, tint family, reach 300, strength 0.8, lasts 480, blend screen, direction along` | [Glow](../battle-craft.md#glow), [A beat on the ground](../battle-craft.md#a-beat-on-the-ground) |
| 840 | 400 | impact | the effect | Dust blown off the ground round a1's feet: 14 small grains (*house*), out on every side, flat. `burst at 840, on caster, count 14, shape dot, tint family, direction out, spread 360, reach 340, strength 0.25, lasts 400, blend normal, level feet` | [Burst](../battle-craft.md#burst), [Contact and follow-through](../battle-craft.md#contact-and-follow-through) |
| 840 | 300 | impact | the camera | The roar shakes the field, lightly: no health is lost, so this is the only trauma. `shake at 840, strength 0.15` | [Shake from trauma](../battle-craft.md#shake-from-trauma) |
| 1000 | 520 | impact | the effect | The second arc, wider. `glow at 1000, on caster, shape ring, tint family, reach 360, strength 0.7, lasts 520, blend screen, direction along` | [Glow](../battle-craft.md#glow), [Stagger and overlap](../battle-craft.md#stagger-and-overlap) |
| 1160 | 480 | impact | the effect | The third arc, the widest, its edge reaching a2's ground ring at about 1400 ms (*house*, the film tunes it). `glow at 1160, on caster, shape ring, tint bone, reach 400, strength 0.6, lasts 480, blend screen, direction along` | [Glow](../battle-craft.md#glow), [Stagger and overlap](../battle-craft.md#stagger-and-overlap) |
| 1400 | 400 | impact | the effect | The ally takes the light: a soft bone light swells on a2 and goes. `glow at 1400, tint bone, reach 180, strength 0.4, lasts 400, blend screen` | [Glow](../battle-craft.md#glow), [Additive light](../battle-craft.md#additive-light) |
| 1400 | 500 | impact | the target | The one pulse: a bone halo on a2's own outline, "this one is stronger". `pulse at 1400, tint bone, strength 0.6, lasts 500` | [Glow](../battle-craft.md#glow) |
| 1400 | 900 | impact | the effect | The `roar` motif on a2, gone by 2300 ms. `motif at 1400, lasts 900` | [The three parts of an effect](../battle-craft.md#the-three-parts-of-an-effect), [Shape before colour](../battle-craft.md#shape-before-colour) |
| 1440 | 800 | impact | the effect | The ally standing taller: 12 bone motes alive at once rise from a2, gone by 2240 ms. `flow at 1440, count 12, shape dot, tint bone, direction up, spread 40, reach 160, strength 0.4, lasts 800, blend screen` | [Flow](../battle-craft.md#flow), [Additive light](../battle-craft.md#additive-light) |
| 2300 | 240 | impact | the field | Nothing new. Both figures breathing. The result is read here; the hold from 2400 ms is empty. | [The hold before the number](../battle-craft.md#the-hold-before-the-number), [Breath at rest](../battle-craft.md#breath-at-rest) |

Ten staged beats, one in the windup. 30 particles on a1 (16, 14) and 12 on a2. Four glows, one pulse on a2, a
shake of 0.15, the last gone by 2300 ms: inside Stage III. No flash: a2 lost nothing. One change of light, a2's.

### Reduced motion

Nothing. The frame after the action is shown at once with the result text: no rings, no light, no motes.

### Cost

- Objects at the peak (1400 to 1640 ms): the third ring, the light, the pulse filter on a2, the motif `Graphics`,
  one emitter. Draw calls: about 5; the pulse is a framebuffer and a pass each frame for 500 ms, the costliest beat.
- Particles at the peak: 14 on a1 at 840 to 1240 ms (the gather is gone by 800), 12 on a2 from 1440 ms; of 150.
- Bytes: not measured. Built by BC4 (motif, burst, gather, flow, glow, pulse, shake, `staging`), BC5 (the cast's
  body), BC6 (the ring shape, `reach`, `strength`); BC6 places the beats. The cancel path is the kit's, the pulse's
  filter removed with the action.

### On the film strip

```bash
node scripts/battle/filmstrip.mjs spell:thundering_seal:v1 --actor a1 --targets a2 --out <folder>/alpha-roar.png
node scripts/battle/filmstrip.mjs spell:thundering_seal:v1 --actor a1 --targets a2 \
  --frames 24 --interval 60 --columns 6 --out <folder>/alpha-roar-contact.png
```

- 0 to 420 ms (contact): no arc or particle on a1. 480 to 660 ms: grains over a1's back, closing on it.
- 900 ms: one short arc at a1's front feet, on a2's side; dust grains round a1's feet; nothing on a2.
- 960 to 1080 ms: two arcs, the second inside the first, opening toward a2 only; no arc behind a1.
- 1140 to 1320 ms: two arcs in the gap between a1 and a2, the first fading. 1380 ms: the widest at a2's ground
  ring, touching a2's near legs; a2 unlit.
- 1440 ms (whole): a2 a shade paler under its light, the roar motif on its middle. a2 not filled, not moved.
- 1680 ms (whole): the motif fading. 1920 ms (whole) and every frame after: no effect object on the field.
  Nothing in the effect is gold.

The sheets cannot show the roar's sound, the light shake, or the feel at full speed.

### Left

- The rings are arcs aimed at a2 (`direction along`): the roar is seen going to the ally, and through no one.
- The `roar` motif on a2 is also rings; drawn after a1's rings it may read as a fourth. If it does, keep the motif
  and drop the third ring's reach to 360, before dropping the motif.
- The buff is two (defense, damage); the staging shows one gift. The rounds and the numbers are the status chip's.
- The shake on a roar is a choice: a support with a camera move. Remove it if the owner finds it a hit's language.

## Crushing Stomp (`spell:crushing_stomp:v1`)

### Intent

The player reads "the Colossus raised its immense forefoot and crushed that one into the ground, and it stays down":
a charge seen, one crushing blow, the target stunned. Crushing Stomp costs 4 energy, deals 7 damage with a 75%
critical chance and stuns one enemy for 2 rounds: the Colossus' great blow, Stage III, and it may take the whole
impact. Its signature is the crown: a bone halo on a1 in the windup, then at contact a crown of shards thrown up, a
ground ring, an aftershock ring, and dust that stays low around the stunned target.

### Today

Filmed at `15ee3b0`, 390 by 844, 1×, a1 on b1, 7 health lost: a whole action and a contact.

- 0 to 120 ms: a pale arc on a1. 540 to 780 ms: a1 draws back and leaps.
- 840 to 900 ms: b1 filled bone by the body; a1 covers b1's lower half, b1's head clear.
- 960 to 1380 ms: pale upright strokes and a ring on b1, as on any blow: no foot, no crush, no stun.

### Beats

| At | Lasts | Window | Layer | Beat | Pattern |
| --- | --- | --- | --- | --- | --- |
| 0 | 840 | windup | the caster | A heavy melee blow's draw back, hold and strike ([movement.md](movement.md)), scaled by the health lost. | [Anticipation](../battle-craft.md#anticipation), [Contact and follow-through](../battle-craft.md#contact-and-follow-through) |
| 120 | 560 | windup | the effect | The weight gathered: 16 bone shards (*house*) start wide around a1 and land on its body. `gather at 120, on caster, count 16, shape shard, tint bone, reach 260, strength 0.75, lasts 560, blend normal` | [Gravity well and convergence](../battle-craft.md#gravity-well-and-convergence), [Anticipation](../battle-craft.md#anticipation) |
| 440 | 400 | windup | the caster | The halo, the one pulse: a bone glow on a1's outline as the foot rises, gone at contact. `pulse at 440, on caster, tint bone, strength 0.7, lasts 400` | [Glow](../battle-craft.md#glow), [Anticipation](../battle-craft.md#anticipation) |
| 480 | 360 | windup | the effect | A bone light under the halo. `glow at 480, on caster, tint bone, reach 220, strength 0.3, lasts 360, blend screen` | [Glow](../battle-craft.md#glow), [Additive light](../battle-craft.md#additive-light) |
| 840 | 900 | impact | the effect | The `impact` motif on b1, leaning along the blow on its dark edge, gone by 1740 ms. `motif at 840, lasts 900` | [An impact that keeps the direction](../battle-craft.md#an-impact-that-keeps-the-direction), [Shape before colour](../battle-craft.md#shape-before-colour) |
| 840 | 900 | impact | the caster and the target | The heavy blow's hit stop, b1's bone flash, its tremor and recoil, a1's return; the punch zoom on a critical ([movement.md](movement.md)). | [Hit stop](../battle-craft.md#hit-stop), [Recoil](../battle-craft.md#recoil) |
| 840 | 300 | impact | the camera | The stomp: a staged shake over the blow's trauma. `shake at 840, strength 0.3` | [Shake from trauma](../battle-craft.md#shake-from-trauma) |
| 840 | 640 | impact | the effect | The ground ring: a bone shock ring opens on the ground at b1's feet, far past its outline. `glow at 840, shape ring, tint bone, reach 400, strength 1, lasts 640, blend screen` | [Glow](../battle-craft.md#glow), [Stagger and overlap](../battle-craft.md#stagger-and-overlap) |
| 840 | 300 | impact | the effect | The light of the crush on b1. `glow at 840, tint bone, reach 240, strength 0.8, lasts 300, blend screen` | [Glow](../battle-craft.md#glow), [Additive light](../battle-craft.md#additive-light) |
| 840 | 520 | impact | the effect | The crown, the signature: 24 large bone shards (*house*) thrown up from the ground at b1's feet in a 150° fan, over its body and above its head. `burst at 840, count 24, shape shard, tint bone, direction up, spread 150, reach 300, strength 1, lasts 520, blend normal, level feet` | [Burst](../battle-craft.md#burst), [A beat on the ground](../battle-craft.md#a-beat-on-the-ground) |
| 860 | 360 | impact | the effect | Ground debris thrown out flat on every side of b1's feet: 12 taupe chips (*house*), replacing the draft's sparks. `burst at 860, count 12, shape shard, tint family, direction out, spread 360, reach 320, strength 0.75, lasts 360, blend normal, level feet` | [Burst](../battle-craft.md#burst), [Shape before colour](../battle-craft.md#shape-before-colour) |
| 1040 | 600 | impact | the effect | Dust pushed out by the aftershock: 10 small grains (*house*, strength 0.4, not the draft's 16 at 1, a cloud over b1). `burst at 1040, count 10, shape dot, tint family, direction out, spread 360, reach 300, strength 0.4, lasts 600, blend normal, level feet` | [Burst](../battle-craft.md#burst), [Contact and follow-through](../battle-craft.md#contact-and-follow-through) |
| 1100 | 1100 | impact | the effect | Dust that stays on the stunned one: 10 small grains alive at once hang low round b1's feet and drift outward on the ground, not up (rising motes are Crash's), gone by 2200 ms. `flow at 1100, count 10, shape dot, tint family, direction out, spread 360, reach 140, strength 0.6, lasts 1100, blend normal, level feet` | [Flow](../battle-craft.md#flow), [The three parts of an effect](../battle-craft.md#the-three-parts-of-an-effect) |
| 2200 | 340 | impact | the field | Nothing new. b1 breathing, a1 home. The result is read here; the hold from 2400 ms is empty. | [The hold before the number](../battle-craft.md#the-hold-before-the-number), [Breath at rest](../battle-craft.md#breath-at-rest) |

Eleven staged beats, three in the windup. 56 particles on b1 (24, 12, 10, 10), at most 46 at once (1040 ms), 16 on
a1. Four glows, one pulse on a1, a shake of 0.3, the last gone by 2200 ms: inside Stage III. One flash, the body's.

### Reduced motion

Nothing. The frame after the action is shown at once with the result text: no halo, no crown, no ring, no dust.

### Cost

- Objects at the peak (1040 ms): the motif `Graphics`, two rings, three emitters. Draw calls about 6. In the
  windup the pulse on a1 is a framebuffer and a pass each frame for 400 ms.
- Particles at the peak: 46 at 1040 ms on b1; 70 asked for in all; of the field's 150.
- Bytes: not measured. Built by BC4 (the kit and `staging`), BC5 (the body), BC6 (the ring, `reach`, `strength`);
  BC6 places the beats. The cancel path is the kit's: emitters, glows and the pulse's filter go with the action.

### On the film strip

```bash
node scripts/battle/filmstrip.mjs spell:crushing_stomp:v1 --actor a1 --targets b1 --damage 7 --out <folder>/crushing-stomp.png
node scripts/battle/filmstrip.mjs spell:crushing_stomp:v1 --actor a1 --targets b1 --damage 7 \
  --frames 24 --interval 60 --columns 6 --out <folder>/crushing-stomp-contact.png
```

- 120 to 480 ms (contact): shards over a1's back, closing on it; no pale arc.
- 540 to 780 ms: a1's gold rim quieted and a pale bone halo on its outline, the whole figure lighter; it goes up
  the field with a1 and is gone at 840 ms.
- 840 ms: b1 filled bone; a1 covers b1's lower half, not its head; a small ring at b1's feet.
- 900 ms: the ring as wide as b1, lying at its feet over a1's back.
- 960 to 1080 ms: the crown: shards rise from b1's feet through its body and above its head, b1 washed pale under
  the light of the crush; its head half covered at 1020 ms. The ring wider than b1; chips flat on both sides.
- 1140 ms: the last shards above b1's head; b1's outline clear again.
- 1200 to 1320 ms: one ring, the widest of the family, reaching the next figure's body; small grains low round
  b1's feet, none above its back.
- 1440 ms (whole): no shard; a few grains low round b1's feet. 2400 ms (whole) and every frame after: no effect
  object on the field. Nothing in the effect is gold.

The sheets cannot show the stop, the shake, the punch zoom of a critical, or the feel at full speed.

### Left

- The draft's 20 sparks with `add` are cut: "no sparkle" in this family, and sparks are Deranged Charge's. Taupe
  chips take their place. If the contact reads weaker on the film, raise the crown to 28 before adding light.
- The draft's dust (16 dots at strength 1, a 1000 ms upward flow at 16) clouded b1 and drifted into Crash's motes;
  it is now 10 small grains and a low flow of 8 that spreads, not rises.
- The stun is told by the dust that stays; the 2 rounds are the status chip's. A stun mark on b1 is the scene's.
- The crown, the debris and the dust start at b1's feet (`level feet`), and the crown's reach came down to 300.
  From the feet the crown passes through b1's body before it stands above it: for about 120 ms (960 to 1080 ms)
  b1's head is half hidden. Chosen: the crown rises out of the ground. Settled by the owner, 2026-11-19: kept at
  the feet.
- The halo is read now that the pulse quiets a1's gold rim (540 to 780 ms).
- The staged shake rides the blow's trauma (ceiling 0.6) and may add little on a heavy hit; the film cannot say.

## Vital Surge (`spell:restorative_gush:v1`)

### Intent

The player reads "the Colossus's growths lit, a wave left them and washed over its ally": who gave, who received,
and that it is life, not a blow. Vital Surge costs 1 energy and heals one ally of 10 with a 50% critical chance; it
is a Stage III spell, a few times a game, so it uses the band's light and its one pulse. Its signature is the wave:
light drawn into a1's growths, a trail that travels to a2 through the windup, then a2 lit from inside and a long
rise. A heal: nothing sharp, no shard, no spark, no shake, no flash.

### Today

Filmed at `15ee3b0`, 390 by 844, 1×, a1 on a2: a whole action and a contact.

- 0 to 60 ms: a pale arc on a1's body, the shared charge. a1 loads where it stands; a2 does not move on any frame.
- 900 to 1380 ms: the `vital` motif on a2, thin pale spirals, low in contrast on its pale hide.
- Nothing travels from a1 to a2 and nothing lights: it could be any family's ward.

### Beats

| At | Lasts | Window | Layer | Beat | Pattern |
| --- | --- | --- | --- | --- | --- |
| 0 | 840 | windup | the caster | A cast's load and rise ([movement.md](movement.md)): a1 squats and rises where it stands. No shared arc: the staging's windup beats replace it. | [Anticipation](../battle-craft.md#anticipation), [Squash and stretch](../battle-craft.md#squash-and-stretch) |
| 120 | 360 | windup | the effect | The growths drink: 12 small motes (*house*) start on a ring around a1 and land on its body. `gather at 120, on caster, count 12, shape dot, tint family, reach 220, strength 0.4, lasts 360, blend add` | [Gravity well and convergence](../battle-craft.md#gravity-well-and-convergence), [Anticipation](../battle-craft.md#anticipation) |
| 360 | 400 | windup | the effect | The growths light: a soft taupe light swells on a1 and goes. `glow at 360, on caster, tint family, reach 160, strength 0.5, lasts 400, blend screen` | [Glow](../battle-craft.md#glow), [Additive light](../battle-craft.md#additive-light) |
| 480 | 360 | windup | the effect | The wave travels, the signature: a head leaves a1 and reaches a2 at 840 ms, leaving 14 motes (*house*) on its path. `trail at 480, count 20, shape dot, tint family, strength 0.8, lasts 360, blend add` | [Trail](../battle-craft.md#trail), [Easing families](../battle-craft.md#easing-families) |
| 840 | 360 | impact | the caster | The release, as a cast lets go ([movement.md](movement.md)). a2 lost nothing: no flash, no recoil. | [Contact and follow-through](../battle-craft.md#contact-and-follow-through) |
| 840 | 1200 | impact | the effect | The `vital` motif on a2, as the wave lands; gone by 2040 ms. `motif at 840, lasts 1200` | [The three parts of an effect](../battle-craft.md#the-three-parts-of-an-effect), [Shape before colour](../battle-craft.md#shape-before-colour) |
| 840 | 480 | impact | the effect | The wave breaks over a2: a wide taupe light, gentler than a blow's. `glow at 840, tint family, reach 260, strength 0.5, lasts 480, blend screen` | [Glow](../battle-craft.md#glow), [A light that answers a cast](../battle-craft.md#a-light-that-answers-a-cast) |
| 840 | 480 | impact | the effect | Spray lifted off the wave: 12 bone motes (*house*) thrown up from a2, small, slowing as they rise. `burst at 840, count 12, shape dot, tint bone, direction up, spread 90, reach 300, strength 0.4, lasts 480, blend screen` | [Burst](../battle-craft.md#burst), [Additive light](../battle-craft.md#additive-light) |
| 880 | 520 | impact | the effect | The one pulse: a taupe halo on a2's own outline swells and goes, "this one was healed". `pulse at 880, tint family, strength 0.35, lasts 520` | [Glow](../battle-craft.md#glow), [One hero shader](../battle-craft.md#one-hero-shader) |
| 960 | 1200 | impact | the effect | The long rise: 10 motes alive at once climb slowly off a2's body, gone by 2160 ms. `flow at 960, count 10, shape dot, tint family, direction up, spread 50, reach 260, strength 0.4, lasts 1200, blend add` | [Flow](../battle-craft.md#flow), [The three parts of an effect](../battle-craft.md#the-three-parts-of-an-effect) |
| 1000 | 560 | impact | the effect | The wave spreads past the ally: ten pale motes thrown out all round its feet, on the ground, fading as they slow; screened, so they never stack into a white blot on a2. `burst at 1000, count 10, shape dot, tint family, direction out, spread 360, reach 200, strength 0.5, lasts 560, blend screen, level feet` | [Burst](../battle-craft.md#burst) |
| 2160 | 380 | impact | the field | Nothing new. Both figures home, breathing. The result is read here. | [The hold before the number](../battle-craft.md#the-hold-before-the-number), [Breath at rest](../battle-craft.md#breath-at-rest) |

Nine staged beats, three in the windup; 52 particles asked for on a2 (20, 12, 10, 10) and 12 on a1; two glows and the
one pulse, on a2; no shake; the last gone by 2160 ms: inside Stage III. No flash. Nothing in the settle.

### Reduced motion

Nothing. The frame after the action is shown at once with the result text: no load, no trail, no light, no motif.

### Cost

- Objects at the peak (960 to 1320 ms): the motif's `Graphics`, the pulse filter on a2, the two glows, two emitters
  (spray, rise). Draw calls: the pulse is one pass a frame (*house*: about ten times a frame without it, without a
  graphics card, the craft book's Glow); the screen glows batch together, apart from the normal objects.
- Particles at the peak: 22 at 960 ms (12 spray, 10 rise); of the field's 150.
- Bytes: not measured. Built by BC4 (motif, gather, trail, burst, flow, glow) and BC6 (the ring, the pulse); BC6
  places the beats. The cancel path is the kit's: every emitter, glow and filter killed with the action.

### On the film strip

```bash
node scripts/battle/filmstrip.mjs spell:restorative_gush:v1 --actor a1 --targets a2 --out <folder>/vital-surge.png
node scripts/battle/filmstrip.mjs spell:restorative_gush:v1 --actor a1 --targets a2 \
  --frames 24 --interval 60 --columns 6 --out <folder>/vital-surge-contact.png
```

- 0 and 60 ms: no arc on a1. 240 ms: motes over a1's back, closing.
- 480 ms: a1 softly lit. 600 to 780 ms: the wave's head, a bright point, crosses a1's body to its front; it is in the
  gap only on the last frame before contact. No line of motes behind it reads.
- 840 ms: the vital motif on a2; a2 not filled flat, not moved.
- 900 ms: a small pale puff on a2's back where the spray starts, one frame; a2's middle and the motif clear.
- 960 and 1020 ms: motes thrown up off a2's back, the motif's spirals on it.
- 1080 to 1380 ms: the out burst's dots on the ground round a2's feet, spreading along its ring; no blot on a2.
- 1440 ms (whole): motes still rising over a2, fewer; no light left on a1.
- 2160 ms (whole) and every frame after: no effect object on the field.
- Nothing is a shard or a spark; nothing in the effect is gold, a2's marker the only gold on it.

The sheets cannot show the pulse's swell at full speed, nor the sound.

### Left

- The trail runs in the windup, so the wave arrives as the release does. A person may prefer it to leave at the
  release; the impact would then open on an empty a2 for 360 ms, which reads as a delay.
- The third films showed a white blot on a2's middle (900 ms, then 1080 to 1140 ms): additive dots stacked where
  a burst starts. The spray and the out burst are now `screen`, and the out burst lies at a2's feet: the wave
  spreads on the ground, and only a small puff is left on a2's back at 900 ms.
- The trail's head reads; its motes do not: it crosses a1's own body for most of its 360 ms. Left as it is.
- No green and no "life" colour: the kit offers family, bone, ember and blood. A vital tint needs its own change.
- The critical heal (50%) is told by the number alone; the body's punch zoom answers health lost, not gained.

## Deranged Charge (`spell:psycho_rush:v1`)

### Intent

The player reads "the Ravager threw its whole body through that one, and the hit went on beyond it": reckless, not
precise. Deranged Charge costs 3 energy, deals 10 damage, the heaviest blow of the family, with a 50% critical
chance, to one enemy in melee, and the caster loses 2 defense for a round. Stage III: the band's shake and two
lights. Its signature is the cone: ember drawn into a1 while it draws back, then debris and sparks thrown far past
b1 in a narrow cone along the blow, under an ember ring.

### Today

Filmed at `15ee3b0`, 390 by 844, 1×, a1 on b1, 10 health lost: a whole action and a contact.

- 0 to 60 ms: a pale arc on a1's body. 120 to 540 ms: a1 draws back; 660 to 780 ms it lunges up the field.
- 840 and 900 ms: b1 filled flat in bone; a1 covers b1's lower half, b1's head clear.
- 960 to 1380 ms: three upright pale strokes on b1, fading; a1 back home by 1380 ms. No particle: Pummel's blow, bigger.

### Beats

| At | Lasts | Window | Layer | Beat | Pattern |
| --- | --- | --- | --- | --- | --- |
| 0 | 840 | windup | the caster | A heavy melee blow's draw back, hold and strike ([movement.md](movement.md)), scaled by the 10 health b1 lost. No shared arc. | [Anticipation](../battle-craft.md#anticipation), [Contact and follow-through](../battle-craft.md#contact-and-follow-through) |
| 120 | 480 | windup | the effect | The fury loads: 16 ember sparks (*house*) start on a ring around a1 and land in its body as it draws back. `gather at 120, on caster, count 16, shape spark, tint ember, reach 200, strength 0.5, lasts 480, blend add` | [Gravity well and convergence](../battle-craft.md#gravity-well-and-convergence), [Anticipation](../battle-craft.md#anticipation) |
| 360 | 420 | windup | the effect | The fissures burn: an ember light on a1 swells through the hold and goes as it lunges; additive, as the owner chose for ember light. `glow at 360, on caster, tint ember, reach 180, strength 0.6, lasts 420, blend add` | [Glow](../battle-craft.md#glow), [Additive light](../battle-craft.md#additive-light) |
| 840 | 1000 | impact | the effect | The `impact` motif on b1, its strokes leaning along a1 to b1 on their dark edge; gone by 1840 ms. `motif at 840, lasts 1000` | [An impact that keeps the direction](../battle-craft.md#an-impact-that-keeps-the-direction), [Shape before colour](../battle-craft.md#shape-before-colour) |
| 840 | 400 | impact | the effect | The cone, the signature: 24 bone shards (*house*) thrown past b1 in a 40° cone along the blow; the reach came up to 400 so the cone, shortened on a blow struck up the field, still clears b1's back. `burst at 840, count 24, shape shard, tint bone, direction along, spread 40, reach 400, strength 1, lasts 400, blend normal` | [Burst](../battle-craft.md#burst), [An impact that keeps the direction](../battle-craft.md#an-impact-that-keeps-the-direction) |
| 840 | 360 | impact | the effect | Sparks inside the cone: 20 ember sparks (*house*), narrower still. `burst at 840, count 20, shape spark, tint ember, direction along, spread 24, reach 360, strength 0.8, lasts 360, blend add` | [Burst](../battle-craft.md#burst), [Additive light](../battle-craft.md#additive-light) |
| 840 | 300 | impact | the effect | The short ember light of the hit on b1, the cone's only light at contact. `glow at 840, tint ember, reach 220, strength 0.7, lasts 300, blend add` | [Glow](../battle-craft.md#glow), [Contact and follow-through](../battle-craft.md#contact-and-follow-through) |
| 840 | 300 | impact | the view | 0.3 of trauma (*house*) over the heavy blow's own, under the 0.6 ceiling; it decays in about 300 ms. `shake at 840, strength 0.3` | [Shake from trauma](../battle-craft.md#shake-from-trauma) |
| 840 | 900 | impact | the caster and the target | The heavy blow's hit stop, b1's bone flash, its recoil, a1's return ([movement.md](movement.md)); a critical adds the punch zoom. | [Hit stop](../battle-craft.md#hit-stop), [Recoil](../battle-craft.md#recoil) |
| 960 | 520 | impact | the effect | Dust dragged behind the cone: 12 grains (*house*), slower, along the blow. `burst at 960, count 12, shape dot, tint family, direction along, spread 36, reach 360, strength 0.4, lasts 520, blend normal` | [Burst](../battle-craft.md#burst), [Stagger and overlap](../battle-craft.md#stagger-and-overlap) |
| 1000 | 700 | impact | the effect | Embers left smouldering: 8 alive at once rise off b1, gone by 1700 ms. `flow at 1000, count 8, shape spark, tint ember, direction up, spread 50, reach 140, strength 0.7, lasts 700, blend add` | [Flow](../battle-craft.md#flow), [The three parts of an effect](../battle-craft.md#the-three-parts-of-an-effect) |
| 1840 | 700 | impact | the field | Nothing new. Both figures home, breathing. The result is read here. | [The hold before the number](../battle-craft.md#the-hold-before-the-number), [Breath at rest](../battle-craft.md#breath-at-rest) |

Nine staged beats, two in the windup; 64 particles asked for on b1 (24, 20, 12, 8) and 16 on a1; two glows, no
pulse; a staged shake of 0.3; the last gone by 1840 ms: inside Stage III. One flash, the body's, on b1.

### Reduced motion

Nothing. The frame after the action is shown at once with the result text: no charge, no cone, no ring, no shake.

### Cost

- Objects at the peak (840 to 1200 ms): the motif's `Graphics`, two glows, three emitters (shards, sparks, dust from
  960 ms), the body's tweens. Draw calls: lit objects batch above plain ones; about five during 360 ms.
- Particles at the peak: 44 at 900 ms (24 shards, 20 sparks), 56 at 960 ms with the dust; of the field's 150.
- Bytes: not measured. Built by BC4 (motif, gather, burst, flow, glow, shake) and BC5 (the body), BC6 (the ring
  and the leaning strokes); BC6 places the beats. The cancel path is the kit's.

### On the film strip

```bash
node scripts/battle/filmstrip.mjs spell:psycho_rush:v1 --actor a1 --targets b1 --damage 10 --out <folder>/deranged-charge.png
node scripts/battle/filmstrip.mjs spell:psycho_rush:v1 --actor a1 --targets b1 --damage 10 \
  --frames 24 --interval 60 --columns 6 --out <folder>/deranged-charge-contact.png
```

- 0 and 60 ms: no arc on a1. 240 ms: a few faint warm sparks round a1.
- 480 to 600 ms: a1's back lit warm by the additive ember light; 720 ms fading; 780 ms a1 has lunged, a faint warm
  haze left where it stood.
- 840 ms: b1 filled bone; a1 covers b1's lower half, not its head.
- 900 ms: a short column of shards rising from b1's back, none on a1's side; the strokes upright, along the blow.
- 960 to 1080 ms: the cone is a dense, near-white column over b1's back, as high again as b1's head, no wider than b1's
  middle; b1's head beside it, clear. No ring around b1.
- 1140 ms: the column gone; a little dust over b1's back. 1260 ms: the strokes fading, a mote or two.
- 1440 ms (whole) and every frame after: no effect object on the field. Nothing in the effect is gold.
- Set beside Crushing Stomp's sheet at 1020 ms: Stomp's crown is a wide fan from the feet that covers b1; Deranged
  Charge's cone is a narrow column on its back.

The sheets cannot show the shake, the stop, the punch zoom of a critical or the feel at full speed.

### Left

- "Flanks exposed after attack" (the 2 defense lost) is the scene's small mark on a1, not the staging's. If the
  owner wants it seen, it is a wound or a crack on a1's flank after the return: a scene change, not a beat.
- No pulse: Crushing Stomp owns the halo on a Brute's windup; the ember gather keeps the two apart.
- Two ember lights, one in the windup and one at contact. A cast counts one change of light, the contact's: the
  windup's is faint and on the caster.
- The sparks are additive over bone shards and read near white in the column (960 to 1080 ms): the cone reads as
  white-hot, not as ember. Left: it is the owner's ember choice, and the column still reads as Deranged Charge's.
- No blood: Wild Swing owns the tearing; this is mass thrown through.

## Groundbreaker (`spell:tornado:v1`)

### Intent

The player reads "the Ravager brought both forelegs down where it stood and the ground broke open under all three":
one blow, felt at a distance. Groundbreaker costs 3 energy and deals 8 damage to up to three enemies with a 40%
critical chance; delivery `cast`, so a1 loads and lets go in place. Stage III, the widest of the family. Its signature
is the travelling shock: the largest ring of the game opens under a1 at contact, then the ground erupts under each
target in turn, a ring and shards thrown up.

### Today

Filmed at `15ee3b0`, 390 by 844, 1×, a1 on b1, b2 and b3, 8 health lost each: a whole action and a contact.

- 0 to 60 ms: a pale arc on a1's body. a1 loads in place; it never travels.
- 840 and 900 ms: b1, b2 and b3 filled flat in bone together.
- 960 to 1380 ms: concentric pale rings (the `wave` motif) on each target. Nothing under a1, nothing between them.

### Beats

| At | Lasts | Window | Layer | Beat | Pattern |
| --- | --- | --- | --- | --- | --- |
| 0 | 840 | windup | the caster | A cast's load and rise ([movement.md](movement.md)): a1 squats with its weight on its forelegs, and rises. No shared arc. | [Anticipation](../battle-craft.md#anticipation), [Squash and stretch](../battle-craft.md#squash-and-stretch) |
| 240 | 560 | windup | the effect | The ground answers the load: 14 grains of dust alive at once lift off a1. `flow at 240, on caster, count 14, shape dot, tint bone, direction up, spread 70, reach 160, strength 0.8, lasts 560, blend normal` | [Flow](../battle-craft.md#flow), [Anticipation](../battle-craft.md#anticipation) |
| 420 | 420 | windup | the effect | The one pulse: an ember halo on a1's outline swells to the release. `pulse at 420, on caster, tint ember, strength 1, lasts 420` | [Glow](../battle-craft.md#glow), [One hero shader](../battle-craft.md#one-hero-shader) |
| 420 | 360 | windup | the ground | The crack: a head runs along the ground at an even pace from a1's feet to each target's feet, leaving 14 bone shards behind it (*house*) that stay until the contact. It is whole at 780 ms, as the forelegs come down, and is wiped at the contact with the windup. `trail at 420, lasts 360, count 14, shape shard, tint bone, blend normal, level feet, strength 1` | [Trail](../battle-craft.md#trail), [A beat on the ground](../battle-craft.md#a-beat-on-the-ground) |
| 840 | 360 | impact | the caster | The release, as a cast lets go ([movement.md](movement.md)): the forelegs come down. | [Contact and follow-through](../battle-craft.md#contact-and-follow-through) |
| 840 | 600 | impact | the effect | The largest ring: a bone arc of 120° opens on the ground at a1's feet toward the targets, out to 400, the kit's widest, and fades; it no longer sweeps through a1's ally. `glow at 840, on caster, shape ring, tint bone, reach 400, strength 0.9, lasts 600, blend screen, direction along` | [Glow](../battle-craft.md#glow), [A beat on the ground](../battle-craft.md#a-beat-on-the-ground) |
| 840 | 360 | impact | the effect | The ground breaks: 16 bone shards (*house*) thrown up from the ground at a1's forelegs. `burst at 840, on caster, count 16, shape shard, tint bone, direction up, spread 70, reach 300, strength 0.6, lasts 360, blend normal, level feet` | [Burst](../battle-craft.md#burst), [A beat on the ground](../battle-craft.md#a-beat-on-the-ground) |
| 840 | 300 | impact | the view | 0.3 of trauma (*house*) over the three targets' own, under the ceiling. `shake at 840, strength 0.3` | [Shake from trauma](../battle-craft.md#shake-from-trauma) |
| 840 | 900 | impact | the targets | Each target's bone flash, tremor and recoil on the 8 it lost ([movement.md](movement.md)). | [Hit flash](../battle-craft.md#hit-flash), [Recoil](../battle-craft.md#recoil) |
| 880 | 1000 | impact | the effect | The `wave` motif on b1, b2, b3, 40 ms apart; the last gone by 1960 ms. `motif at 880, lasts 1000` | [Stagger and overlap](../battle-craft.md#stagger-and-overlap), [Shape before colour](../battle-craft.md#shape-before-colour) |
| 880 | 520 | impact | the effect | The shock arrives: a bone ring opens on the ground at each target's feet to 300, in turn. `glow at 880, shape ring, tint bone, reach 300, strength 0.8, lasts 520, blend screen` | [Glow](../battle-craft.md#glow), [Stagger and overlap](../battle-craft.md#stagger-and-overlap) |
| 880 | 400 | impact | the effect | The ground erupts: 16 bone shards (*house*) thrown up from the ground at each target's feet. `burst at 880, count 16, shape shard, tint bone, direction up, spread 50, reach 340, strength 0.6, lasts 400, blend normal, level feet` | [Burst](../battle-craft.md#burst), [Stagger and overlap](../battle-craft.md#stagger-and-overlap) |
| 880 | 320 | impact | the effect | The fissures: 10 ember sparks (*house*) up with the shards, from the feet. `burst at 880, count 10, shape spark, tint ember, direction up, spread 60, reach 260, strength 0.8, lasts 320, blend add, level feet` | [Burst](../battle-craft.md#burst), [Additive light](../battle-craft.md#additive-light) |
| 1120 | 700 | impact | the effect | Dust settling: 6 grains alive at once over each target, gone by 1900 ms. `flow at 1120, count 6, shape dot, tint family, direction up, spread 50, reach 120, strength 0.7, lasts 700, blend normal` | [Flow](../battle-craft.md#flow), [The three parts of an effect](../battle-craft.md#the-three-parts-of-an-effect) |
| 1960 | 580 | impact | the field | Nothing new. All figures home, breathing. The result is read here. | [The hold before the number](../battle-craft.md#the-hold-before-the-number), [Breath at rest](../battle-craft.md#breath-at-rest) |

Eleven staged beats, three in the windup; 32 particles asked for on each target (16, 10, 6), 30 on a1 (14, 16) and
14 for the crack; two glows, both rings; the one pulse, on a1; a staged shake of 0.3; the last gone by 1960 ms:
inside Stage III. One flash a target, the body's.

### Reduced motion

Nothing. The frame after the action is shown at once with the result text: no load, no ring, no shard, no shake.

### Cost

- Objects at the peak (920 to 1200 ms): three motif `Graphics`, four rings (a1's arc is one `Graphics`), seven
  emitters (a1's shards, two a target); the crack's three emitters are cleared at the contact, with the windup.
- Draw calls: lit objects batch above plain ones; the pulse's pass ends at 840 ms, before the peak.
- Particles at the peak: 94 at 960 ms (16 on a1, 26 on each target); under 48 a target and the field's 150.
- Bytes: not measured. Built by BC4 (motif, burst, flow, glow, shake), BC5 (the cast's body), BC6 (ring, pulse);
  BC6 places the beats. The cancel path is the kit's: every emitter, ring and filter killed with the action.

### On the film strip

```bash
node scripts/battle/filmstrip.mjs spell:tornado:v1 --actor a1 --targets b1,b2,b3 --damage 8 --out <folder>/groundbreaker.png
node scripts/battle/filmstrip.mjs spell:tornado:v1 --actor a1 --targets b1,b2,b3 --damage 8 \
  --frames 24 --interval 60 --columns 6 --out <folder>/groundbreaker-contact.png
```

- 0 ms: a1's gold rim. 360 to 540 ms: a1's rim quieted and its body paler under the ember halo, dust over its
  middle. 600 to 780 ms: the halo fading.
- 600 ms: the crack's first shards leaving a1's front feet toward the targets. 660 ms: three short dotted lines.
- 720 ms: three dotted lines of shards across the gap, low over the ground, one toward each target. 780 ms: the
  lines whole, reaching the targets' ground; none beside a1 or behind it. 840 ms: the crack gone with the windup.
- 840 ms: the three targets filled flat in bone; a bright arc at a1's front feet, toward the targets.
- 900 ms: shards rising out of a1's forelegs; on b1 a white burst at its feet, rings and the wave motif on b2 and b3.
- 960 and 1020 ms: shards and sparks erupt from each target's feet as near-white spurts; rings at each target's
  feet. a1's arc wider, across its front, none behind it; no ring on a2 or a3.
- 1080 ms: the target rings wide and touching each other between the three; a1's arc reaches a2's legs.
- 1200 to 1380 ms: no shard; the rings fading; dust over each target; the motif fading on all three.
- 1920 ms (whole) and every frame after: no effect object on the field. Nothing in the effect is gold.

The sheets cannot show the shake, the order of the three at full speed, or the sound.

### Left

- The crack runs at 480 for 360 ms, at strength 1, and the kit's trail at the feet now moves its head at an even
  pace and keeps what it leaves for the whole beat: on the third films it was not seen, and on the fourth it is
  read from 660 to 780 ms. It reads as dotted lines of shards rather than a split in the ground; a crack drawn into
  the ground would need a pattern the book lacks.
- The rings and the `wave` motif are both rings on each target. If the film reads them as one, cut the target ring's
  reach to 240 before cutting it: the band needs both glows.
- The ring under a1 is an arc at its feet, aimed at the targets: it no longer runs through the ally beside a1.
- The pulse is on a1 in the windup, in ember, against Crushing Stomp's bone halo; no pulse on the targets. It reads
  now that it quiets a1's gold rim (360 to 600 ms).
