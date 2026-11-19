# Storyboards: the biological family

The Scoundrel's spells, drawn on the body the art calls the Predator: claws that rake, teeth that bite, bone quills
thrown from the dorsal ridge, and the rounded drops of glands and spores ([the vocabulary of the
family](../spell-vfx.md)), on the family's pale green (0xadc777). Its 14 spells climb [the ladder](README.md#the-ladder):
Stage I (Venom Claw, Quill), then Stage II (Overdrive, Toxic Mend, Ambush, Pursuit, Latch, Bite), then Stage III
(Blood Hunt, Eviscerate, Blood Price, Devour, Paralyzing Barb, Contagion). This file holds Stage I and Stage II; a
later pass appends Stage III. Each step must look richer than the one below it, and each spell must keep one
signature a player could name: Stage II's two spells of each mutation are told apart by shape first. The body's own
movement is [movement.md](movement.md)'s: a storyboard here names it in one row per window and spends its rows on
the spell.

The family keeps one meaning per look, so a player learns it once ([One effect, one
meaning](../battle-craft.md#one-effect-one-meaning)). The motif carries the family's green and stays the primary
element. Rounded drops in the family's green are what a gland makes: venom, spores, a secretion. Shards and dots in
`bone` are the creature's own hard parts: a quill, a claw, a splinter of hide. Drops in `blood` are a wound that
bleeds. A spell that does not poison does not throw green drops, so Quill's debris is bone and Venom Claw's is green.

Stage II adds three readings, each kept to one look. Green sparks, pointed and quick, rising off a figure are energy
it gained (Overdrive's allies, Pursuit's caster); the rounded green drop stays the gland's. Blood that stays on the
struck figure is its Bleed (Venom Claw, Latch); blood carried back to the caster is what it drank (Bite). Pale grains
on a flat ring at a figure's feet are grit off the ground (Ambush), not a quill. A spell that neither bleeds nor
drinks shows no blood, whatever it deals: Ambush's ten damage throw bone.

## Venom Claw (`spell:poison_slash:v1`)

### Intent

The player reads "the Predator raked that one with a poisoned claw, and the scratch is bleeding": a scratch, not a
hammer, and a wound that keeps going. Venom Claw costs 2 energy, deals 3 damage to one enemy in melee and leaves a
Bleed of 3 for one round; it is the Predator's first blow, cast most rounds it has energy, so it takes about Pummel's
attention: no light, no windup mark, no camera of its own. Its signature is the weeping scratch: after the claw,
deep red beads well up on the struck figure and a thin green fume rises off it, where Wild Swing's blood is thrown
away in a fan.

### Today

Filmed at `28f2dc2`, 390 by 844, 1×, a1 on b1, 3 health lost: a whole action and a contact.

- 0 to 180 ms: a faint green arc on a1's body, the shared charge. A melee blow shows a cast's gather.
- 600 to 780 ms: a1 leaves its ring and climbs up the field to b1.
- 840 and 900 ms: b1 filled flat in bone; a1 covers b1's lower half, b1's head clear.
- 960 to 1380 ms: three pale green slash strokes cut across b1 on their dark edge, low in contrast on b1's hide,
  fading; all but gone at 1440 ms (whole). No particle, no blood. The sheet is any slash spell's: nothing says venom,
  nothing says bleed.

### Beats

| At | Lasts | Window | Layer | Beat | Pattern |
| --- | --- | --- | --- | --- | --- |
| 0 | 840 | windup | the caster | A light melee blow's draw back and strike by the 3 health b1 lost ([movement.md](movement.md)). No mark on a1's body: the staging has no windup beat, so the shared arc is not drawn. | [Anticipation](../battle-craft.md#anticipation), [Contact and follow-through](../battle-craft.md#contact-and-follow-through) |
| 840 | 700 | impact | the effect | The `slash` motif on b1: three claw strokes cutting 60° across the blow, each on its dark edge, gone by 1540 ms. `motif at 840, lasts 700` | [An impact that keeps the direction](../battle-craft.md#an-impact-that-keeps-the-direction), [Shape before colour](../battle-craft.md#shape-before-colour) |
| 840 | 280 | impact | the effect | The venom flicked off the claw: 8 small rounded drops (*house*) in the family's green thrown on beyond b1 in a narrow 40° fan along the blow, past its outline. `burst at 840, count 8, shape dot, tint family, direction along, spread 40, reach 260, strength 0.3, lasts 280, blend normal` | [Burst](../battle-craft.md#burst), [An impact that keeps the direction](../battle-craft.md#an-impact-that-keeps-the-direction) |
| 840 | 840 | impact | the caster and the target | The light blow's hit stop, b1's bone flash, its recoil, a1's return, home by 1680 ms ([movement.md](movement.md)). | [Hit stop](../battle-craft.md#hit-stop), [Recoil](../battle-craft.md#recoil) |
| 920 | 640 | impact | the effect | The scratch weeping, the signature's first half: 8 deep red beads (*house*, the deep red 0x9a3b34) well up over b1's body, a few at a time, drift a little off it and shrink, gone by 1560 ms. They stay on b1: the blood is not thrown. `flow at 920, count 8, shape dot, tint blood, direction out, spread 360, reach 60, strength 0.3, lasts 640, blend normal` | [Flow](../battle-craft.md#flow), [One effect, one meaning](../battle-craft.md#one-effect-one-meaning) |
| 960 | 600 | impact | the effect | The venom working, the signature's second half: 6 green motes (*house*) alive at once rise slowly off b1 in a thin column and fade, gone by 1560 ms. `flow at 960, count 6, shape dot, tint family, direction up, spread 30, reach 100, strength 0.3, lasts 600, blend screen` | [Flow](../battle-craft.md#flow), [The three parts of an effect](../battle-craft.md#the-three-parts-of-an-effect) |
| 1680 | 860 | impact | the field | Nothing new. Both figures home, breathing. The result is read here. | [The hold before the number](../battle-craft.md#the-hold-before-the-number), [Breath at rest](../battle-craft.md#breath-at-rest) |

Four staged beats, 22 particles asked for on b1 (8, 8 and 6), no glow, no shake, no windup beat, the last gone by
1560 ms: inside Stage I. One flash, the body's, on b1; no change of light (the fume is six small motes on a screen
blend, not a light). Nothing in the settle.

### Reduced motion

Nothing. The frame after the action is shown at once with the result text: no strike, no strokes, no drop, no bead,
no fume. The Bleed is the status chip's to say.

### Cost

- Objects at the peak (960 to 1120 ms): one `Graphics` for the motif, three emitters (the flick's last drops, the
  beads, the fume), the body's tweens. Draw calls: the motif batches with the vectors; each emitter is one, three
  for about 160 ms, two after.
- Particles at the peak: about 20 at 1000 ms (the 8 drops, the beads alive, the first motes); 22 asked for in all; of
  the field's 150.
- Bytes: not measured. Built by BC4 (motif, burst, flow, the `staging` field), BC5 (the body) and BC6 (the `blood`
  tint, the slash cutting across, the dark edge); BC7 places the beats. The cancel path is the kit's: every emitter
  killed with the action.

### On the film strip

```bash
node scripts/battle/filmstrip.mjs spell:poison_slash:v1 --actor a1 --targets b1 --damage 3 --out <folder>/venom-claw.png
node scripts/battle/filmstrip.mjs spell:poison_slash:v1 --actor a1 --targets b1 --damage 3 \
  --frames 24 --interval 60 --columns 6 --out <folder>/venom-claw-contact.png
```

- 0 to 780 ms (contact): no mark, arc or particle on a1's body; a1 leaves its ring by 660 ms.
- 840 and 900 ms: a1's silhouette covers b1's feet and lower body, not its head. b1, which lost 3, is filled flat in
  bone in these two frames and in no other.
- 900 ms: small green drops over b1's back and head, on its far side from a1, none on a1's side. Shortened (`along`
  on a blow struck up the field), they stay on b1's outline or just over it.
- 960 to 1080 ms: the slash strokes cut diagonally across the blow, each on a dark edge; deep red beads on b1's
  body, a few just outside its outline on every side, dark red and not pink; the first green motes over b1's back.
- 1140 ms: no green drop left over b1's head; the red beads and a thin column of green motes on b1.
- 1200 ms (whole): the strokes at about half their brightness; red beads and green motes still on b1, none on a1.
- 1440 ms (whole): the strokes all but gone; a bead or a mote at most.
- 1680 ms (whole) and every frame after: no effect object on the field; a1 home. Nothing in the effect is gold.
- Set beside Wild Swing's sheet at 960 ms: Wild Swing's red is a fan thrown past b1 with bone shards; Venom Claw's red
  stays on b1's body under green, and no shard flies.

The sheets cannot show the stop, the shake of the health lost, or the feel at full speed.

### Left

- The venom is flicked along the blow, while the claw's strokes cut 60° across it: the kit aims a burst `along`,
  `back`, `up` or `out`, not across. A burst aimed with the slash is a word the kit lacks (its own change, in the
  same entry, "An impact that keeps the direction"). Chosen along for now, narrow, so it does not read as a fan.
- The beads drift off the body rather than run down it: a flow has no fall. Blood that runs down needs a `down`
  heading or a weight on the flow, a change to the kit, not a new pattern.
- Venom Claw shows blood at Stage I, where physical keeps blood for Wild Swing at Stage II. Chosen because Bleed is
  what the spell does: blood here says "bleeds", as a hit flash says "lost health". The owner may refuse it; then
  the weep becomes green beads, and the bleed is told by the status chip alone.
- No windup beat, by the band: a venom sheen gathering on the claw before the blow would be a windup gather, which
  Stage I refuses. It is kept for the Stage II and III claws.
- The Bleed lasts a round; the effect ends by 1560 ms. The round is the status chip's to say.

## Quill (`spell:throwing_star:v1`)

### Intent

The player reads "the Predator shook a bone quill off its back at each of them, and it went through": who threw, at
whom, and that it pierced. Quill costs 2 energy and deals 3 damage to up to two enemies from a distance; it is the
Predator's other Stage I spell, cast often, so it takes little attention: no light, no windup beat of its own, no
camera. Its signature is the quill going through: a narrow needle of bone splinters out of the far side of each
target, with a small spatter at the side it entered. Pummel's chips fan out from a hammer; Quill's go on in a line.
It also settles #909: the quill leaves the thrower's back with the toss, never stands on its body.

### Today

Filmed at `28f2dc2`, 390 by 844, 1×, a1 on b1 and b3, 3 health lost each: a whole action and a contact.

- 0 to 540 ms: two pale quills drawn on a1's body, one upright and one turning, as if part of its hide (#909). a1
  barely moves.
- 600 ms: the quills at a1's back; 660 to 780 ms: across the field, one straight up toward b1, one toward b3.
- 840 and 900 ms: b1 and b3 filled flat in bone; no motif yet.
- 960 to 1380 ms: the green strokes on each, upright on b1 and diagonal on b3, along each quill's flight, fading;
  faint at 1440 ms (whole), gone by 1680 ms. No particle: nothing says "through", and the strokes read like a slash.

### Beats

| At | Lasts | Window | Layer | Beat | Pattern |
| --- | --- | --- | --- | --- | --- |
| 0 | 240 | windup | the caster | The load ([movement.md](movement.md), `THROW` in `table/battle-motion.js`): a1 draws 8 px away from its targets with a squash of 4%. No quill is drawn while the body loads. | [Anticipation](../battle-craft.md#anticipation) |
| 240 | 120 | windup | the caster | The toss as the quills leave: a1 goes 6 px past home toward its targets with a stretch of 4%, then back home and still from 360 to 840 ms ([movement.md](movement.md)). | [Contact and follow-through](../battle-craft.md#contact-and-follow-through), [Squash and stretch](../battle-craft.md#squash-and-stretch) |
| 240 | 600 | windup | the effect | The flight, drawn by the scene (`FLIGHT`, `flightAt`), not a staged beat: one quill for each target appears on a1's back, 0.6 of its radius above its centre (*house*), and flies for 600 ms at an even pace, no ease in, on a low arc whose one control point stands above the middle by 16% of the flight's length (*house*), turned along its path. It lands at 840 ms. No trail: Stage I flies it bare. | [Arcing travel](../battle-craft.md#arcing-travel) |
| 840 | 600 | impact | the effect | The `quill` motif on b1 and on b3: its strokes along each quill's flight, on their dark edge, gone by 1440 ms. `motif at 840, lasts 600` | [An impact that keeps the direction](../battle-craft.md#an-impact-that-keeps-the-direction), [Shape before colour](../battle-craft.md#shape-before-colour) |
| 840 | 160 | impact | the effect | The tip's bite, the brightest and shortest part: 6 small bone sparks (*house*) out on every side of the point of entry, short. `burst at 840, count 6, shape spark, tint bone, direction out, spread 360, reach 120, strength 0.3, lasts 160, blend normal` | [Burst](../battle-craft.md#burst), [The three parts of an effect](../battle-craft.md#the-three-parts-of-an-effect) |
| 840 | 240 | impact | the effect | Through, the signature: 10 bone splinters (*house*) driven on out of the far side of each target in a narrow 20° needle along the quill's flight, past its outline. Pummel's fan is 50°. `burst at 840, count 10, shape shard, tint bone, direction along, spread 20, reach 320, strength 0.4, lasts 240, blend normal` | [Burst](../battle-craft.md#burst), [An impact that keeps the direction](../battle-craft.md#an-impact-that-keeps-the-direction) |
| 880 | 280 | impact | the effect | The entry: 6 grains of bone (*house*) spattered back from each target toward a1, short, none past the gap. Bone, not green: Quill is not venom. `burst at 880, count 6, shape dot, tint bone, direction back, spread 60, reach 140, strength 0.3, lasts 280, blend normal` | [Burst](../battle-craft.md#burst), [One effect, one meaning](../battle-craft.md#one-effect-one-meaning) |
| 840 | 590 | impact | the target | Each struck figure's light hit from a distance ([movement.md](movement.md)): its bone flash for 80 ms, a stop of 30 ms, a recoil of 12 px away from a1, home by about 1430 ms. Both on the contact frame, each by its own loss. | [Hit flash](../battle-craft.md#hit-flash), [Recoil](../battle-craft.md#recoil) |
| 1440 | 1100 | impact | the field | Nothing new. Every figure home. The result is read here. | [The hold before the number](../battle-craft.md#the-hold-before-the-number), [Breath at rest](../battle-craft.md#breath-at-rest) |

Four staged beats, all in the impact; 22 particles asked for on each target (6, 10, 6), 44 on the field at most; no
glow, no shake, the last gone by 1440 ms: inside Stage I. The quills in flight are the scene's, not staged beats, and
are not counted. One flash a figure, the body's, on b1 and on b3; no change of light. Nothing in the settle.

### Reduced motion

Nothing. No quill is launched, as today; the frame after the action is shown at once with the result text: no load,
no toss, no motif, no splinter.

### Cost

- In the windup: two `Graphics` (the quills) and two counter tweens, one each target, gone at contact.
- Objects at the peak (880 to 1000 ms): on each target one `Graphics` for the motif and up to three emitters (the
  sparks until 1000 ms, the needle, the spatter); six emitters on the field for about 120 ms, four after, the
  figures' tweens. Draw calls: the motifs batch with the vectors; each emitter is one.
- Particles at the peak: 44 at 880 ms (6, 10 and 6 on each of two targets); of the field's 150.
- Bytes: not measured. Built by BC4 (motif, burst, the `staging` field), BC5 (the throw, the hit from a distance), BC6
  (the strokes along the line, the dark edge) and BC7 (`FLIGHT` and `flightAt` in `table/battle-motion.js`, #909);
  BC7 places the beats. The cancel path: the kit's for every emitter; the flight's counter is one of the effect's
  timers and each quill one of its graphics, killed and destroyed with the action.

### On the film strip

```bash
node scripts/battle/filmstrip.mjs spell:throwing_star:v1 --actor a1 --targets b1,b3 --damage 3 --out <folder>/quill.png
node scripts/battle/filmstrip.mjs spell:throwing_star:v1 --actor a1 --targets b1,b3 --damage 3 \
  --frames 24 --interval 60 --columns 6 --out <folder>/quill-contact.png
```

- 0 to 180 ms (contact): no quill on a1's body or anywhere on the field; no mark or arc on a1 (#909). a1 a few pixels
  below its home, away from b1 and b3.
- 240 ms (contact): no quill on a1's body; at most two quills just above a1's back, over its centre.
- 300 ms: two quills clear of a1's outline, above it, each turned toward its target; neither overlaps a1's body.
- 360 to 780 ms: each quill a step further on every frame, the steps about equal from frame to frame (an even pace,
  no quill slow off a1's back); the one toward b3 bows above the straight line from a1 to b3, its point turning
  down as it nears; at 540 ms each about halfway. a1 home from 840 ms, never more than 8 px from it.
- 840 ms: no quill in the air; b1 and b3 filled flat in bone (840 and 900 ms only); small pale sparks at each.
- 900 ms: on each target a narrow line of bone splinters on its far side from a1 (b1: short, over its head; b3: up
  and to the right of it), none toward a1; pale grains on its near side, toward a1, not past the gap.
- 960 to 1080 ms: the strokes along each flight, upright on b1, diagonal on b3, on their dark edge; b1 and b3 off
  their rings, away from a1. 1140 ms: no splinter or grain left.
- 1200 ms (whole): the strokes at about half their brightness; no particle. 1440 ms (whole): the strokes all but
  gone; b1 and b3 home.
- 1680 ms (whole) and every frame after: no effect object on the field. b2 is not filled, moved or marked on any
  frame. Nothing in the effect is gold, and no particle is green.
- Set beside Pummel's sheet at 960 ms: Pummel's chips are a 50° fan with dust at the feet; Quill's splinters a thin
  line, no dust.

The sheets cannot show the stop, the shake of the health lost, or the feel of the flight at full speed.

### Left

- The flight is settled (#909, built in this stage): the thrower's body is `THROW`'s, unchanged; the quill appears
  at 240 ms and flies `FLIGHT`. The 0.6 radius and the 16% lift are *house*: the film tunes them.
- No trail behind the quill: the ladder test counts every beat that starts before 840 ms, and Stage I allows none.
  The trail on a projectile is kept for Paralyzing Barb at Stage III, where a windup beat is asked for.
- One quill a target, as the host confirms one hit each. The approved execution says "quills": a volley of three
  per target would read as three hits. The owner may want it; it is the scene's flight, not the staging's.
- On the bench b1 stands straight up the field from a1, so its quill's arc is seen as a line and its splinters are
  shortened over its head; b3 shows the arc and the needle at nearly full reach.
- Debris is bone, the motif green: green drops in this family mean venom, and Quill has none. If the film reads
  Quill as a physical spell, the family is told by the motif's green strokes, not by greener debris.

## Overdrive (`spell:adrenaline_tonic:v1`)

### Intent

The player reads "the Blighted vented a scent off its back, and its allies jolted awake": a stimulant given, and a
kick. Overdrive costs 1 energy; up to three allies gain 4 energy and +1 damage for a round. Stage II, a support cast
a few times a game, so it earns one windup beat and one light, and no shake and no flash: nobody lost health. Its
signature is the double jolt: green sparks shoot straight up off each ally twice, a heartbeat apart, the second higher
than the first. Toxic Mend's drops close in on its allies and soak; Overdrive's sparks leave them, upward.

### Today

Filmed at `28f2dc2`, 390 by 844, 1×, a1 on a1, a2 and a3, nobody struck: a whole action and a contact.

- 0 to 180 ms: the shared faint green arc on a1's body. a1 barely moves through the windup.
- 900 to 1380 ms: pale green spore rings, open circles, drift over a1, a2 and a3, fading; faint at 1440 ms (whole),
  gone by 1680 ms. No particle, no light.
- Toxic Mend's sheet is the same, frame for frame: nothing says energy, nothing says a kick.

### Beats

| At | Lasts | Window | Layer | Beat | Pattern |
| --- | --- | --- | --- | --- | --- |
| 0 | 600 | windup | the caster | A cast's load where it stands ([movement.md](movement.md), `CAST`): a1 squats 4%. | [Anticipation](../battle-craft.md#anticipation), [Squash and stretch](../battle-craft.md#squash-and-stretch) |
| 360 | 480 | windup | the effect | The scent leaving the glands on a1's back, the one windup beat: 10 rounded green drops (*house*) rise off a1 in a loose column, a few at a time, gone by 840 ms. Drops, because a gland makes them. `flow at 360, on caster, count 10, shape dot, tint family, direction up, spread 60, reach 200, strength 0.4, lasts 480, blend screen` | [Flow](../battle-craft.md#flow), [Anticipation](../battle-craft.md#anticipation) |
| 600 | 240 | windup | the caster | The rise: a1 stretches 4% as it lets the scent go ([movement.md](movement.md)). | [Squash and stretch](../battle-craft.md#squash-and-stretch) |
| 840 | 360 | impact | the caster | The release, as a cast lets go ([movement.md](movement.md)). Nobody lost health: no flash on any figure. | [Contact and follow-through](../battle-craft.md#contact-and-follow-through) |
| 840 | 900 | impact | the effect | The `spores` motif on each ally, gone by 1740 ms. `motif at 840, lasts 900` | [Shape before colour](../battle-craft.md#shape-before-colour), [The three parts of an effect](../battle-craft.md#the-three-parts-of-an-effect) |
| 840 | 400 | impact | the effect | The one light: a thin green ring opens fast on the ground at each ally's feet and fades by 1240 ms. Aimed at nobody, it opens all round. `glow at 840, shape ring, tint family, reach 200, strength 0.6, lasts 400, blend add` | [Glow](../battle-craft.md#glow), [A beat on the ground](../battle-craft.md#a-beat-on-the-ground) |
| 840 | 300 | impact | the effect | The first jolt, the signature's first half: 14 green sparks (*house*) shoot straight up off each ally in a narrow 40° fan, past its head. `burst at 840, count 14, shape spark, tint family, direction up, spread 40, reach 300, strength 0.5, lasts 300, blend add` | [Burst](../battle-craft.md#burst), [Additive light](../battle-craft.md#additive-light) |
| 1100 | 300 | impact | the effect | The second jolt, a heartbeat later (*house*, 260 ms), the signature's second half: 10 sparks, narrower and higher. `burst at 1100, count 10, shape spark, tint family, direction up, spread 30, reach 340, strength 0.45, lasts 300, blend add` | [Burst](../battle-craft.md#burst), [Stagger and overlap](../battle-craft.md#stagger-and-overlap) |
| 1160 | 560 | impact | the effect | The rush that stays: 6 small sparks alive at once climb off each ally, gone by 1720 ms. `flow at 1160, count 6, shape spark, tint family, direction up, spread 20, reach 200, strength 0.3, lasts 560, blend add` | [Flow](../battle-craft.md#flow), [The three parts of an effect](../battle-craft.md#the-three-parts-of-an-effect) |
| 1740 | 800 | impact | the field | Nothing new. Every figure home, breathing. The result is read here. | [The hold before the number](../battle-craft.md#the-hold-before-the-number), [Breath at rest](../battle-craft.md#breath-at-rest) |

The scene starts each ally's copy of a beat 40 ms after the one before (a1, a2, a3: 0, 40 and 80 ms), so the third
ally's last spark is gone by 1800 ms. Six staged beats, one in the windup; 30 particles asked for on each ally (14,
10, 6) and 10 on a1's glands; 72 on the field at most (at 1100 ms, 14 and 10 on each of three); one glow, the ring
under each ally, no shake, the last gone by 1740 ms: inside Stage II. No flash; one change of light, the three rings
together.

### Reduced motion

Nothing. The frame after the action is shown at once with the result text: no load, no drop, no ring, no spark. The
energy and the +1 damage are the result text's and the status chip's.

### Cost

- In the windup: one emitter on a1 (the drops), gone at 840 ms.
- Objects at the peak (1100 to 1140 ms): on each ally one `Graphics` for the motif, its ring until about 1240 ms, and
  two emitters (the first jolt's last sparks, the second jolt); nine to twelve draw calls for about 40 ms, about six
  from 1240 ms. Each emitter and each ring is one; the motifs batch with the vectors.
- Particles at the peak: 72 at 1100 ms; of the field's 150.
- Bytes: not measured. Built by BC4 (motif, burst, flow, glow, the `staging` field), BC5 (the cast's body) and BC6
  (the ring shape of `glow`, the `level`); BC7 places the beats. The cancel path is the kit's: the windup's drops die
  with the action.

### On the film strip

```bash
node scripts/battle/filmstrip.mjs spell:adrenaline_tonic:v1 --actor a1 --targets a1,a2,a3 --out <folder>/overdrive.png
node scripts/battle/filmstrip.mjs spell:adrenaline_tonic:v1 --actor a1 --targets a1,a2,a3 \
  --frames 24 --interval 60 --columns 6 --out <folder>/overdrive-contact.png
```

- 0 to 300 ms (contact): no arc, mark or particle on a1; a2 and a3 unchanged.
- 420 to 780 ms: small round green drops just above a1's back, rising, a few at once; none on a2 or a3.
- 840 ms: no drop left over a1. No figure filled on any frame.
- 900 ms: a thin green ring on the ground under each of a1, a2 and a3, flat, wider than its feet; green sparks over
  each one's head, pointing up; none below the feet, none sideways onto a neighbour.
- 960 to 1080 ms: the sparks well above each head in a narrow column, fading; the spore motif on each body; the rings
  wide and faint, an edge at most reaching a neighbour's feet.
- 1140 to 1260 ms: a second column of sparks over each head, its top higher than the first column's at 960 ms. No
  ring left at 1260 ms.
- 1320 to 1380 ms: a few small sparks rising over each ally; the motif fading.
- 1440 ms (whole): a spark or two at most. 1920 ms (whole) and every frame after: no effect object on the field.
- b1, b2 and b3 are not lit, moved or marked on any frame. Nothing in the effect is gold.
- Set beside Toxic Mend's sheet at 960 ms: Overdrive's are sparks pointing up over the heads; Toxic Mend's are round
  drops closing on the bodies, nothing over the heads.

The sheets cannot show the feel of the two jolts at full speed, nor the sound.

### Left

- Green sparks rising mean energy gained, here and on Pursuit's caster: a reading the family did not have. Chosen
  because a spark is quick and pointed where a drop is round and slow, so it is not read as venom. The owner may
  refuse it; then the jolts keep their shape in `bone`, and lose the family's colour.
- The two jolts are one gift told twice, a heartbeat: they do not split the 4 energy from the +1 damage. The round
  of +1 damage is the status chip's to say.
- Each ally's ring reaches twice its radius (*house*): on the bench the three rings may touch at their widest. The
  film tunes the reach.
- a1 is among the targets on the bench: its drops rise in the windup and it jolts with the others. Cast on two allies
  other than itself, the drops stay a1's and the jolts are theirs.

## Toxic Mend (`spell:noxious_cure:v1`)

### Intent

The player reads "the Blighted spat its secretion onto its allies: they mend, and their hide goes soft": a cure that
costs. Toxic Mend costs 2 energy; up to three allies are healed 6 and their defense is lowered by 3 for a round.
Stage II, cast at need, so one windup beat and one soft light, no shake and no flash. Its signature is the mend that
sheds: green drops close in and soak into each ally under a soft light, then small bone flakes fall off its hide and
a few green beads cling to it. Overdrive's sparks leave upward; Toxic Mend's drops come in, and something falls off.

### Today

Filmed at `28f2dc2`, 390 by 844, 1×, a1 on a1, a2 and a3, nobody struck: a whole action and a contact.

- 0 to 180 ms: the shared faint green arc on a1's body. a1 barely moves through the windup.
- 900 to 1380 ms: pale green spore rings, open circles, drift over a1, a2 and a3, fading; gone by 1680 ms.
- The same sheet as Overdrive's: nothing says a heal, nothing says its price.

### Beats

| At | Lasts | Window | Layer | Beat | Pattern |
| --- | --- | --- | --- | --- | --- |
| 0 | 600 | windup | the caster | A cast's load where it stands ([movement.md](movement.md), `CAST`): a1 squats 4%. | [Anticipation](../battle-craft.md#anticipation), [Squash and stretch](../battle-craft.md#squash-and-stretch) |
| 480 | 360 | windup | the effect | The glands filling, the one windup beat: 12 round green drops (*house*) on a ring well outside a1 close in on its body by 840 ms. `gather at 480, on caster, count 12, shape dot, tint family, reach 220, strength 0.4, lasts 360, blend normal` | [Gravity well and convergence](../battle-craft.md#gravity-well-and-convergence), [Anticipation](../battle-craft.md#anticipation) |
| 600 | 240 | windup | the caster | The rise: a1 stretches 4% ([movement.md](movement.md)). | [Squash and stretch](../battle-craft.md#squash-and-stretch) |
| 840 | 360 | impact | the caster | The release, as a cast lets go ([movement.md](movement.md)). Nobody lost health: no flash on any figure. | [Contact and follow-through](../battle-craft.md#contact-and-follow-through) |
| 840 | 900 | impact | the effect | The `spores` motif on each ally, gone by 1740 ms. `motif at 840, lasts 900` | [Shape before colour](../battle-craft.md#shape-before-colour), [The three parts of an effect](../battle-craft.md#the-three-parts-of-an-effect) |
| 840 | 320 | impact | the effect | The secretion landing, the signature's first half: 12 round green drops (*house*) on a ring around each ally close on its centre by 1160 ms. Drops, opaque: what a gland makes. `gather at 840, on target, count 12, shape dot, tint family, reach 200, strength 0.5, lasts 320, blend normal` | [Gravity well and convergence](../battle-craft.md#gravity-well-and-convergence), [One effect, one meaning](../battle-craft.md#one-effect-one-meaning) |
| 1160 | 420 | impact | the effect | The mend, the one light: a soft green glow on each body, close to it, as the drops soak in, gone by 1580 ms. Not a ring: nothing struck the ground. `glow at 1160, tint family, reach 170, strength 0.55, lasts 420, blend screen` | [Glow](../battle-craft.md#glow), [Additive light](../battle-craft.md#additive-light) |
| 1240 | 400 | impact | the effect | The price, the signature's second half: 10 small bone flakes (*house*), the ally's own hide loosened, knocked off all round, short, and falling. `burst at 1240, count 10, shape shard, tint bone, direction out, spread 360, reach 180, strength 0.25, lasts 400, blend normal` | [Burst](../battle-craft.md#burst), [One effect, one meaning](../battle-craft.md#one-effect-one-meaning) |
| 1240 | 600 | impact | the effect | What clings for the round: 6 green beads alive at once well up over each body, drift a little and shrink, gone by 1840 ms. `flow at 1240, count 6, shape dot, tint family, direction out, spread 360, reach 50, strength 0.3, lasts 600, blend normal` | [Flow](../battle-craft.md#flow), [The three parts of an effect](../battle-craft.md#the-three-parts-of-an-effect) |
| 1840 | 700 | impact | the field | Nothing new. Every figure home, breathing. The result is read here. | [The hold before the number](../battle-craft.md#the-hold-before-the-number), [Breath at rest](../battle-craft.md#breath-at-rest) |

Staggered 40 ms an ally as Overdrive's, the third ally's last bead is gone by 1920 ms. Six staged beats, one in the
windup; 28 particles asked for on each ally (12, 10, 6) and 12 on a1; 48 on the field at most (at 1240 ms, 10 and 6
on each of three); one glow, no shake, the last gone by 1840 ms: inside Stage II. No flash: a heal is not a loss. One
change of light.

### Reduced motion

Nothing. The frame after the action is shown at once with the result text: no load, no drop, no light, no flake. The
lowered defense is the status chip's to say.

### Cost

- In the windup: one emitter on a1 (the drops), gone at 840 ms.
- Objects at the peak (1240 to 1580 ms): on each ally one `Graphics` for the motif, one glow image and two emitters
  (flakes, beads); about nine draw calls for 340 ms, then six.
- Particles at the peak: 48 at 1240 ms; of the field's 150.
- Bytes: not measured. Built by BC4 (motif, burst, flow, gather, glow, the `staging` field) and BC5 (the cast's
  body); BC7 places the beats. The cancel path is the kit's.

### On the film strip

```bash
node scripts/battle/filmstrip.mjs spell:noxious_cure:v1 --actor a1 --targets a1,a2,a3 --out <folder>/toxic-mend.png
node scripts/battle/filmstrip.mjs spell:noxious_cure:v1 --actor a1 --targets a1,a2,a3 \
  --frames 24 --interval 60 --columns 6 --out <folder>/toxic-mend-contact.png
```

- 0 to 420 ms (contact): no arc, mark or particle on a1; a2 and a3 unchanged.
- 540 to 780 ms: round green drops around a1, well outside its outline at 540 ms, closing on its body by 780 ms; none
  around a2 or a3.
- 840 ms: no drop left around a1. No figure filled on any frame.
- 900 to 1080 ms: round green drops on a ring around each of a1, a2 and a3, closing on each body; the spore motif on
  each. Nothing over the heads, no ring on the ground.
- 1140 to 1260 ms: the drops landed; a soft green light on each body, close to it.
- 1320 to 1380 ms: small pale flakes just outside each outline on every side, lower than where they left; a few green
  beads on each body.
- 1440 ms (whole): the light faint or gone; a flake or two below each figure's middle; beads on each body.
- 1680 ms (whole): a bead or two at most. 1920 ms (whole) and every frame after: no effect object on the field.
- b1, b2 and b3 are not lit, moved or marked on any frame. Nothing in the effect is gold.
- Set beside Overdrive's sheet at 1200 ms: Toxic Mend has a light on the bodies and nothing above them; Overdrive has
  sparks above the heads and no light on the bodies.

The sheets cannot show the feel at full speed, nor the sound.

### Left

- The lowered defense is told by flakes of the ally's own hide falling off: bone in this family is a creature's hard
  parts, here loosened. The owner may read flakes as a hit; then the cost is the status chip's alone and the burst
  becomes a second, smaller soak of drops, so the staging keeps its six beats.
- The beads that cling are green, where Venom Claw's are blood: what stays on the ally is the gland's, not a wound.
- The heal is a light, not a number: the number is the result text's. A critical heal (35%) cannot be told: a staging
  cannot tell a critical hit.
- Toxic Mend and Overdrive both begin on a1 in the windup: drops gather in for Toxic Mend, drops rise out for
  Overdrive. Chosen so the two casts of one mutation differ from their first frame.

## Ambush (`spell:ambush:v1`)

### Intent

The player reads "the Deathmarked burst out of hiding onto that one, and the leap left it heavy on its feet": a blast
at contact, then a hunter spent. Ambush costs 3 energy, deals 10 damage to one enemy in melee with a 35% critical
chance, and lowers its caster's initiative for a round. The hardest single blow of the family's Stage II, cast a few
times a game: one bright light, a staged shake, no windup beat. Its signature is the burst from the point of contact:
claw splinters thrown out on every side of b1 under a hard green light, then grit closing slowly on a1's feet once it
is home. Pursuit draws a line; Ambush explodes all round.

### Today

Filmed at `28f2dc2`, 390 by 844, 1×, a1 on b1, 10 health lost: a whole action and a contact.

- 0 to 120 ms: the shared faint green arc on a1's body.
- 60 to 540 ms: a1 crouched below its ring, leaning away from b1; 600 to 780 ms: a1 leaves its ring and climbs up the
  field to b1.
- 840 and 900 ms: b1 filled flat in bone; a1 covers b1's lower half, b1's head clear.
- 960 to 1380 ms: three pale green slash strokes cut diagonally across b1, fading; a1 nearly home at 1380 ms.
- Pursuit's sheet differs only by the body: the same strokes, nothing of a blast, nothing on a1.

### Beats

| At | Lasts | Window | Layer | Beat | Pattern |
| --- | --- | --- | --- | --- | --- |
| 0 | 840 | windup | the caster | A heavy blow's draw back, hold and strike by the 10 health b1 lost ([movement.md](movement.md)). No mark on a1: no windup beat, so no shared arc. The ambush is not announced. | [Anticipation](../battle-craft.md#anticipation), [Contact and follow-through](../battle-craft.md#contact-and-follow-through) |
| 840 | 800 | impact | the effect | The `slash` motif on b1, its strokes cutting 60° across the blow on their dark edge, gone by 1640 ms. `motif at 840, lasts 800` | [An impact that keeps the direction](../battle-craft.md#an-impact-that-keeps-the-direction), [Shape before colour](../battle-craft.md#shape-before-colour) |
| 840 | 280 | impact | the effect | The blast's light, the one light: a hard green glow on b1, brighter than any other of the family's Stage II, gone by 1120 ms. `glow at 840, tint family, reach 240, strength 0.8, lasts 280, blend add` | [Glow](../battle-craft.md#glow), [Additive light](../battle-craft.md#additive-light) |
| 840 | 360 | impact | the effect | The burst, the signature's first half: 20 bone splinters (*house*) thrown out on every side of b1, past its outline. Bone, not blood: Ambush leaves no Bleed. `burst at 840, count 20, shape shard, tint bone, direction out, spread 360, reach 300, strength 0.5, lasts 360, blend normal` | [Burst](../battle-craft.md#burst), [The three parts of an effect](../battle-craft.md#the-three-parts-of-an-effect) |
| 840 | 260 | impact | the effect | The pounce carried through: 12 bright bone sparks (*house*) on along the blow, beyond b1, in a 60° fan. `burst at 840, count 12, shape spark, tint bone, direction along, spread 60, reach 360, strength 0.5, lasts 260, blend add` | [Burst](../battle-craft.md#burst), [An impact that keeps the direction](../battle-craft.md#an-impact-that-keeps-the-direction) |
| 840 | 240 | impact | the camera | A staged shake on the body's tremor, decaying over about 240 ms (*house*). `shake at 840, strength 0.12` | [Shake from trauma](../battle-craft.md#shake-from-trauma) |
| 840 | 900 | impact | the caster and the target | The heavy blow's hit stop (120 ms), b1's bone flash, its recoil of 26 px, a1's return, home by 1740 ms ([movement.md](movement.md)). | [Hit stop](../battle-craft.md#hit-stop), [Recoil](../battle-craft.md#recoil) |
| 1440 | 480 | impact | the effect | The weight, the signature's second half: 10 pale grains of grit (*house*) on a flat ring on the ground around a1's feet close slowly on them as a1 lands home, gone by 1920 ms. Played at a1's home: a1 is there, or all but, by 1440 ms. `gather at 1440, on caster, count 10, shape dot, tint bone, reach 200, strength 0.3, lasts 480, blend normal, level feet` | [Gravity well and convergence](../battle-craft.md#gravity-well-and-convergence), [A beat on the ground](../battle-craft.md#a-beat-on-the-ground) |
| 1920 | 620 | impact | the field | Nothing new. Both figures home, breathing. The result is read here. | [The hold before the number](../battle-craft.md#the-hold-before-the-number), [Breath at rest](../battle-craft.md#breath-at-rest) |

Six staged beats, none in the windup; 32 particles asked for on b1 (20, 12) and 10 on a1; one glow, a shake of 0.12,
the last gone by 1920 ms: inside Stage II. One flash, the body's, on b1; one change of light. A critical hit is told
by the body's punch of the zoom, not by the staging.

### Reduced motion

Nothing. The frame after the action is shown at once with the result text: no blow, no light, no splinter, no grit.
The lowered initiative is the status chip's to say.

### Cost

- Objects at the peak (840 to 1100 ms): one `Graphics` for the motif, one glow image, two emitters (splinters,
  sparks), the body's tweens; about four draw calls for 260 ms, then two; one emitter on a1 from 1440 ms.
- Particles at the peak: 32 at 840 ms; 42 asked for in all; of the field's 150.
- Bytes: not measured. Built by BC4 (motif, burst, gather, glow, shake, the `staging` field), BC5 (the body, the
  trauma) and BC6 (the slash cutting across, the dark edge, the `level`); BC7 places the beats. The cancel path is
  the kit's.

### On the film strip

```bash
node scripts/battle/filmstrip.mjs spell:ambush:v1 --actor a1 --targets b1 --damage 10 --out <folder>/ambush.png
node scripts/battle/filmstrip.mjs spell:ambush:v1 --actor a1 --targets b1 --damage 10 \
  --frames 24 --interval 60 --columns 6 --out <folder>/ambush-contact.png
```

- 0 to 780 ms (contact): no arc, mark or particle on a1; a1 plainly below its ring at 240 ms; it leaves the ring by
  660 ms.
- 840 and 900 ms: b1 filled flat in bone, in these two frames and no other; a1 covers b1's lower half, not its head; a
  green light around b1, the brightest of the action.
- 900 to 960 ms: pale bone splinters on every side of b1, past its outline, some over a1's back; a short line of
  bright pale sparks over b1's head (`along` shortened on a blow struck up the field).
- 1020 to 1080 ms: the light gone; a few splinters, far from b1; the slash strokes diagonal on their dark edge.
- 1140 ms: no splinter or spark left; the strokes on b1.
- 1440 ms (whole): a1 home or all but; the strokes faint; at most a faint grain near a1's feet.
- 1680 ms (whole): pale grains on a flat ring on the ground around a1's feet, about as wide as its ground ring,
  closing; none on b1.
- 1920 ms (whole) and every frame after: no effect object on the field. Nothing in the effect is gold, and none of it
  is red.
- Set beside Pursuit's sheet at 960 ms: Ambush's debris lies all round b1 under a light; Pursuit's is a narrow line
  over b1's head, and b1 is not lit.

The sheets cannot show the stop, the shake, the punch of a critical, or the feel at full speed.

### Left

- The slowed caster is told by grit closing on its feet, not by a slower body: the return home is
  [movement.md](movement.md)'s and does not change by spell. A slower next breath on a1 is not the staging's.
- Pale grains at the feet are grit, a reading the opening adds: bone elsewhere in the family is a quill or a claw.
  The owner may prefer them in the family's green; then they would read as a gland's.
- Ten damage and no blood: blood here says "bleeds" or "drunk". The owner may want blood on the family's hardest
  blow; then Ambush's splinters share the field with Wild Swing's fan.
- No windup beat, by choice: the ambush is not announced, and the body's heavy crouch is the windup. The venom sheen
  Venom Claw's storyboard kept for a Stage II claw is not used: neither Ambush nor Pursuit poisons.
- In a match the host may list a1 among the recipients of its own initiative loss; the scene then draws its own mark
  on a1, which goes with its body. The bench's film, on `--targets b1`, does not show it beside the grit.

## Pursuit (`spell:momentum:v1`)

### Intent

The player reads "the Deathmarked ran that one down in one stride, and came back keener": a fast lunge, a scratch on
the run, and energy gained. Pursuit costs nothing, deals 3 damage to one enemy in melee and gives its caster 2
energy; it is cast often, so it is the quietest of the family's Stage II: small particles, no shake, the one light
kept on a1. Its signature is the streak: a line of green sparks drawn behind a1's lunge, carried on past b1 at the
scratch. Back home, a1 sparks up: the energy, Overdrive's look. Ambush explodes all round; Pursuit runs through.

### Today

Filmed at `28f2dc2`, 390 by 844, 1×, a1 on b1, 3 health lost: a whole action and a contact.

- 0 to 120 ms: the shared faint green arc on a1's body.
- 60 to 540 ms: a1 crouched below its ring, leaning away from b1; 600 to 780 ms: a1 leaves its ring and climbs to b1.
- 840 and 900 ms: b1 filled flat in bone; a1 covers b1's lower half, b1's head clear.
- 960 to 1380 ms: three pale green slash strokes cut diagonally across b1, fading. Nothing says fast, nothing says
  energy: the same strokes as Ambush's.

### Beats

| At | Lasts | Window | Layer | Beat | Pattern |
| --- | --- | --- | --- | --- | --- |
| 0 | 840 | windup | the caster | A light blow's draw back and strike by the 3 health b1 lost ([movement.md](movement.md)). No mark on a1's body. | [Anticipation](../battle-craft.md#anticipation), [Contact and follow-through](../battle-craft.md#contact-and-follow-through) |
| 540 | 300 | windup | the effect | The streak, the one windup beat and the signature: a head runs from a1's place to b1, easing in as the body does, dropping 12 green sparks (*house*) that fade behind it; it reaches b1 at 840 ms. Drawn from where a1 stood, by design: it is the path, not the body. `trail at 540, count 12, shape spark, tint family, strength 0.3, lasts 300, blend add` | [Trail](../battle-craft.md#trail), [Contact and follow-through](../battle-craft.md#contact-and-follow-through) |
| 840 | 600 | impact | the effect | The `slash` motif on b1, quick, gone by 1440 ms. `motif at 840, lasts 600` | [An impact that keeps the direction](../battle-craft.md#an-impact-that-keeps-the-direction), [Shape before colour](../battle-craft.md#shape-before-colour) |
| 840 | 220 | impact | the effect | The run carried on: 8 green sparks (*house*) go on beyond b1 along the blow in a narrow 30° line, the streak's end. `burst at 840, count 8, shape spark, tint family, direction along, spread 30, reach 300, strength 0.4, lasts 220, blend add` | [Burst](../battle-craft.md#burst), [An impact that keeps the direction](../battle-craft.md#an-impact-that-keeps-the-direction) |
| 840 | 840 | impact | the caster and the target | The light blow's hit stop (60 ms), b1's bone flash, its recoil of 12 px, a1's return, home by 1680 ms ([movement.md](movement.md)). | [Hit stop](../battle-craft.md#hit-stop), [Recoil](../battle-craft.md#recoil) |
| 860 | 200 | impact | the effect | The scratch: 6 small bone chips (*house*) off b1's hide, short, all round. `burst at 860, count 6, shape shard, tint bone, direction out, spread 360, reach 120, strength 0.3, lasts 200, blend normal` | [Burst](../battle-craft.md#burst), [The three parts of an effect](../battle-craft.md#the-three-parts-of-an-effect) |
| 1440 | 260 | impact | the effect | The one light, on a1 as it lands home: the energy taken in. Played at a1's home. `glow at 1440, on caster, tint family, reach 150, strength 0.5, lasts 260, blend add` | [Glow](../battle-craft.md#glow), [A light that answers a cast](../battle-craft.md#a-light-that-answers-a-cast) |
| 1440 | 300 | impact | the effect | Keener: 8 green sparks (*house*) jump straight up off a1, Overdrive's jolt on one figure: energy gained. `burst at 1440, on caster, count 8, shape spark, tint family, direction up, spread 50, reach 220, strength 0.4, lasts 300, blend add` | [Burst](../battle-craft.md#burst), [One effect, one meaning](../battle-craft.md#one-effect-one-meaning) |
| 1740 | 800 | impact | the field | Nothing new. Both figures home, breathing. The result is read here. | [The hold before the number](../battle-craft.md#the-hold-before-the-number), [Breath at rest](../battle-craft.md#breath-at-rest) |

Six staged beats, one in the windup (the trail); 26 particles asked for on b1 (12, 8, 6) and 8 on a1; one glow, on
a1; no shake; the last gone by 1740 ms: inside Stage II. One flash, the body's, on b1; one change of light, on a1.

### Reduced motion

Nothing. The frame after the action is shown at once with the result text: no lunge, no streak, no spark, no light.

### Cost

- In the windup: one emitter (the streak), its last sparks gone about 150 ms after 840 ms or with the window.
- Objects at the peak (860 to 1060 ms): one `Graphics` for the motif, up to three emitters (the streak's last, the
  sparks, the chips), the body's tweens; about four draw calls. At 1440 ms one glow image and one emitter on a1.
- Particles at the peak: about 20 at 860 ms; 34 asked for in all; of the field's 150.
- Bytes: not measured. Built by BC4 (motif, burst, trail, glow, the `staging` field), BC5 (the body) and BC6 (the
  slash cutting across); BC7 places the beats. The cancel path is the kit's: the trail dies with the action.

### On the film strip

```bash
node scripts/battle/filmstrip.mjs spell:momentum:v1 --actor a1 --targets b1 --damage 3 --out <folder>/pursuit.png
node scripts/battle/filmstrip.mjs spell:momentum:v1 --actor a1 --targets b1 --damage 3 \
  --frames 24 --interval 60 --columns 6 --out <folder>/pursuit-contact.png
```

- 0 to 480 ms (contact): no arc, mark or particle on a1 or anywhere on the field.
- 600 to 780 ms: green sparks in a line from a1's ring up toward b1, behind a1's body and never ahead of its head; the
  line longer at each frame.
- 840 ms: b1 filled flat in bone (840 and 900 ms only); a1 over b1's lower half, b1's head clear; the line's last
  sparks fading between a1's ring and b1.
- 900 ms: a few bright green sparks in a short narrow line over b1's head (`along` shortened on a blow struck up the
  field); small pale chips just outside b1's outline. No light on b1, on this frame or any.
- 960 to 1080 ms: the slash strokes diagonal on their dark edge; no chip or spark left by 1080 ms.
- 1140 to 1380 ms: the strokes fading; a1 returning; nothing on a1.
- 1440 ms (whole): a1 home or all but, a soft green light on it, green sparks over its back pointing up.
- 1680 ms (whole) and every frame after: no effect object on the field. Nothing in the effect is gold or red.
- Set beside Ambush's sheet at 960 ms: Pursuit's debris is a thin line and b1 is not lit; Ambush's lies all round b1
  under a light.

The sheets cannot show the stop or the feel of the lunge at full speed.

### Left

- The streak is a trail from a1's home to b1's centre, timed by eye to stay behind the lunging body: the two are not
  tied. If the film shows the head ahead of a1, the trail starts later; a trail that follows the body is a change to
  the kit.
- Green sparks mean energy gained, as on Overdrive: the owner may refuse that reading (Overdrive's **Left**).
- The light is kept on a1, not on b1, so a spell cast this often stays quiet on its target.
- In a match the host may list a1 among the recipients of its own energy; the scene then draws its own mark on a1,
  which goes with its body. The bench's film, on `--targets b1`, does not show it beside the sparks.

## Latch (`spell:latch:v1`)

### Intent

The player reads "the Parasite grabbed that one with its six legs and fed, and the wound keeps bleeding": a grip, a
feed and a Bleed. Latch costs 1 energy, deals 1 damage to one enemy in melee, leaves a Bleed of 2 for three rounds
and gives its caster a Regeneration for three rounds. A light blow with a long tail, so its effect is in what stays:
no shake, no windup beat. Its signature is the clamp: six bone hooks close on b1 from all round at contact, blood
spurts where they land, and the weep that follows lasts longer than Venom Claw's, with a thin stream drawn toward a1.
Bite's blood goes to a1; Latch's stays on b1.

### Today

Filmed at `28f2dc2`, 390 by 844, 1×, a1 on b1, 1 health lost: a whole action and a contact.

- 0 to 120 ms: the shared faint green arc on a1's body.
- 60 to 540 ms: a1 crouched below its ring; 600 to 780 ms: a1 leaves its ring and climbs to b1.
- 840 and 900 ms: b1 filled flat in bone; a1 covers b1's lower half.
- 960 to 1380 ms: the `bite` motif on b1, pale green teeth, low in contrast on its hide, fading. No particle. Bite's
  sheet shows the same teeth: nothing grips, nothing bleeds.

### Beats

| At | Lasts | Window | Layer | Beat | Pattern |
| --- | --- | --- | --- | --- | --- |
| 0 | 840 | windup | the caster | A light blow's draw back and strike by the 1 health b1 lost ([movement.md](movement.md)). No mark on a1's body. | [Anticipation](../battle-craft.md#anticipation), [Contact and follow-through](../battle-craft.md#contact-and-follow-through) |
| 840 | 900 | impact | the effect | The `bite` motif on b1, held longer than Bite's, gone by 1740 ms. `motif at 840, lasts 900` | [Shape before colour](../battle-craft.md#shape-before-colour), [The three parts of an effect](../battle-craft.md#the-three-parts-of-an-effect) |
| 840 | 200 | impact | the effect | The clamp, the signature's first half: 6 bone hooks (*house*, one a leg) on a ring well outside b1 close on its body by 1040 ms. `gather at 840, on target, count 6, shape shard, tint bone, reach 240, strength 0.5, lasts 200, blend normal` | [Gravity well and convergence](../battle-craft.md#gravity-well-and-convergence), [Shape before colour](../battle-craft.md#shape-before-colour) |
| 840 | 840 | impact | the caster and the target | The light blow's hit stop (60 ms), b1's bone flash, its recoil of 12 px, a1's return, home by 1680 ms ([movement.md](movement.md)). | [Hit stop](../battle-craft.md#hit-stop), [Recoil](../battle-craft.md#recoil) |
| 1000 | 900 | impact | the effect | The Bleed: 10 deep red beads (*house*) well up over b1's body, a few at a time, drift a little and shrink, gone by 1900 ms; longer than Venom Claw's 640 ms, for three rounds against one. `flow at 1000, count 10, shape dot, tint blood, direction out, spread 360, reach 60, strength 0.35, lasts 900, blend normal` | [Flow](../battle-craft.md#flow), [One effect, one meaning](../battle-craft.md#one-effect-one-meaning) |
| 1040 | 240 | impact | the effect | The punctures, as the hooks land: 6 small drops of blood (*house*) spurt just past b1's outline, all round. `burst at 1040, count 6, shape dot, tint blood, direction out, spread 360, reach 80, strength 0.3, lasts 240, blend normal` | [Burst](../battle-craft.md#burst), [Stagger and overlap](../battle-craft.md#stagger-and-overlap) |
| 1040 | 560 | impact | the effect | The feed: 8 deep red motes (*house*) alive at once drawn out of b1 toward a1 in a narrow stream, gone by 1600 ms. It does not arrive: the arrival is Bite's. `flow at 1040, count 8, shape dot, tint blood, direction back, spread 30, reach 240, strength 0.3, lasts 560, blend normal` | [Flow](../battle-craft.md#flow), [Gravity well and convergence](../battle-craft.md#gravity-well-and-convergence) |
| 1440 | 500 | impact | the effect | The Regeneration, the one light: a slow, faint green glow on a1 at home, gone by 1940 ms. `glow at 1440, on caster, tint family, reach 150, strength 0.4, lasts 500, blend screen` | [Glow](../battle-craft.md#glow), [A light that answers a cast](../battle-craft.md#a-light-that-answers-a-cast) |
| 1940 | 600 | impact | the field | Nothing new. Both figures home, breathing. The result is read here. | [The hold before the number](../battle-craft.md#the-hold-before-the-number), [Breath at rest](../battle-craft.md#breath-at-rest) |

Six staged beats, none in the windup; 30 particles asked for on b1 (6, 10, 6, 8); one glow, on a1; no shake; the last
gone by 1940 ms: inside Stage II. One flash, the body's, on b1; one change of light, on a1.

### Reduced motion

Nothing. The frame after the action is shown at once with the result text: no grip, no blood, no light. The Bleed and
the Regeneration are the status chips' to say.

### Cost

- Objects at the peak (1040 to 1280 ms): one `Graphics` for the motif, three emitters (beads, punctures, stream), the
  body's tweens; about four draw calls, then three. From 1440 ms one glow image on a1.
- Particles at the peak: 24 at 1040 ms (10, 6 and 8 asked for, fewer alive); 30 asked for in all; of the field's 150.
- Bytes: not measured. Built by BC4 (motif, burst, flow, gather, glow, the `staging` field), BC5 (the body) and BC6
  (the `blood` tint); BC7 places the beats. The cancel path is the kit's.

### On the film strip

```bash
node scripts/battle/filmstrip.mjs spell:latch:v1 --actor a1 --targets b1 --damage 1 --out <folder>/latch.png
node scripts/battle/filmstrip.mjs spell:latch:v1 --actor a1 --targets b1 --damage 1 \
  --frames 24 --interval 60 --columns 6 --out <folder>/latch-contact.png
```

- 0 to 780 ms (contact): no arc, mark or particle on a1.
- 840 and 900 ms: b1 filled flat in bone (these two only); a1 over b1's lower half, b1's head clear; pale bone hooks
  on a ring well outside b1 at 900 ms, some over a1, closing.
- 960 to 1020 ms: the hooks on b1's outline or inside it; none left outside it by 1080 ms.
- 1080 ms: small deep red drops just outside b1's outline on every side; deep red beads on b1's body, not pink; the
  bite motif on b1.
- 1140 to 1380 ms: beads on b1, a few at once; deep red motes leaving b1's lower side toward a1, none above b1's head;
  a1 returning. No red on a1.
- 1440 ms (whole): a1 home or all but, a faint green light on it; beads still on b1.
- 1680 ms (whole): a bead or two on b1; the light on a1 faint. 1920 ms (whole): a last bead at most.
- 2160 ms (whole) and every frame after: no effect object on the field. Nothing in the effect is gold.
- Set beside Bite's sheet at 1440 ms: Latch's red is on b1 and a1 is only lit; Bite's red is on a1.

The sheets cannot show the stop, or the feel of the grip at full speed.

### Left

- The six hooks are a gather of six shards: the kit places them at random on its ring, not as six legs evenly, and
  lands them on b1's centre, so they read as closing, not holding. A grip that stops at the outline is a change to
  the kit (a gather that ends short of the centre), not a new pattern.
- The stream toward a1 is a flow aimed `back`: it leaves b1 toward a1 and fades before it arrives, so the arrival
  stays Bite's signature. The owner may want it to land; then Latch and Bite read alike.
- The Bleed and the Regeneration last three rounds; the effect ends by 1940 ms. The rounds are the status chips'.
- In a match the host may list a1 among the recipients of its Regeneration; the scene then draws its own mark on a1,
  which goes with its body. The bench's film, on `--targets b1`, does not show it beside the light.

## Bite (`spell:parasite_jab:v1`)

### Intent

The player reads "the Parasite bit a mouthful out of that one and swallowed it": a snap, then the caster fed. Bite
costs 2 energy, deals 6 damage to one enemy in melee with a 45% critical chance, and heals its caster 4. A medium
blow cast often by its mutation: a small staged shake, one light on a1, no windup beat. Its signature is the gulp:
blood torn back from b1 toward a1, then landing on a1 as it reaches home, under one light. Latch's blood stays on b1;
Bite's goes to a1.

### Today

Filmed at `28f2dc2`, 390 by 844, 1×, a1 on b1, 6 health lost: a whole action and a contact.

- 0 ms: the shared faint green arc on a1's body.
- 240 to 480 ms: a1 crouched below its ring, leaning away from b1; at 720 ms on its way up to b1.
- 960 ms: a1 over b1's lower half; the `bite` motif, pale green teeth, on b1, fading through 1440 ms.
- 1440 ms: a1 home. Nothing on a1 at any frame: the heal is not seen.

### Beats

| At | Lasts | Window | Layer | Beat | Pattern |
| --- | --- | --- | --- | --- | --- |
| 0 | 840 | windup | the caster | A medium blow's draw back, hold and strike by the 6 health b1 lost ([movement.md](movement.md)). No mark on a1's body. | [Anticipation](../battle-craft.md#anticipation), [Contact and follow-through](../battle-craft.md#contact-and-follow-through) |
| 840 | 700 | impact | the effect | The `bite` motif on b1, gone by 1540 ms. `motif at 840, lasts 700` | [Shape before colour](../battle-craft.md#shape-before-colour), [The three parts of an effect](../battle-craft.md#the-three-parts-of-an-effect) |
| 840 | 180 | impact | the effect | The snap, the brightest and shortest part: 10 small bone sparks (*house*), the teeth meeting, out all round, short. `burst at 840, count 10, shape spark, tint bone, direction out, spread 360, reach 140, strength 0.4, lasts 180, blend normal` | [Burst](../battle-craft.md#burst), [The three parts of an effect](../battle-craft.md#the-three-parts-of-an-effect) |
| 840 | 240 | impact | the camera | A light staged shake on the body's tremor, a bite and not a blast, decaying over about 240 ms (*house*). `shake at 840, strength 0.08` | [Shake from trauma](../battle-craft.md#shake-from-trauma) |
| 840 | 870 | impact | the caster and the target | The medium blow's hit stop (90 ms), b1's bone flash, its recoil of 18 px, a1's return, home by 1710 ms ([movement.md](movement.md)). | [Hit stop](../battle-craft.md#hit-stop), [Recoil](../battle-craft.md#recoil) |
| 860 | 320 | impact | the effect | The mouthful, the signature's first half: 12 deep red drops (*house*) torn back from b1 toward a1 in a 40° fan, none beyond b1. `burst at 860, count 12, shape dot, tint blood, direction back, spread 40, reach 280, strength 0.35, lasts 320, blend normal` | [Burst](../battle-craft.md#burst), [Gravity well and convergence](../battle-craft.md#gravity-well-and-convergence) |
| 1200 | 320 | impact | the effect | Swallowed, the signature's second half: 12 deep red drops (*house*) on a ring around a1's home land on it by 1520 ms, as a1 reaches home. `gather at 1200, on caster, count 12, shape dot, tint blood, reach 240, strength 0.35, lasts 320, blend normal` | [Gravity well and convergence](../battle-craft.md#gravity-well-and-convergence), [One effect, one meaning](../battle-craft.md#one-effect-one-meaning) |
| 1520 | 300 | impact | the effect | Fed, the one light: a green glow on a1 as the drops land, gone by 1820 ms. `glow at 1520, on caster, tint family, reach 170, strength 0.6, lasts 300, blend screen` | [Glow](../battle-craft.md#glow), [A light that answers a cast](../battle-craft.md#a-light-that-answers-a-cast) |
| 1820 | 720 | impact | the field | Nothing new. Both figures home, breathing. The result is read here. | [The hold before the number](../battle-craft.md#the-hold-before-the-number), [Breath at rest](../battle-craft.md#breath-at-rest) |

Six staged beats, none in the windup; 22 particles asked for on b1 (10, 12) and 12 on a1; one glow, on a1; a shake
of 0.08; the last gone by 1820 ms: inside Stage II. One flash, the body's, on b1; one change of light, on a1.

### Reduced motion

Nothing. The frame after the action is shown at once with the result text: no bite, no blood, no light on a1.

### Cost

- Objects at the peak (860 to 1020 ms): one `Graphics` for the motif, two emitters (the snap's last sparks, the
  mouthful), the body's tweens; about three draw calls. From 1200 ms one emitter on a1, then one glow image.
- Particles at the peak: 22 at 860 ms; 34 asked for in all; of the field's 150.
- Bytes: not measured. Built by BC4 (motif, burst, gather, glow, shake, the `staging` field), BC5 (the body, the
  trauma) and BC6 (the `blood` tint); BC7 places the beats. The cancel path is the kit's.

### On the film strip

```bash
node scripts/battle/filmstrip.mjs spell:parasite_jab:v1 --actor a1 --targets b1 --damage 6 --out <folder>/bite.png
node scripts/battle/filmstrip.mjs spell:parasite_jab:v1 --actor a1 --targets b1 --damage 6 \
  --frames 24 --interval 60 --columns 6 --out <folder>/bite-contact.png
```

- 0 to 780 ms (contact): no arc, mark or particle on a1.
- 840 and 900 ms: b1 filled flat in bone (these two only); a1 over b1's lower half, b1's head clear; small pale sparks
  all round the point of contact, gone by 1020 ms.
- 900 to 1080 ms: deep red drops on b1's lower body, on a1's side, none above b1. Shortened (`back` on a blow struck
  up the field), they stay on b1's outline or just past it.
- 1140 ms: no red left on b1; the bite motif fading.
- 1260 to 1380 ms: deep red drops on a ring around a1's home, faint at 1260 ms, closing on a1 as it returns.
- 1440 ms (whole): a1 home or all but, red drops on or just outside its body; no light yet.
- 1680 ms (whole): a soft green light on a1, fading; no red anywhere. 1920 ms (whole) and every frame after: no
  effect object on the field. Nothing in the effect is gold.
- Set beside Latch's sheet at 1440 ms: Bite's red is on a1; Latch's is on b1.

The sheets cannot show the stop, the shake, the punch of a critical, or the feel at full speed.

### Left

- Blood carried to the caster means "drunk": a reading the opening adds, where Stage I held blood to a wound that
  bleeds. Bite leaves no Bleed. The owner may keep blood for the Bleed alone; then the mouthful becomes pale motes,
  and Bite reads as Seize does.
- Bite has Seize's shape (taken from b1, landed on a1, one light on a1) in the other family's colours: the owner may
  find the two too close. The red is what tells them apart.
- The gather is placed at a1's home: a1 is all but home at 1440 ms on the sheets, so the drops land on its body.
- A critical hit (45%) is the body's punch of the zoom; the staging cannot tell it.
- In a match the host may list a1 among the recipients of its heal; the scene then draws its own mark on a1, which
  goes with its body. The bench's film, on `--targets b1`, does not show it beside the drops.
