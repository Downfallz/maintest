# Storyboards: the biological family

The Scoundrel's spells, drawn on the body the art calls the Predator: claws that rake, teeth that bite, bone quills
thrown from the dorsal ridge, and the rounded drops of glands and spores ([the vocabulary of the
family](../spell-vfx.md)), on the family's pale green (0xadc777). Its 14 spells climb [the ladder](README.md#the-ladder):
Stage I (Venom Claw, Quill), then Stage II (Overdrive, Toxic Mend, Ambush, Pursuit, Latch, Bite), then Stage III
(Contagion, Paralyzing Barb, Eviscerate, Blood Hunt, Blood Price, Devour). The file holds the whole family, 2, 6 and 6
storyboards. Each step must look richer than the one below it, and each spell must keep one signature a player could
name: the two spells of each mutation are told apart by shape first. The body's own movement is
[movement.md](movement.md)'s: a storyboard here names it in one row per window and spends its rows on the spell.

The family keeps one meaning per look, so a player learns it once ([One effect, one
meaning](../battle-craft.md#one-effect-one-meaning)). The motif carries the family's green and stays the primary
element. Rounded drops in the family's green are what a gland makes: venom, spores, a secretion. Shards and dots in
`bone` are the creature's own hard parts: a quill, a claw, a splinter of hide. Drops in `blood` are a wound that
bleeds, or what the caster drank (below). A spell that does not poison does not throw green drops, so Quill's debris is bone and Venom Claw's is green.

Stage II adds three readings, each kept to one look. Green sparks, pointed and quick, rising off a figure are energy
it gained (Overdrive's allies, Pursuit's caster); the rounded green drop stays the gland's. Blood that stays on the
struck figure is its Bleed (Venom Claw, Latch); blood carried back to the caster is what it drank (Bite). Pale grains
on a flat ring at a figure's feet are grit off the ground (Ambush), not a quill. A spell that neither bleeds nor
drinks shows no blood, whatever it deals: Ambush deals ten damage and its pieces are bone.

Stage III adds six readings, each kept to one look. Bone flakes knocked off a figure's hide are its defense lowered
(Toxic Mend, Contagion). Green sparks drawn off a figure toward its foe, gone before they arrive, are energy it lost
(Blood Hunt, Devour); rising off a figure, the same sparks are energy gained. Blood torn off the caster's own body is
the price it pays (Blood Price), and the wound the scene draws on it after the blow is the same price. A dark red
ring opening on the ground under a struck figure is blood pooling, the deepest Bleed (Eviscerate). A tight green ring
that lingers at a figure's feet is venom pinning it: the stun (Paralyzing Barb). The one halo on a figure's outline
(`pulse`) says it was healed, as Vital Surge's does in the physical family (Devour): a halo is never a stun or a
charge.

The pass (2026-11-19) ran three rounds, the most `/battle-pass` allows. The designer read the first films of all
fourteen against these storyboards and asked for about 50 changes; most had one of three causes. Pieces thrown `along` or
`back` under a reach of about 300, or flowing under a reach of about 120, never left the struck body. Dots under a
strength of 0.4 piled into one soft cloud. Bone pieces cannot be seen on a figure its hit flash fills in bone, at 840
and 900 ms. The second films settled the quill's leave (#909) and left 22 changes, all strengths, reaches and
sentences; they were made and the spells filmed a third time. The session looked at those last films; the designer
did not read them. The coded rows below are the stagings as built. What is still open, for the kit or the scene,
each its own change:

- Built since (#933): a beat on a caster goes with its body while it crouches or lunges: Blood Hunt's ring opens
  under the crouched feet, Blood Price's red light sits on the crouched back, and Pursuit's streak leaves the body
  where it stands when the streak starts.
- Built since (#942): a gather closes on its figure's outline, at 0.6 of its radius, not on its centre: Latch's
  hooks and Bite's swallow stop at the body's edge.
- Built since (#942): under a strength of 0.2 a piece shrinks to a fifth of its base, and a burst aimed `down`
  drops its pieces off the figure. Toxic Mend's and Contagion's flakes of hide (the owner's word, 2026-11-19:
  fewer, smaller, darker, in `hide`) now fall in a fan of 120° under the body instead of being thrown out.
- Built since (#942): the motif is drawn under the particles on its figure, so beads and motes on a body are no
  longer hidden by the strokes.
- On the effects bench every blow is struck up the field, so `along` and `back` are shortened to 0.42 of their
  reach, as in the physical family: the sheets show only that case.

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
| 840 | 280 | impact | the effect | The venom flicked off the claw: 8 small rounded drops (*house*) in the family's green thrown on beyond b1 in a narrow 40° fan along the blow, past its outline. `burst at 840, count 8, shape dot, tint family, direction along, spread 70, reach 380, strength 0.15, lasts 320, blend normal` | [Burst](../battle-craft.md#burst), [An impact that keeps the direction](../battle-craft.md#an-impact-that-keeps-the-direction) |
| 840 | 840 | impact | the caster and the target | The light blow's hit stop, b1's bone flash, its recoil, a1's return, home by 1680 ms ([movement.md](movement.md)). | [Hit stop](../battle-craft.md#hit-stop), [Recoil](../battle-craft.md#recoil) |
| 920 | 640 | impact | the effect | The scratch weeping, the signature's first half: 8 deep red beads (*house*, the deep red 0x9a3b34) well up over b1's body, a few at a time, drift a little off it and shrink, gone by 1560 ms. They stay on b1: the blood is not thrown. `flow at 920, count 8, shape dot, tint blood, direction out, spread 360, reach 180, strength 0.8, lasts 640, blend normal` | [Flow](../battle-craft.md#flow), [One effect, one meaning](../battle-craft.md#one-effect-one-meaning) |
| 960 | 600 | impact | the effect | The venom working, the signature's second half: 6 green motes (*house*) alive at once rise slowly off b1 in a thin column and fade, gone by 1560 ms. `flow at 960, count 6, shape dot, tint family, direction up, spread 30, reach 220, strength 0.35, lasts 600, blend screen` | [Flow](../battle-craft.md#flow), [The three parts of an effect](../battle-craft.md#the-three-parts-of-an-effect) |
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
  what the spell does: blood here says "bleeds", as a hit flash says "lost health". Settled by the owner, 2026-11-19: kept.
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
| 240 | 120 | windup | the caster | The toss as the quills leave: a1 goes 6 px past home toward its targets with a stretch of 4%, then eases back home between 360 and 840 ms ([movement.md](movement.md)). | [Contact and follow-through](../battle-craft.md#contact-and-follow-through), [Squash and stretch](../battle-craft.md#squash-and-stretch) |
| 240 | 600 | windup | the effect | The flight, drawn by the scene (`FLIGHT`, `flightAt`), not a staged beat: one quill for each target appears just over a1's head line, 0.3 of its radius above it (*house*), clear of the spines. It flies for 600 ms at an even pace, no ease in, on a low arc whose one control point stands above the middle by 16% of the flight's length (*house*), turned along its path. It lands at 840 ms. No trail: Stage I flies it bare. | [Arcing travel](../battle-craft.md#arcing-travel) |
| 840 | 600 | impact | the effect | The `quill` motif on b1 and on b3: its strokes along each quill's flight, on their dark edge, gone by 1440 ms. `motif at 840, lasts 600` | [An impact that keeps the direction](../battle-craft.md#an-impact-that-keeps-the-direction), [Shape before colour](../battle-craft.md#shape-before-colour) |
| 840 | 160 | impact | the effect | The tip's bite, the brightest and shortest part: 6 small bone sparks (*house*) out on every side of the point of entry, short. `burst at 840, count 6, shape spark, tint bone, direction out, spread 360, reach 120, strength 0.3, lasts 160, blend normal` | [Burst](../battle-craft.md#burst), [The three parts of an effect](../battle-craft.md#the-three-parts-of-an-effect) |
| 840 | 240 | impact | the effect | Through, the signature: 10 bone splinters (*house*) driven on out of the far side of each target in a narrow 20° needle along the quill's flight, past its outline. Pummel's fan is 50°. `burst at 840, count 10, shape shard, tint bone, direction along, spread 20, reach 320, strength 0.4, lasts 240, blend normal` | [Burst](../battle-craft.md#burst), [An impact that keeps the direction](../battle-craft.md#an-impact-that-keeps-the-direction) |
| 880 | 280 | impact | the effect | The entry: 6 grains of bone (*house*) spattered back from each target toward a1, short, none past the gap. Bone, not green: Quill is not venom. `burst at 880, count 6, shape dot, tint bone, direction back, spread 90, reach 300, strength 0.15, lasts 220, blend normal` | [Burst](../battle-craft.md#burst), [One effect, one meaning](../battle-craft.md#one-effect-one-meaning) |
| 840 | 590 | impact | the target | Each struck figure's light hit from a distance ([movement.md](movement.md)): its bone flash for 80 ms, a stop of 30 ms, a recoil of 12 px away from a1, home by about 1430 ms. The flash and the stop start on the contact frame; each figure's recoil follows its own loss. | [Hit flash](../battle-craft.md#hit-flash), [Recoil](../battle-craft.md#recoil) |
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
  at 240 ms and flies `FLIGHT`. The 0.3 radius over the head line and the 16% lift are *house*: the film tunes them.
- No trail behind the quill: the ladder test counts every beat that starts before 840 ms, and Stage I allows none.
  The trail on a projectile is kept for Paralyzing Barb at Stage III, where a windup beat is asked for.
- One quill a target, as the host confirms one hit each. The approved execution in `docs/art/creature-art-direction.md` says "quills": a volley of three
  per target would read as three hits. Settled by the owner, 2026-11-19: one quill.
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
| 360 | 480 | windup | the effect | The scent leaving the glands on a1's back, the one windup beat: 10 rounded green drops (*house*) rise off a1 in a loose column, a few at a time, gone by 840 ms. Drops, because a gland makes them. `flow at 360, on caster, count 10, shape dot, tint family, direction up, spread 60, reach 200, strength 0.7, lasts 480, blend screen, level head` | [Flow](../battle-craft.md#flow), [Anticipation](../battle-craft.md#anticipation) |
| 600 | 240 | windup | the caster | The rise: a1 stretches 4% as it lets the scent go ([movement.md](movement.md)). | [Squash and stretch](../battle-craft.md#squash-and-stretch) |
| 840 | 360 | impact | the caster | The release, as a cast lets go ([movement.md](movement.md)). Nobody lost health: no flash on any figure. | [Contact and follow-through](../battle-craft.md#contact-and-follow-through) |
| 840 | 900 | impact | the effect | The `spores` motif on each ally, gone by 1740 ms. `motif at 840, lasts 900` | [Shape before colour](../battle-craft.md#shape-before-colour), [The three parts of an effect](../battle-craft.md#the-three-parts-of-an-effect) |
| 840 | 400 | impact | the effect | The one light: a thin green ring opens fast on the ground at each ally's feet and fades by 1240 ms. Aimed at nobody, it opens all round. `glow at 840, shape ring, tint family, reach 200, strength 0.6, lasts 400, blend add` | [Glow](../battle-craft.md#glow), [A beat on the ground](../battle-craft.md#a-beat-on-the-ground) |
| 840 | 300 | impact | the effect | The first jolt, the signature's first half: 14 green sparks (*house*) shoot straight up off each ally in a narrow 40° fan, past its head. `burst at 840, count 14, shape spark, tint family, direction up, spread 40, reach 300, strength 0.5, lasts 300, blend add` | [Burst](../battle-craft.md#burst), [Additive light](../battle-craft.md#additive-light) |
| 1100 | 300 | impact | the effect | The second jolt, a heartbeat later (*house*, 260 ms), the signature's second half: 10 sparks, narrower and higher. `burst at 1100, count 10, shape spark, tint family, direction up, spread 30, reach 400, strength 0.6, lasts 300, blend add` | [Burst](../battle-craft.md#burst), [Stagger and overlap](../battle-craft.md#stagger-and-overlap) |
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
  ring left at 1320 ms (a3's starts 80 ms after a1's).
- 1320 to 1380 ms: a few small sparks rising over each ally; the motif fading.
- 1440 ms (whole): a few sparks over a3, none over a1 or a2. 1920 ms (whole) and every frame after: no effect object on the field.
- b1, b2 and b3 are not lit, moved or marked on any frame. Nothing in the effect is gold.
- Set beside Toxic Mend's sheet at 960 ms: Overdrive's are sparks pointing up over the heads; Toxic Mend's are round
  drops closing on the bodies, nothing over the heads.

The sheets cannot show the feel of the two jolts at full speed, nor the sound.

### Left

- Green sparks rising mean energy gained, here and on Pursuit's caster: a reading the family did not have. Chosen
  because a spark is quick and pointed where a drop is round and slow, so it is not read as venom. Settled by the owner,
  2026-11-19: kept.
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
| 480 | 360 | windup | the effect | The glands filling, the one windup beat: 12 round green drops (*house*) on a ring well outside a1 close in on its body by 840 ms. `gather at 480, on caster, count 12, shape dot, tint family, reach 320, strength 0.7, lasts 360, blend normal` | [Gravity well and convergence](../battle-craft.md#gravity-well-and-convergence), [Anticipation](../battle-craft.md#anticipation) |
| 600 | 240 | windup | the caster | The rise: a1 stretches 4% ([movement.md](movement.md)). | [Squash and stretch](../battle-craft.md#squash-and-stretch) |
| 840 | 360 | impact | the caster | The release, as a cast lets go ([movement.md](movement.md)). Nobody lost health: no flash on any figure. | [Contact and follow-through](../battle-craft.md#contact-and-follow-through) |
| 1160 | 580 | impact | the effect | The `spores` motif on each ally, once the drops have soaked in, so the soak and the motif are not one cloud; gone by 1740 ms. `motif at 1160, lasts 580` | [Shape before colour](../battle-craft.md#shape-before-colour), [The three parts of an effect](../battle-craft.md#the-three-parts-of-an-effect) |
| 840 | 320 | impact | the effect | The secretion landing, the signature's first half: 12 round green drops (*house*) on a ring around each ally close on its centre by 1160 ms. Drops, opaque: what a gland makes. `gather at 840, on target, count 12, shape dot, tint family, reach 200, strength 0.8, lasts 320, blend normal` | [Gravity well and convergence](../battle-craft.md#gravity-well-and-convergence), [One effect, one meaning](../battle-craft.md#one-effect-one-meaning) |
| 1160 | 420 | impact | the effect | The mend, the one light: a soft green glow on each body, close to it, as the drops soak in, gone by 1580 ms. Not a ring: nothing struck the ground. `glow at 1160, tint family, reach 170, strength 0.55, lasts 420, blend screen` | [Glow](../battle-craft.md#glow), [Additive light](../battle-craft.md#additive-light) |
| 1240 | 400 | impact | the effect | The price, the signature's second half: 6 small dark flakes (*house*), the ally's own hide loosened, bone in shadow, dropping off its body and falling in a fan under it, short. Fewer, smaller and darker on the owner's word (2026-11-19), so they are not read as a hit; falling since #942, where they were thrown out all round. `burst at 1240, count 6, shape shard, tint hide, direction down, spread 120, reach 140, strength 0.05, lasts 400, blend normal` | [Burst](../battle-craft.md#burst), [One effect, one meaning](../battle-craft.md#one-effect-one-meaning) |
| 1240 | 600 | impact | the effect | What clings for the round: 6 green beads alive at once on or just over each body (a reach of 60: at 120 they stood off it like Contagion's cloud), drift a little and shrink, gone by 1840 ms. `flow at 1240, count 6, shape dot, tint family, direction out, spread 360, reach 60, strength 0.5, lasts 600, blend normal` | [Flow](../battle-craft.md#flow), [The three parts of an effect](../battle-craft.md#the-three-parts-of-an-effect) |
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
- 1200 to 1380 ms: the drops landed; a soft green light coming up on each body, close to it.
- 1320 to 1380 ms: a few small dark flakes falling below each outline, none bright, no piece that reads as a
  splinter of a hit; a few green beads on each body.
- 1440 ms (whole): the light on each body, fading; a flake or two below each figure's middle; beads on each body.
- 1680 ms (whole): a bead or two at most. 1920 ms (whole) and every frame after: no effect object on the field.
- b1, b2 and b3 are not lit, moved or marked on any frame. Nothing in the effect is gold.
- Set beside Overdrive's sheet at 1200 ms: Toxic Mend has a light on the bodies and nothing above them; Overdrive has
  sparks above the heads and no light on the bodies.

The sheets cannot show the feel at full speed, nor the sound.

### Left

- The lowered defense is told by flakes of the ally's own hide falling off: bone in this family is a creature's hard
  parts, here loosened. The owner read the first flakes as a hit (2026-11-19): they are now six, small,
  in `hide`, kept as the reading.
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
| 840 | 360 | impact | the effect | The burst, the signature's first half: 14 bone splinters (*house*) thrown out on every side of b1, past its outline. Bone, not blood: Ambush leaves no Bleed. `burst at 840, count 14, shape shard, tint bone, direction out, spread 360, reach 300, strength 0.5, lasts 360, blend normal` | [Burst](../battle-craft.md#burst), [The three parts of an effect](../battle-craft.md#the-three-parts-of-an-effect) |
| 840 | 260 | impact | the effect | The pounce carried through: 12 bright bone sparks (*house*) on along the blow, beyond b1, in a 60° fan. `burst at 840, count 12, shape spark, tint bone, direction along, spread 60, reach 360, strength 0.5, lasts 260, blend add` | [Burst](../battle-craft.md#burst), [An impact that keeps the direction](../battle-craft.md#an-impact-that-keeps-the-direction) |
| 840 | 240 | impact | the camera | A staged shake on the body's tremor, decaying over about 240 ms (*house*). `shake at 840, strength 0.12` | [Shake from trauma](../battle-craft.md#shake-from-trauma) |
| 840 | 900 | impact | the caster and the target | The heavy blow's hit stop (120 ms), b1's bone flash, its recoil of 26 px, a1's return, home by 1740 ms (all but home by about 1380 ms on the film) ([movement.md](movement.md)). | [Hit stop](../battle-craft.md#hit-stop), [Recoil](../battle-craft.md#recoil) |
| 1440 | 480 | impact | the effect | The weight, the signature's second half: 10 pale grains of grit (*house*) on a flat ring on the ground around a1's feet close slowly on them as a1 lands home, gone by 1920 ms. Played at a1's home: a1 is there, or all but, by 1440 ms. `gather at 1440, on caster, count 10, shape dot, tint bone, reach 200, strength 1, lasts 480, blend normal, level feet` | [Gravity well and convergence](../battle-craft.md#gravity-well-and-convergence), [A beat on the ground](../battle-craft.md#a-beat-on-the-ground) |
| 1920 | 620 | impact | the field | Nothing new. Both figures home, breathing. The result is read here. | [The hold before the number](../battle-craft.md#the-hold-before-the-number), [Breath at rest](../battle-craft.md#breath-at-rest) |

Six staged beats, none in the windup; 26 particles asked for on b1 (14, 12) and 10 on a1; one glow, a shake of 0.12,
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
- 1020 ms: the light fading; 1080 ms: the light gone; a few splinters, far from b1; the slash strokes diagonal on their dark edge.
- 1200 ms: no splinter or spark left; the strokes on b1.
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
- Ten damage and no blood: blood here says "bleeds" or "drunk". Settled by the owner, 2026-11-19: blood for a
  Bleed or a drink only.
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
| 540 | 300 | windup | the effect | The streak, the one windup beat and the signature: a head runs from a1's place to b1, easing in as the body does, dropping 20 green sparks (*house*) that fade behind it; it reaches b1 at 840 ms. Drawn from where a1 stood, by design: it is the path, not the body. `trail at 540, count 20, shape spark, tint family, strength 1, lasts 300, blend add` | [Trail](../battle-craft.md#trail), [Contact and follow-through](../battle-craft.md#contact-and-follow-through) |
| 840 | 600 | impact | the effect | The `slash` motif on b1, quick, gone by 1440 ms. `motif at 840, lasts 600` | [An impact that keeps the direction](../battle-craft.md#an-impact-that-keeps-the-direction), [Shape before colour](../battle-craft.md#shape-before-colour) |
| 840 | 220 | impact | the effect | The run carried on: 8 green sparks (*house*) go on beyond b1 along the blow in a narrow 30° line, the streak's end. `burst at 840, count 8, shape spark, tint family, direction along, spread 30, reach 300, strength 0.4, lasts 220, blend add` | [Burst](../battle-craft.md#burst), [An impact that keeps the direction](../battle-craft.md#an-impact-that-keeps-the-direction) |
| 840 | 840 | impact | the caster and the target | The light blow's hit stop (60 ms), b1's bone flash, its recoil of 12 px, a1's return, home by 1680 ms ([movement.md](movement.md)). | [Hit stop](../battle-craft.md#hit-stop), [Recoil](../battle-craft.md#recoil) |
| 860 | 200 | impact | the effect | The scratch: 6 small bone chips (*house*) off b1's hide, short, all round. `burst at 860, count 6, shape shard, tint bone, direction out, spread 360, reach 120, strength 0.3, lasts 200, blend normal` | [Burst](../battle-craft.md#burst), [The three parts of an effect](../battle-craft.md#the-three-parts-of-an-effect) |
| 1440 | 260 | impact | the effect | The one light, on a1 as it lands home: the energy taken in. Played at a1's home. `glow at 1440, on caster, tint family, reach 150, strength 0.5, lasts 260, blend add` | [Glow](../battle-craft.md#glow), [A light that answers a cast](../battle-craft.md#a-light-that-answers-a-cast) |
| 1440 | 300 | impact | the effect | Keener: 8 green sparks (*house*) jump straight up off a1, Overdrive's jolt on one figure: energy gained. `burst at 1440, on caster, count 8, shape spark, tint family, direction up, spread 50, reach 220, strength 0.4, lasts 300, blend add` | [Burst](../battle-craft.md#burst), [One effect, one meaning](../battle-craft.md#one-effect-one-meaning) |
| 1740 | 800 | impact | the field | Nothing new. Both figures home, breathing. The result is read here. | [The hold before the number](../battle-craft.md#the-hold-before-the-number), [Breath at rest](../battle-craft.md#breath-at-rest) |

Six staged beats, one in the windup (the trail); 34 particles asked for on b1 (20, 8, 6) and 8 on a1; one glow, on
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
- 1440 ms (whole): a1 home or all but, nothing on it yet. 1680 ms (whole): a soft green light on a1, green sparks
  over its back pointing up.
- 1920 ms (whole) and every frame after: no effect object on the field. Nothing in the effect is gold or red.
- Set beside Ambush's sheet at 960 ms: Pursuit's debris is a thin line and b1 is not lit; Ambush's lies all round b1
  under a light.

The sheets cannot show the stop or the feel of the lunge at full speed.

### Left

- The streak is a trail from a1's home to b1's centre, timed by eye to stay behind the lunging body: the two are not
  tied. If the film shows the head ahead of a1, the trail starts later; a trail that follows the body is a change to
  the kit.
- Green sparks mean energy gained, as on Overdrive. Settled by the owner, 2026-11-19: kept.
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
| 840 | 300 | impact | the effect | The clamp, the signature's first half: 6 bone hooks (*house*, one a leg) on a ring well outside b1 close on its body by 1140 ms, the last of them seen once b1's bone fill is gone. `gather at 840, on target, count 6, shape shard, tint bone, reach 240, strength 1, lasts 300, blend normal` | [Gravity well and convergence](../battle-craft.md#gravity-well-and-convergence), [Shape before colour](../battle-craft.md#shape-before-colour) |
| 840 | 840 | impact | the caster and the target | The light blow's hit stop (60 ms), b1's bone flash, its recoil of 12 px, a1's return, home by 1680 ms ([movement.md](movement.md)). | [Hit stop](../battle-craft.md#hit-stop), [Recoil](../battle-craft.md#recoil) |
| 1000 | 900 | impact | the effect | The Bleed: 10 deep red beads (*house*) well up over b1's body, a few at a time, drift a little and shrink, gone by 1900 ms; longer than Venom Claw's 640 ms, for three rounds against one. `flow at 1000, count 10, shape dot, tint blood, direction out, spread 360, reach 130, strength 0.5, lasts 900, blend normal` | [Flow](../battle-craft.md#flow), [One effect, one meaning](../battle-craft.md#one-effect-one-meaning) |
| 1140 | 240 | impact | the effect | The punctures, as the hooks land: 6 small drops of blood (*house*) spurt just past b1's outline, all round. `burst at 1140, count 6, shape dot, tint blood, direction out, spread 360, reach 260, strength 0.5, lasts 240, blend normal` | [Burst](../battle-craft.md#burst), [Stagger and overlap](../battle-craft.md#stagger-and-overlap) |
| 1040 | 560 | impact | the effect | The feed: 8 deep red motes (*house*) alive at once drawn out of b1 toward a1 in a narrow stream, gone by 1600 ms. It does not arrive: the arrival is Bite's. `flow at 1040, count 8, shape dot, tint blood, direction back, spread 30, reach 400, strength 0.5, lasts 560, blend normal` | [Flow](../battle-craft.md#flow), [Gravity well and convergence](../battle-craft.md#gravity-well-and-convergence) |
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
- 1440 ms (whole): a1 home or all but, no light yet; beads still on b1.
- 1680 ms (whole): a bead or two on b1; a faint green light on a1. 1920 ms (whole): a last bead at most.
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
| 840 | 300 | impact | the effect | The snap, the brightest part, lit so it is read once b1's bone fill is gone: 10 small bone sparks (*house*), the teeth meeting, out all round, short. `burst at 840, count 10, shape spark, tint bone, direction out, spread 360, reach 200, strength 0.5, lasts 300, blend add` | [Burst](../battle-craft.md#burst), [The three parts of an effect](../battle-craft.md#the-three-parts-of-an-effect) |
| 840 | 240 | impact | the camera | A light staged shake on the body's tremor, a bite and not a blast, decaying over about 240 ms (*house*). `shake at 840, strength 0.08` | [Shake from trauma](../battle-craft.md#shake-from-trauma) |
| 840 | 870 | impact | the caster and the target | The medium blow's hit stop (90 ms), b1's bone flash, its recoil of 18 px, a1's return, home by 1710 ms (all but home by about 1380 ms on the film) ([movement.md](movement.md)). | [Hit stop](../battle-craft.md#hit-stop), [Recoil](../battle-craft.md#recoil) |
| 860 | 320 | impact | the effect | The mouthful, the signature's first half: 12 deep red drops (*house*) torn back from b1 toward a1 in a 40° fan, none beyond b1. `burst at 860, count 12, shape dot, tint blood, direction back, spread 40, reach 280, strength 0.6, lasts 320, blend normal` | [Burst](../battle-craft.md#burst), [Gravity well and convergence](../battle-craft.md#gravity-well-and-convergence) |
| 1200 | 320 | impact | the effect | Swallowed, the signature's second half: 12 deep red drops (*house*) on a ring around a1's home land on it by 1520 ms, as a1 reaches home. `gather at 1200, on caster, count 12, shape dot, tint blood, reach 240, strength 1, lasts 320, blend normal` | [Gravity well and convergence](../battle-craft.md#gravity-well-and-convergence), [One effect, one meaning](../battle-craft.md#one-effect-one-meaning) |
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
  all round the point of contact, seen at 960 and 1020 ms, gone by 1140 ms.
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
  bleeds. Bite leaves no Bleed. Settled by the owner, 2026-11-19: blood for a Bleed
  or a drink, kept.
- Bite has Seize's shape (taken from b1, landed on a1, one light on a1) in the other family's colours. The red is what
  tells them apart. Settled by the owner, 2026-11-19: kept.
- The gather is placed at a1's home: a1 is all but home at 1440 ms on the sheets, so the drops land on its body.
- A critical hit (45%) is the body's punch of the zoom; the staging cannot tell it.
- In a match the host may list a1 among the recipients of its heal; the scene then draws its own mark on a1, which
  goes with its body. The bench's film, on `--targets b1`, does not show it beside the drops.

## Contagion (`spell:infectious_blast:v1`)

### Intent

The player reads "the Blightweaver vented a spore cloud over them, and it eats their hide for good": a cloud that
lands on up to three, hangs, and leaves them weaker. Contagion costs 1 energy; up to three enemies take 2 damage and
lose 3 defense for the rest of the game. Stage III, a few times a game, so it earns a windup on the caster's
reservoirs and a light that lingers, and no shake: two damage is a light blow. Its signature is the cloud that hangs:
each target sits in a green haze of billowing drops for more than a second while flakes of its hide keep crumbling
off after the haze is gone. Toxic Mend's drops close in and soak, once; Contagion's cloud swells out and stays.

### Today

Filmed at `28f2dc2`, 390 by 844, 1×, a1 on b1, b2 and b3, 2 health lost each: a whole action and a contact.

- 0 to 60 ms: the shared faint green arc on a1's body. a1 barely moves through the windup.
- 840 and 900 ms: b1, b2 and b3 filled flat in bone.
- 960 to 1380 ms: pale green spore rings, open circles, drift over each of the three, low in contrast on their hides;
  faint at 1440 ms (whole), gone by 1680 ms. No particle, no light: Overdrive's sheet on enemies. Nothing says a cloud,
  nothing says eaten.

### Beats

| At | Lasts | Window | Layer | Beat | Pattern |
| --- | --- | --- | --- | --- | --- |
| 0 | 840 | windup | the caster | A cast's load where it stands, then the rise ([movement.md](movement.md), `CAST`): a1 squats 4% to 600 ms, then stretches 4%. No shared arc: the staging's windup beats replace it. | [Anticipation](../battle-craft.md#anticipation), [Squash and stretch](../battle-craft.md#squash-and-stretch) |
| 120 | 400 | windup | the effect | The reservoirs fill: 12 round green drops (*house*) on a ring around a1 close on its back by 520 ms. `gather at 120, on caster, count 12, shape dot, tint family, reach 300, strength 0.6, lasts 400, blend screen` | [Gravity well and convergence](../battle-craft.md#gravity-well-and-convergence), [Anticipation](../battle-craft.md#anticipation) |
| 360 | 300 | windup | the effect | The reservoirs light, the windup's light: a soft green glow on a1, gone by 660 ms. `glow at 360, on caster, tint family, reach 170, strength 0.5, lasts 300, blend screen` | [Glow](../battle-craft.md#glow), [Anticipation](../battle-craft.md#anticipation) |
| 620 | 220 | windup | the effect | The vents open: 16 green drops (*house*) blown up off a1's back in a 70° plume, gone by 840 ms. Overdrive's drops rise a few at a time; these leave at once. `burst at 620, on caster, count 16, shape dot, tint family, direction up, spread 70, reach 260, strength 0.5, lasts 220, blend screen` | [Burst](../battle-craft.md#burst), [Contact and follow-through](../battle-craft.md#contact-and-follow-through) |
| 840 | 360 | impact | the caster | The release, as a cast lets go ([movement.md](movement.md)). Nothing more on a1. | [Contact and follow-through](../battle-craft.md#contact-and-follow-through) |
| 840 | 900 | impact | the effect | The `spores` motif on each target, gone by 1740 ms. `motif at 840, lasts 900` | [Shape before colour](../battle-craft.md#shape-before-colour), [The three parts of an effect](../battle-craft.md#the-three-parts-of-an-effect) |
| 840 | 590 | impact | the target | Each struck figure's light hit from a distance ([movement.md](movement.md)): its bone flash for 80 ms, a stop of 30 ms, a recoil of 12 px away from a1, home by about 1430 ms. All three on the contact frame. | [Hit flash](../battle-craft.md#hit-flash), [Recoil](../battle-craft.md#recoil) |
| 840 | 360 | impact | the effect | The cloud lands: 14 green drops (*house*) puff out on every side of each target, past its outline. `burst at 840, count 14, shape dot, tint family, direction out, spread 360, reach 200, strength 0.5, lasts 360, blend screen` | [Burst](../battle-craft.md#burst), [Stagger and overlap](../battle-craft.md#stagger-and-overlap) |
| 840 | 900 | impact | the effect | The haze, the impact's light and the signature's first half: a wide, faint green light hangs on each target and fades slowly, gone by 1740 ms. Faint, so three of them do not light the field. `glow at 840, tint family, reach 260, strength 0.35, lasts 900, blend screen` | [Glow](../battle-craft.md#glow), [Additive light](../battle-craft.md#additive-light) |
| 900 | 1100 | impact | the effect | The cloud that hangs, the signature's second half: 14 large green drops (*house*) alive at once billow slowly off each target on every side, gone by 2000 ms. `flow at 900, count 14, shape dot, tint family, direction out, spread 360, reach 120, strength 0.7, lasts 1100, blend screen` | [Flow](../battle-craft.md#flow), [The three parts of an effect](../battle-craft.md#the-three-parts-of-an-effect) |
| 1200 | 500 | impact | the effect | The hide eaten: 5 small dark flakes (*house*) dropping off each target and falling under it, short (#942). Toxic Mend's reading: defense lowered. `burst at 1200, count 5, shape shard, tint hide, direction down, spread 120, reach 220, strength 0.05, lasts 500, blend normal` | [Burst](../battle-craft.md#burst), [One effect, one meaning](../battle-craft.md#one-effect-one-meaning) |
| 1300 | 800 | impact | the effect | For good: 4 flakes alive at once keep crumbling off each target, gone by 2100 ms, after the cloud. Toxic Mend sheds once; Contagion goes on. `flow at 1300, count 4, shape shard, tint hide, direction out, spread 360, reach 70, strength 0.1, lasts 800, blend normal` | [Flow](../battle-craft.md#flow), [One effect, one meaning](../battle-craft.md#one-effect-one-meaning) |
| 2100 | 440 | impact | the field | Nothing new. Every figure home, breathing. The result is read here; the hold from 2400 ms is empty. | [The hold before the number](../battle-craft.md#the-hold-before-the-number), [Breath at rest](../battle-craft.md#breath-at-rest) |

Nine staged beats, three in the windup; 42 particles asked for on each target (14, 14, 8, 6) and 28 on a1, all of
a1's gone by 840 ms; 84 on the field at most (at 1300 ms, 14, 8 and 6 on each of three); two glows, no pulse, no
shake, the last gone by 2100 ms: inside Stage III. One flash a figure, the body's, on b1, b2 and b3; two changes of
light, one in the windup on a1, one at contact on the three.

### Reduced motion

Nothing. The frame after the action is shown at once with the result text: no load, no plume, no cloud, no flake.
The lowered defense is the status chip's to say.

### Cost

- In the windup: two emitters on a1 (the gather, then the plume) and one glow image, all gone by 840 ms.
- Objects at the peak (1300 to 1700 ms): on each target one `Graphics` for the motif, the haze's image until 1740 ms,
  and three emitters (the cloud, the flakes, the crumbling); about twelve draw calls for 400 ms, the screen objects
  batched after the normal ones.
- Particles at the peak: 84 at 1300 ms; of the field's 150.
- Bytes: not measured. Built by BC4 (motif, burst, flow, gather, glow, the `staging` field) and BC5 (the cast's
  body); BC7 places the beats. The cancel path is the kit's: every emitter and image dies with the action.

### On the film strip

```bash
node scripts/battle/filmstrip.mjs spell:infectious_blast:v1 --actor a1 --targets b1,b2,b3 --damage 2 --out <folder>/contagion.png
node scripts/battle/filmstrip.mjs spell:infectious_blast:v1 --actor a1 --targets b1,b2,b3 --damage 2 \
  --frames 24 --interval 60 --columns 6 --out <folder>/contagion-contact.png
```

- 0 and 60 ms (contact): no arc or mark on a1.
- 180 to 420 ms: round green drops on a ring around a1, well outside its outline at 180 ms, on its back by 480 ms;
  none near b1, b2 or b3.
- 420 to 600 ms: a soft green light on a1.
- 720 and 780 ms: a plume of green drops above a1's back, rising, none sideways onto a2; none left at 840 ms.
- 840 and 900 ms: b1, b2 and b3 filled flat in bone, in these two frames and no other. At 960 ms (at
  900 ms they are lost on the bone fill) green drops on every side of each, past its outline, and a wide faint green
  light on each.
- 960 to 1140 ms: the spore motif on each; larger, slow green drops around each body, a cloud and not a line; the light
  still on each, fainter.
- 1260 to 1380 ms: a few small dark flakes falling below each outline, none bright; the drops still around
  each body.
- 1440 ms (whole): drops and a few flakes around each target; the light faint. 1680 ms (whole): the cloud thinning, a
  flake or two below each figure's middle. 1920 ms (whole): a last drop or flake at most.
- 2160 ms (whole) and every frame after: no effect object on the field. a2 and a3 are not lit, moved or marked on any
  frame. Nothing in the effect is gold, and none of it is red.
- Set beside Toxic Mend's sheet at 1200 ms: Contagion's drops stand off the bodies in a cloud; Toxic Mend's have
  landed and its light is close on the body.

The sheets cannot show the feel of the cloud at full speed, nor the sound.

### Left

- The permanence is told by flakes that keep falling after the cloud has gone, 400 ms of it: the "for the rest of
  the game" is the status chip's. The crumbling is now four small `hide` flakes, on the owner's word
  (2026-11-19).
- Three hazes at 0.45 on a screen blend may still read as the field lit: the film tunes the strength. One wider haze
  over the three is a beat on the ground, not a kit word.
- The cloud's drops are the same green drops as the gland's: a spore is what a gland makes. If the film reads them as
  Toxic Mend's, the cloud's drops go larger (strength) before they change colour.

## Paralyzing Barb (`spell:tranquilizer_dart:v1`)

### Intent

The player reads "the Blightweaver shot a poisoned barb into that one: it is pinned, and it bleeds": a dart with a
wake, then a target held to its spot. Paralyzing Barb costs 2 energy; one enemy takes 2 damage, is stunned for a round
and bleeds 1 for 2 rounds. Stage III, a few times a game, but a light blow: no shake, the spectacle in the flight and
in what stays. Its signature is the pin: after the barb lands, a tight green ring holds at b1's feet for a second,
opening barely past its outline, under a thin rising fume and a few beads of blood. Quill flies bare and goes through;
the barb drips venom in flight and stays in.

### Today

Filmed at `28f2dc2`, 390 by 844, 1×, a1 on b1, 2 health lost: a whole action and a contact.

- 0 to 600 ms (contact): one pale quill stands upright on a1's body, as Quill's did before #909. a1 barely moves.
- 660 ms: the quill just above a1's back; 720 and 780 ms: in the gap, pointing up the field toward b1.
- 840 and 900 ms: b1 filled flat in bone, the motif's strokes already on it.
- 960 to 1380 ms: three upright pale green strokes on b1, fading; faint at 1440 ms (whole), gone by 1680 ms. No
  trail, no particle: Quill's sheet with one target. Nothing says venom, a stun or a bleed.

### Beats

| At | Lasts | Window | Layer | Beat | Pattern |
| --- | --- | --- | --- | --- | --- |
| 0 | 240 | windup | the caster | The load ([movement.md](movement.md), `THROW`): a1 draws 8 px away from b1 with a squash of 4%. | [Anticipation](../battle-craft.md#anticipation) |
| 0 | 240 | windup | the effect | The barb loaded: 10 round green drops (*house*) on a ring around a1 close on its back by 240 ms, where the barb leaves. `gather at 0, on caster, count 10, shape dot, tint family, reach 300, strength 0.6, lasts 240, blend screen` | [Gravity well and convergence](../battle-craft.md#gravity-well-and-convergence), [Anticipation](../battle-craft.md#anticipation) |
| 120 | 300 | windup | the effect | The reservoirs light as the barb goes: a soft green glow on a1, gone by 420 ms. `glow at 120, on caster, tint family, reach 170, strength 0.45, lasts 300, blend screen` | [Glow](../battle-craft.md#glow), [Anticipation](../battle-craft.md#anticipation) |
| 240 | 120 | windup | the caster | The toss: a1 goes 6 px past home toward b1 with a stretch of 4%, then easing back home by about 780 ms ([movement.md](movement.md)). | [Contact and follow-through](../battle-craft.md#contact-and-follow-through), [Squash and stretch](../battle-craft.md#squash-and-stretch) |
| 240 | 600 | windup | the effect | The flight, drawn by the scene (`FLIGHT`, `flightAt`), not a staged beat: one bone barb appears on a1's back and flies for 600 ms at an even pace on Quill's low arc, landing at 840 ms. | [Arcing travel](../battle-craft.md#arcing-travel) |
| 240 | 600 | windup | the effect | The venom wake, the family's one projectile trail: 16 small green drops (*house*) left behind the barb on its arc, fading, the barb always ahead of them. `trail at 240, count 16, shape dot, tint family, strength 0.35, lasts 600, blend screen` | [Trail](../battle-craft.md#trail), [Arcing travel](../battle-craft.md#arcing-travel) |
| 840 | 700 | impact | the effect | The `quill` motif on b1, its strokes along the flight on their dark edge, gone by 1540 ms. `motif at 840, lasts 700` | [An impact that keeps the direction](../battle-craft.md#an-impact-that-keeps-the-direction), [Shape before colour](../battle-craft.md#shape-before-colour) |
| 840 | 590 | impact | the target | b1's light hit from a distance ([movement.md](movement.md)): its bone flash for 80 ms, a stop of 30 ms, a recoil of 12 px away from a1, home by about 1430 ms. | [Hit flash](../battle-craft.md#hit-flash), [Recoil](../battle-craft.md#recoil) |
| 840 | 260 | impact | the effect | The sting, the impact's brightest part: a hard green light on b1, gone by 1100 ms. `glow at 840, tint family, reach 200, strength 0.7, lasts 260, blend add` | [Glow](../battle-craft.md#glow), [Additive light](../battle-craft.md#additive-light) |
| 840 | 300 | impact | the effect | The venom driven in: 12 green drops (*house*) carried on through b1 along the flight in a narrow 40° fan. `burst at 840, count 12, shape dot, tint family, direction along, spread 40, reach 320, strength 0.55, lasts 300, blend normal` | [Burst](../battle-craft.md#burst), [An impact that keeps the direction](../battle-craft.md#an-impact-that-keeps-the-direction) |
| 860 | 260 | impact | the effect | The entry: 8 bone splinters (*house*) spattered back from b1 toward a1, short. `burst at 860, count 8, shape shard, tint bone, direction back, spread 60, reach 140, strength 0.3, lasts 260, blend normal` | [Burst](../battle-craft.md#burst), [Stagger and overlap](../battle-craft.md#stagger-and-overlap) |
| 960 | 1000 | impact | the ground | The pin, the signature: a tight green ring on the ground at b1's feet opens fast to just past its outline and holds there, fading slowly, gone by 1960 ms. Not a shock ring: it does not reach a neighbour. `glow at 960, shape ring, tint family, reach 130, strength 0.6, lasts 1000, blend screen` | [Glow](../battle-craft.md#glow), [A beat on the ground](../battle-craft.md#a-beat-on-the-ground) |
| 1000 | 800 | impact | the effect | The Bleed: 6 deep red beads (*house*) well up over b1, a few at a time, gone by 1800 ms. Fewer than Venom Claw's: one a round. `flow at 1000, count 6, shape dot, tint blood, direction out, spread 360, reach 50, strength 0.4, lasts 800, blend normal` | [Flow](../battle-craft.md#flow), [One effect, one meaning](../battle-craft.md#one-effect-one-meaning) |
| 1040 | 760 | impact | the effect | The venom working, Venom Claw's reading: 6 green motes alive at once rise slowly off b1 in a thin column, gone by 1800 ms. `flow at 1040, count 6, shape dot, tint family, direction up, spread 30, reach 100, strength 0.3, lasts 760, blend screen` | [Flow](../battle-craft.md#flow), [The three parts of an effect](../battle-craft.md#the-three-parts-of-an-effect) |
| 1960 | 580 | impact | the field | Nothing new. Both figures home, breathing. The result is read here. | [The hold before the number](../battle-craft.md#the-hold-before-the-number), [Breath at rest](../battle-craft.md#breath-at-rest) |

Ten staged beats, three in the windup (the gather, the glow, the trail); 48 particles asked for on b1 (16, 12, 8, 6,
6) and 10 on a1; three glows, no pulse, no shake, the last gone by 1960 ms: inside Stage III. The barb in flight is
the scene's and not counted. One flash, the body's, on b1; two changes of light, one on a1 as it throws, one on b1 at
contact. The ring at the feet is a mark on the ground, faint, not a light on a figure.

### Reduced motion

Nothing. No barb is launched; the frame after the action is shown at once with the result text: no load, no wake, no
ring, no bead. The stun and the Bleed are the status chips' to say.

### Cost

- In the windup: one emitter (the gather) and one glow image on a1, gone by 420 ms; the barb's `Graphics` and its
  counter tween (the scene's) and the trail's emitter from 240 ms, the trail's last drops gone about 220 ms after 840.
- Objects at the peak (860 to 1100 ms): one `Graphics` for the motif, the sting's image, three emitters (the venom,
  the splinters, the wake's last); about five draw calls. From 1040 ms the ring's image and two emitters.
- Particles at the peak: about 30 at 860 ms (12, 8 and the wake's last drops); 48 asked for on b1 in all; of the
  field's 150.
- Bytes: not measured. Built by BC4 (motif, burst, flow, gather, trail, glow, the `staging` field), BC5 (the throw,
  the hit from a distance), BC6 (the ring of `glow`) and BC7 (`FLIGHT`, `flightAt`, the trail on the flight's arc);
  BC7 places the beats. The cancel path is the kit's, and the scene's for the barb.

### On the film strip

```bash
node scripts/battle/filmstrip.mjs spell:tranquilizer_dart:v1 --actor a1 --targets b1 --damage 2 --out <folder>/paralyzing-barb.png
node scripts/battle/filmstrip.mjs spell:tranquilizer_dart:v1 --actor a1 --targets b1 --damage 2 \
  --frames 24 --interval 60 --columns 6 --out <folder>/paralyzing-barb-contact.png
```

- 0 to 180 ms (contact): no barb on a1's body or anywhere on the field (#909); green drops on a ring around a1,
  closing; a1 a few pixels below its home.
- 180 ms: a soft green light on a1. 240 ms: no drop left around a1; at most one barb just above a1's back.
- 300 to 780 ms: the barb a step further up the field on each frame, the steps about equal; small green drops behind
  it on its path, never ahead of its point, the oldest faded; at 540 ms about halfway. No light on a1 from 420 ms.
- 840 ms: no barb in the air; b1 filled flat in bone (840 and 900 ms only); a hard green light on b1.
- 960 ms (at 900 ms the bone fill hides them): green drops on b1's far side from a1, shortened over its head (`along` on a shot up the field); pale
  splinters on b1's near side, not past the gap.
- 960 to 1080 ms: the quill strokes upright on b1; a small green ring on the ground at b1's feet, about as wide as its
  ground ring, not wider; no light left on b1 at 1140 ms.
- 1140 to 1380 ms: the ring still at b1's feet, opening slowly to about b1's ground ring and never past it; a few deep red beads on b1's body, not pink;
  a thin column of green motes over b1's back.
- 1440 ms (whole): b1 home; the ring faint at its feet; a bead or a mote. 1680 ms (whole): the ring all but gone.
- 1920 ms (whole) and every frame after: no effect object on the field. b2 and b3 are not lit, moved or marked on any
  frame. Nothing in the effect is gold.
- Set beside Quill's sheet at 600 ms: the barb has green drops behind it, Quill's quills none; at 1200 ms Quill's b1 is
  clear, Barb's has a ring at its feet.

The sheets cannot show the stop, the feel of the flight at full speed, or the sound.

### Left

- The stun is a tight ring at the feet that holds, not a halo: the one halo in the family says "healed" (Devour), and
  a halo on the struck figure would read as a heal. Settled by the owner, 2026-11-19: the halo is
  for a heal only.
- The ring's reach of 130 (*house*) is chosen to stay at b1's own ring, so it reads as held rather than a shock. If
  the film reads it as a ripple, the reach comes down before the strength goes up.
- The wake is drops, so it reads as venom. Chosen over sparks: green sparks in this family are energy.
- The stun lasts a round and the Bleed two; the effect ends by 1960 ms. The rounds are the status chips'.

## Eviscerate (`spell:mortal_wound:v1`)

### Intent

The player reads "the Deathstalker hooked its talons into that one and tore it open, and the wound keeps pouring":
a deep rip, then the family's worst Bleed. Eviscerate costs 3 energy, deals 5 damage with a 45% critical chance and
leaves a Bleed of 4 for 2 rounds. Stage III, a few times a game: a talon seen bared before the blow, a staged shake,
and the longest blood of the family. Its signature is the pool: blood that pours on b1 for well over a second while a
dark red ring spreads on the ground under it. Venom Claw weeps a little and Latch spurts where the hooks land;
Eviscerate pours and pools.

### Today

Filmed at `28f2dc2`, 390 by 844, 1×, a1 on b1, 5 health lost: a whole action and a contact.

- 0 to 60 ms: the shared faint green arc on a1's body.
- 120 to 540 ms: a1 crouched below its ring, leaning away from b1; 600 to 660 ms: it leaves its ring; 720 and 780 ms:
  up the field at b1.
- 840 and 900 ms: b1 filled flat in bone; a1 covers b1's lower half, b1's head clear.
- 960 to 1380 ms: three pale green slash strokes diagonal on b1, fading; a1 returning, home or all but at 1440 ms
  (whole). No particle, no blood: Ambush's sheet with a smaller blow.

### Beats

| At | Lasts | Window | Layer | Beat | Pattern |
| --- | --- | --- | --- | --- | --- |
| 0 | 840 | windup | the caster | A medium blow's draw back, hold and strike by the 5 health b1 lost ([movement.md](movement.md)). No shared arc. | [Anticipation](../battle-craft.md#anticipation), [Contact and follow-through](../battle-craft.md#contact-and-follow-through) |
| 200 | 480 | windup | the effect | The talons bared: 12 bright bone sparks (*house*) on a ring around a1 drawn onto its body by 680 ms, before it leaves its ring. `gather at 200, on caster, count 12, shape spark, tint bone, reach 220, strength 0.45, lasts 480, blend add` | [Gravity well and convergence](../battle-craft.md#gravity-well-and-convergence), [Anticipation](../battle-craft.md#anticipation) |
| 360 | 360 | windup | the effect | The gleam: a pale bone light on a1, gone by 720 ms. `glow at 360, on caster, tint bone, reach 160, strength 0.4, lasts 360, blend screen` | [Glow](../battle-craft.md#glow), [Anticipation](../battle-craft.md#anticipation) |
| 840 | 1000 | impact | the effect | The `slash` motif on b1, cutting across the blow on its dark edge, held longer than any of the family's, gone by 1840 ms. `motif at 840, lasts 1000` | [An impact that keeps the direction](../battle-craft.md#an-impact-that-keeps-the-direction), [Shape before colour](../battle-craft.md#shape-before-colour) |
| 840 | 870 | impact | the caster and the target | The medium blow's hit stop (90 ms), b1's bone flash, its recoil of 18 px, a1's return, home by 1710 ms (all but home by about 1380 ms on the film) ([movement.md](movement.md)). | [Hit stop](../battle-craft.md#hit-stop), [Recoil](../battle-craft.md#recoil) |
| 840 | 240 | impact | the camera | A staged shake on the body's tremor, decaying over about 240 ms (*house*). `shake at 840, strength 0.2` | [Shake from trauma](../battle-craft.md#shake-from-trauma) |
| 840 | 280 | impact | the effect | The rip's light: a pale bone light on b1, gone by 1120 ms. `glow at 840, tint bone, reach 220, strength 0.6, lasts 280, blend screen` | [Glow](../battle-craft.md#glow), [Additive light](../battle-craft.md#additive-light) |
| 920 | 300 | impact | the effect | The hooks torn out, once b1's bone fill is gone: 14 bone shards (*house*) ripped back from b1 toward a1 in a 50° fan. Bone, the talons; not blood, which would say "drunk". `burst at 920, count 14, shape shard, tint bone, direction back, spread 50, reach 260, strength 0.5, lasts 300, blend normal` | [Burst](../battle-craft.md#burst), [An impact that keeps the direction](../battle-craft.md#an-impact-that-keeps-the-direction) |
| 860 | 360 | impact | the effect | The gash opens: 18 deep red drops (*house*) spurt on every side of b1, short, staying near it. `burst at 860, count 18, shape dot, tint blood, direction out, spread 360, reach 220, strength 0.2, lasts 360, blend normal` | [Burst](../battle-craft.md#burst), [Stagger and overlap](../battle-craft.md#stagger-and-overlap) |
| 920 | 1300 | impact | the effect | The pour, the signature's first half: 22 deep red beads (*house*) alive at once well up over b1 and drift a little off it, gone by 2220 ms: the longest Bleed of the family, for 4 a round. `flow at 920, count 22, shape dot, tint blood, direction out, spread 360, reach 80, strength 0.55, lasts 1300, blend normal` | [Flow](../battle-craft.md#flow), [One effect, one meaning](../battle-craft.md#one-effect-one-meaning) |
| 1000 | 900 | impact | the ground | The pool, the signature's second half: a dark red ring opens slowly on the ground under b1, a little wider than its ground ring, and fades, gone by 1900 ms. Normal blend: blood is not a light. `glow at 1000, shape ring, tint blood, reach 160, strength 0.85, lasts 900, blend normal` | [Glow](../battle-craft.md#glow), [A beat on the ground](../battle-craft.md#a-beat-on-the-ground) |
| 2220 | 320 | impact | the field | Nothing new. Both figures home, breathing. The result is read here; the hold from 2400 ms is empty. | [The hold before the number](../battle-craft.md#the-hold-before-the-number), [Breath at rest](../battle-craft.md#breath-at-rest) |

Nine staged beats, two in the windup; 54 particles asked for on b1 (14, 18, 22) and 12 on a1; three glows (one a ring
on the ground), no pulse, a shake of 0.2, the last gone by 2220 ms: inside Stage III. One flash, the body's, on b1;
two changes of light, the gleam on a1 and the rip on b1. A critical hit is the body's punch of the zoom.

### Reduced motion

Nothing. The frame after the action is shown at once with the result text: no blow, no talon, no blood, no pool. The
Bleed is the status chip's to say.

### Cost

- In the windup: one emitter and one glow image on a1, both gone by 720 ms; the last sparks may be left where a1
  stood (a melee windup beat stays there), ended before the body leaves its ring.
- Objects at the peak (920 to 1120 ms): one `Graphics` for the motif, the rip's image, three emitters (shards,
  spurt, pour), the body's tweens; from 1000 ms the pool's image; about six draw calls.
- Particles at the peak: 48 asked for at 920 ms (14, 18 and 16), fewer alive; of the field's 150.
- Bytes: not measured. Built by BC4 (motif, burst, flow, gather, glow, shake, the `staging` field), BC5 (the body,
  the trauma) and BC6 (the slash across, the `blood` tint, the ring of `glow`); BC7 places the beats. The cancel path
  is the kit's.

### On the film strip

```bash
node scripts/battle/filmstrip.mjs spell:mortal_wound:v1 --actor a1 --targets b1 --damage 5 --out <folder>/eviscerate.png
node scripts/battle/filmstrip.mjs spell:mortal_wound:v1 --actor a1 --targets b1 --damage 5 \
  --frames 24 --interval 60 --columns 6 --out <folder>/eviscerate-contact.png
```

- 0 to 180 ms (contact): no arc or mark on a1.
- 240 to 600 ms: pale bright sparks on a ring around a1, closing on its crouched body; from 420 ms a pale light on it.
- 660 ms: no spark outside a1's outline; 720 to 780 ms: no light or spark on a1's ring or on a1 as it climbs.
- 840 and 900 ms: b1 filled flat in bone (these two only); a1 over b1's lower half, b1's head clear; a pale light on
  b1.
- 960 to 1080 ms: bone shards over a1's back, on b1's near side, none past the gap.
- 900 to 1020 ms: deep red drops on every side of b1, just past its outline; the slash strokes diagonal on their dark
  edge.
- 1080 to 1380 ms: deep red beads on b1's body, many at once; a dark red ring on the ground under b1, wider than its
  feet, widening; a1 returning; no red on a1.
- 1440 ms (whole): beads still on b1; the ring at its feet. 1680 ms (whole): beads on b1, fewer; the ring faint.
- 1920 ms (whole): a few beads; no ring. 2160 ms (whole): a bead at most. 2400 ms (whole) and every frame after: no
  effect object on the field. Nothing in the effect is gold, and no particle is green.
- Set beside Latch's sheet at 1440 ms: Eviscerate's beads are more and its ground is red; Latch's beads are few, and a
  light is on a1.

The sheets cannot show the stop, the shake, the punch of a critical, or the feel at full speed.

### Left

- The pool is a `glow` ring in `blood` on a normal blend: it opens and fades, as every ring does; a pool that spreads
  and stays would be a scar, which the owner chose not to build (#954). If the film reads the ring as a ripple, a
  smaller reach first.
- The hooks are torn out back toward a1, in bone: blood carried toward the caster says "drunk" in this family. The
  owner may want the rip to spray blood; then it is thrown `along`, away from a1, and joins Wild Swing's fan.
- The beads drift off the body rather than run down it: a flow has no fall (Venom Claw's **Left**).
- The Bleed lasts two rounds; the effect ends by 2220 ms. The rounds are the status chip's.

## Blood Hunt (`spell:night_raid:v1`)

### Intent

The player reads "the Deathstalker struck two of them in one pass and tore the energy out of both": two victims at
once, and something taken that never reaches the hunter. Blood Hunt costs 3 energy; up to two enemies take 4 damage
and lose 3 energy. Stage III, a few times a game: a crouch marked on the ground before the leap, a staged shake, a
light on each victim. Its signature is the theft: from both victims together, green sparks are torn back toward a1
and keep streaming off them for most of a second, going out before they arrive. Pursuit's sparks rise off a1 and stay
its own; Blood Hunt's leave the victims and are lost. It neither bleeds nor drinks, so, by the family's rule, it shows
no blood, whatever its name.

### Today

Filmed at `28f2dc2`, 390 by 844, 1×, a1 on b1 and b3, 4 health lost each: a whole action and a contact.

- 0 to 60 ms: the shared faint green arc on a1's body.
- 120 to 540 ms: a1 crouched below its ring; 600 to 660 ms: it leaves its ring; 720 and 780 ms: up the field at b1.
- 840 and 900 ms: b1 and b3 filled flat in bone; a1 covers b1's lower half; b3 struck where it stands.
- 960 to 1380 ms: slash strokes diagonal on b1 and on b3, fading. Nothing joins b1 to b3, nothing leaves either: no
  chain, no drain.

### Beats

| At | Lasts | Window | Layer | Beat | Pattern |
| --- | --- | --- | --- | --- | --- |
| 0 | 840 | windup | the caster | A medium blow's draw back, hold and strike by the 4 health each lost ([movement.md](movement.md)); the body goes to b1, the first of the two. No shared arc. | [Anticipation](../battle-craft.md#anticipation), [Contact and follow-through](../battle-craft.md#contact-and-follow-through) |
| 120 | 540 | windup | the ground | The crouch marked: a thin green ring opens on the ground at a1's feet and fades, gone by 660 ms, before the body leaves. `glow at 120, on caster, shape ring, tint family, reach 220, strength 0.8, lasts 540, blend screen` | [Glow](../battle-craft.md#glow), [A beat on the ground](../battle-craft.md#a-beat-on-the-ground) |
| 300 | 360 | windup | the effect | The hunter's fix: a soft green light on a1 in its crouch, gone by 660 ms. `glow at 300, on caster, tint family, reach 150, strength 0.4, lasts 360, blend screen` | [Glow](../battle-craft.md#glow), [Anticipation](../battle-craft.md#anticipation) |
| 840 | 800 | impact | the effect | The `slash` motif on b1 and on b3, gone by 1640 ms. `motif at 840, lasts 800` | [An impact that keeps the direction](../battle-craft.md#an-impact-that-keeps-the-direction), [Shape before colour](../battle-craft.md#shape-before-colour) |
| 840 | 870 | impact | the caster and the targets | The medium blow's hit stop (90 ms), each victim's bone flash and recoil of 18 px, both on the contact frame; a1's return, home by 1710 ms (all but home by about 1380 ms on the film) ([movement.md](movement.md)). | [Hit stop](../battle-craft.md#hit-stop), [Recoil](../battle-craft.md#recoil) |
| 840 | 240 | impact | the camera | A staged shake on the body's tremor, decaying over about 240 ms (*house*). `shake at 840, strength 0.18` | [Shake from trauma](../battle-craft.md#shake-from-trauma) |
| 840 | 260 | impact | the effect | The strike's light: a hard green light on each victim, gone by 1100 ms. `glow at 840, tint family, reach 220, strength 0.7, lasts 260, blend add` | [Glow](../battle-craft.md#glow), [Additive light](../battle-craft.md#additive-light) |
| 840 | 300 | impact | the effect | The claws: 14 bone shards (*house*) thrown on beyond each victim along the blow in a 50° fan. `burst at 840, count 14, shape shard, tint bone, direction along, spread 50, reach 200, strength 0.45, lasts 300, blend normal` | [Burst](../battle-craft.md#burst), [An impact that keeps the direction](../battle-craft.md#an-impact-that-keeps-the-direction) |
| 880 | 360 | impact | the effect | The theft, the signature's first half: 16 green sparks (*house*) torn back off each victim toward a1 in a 40° fan, fading before the gap. `burst at 880, count 16, shape spark, tint family, direction back, spread 40, reach 240, strength 0.4, lasts 360, blend add` | [Burst](../battle-craft.md#burst), [Gravity well and convergence](../battle-craft.md#gravity-well-and-convergence) |
| 900 | 480 | impact | the ground | The hunter's line: a green arc opens on the ground at each victim's feet, on a1's side only, gone by 1380 ms. `glow at 900, shape ring, tint family, direction back, spread 120, reach 220, strength 0.45, lasts 480, blend screen` | [Glow](../battle-craft.md#glow), [A beat on the ground](../battle-craft.md#a-beat-on-the-ground) |
| 1000 | 700 | impact | the effect | Still draining, the signature's second half: 10 green sparks alive at once drawn off each victim toward a1, short, gone by 1700 ms. `flow at 1000, count 10, shape spark, tint family, direction back, spread 30, reach 160, strength 0.3, lasts 700, blend add` | [Flow](../battle-craft.md#flow), [One effect, one meaning](../battle-craft.md#one-effect-one-meaning) |
| 1710 | 830 | impact | the field | Nothing new. Every figure home, breathing. Nothing lands on a1: the energy is lost, not gained. The result is read here. | [The hold before the number](../battle-craft.md#the-hold-before-the-number), [Breath at rest](../battle-craft.md#breath-at-rest) |

Nine staged beats, two in the windup; 40 particles asked for on each victim (14, 16, 10), none on a1; 80 on the field
at most (at 1000 ms, 14, 16 and 10 on each of two); four glows (two of them rings on the ground), no pulse, a shake of
0.18, the last gone by 1700 ms: inside Stage III. One flash a figure, the body's, on b1 and b3; two changes of light,
on a1 in the windup and on the two victims at contact.

### Reduced motion

Nothing. The frame after the action is shown at once with the result text: no leap, no ring, no shard, no spark. The
energy lost is the result text's.

### Cost

- In the windup: the ring's image and the glow image on a1, gone by 660 ms, left where a1 stood.
- Objects at the peak (900 to 1100 ms): on each victim one `Graphics` for the motif, the light's image, the arc's
  `Graphics`, two or three emitters; about twelve draw calls for 200 ms, then about six.
- Particles at the peak: 80 asked for at 1000 ms (14, 16 and 10 on each of two); of the field's 150.
- Bytes: not measured. Built by BC4 (motif, burst, flow, glow, shake, the `staging` field), BC5 (the body, the
  trauma) and BC6 (the slash across, the ring and the arc of `glow`, the `level`); BC7 places the beats. The cancel
  path is the kit's.

### On the film strip

```bash
node scripts/battle/filmstrip.mjs spell:night_raid:v1 --actor a1 --targets b1,b3 --damage 4 --out <folder>/blood-hunt.png
node scripts/battle/filmstrip.mjs spell:night_raid:v1 --actor a1 --targets b1,b3 --damage 4 \
  --frames 24 --interval 60 --columns 6 --out <folder>/blood-hunt-contact.png
```

- 0 to 60 ms (contact): no arc or mark on a1's body.
- 180 to 540 ms: a thin green ring on the ground at a1's feet, wider at each frame; from 360 ms a soft green light on
  a1's crouched body.
- 660 to 780 ms: no ring and no light where a1 stood or on a1 as it climbs.
- 840 and 900 ms: b1 and b3 filled flat in bone (these two only); a1 over b1's lower half, b1's head clear; a green
  light on each of b1 and b3.
- 900 to 1020 ms: pale shards on each victim's far side from a1 (b1: short, over its head; b3: up and to the right);
  green sparks on each victim's side toward a1, pointing toward it, none reaching a1's home; a green arc on the
  ground at each victim's feet, on a1's side.
- 1080 to 1380 ms: the slash strokes on both; a few small green sparks still leaving each toward a1; a1 returning, no
  spark on it.
- 1440 ms (whole): the arcs gone (they end at 1380 ms, b3's 40 ms later); a spark or two at each victim. 1680 ms (whole): no spark left.
- 1920 ms (whole) and every frame after: no effect object on the field. b2 is not lit, moved or marked on any frame, but for the ground's own light at the contact ([the ground](ground.md)).
  Nothing in the effect is gold or red.
- Set beside Pursuit's sheet at 1440 ms: Pursuit's sparks rise off a1 under a light; Blood Hunt's a1 is dark, and any
  spark left is on a victim.

The sheets cannot show the stop, the shake, or the feel of the double strike at full speed.

### Left

- "Chain attack" in the approved execution: the kit has no beat that joins one target to the next, and the owner
  chose not to build one (#950): the chain between targets left the craft book. Both victims are struck in one
  moment, and the arcs on a1's side say where the hunter came from.
- No blood, by the family's rule: Blood Hunt neither bleeds nor drinks. Settled by the owner, 2026-11-19: blood for a
  Bleed or a drink only.
- Green sparks drawn off toward the foe mean energy lost: a reading Stage III adds (the opening). The owner may refuse
  it; then the theft is pale motes, and loses the family's colour.
- The body goes to b1 only, as [movement.md](movement.md) has it: b3 is struck where it stands.

## Blood Price (`spell:hateful_sacrifice:v1`)

### Intent

The player reads "the Soulreaver tore at its own body to buy one brutal blow": the price seen first, then the hardest
hit of the family, and the striker wounded. Blood Price costs 3 energy, deals 11 damage with a 50% critical chance
and 3 damage to its own caster. Stage III, a few times a game, and the family's heaviest blow: the largest shake and
the brightest light. Its signature is the price paid first: a deep red light swells on a1 and its own blood is torn
off its back before it leaps, and after the blow the only blood on the field is a1's. b1 takes eleven and sheds bone:
Blood Price leaves it no Bleed.

### Today

Filmed at `28f2dc2`, 390 by 844, 1×, a1 on b1, 11 health lost, a1 paying 3: a whole action and a contact.

- 0 to 60 ms: the shared faint green arc on a1's body.
- 120 to 540 ms: a1 crouched below its ring; 600 to 660 ms: it leaves its ring; 720 and 780 ms: up the field at b1.
- 840 and 900 ms: b1 filled flat in bone; a1 covers b1's lower half, b1's head clear.
- 900 to 1380 ms: deep red strokes on a1's body, the scene's wound, going with a1 as it returns; faint at 1440 ms
  (whole). On b1, three green slash strokes, fading.
- Nothing before contact says the price, and b1's blow looks no heavier than Eviscerate's.

### Beats

| At | Lasts | Window | Layer | Beat | Pattern |
| --- | --- | --- | --- | --- | --- |
| 0 | 840 | windup | the caster | A heavy blow's draw back, hold and strike by the 11 health b1 lost ([movement.md](movement.md)). No shared arc. | [Anticipation](../battle-craft.md#anticipation), [Contact and follow-through](../battle-craft.md#contact-and-follow-through) |
| 200 | 480 | windup | the effect | The price taken, the signature's first half: a deep red light swells on a1 in its crouch and goes, gone by 680 ms. `glow at 200, on caster, tint blood, reach 240, strength 0.8, lasts 480, blend screen` | [Glow](../battle-craft.md#glow), [Anticipation](../battle-craft.md#anticipation) |
| 360 | 300 | windup | the effect | Its own blood: 12 deep red drops (*house*) torn up off a1's back in a 120° fan, short, gone by 660 ms, before the body leaves. `burst at 360, on caster, count 12, shape dot, tint blood, direction up, spread 120, reach 160, strength 0.4, lasts 300, blend normal` | [Burst](../battle-craft.md#burst), [One effect, one meaning](../battle-craft.md#one-effect-one-meaning) |
| 840 | 900 | impact | the effect | The `slash` motif on b1, cutting across the blow on its dark edge, gone by 1740 ms. `motif at 840, lasts 900` | [An impact that keeps the direction](../battle-craft.md#an-impact-that-keeps-the-direction), [Shape before colour](../battle-craft.md#shape-before-colour) |
| 840 | 900 | impact | the caster and the target | The heavy blow's hit stop (120 ms), b1's bone flash, its recoil of 26 px, a1's return, home by 1740 ms (all but home by about 1380 ms on the film) ([movement.md](movement.md)). The scene draws a1's wound, which goes with its body: the price's second half, not a staged beat. | [Hit stop](../battle-craft.md#hit-stop), [Recoil](../battle-craft.md#recoil) |
| 840 | 300 | impact | the camera | The family's largest staged shake, over the heavy blow's trauma, decaying over about 300 ms (*house*). `shake at 840, strength 0.32` | [Shake from trauma](../battle-craft.md#shake-from-trauma) |
| 840 | 300 | impact | the effect | The blow's light, the brightest of the family: a hard green light on b1, gone by 1140 ms. `glow at 840, tint family, reach 280, strength 0.8, lasts 300, blend add` | [Glow](../battle-craft.md#glow), [Additive light](../battle-craft.md#additive-light) |
| 840 | 520 | impact | the ground | The shock: a bone arc opens on the ground at b1's feet, on its far side from a1, gone by 1360 ms. `glow at 840, shape ring, tint bone, direction along, spread 120, reach 230, strength 0.7, lasts 520, blend screen` | [Glow](../battle-craft.md#glow), [A beat on the ground](../battle-craft.md#a-beat-on-the-ground) |
| 840 | 400 | impact | the effect | The blow carried through: 16 large bone shards (*house*) thrown on beyond b1 along the blow in a 70° fan. `burst at 840, count 16, shape shard, tint bone, direction along, spread 70, reach 400, strength 0.45, lasts 400, blend normal` | [Burst](../battle-craft.md#burst), [An impact that keeps the direction](../battle-craft.md#an-impact-that-keeps-the-direction) |
| 840 | 220 | impact | the effect | The crack of it: 12 bright bone sparks (*house*) out on every side of the point of contact, short. `burst at 840, count 12, shape spark, tint bone, direction out, spread 360, reach 220, strength 0.5, lasts 220, blend add` | [Burst](../battle-craft.md#burst), [The three parts of an effect](../battle-craft.md#the-three-parts-of-an-effect) |
| 880 | 420 | impact | the ground | Grit: 10 pale grains (*house*) thrown out flat on the ground on every side of b1's feet. `burst at 880, count 10, shape dot, tint bone, direction out, spread 360, reach 260, strength 0.35, lasts 420, blend normal, level feet` | [Burst](../battle-craft.md#burst), [A beat on the ground](../battle-craft.md#a-beat-on-the-ground) |
| 1400 | 500 | impact | the effect | The price felt: a deep red light on a1 as it lands on its ring, gone by 1900 ms. `glow at 1400, on caster, tint blood, reach 170, strength 0.6, lasts 500, blend screen` | [Glow](../battle-craft.md#glow), [A light that answers a cast](../battle-craft.md#a-light-that-answers-a-cast) |
| 2140 | 400 | impact | the field | Nothing new. Both figures home, breathing. The result is read here; the hold from 2400 ms is empty. | [The hold before the number](../battle-craft.md#the-hold-before-the-number), [Breath at rest](../battle-craft.md#breath-at-rest) |

Ten staged beats, two in the windup; 38 particles asked for on b1 (16, 12, 10) and 12 on a1; four glows (one an arc
on the ground), no pulse, a shake of 0.32, the last gone by 2140 ms: inside Stage III. One flash, the body's, on b1;
a1's wound is the scene's, not a flash of the staging. Three changes of light: red on a1 before, green on b1 at
contact, red on a1 at home.

### Reduced motion

Nothing. The frame after the action is shown at once with the result text: no blow, no red light, no blood, no shard.
The caster's loss is the result text's.

### Cost

- In the windup: one glow image and one emitter on a1, gone by 680 ms, left where a1 stood.
- Objects at the peak (880 to 1060 ms): one `Graphics` for the motif, the light's image, the arc's `Graphics`, three
  emitters (shards, sparks, grit), the scene's wound on a1, the body's tweens; about seven draw calls.
- Particles at the peak: 42 at 880 ms; of the field's 150.
- Bytes: not measured. Built by BC4 (motif, burst, glow, shake, the `staging` field), BC5 (the body, the trauma, the
  wound that goes with the body) and BC6 (the slash across, the arc of `glow`, the `level`, the `blood` tint); BC7
  places the beats. The cancel path is the kit's, and the scene's for the wound.

### On the film strip

```bash
node scripts/battle/filmstrip.mjs spell:hateful_sacrifice:v1 --actor a1 --targets b1 --damage 11 --wound --out <folder>/blood-price.png
node scripts/battle/filmstrip.mjs spell:hateful_sacrifice:v1 --actor a1 --targets b1 --damage 11 --wound \
  --frames 24 --interval 60 --columns 6 --out <folder>/blood-price-contact.png
```

- 0 to 180 ms (contact): no arc or mark on a1.
- 240 to 600 ms: a red light over a1's crouched back, faint at 300 ms and deep red with the drops from 420 ms, not gold; 420 to 600 ms: deep red drops just above
  a1's back, rising a little, none on b1.
- 660 to 780 ms: no red light or drop on a1 or where it stood.
- 840 and 900 ms: b1 filled flat in bone (these two only); a1 over b1's lower half, b1's head clear; a hard green light
  on b1, the brightest of the action; pale sparks all round the point of contact.
- 900 to 1020 ms: large pale shards on b1's far side from a1, shortened over its head; a pale arc on the ground beyond
  b1's feet, not on a1's side; pale grains flat on the ground around b1's feet; red strokes on a1 (the scene's wound).
- 1080 to 1380 ms: the slash strokes diagonal on b1; no shard left by 1260 ms; a1 returning with its wound. No red on
  b1 on any frame.
- 1440 ms (whole): a1 home or all but; the arc and the grains gone. 1440 and 1680 ms (whole): a faint deep red light
  on a1 at home, fading; 1920 ms (whole): none.
- 2160 ms (whole) and every frame after: no effect object on the field. Nothing in the effect is gold.
- Set beside Ambush's sheet at 960 ms: Ambush's splinters lie all round b1; Blood Price's go on beyond it, over an arc
  on the ground, and a1 is wounded.

The sheets cannot show the stop, the shake, the punch of a critical, or the feel at full speed.

### Left

- The price shown before the blow is a reading Stage III adds: blood torn off the caster's own body is what it pays.
  The host has confirmed the whole action when the windup starts, so this shows no outcome ahead of the host
  (ADR 0131). Settled by the owner, 2026-11-19: the cost stays in the windup.
- No blood on b1, by the family's rule: eleven damage and no Bleed. Settled by the owner, 2026-11-19: blood for a Bleed or a drink only.
- The red light on a1 at home starts at 1400 ms, as a1 lands on its ring: the sheets show it all but home by about
  1380 ms, though the heavy blow's return ends at 1740 ms. The light is placed at a1's home either way.
- A critical hit (50%) is the body's punch of the zoom; the staging cannot tell it.

## Devour (`spell:soul_devourer:v1`)

### Intent

The player reads "the Soulreaver sank its jaws in and drew that one's life and energy out through them, and its own
body mended": a held feed, not a snap. Devour costs 3 energy, deals 7 damage, drains 3 energy and heals its caster 4.
The Leech's capstone: it must outdo Bite and not repeat it. Its signature is the siphon: while a1's jaws are on b1, a
braided stream of deep red drops and green sparks is drawn out of b1 toward a1 for nearly a second, and as a1 comes
home a green halo swells on its own outline, the family's one halo: healed. Bite throws one mouthful and catches it
on a1; Devour draws a stream that never lands, and a1 is remade.

### Today

Filmed at `28f2dc2`, 390 by 844, 1×, a1 on b1, 7 health lost: a whole action and a contact.

- 0 to 60 ms: the shared faint green arc on a1's body.
- 120 to 540 ms: a1 crouched below its ring; 600 to 660 ms: it leaves its ring; 720 and 780 ms: up the field at b1.
- 840 and 900 ms: b1 filled flat in bone; a1 covers b1's lower half, b1's head clear.
- 960 to 1380 ms: the `bite` motif on b1, pale green teeth, fading; a1 home or all but at 1440 ms (whole). Nothing
  leaves b1, nothing reaches a1: Bite's sheet, the heal not seen.

### Beats

| At | Lasts | Window | Layer | Beat | Pattern |
| --- | --- | --- | --- | --- | --- |
| 0 | 840 | windup | the caster | A heavy blow's draw back, hold and strike by the 7 health b1 lost ([movement.md](movement.md)). No shared arc. | [Anticipation](../battle-craft.md#anticipation), [Contact and follow-through](../battle-craft.md#contact-and-follow-through) |
| 120 | 520 | windup | the effect | The jaws open: 14 bone shards (*house*), its teeth, on a ring around a1 drawn onto its body by 640 ms. `gather at 120, on caster, count 14, shape shard, tint bone, reach 240, strength 0.55, lasts 520, blend normal` | [Gravity well and convergence](../battle-craft.md#gravity-well-and-convergence), [Anticipation](../battle-craft.md#anticipation) |
| 200 | 640 | windup | the camera | The slow push, the capstone's: the view moves in on a1 while it gathers, slowly at both ends, 3% closer by the contact (*house*), holding a1 where it stands on the screen; it lets go over 240 ms as the blow lands. Not under less motion. `push at 200, strength 0.5, lasts 640` | [Slow push on a capstone](../battle-craft.md#slow-push-on-a-capstone) |
| 300 | 400 | windup | the effect | The siphon organs light: a soft green glow on a1, gone by 700 ms. `glow at 300, on caster, tint family, reach 190, strength 0.5, lasts 400, blend screen` | [Glow](../battle-craft.md#glow), [Anticipation](../battle-craft.md#anticipation) |
| 840 | 900 | impact | the effect | The `bite` motif on b1, gone by 1740 ms. `motif at 840, lasts 900` | [Shape before colour](../battle-craft.md#shape-before-colour), [The three parts of an effect](../battle-craft.md#the-three-parts-of-an-effect) |
| 840 | 900 | impact | the caster and the target | The heavy blow's hit stop (120 ms), b1's bone flash, its recoil of 26 px, a1's return, home by 1740 ms (all but home by about 1380 ms on the film) ([movement.md](movement.md)). | [Hit stop](../battle-craft.md#hit-stop), [Recoil](../battle-craft.md#recoil) |
| 840 | 300 | impact | the camera | A staged shake over the heavy blow's trauma, decaying over about 300 ms (*house*). `shake at 840, strength 0.25` | [Shake from trauma](../battle-craft.md#shake-from-trauma) |
| 840 | 220 | impact | the effect | The jaws meet: 16 bone shards (*house*) out on every side of b1, short. Bite's are ten small sparks. `burst at 840, count 16, shape shard, tint bone, direction out, spread 360, reach 260, strength 0.3, lasts 220, blend normal` | [Burst](../battle-craft.md#burst), [The three parts of an effect](../battle-craft.md#the-three-parts-of-an-effect) |
| 840 | 280 | impact | the effect | The bite's light: a hard green light on b1, gone by 1120 ms. `glow at 840, tint family, reach 240, strength 0.75, lasts 280, blend add` | [Glow](../battle-craft.md#glow), [Additive light](../battle-craft.md#additive-light) |
| 880 | 900 | impact | the effect | The siphon, the signature's first strand: 20 deep red drops (*house*) alive at once drawn out of b1 toward a1 in a narrow stream, gone by 1780 ms. It does not land: Bite's lands. `flow at 880, count 20, shape dot, tint blood, direction back, spread 30, reach 360, strength 0.6, lasts 900, blend normal` | [Flow](../battle-craft.md#flow), [Gravity well and convergence](../battle-craft.md#gravity-well-and-convergence) |
| 900 | 800 | impact | the effect | The siphon's second strand, the energy drained: 14 green sparks alive at once in the same stream, gone by 1700 ms. Blood Hunt's reading: energy lost. `flow at 900, count 14, shape spark, tint family, direction back, spread 30, reach 360, strength 0.5, lasts 800, blend add` | [Flow](../battle-craft.md#flow), [One effect, one meaning](../battle-craft.md#one-effect-one-meaning) |
| 1380 | 800 | impact | the caster | The swell, the signature's end and the family's one halo: a green halo on a1's own outline swells as it comes home, at its fullest near 1780 ms, gone by 2180 ms. It goes with a1's body. `pulse at 1380, on caster, tint family, strength 0.6, lasts 800` | [Glow](../battle-craft.md#glow), [One hero shader](../battle-craft.md#one-hero-shader) |
| 1560 | 520 | impact | the effect | Tissue restored: a soft green light on a1 at home, under the halo, gone by 2080 ms. `glow at 1560, on caster, tint family, reach 200, strength 0.15, lasts 520, blend screen` | [Glow](../battle-craft.md#glow), [A light that answers a cast](../battle-craft.md#a-light-that-answers-a-cast) |
| 2180 | 360 | impact | the field | Nothing new. Both figures home, breathing. The result is read here; the hold from 2400 ms is empty. | [The hold before the number](../battle-craft.md#the-hold-before-the-number), [Breath at rest](../battle-craft.md#breath-at-rest) |

Ten staged beats, two in the windup; 50 particles asked for on b1 (16, 20, 14) and 14 on a1; three glows and the one
pulse, on a1; a shake of 0.25; the last gone by 2180 ms: inside Stage III. One flash, the body's, on b1; three
changes of light: a1 in the windup, b1 at contact, a1 at home under its halo.

### Reduced motion

Nothing. The frame after the action is shown at once with the result text: no bite, no stream, no halo, no light. The
heal and the energy are the result text's.

### Cost

- In the windup: one emitter and one glow image on a1, gone by 700 ms, left where a1 stood.
- Objects at the peak (900 to 1120 ms): one `Graphics` for the motif, the light's image, two emitters (the strands),
  the body's tweens; about four draw calls. From 1380 ms the halo on a1: a filter, a framebuffer and a pass each frame
  for 800 ms (the craft book, "Glow"); from 1560 ms one glow image.
- Particles at the peak: 44 asked for at 900 ms (16, 16 and 12), fewer alive; of the field's 150.
- Bytes: not measured. Built by BC4 (motif, burst, flow, gather, glow, shake, the `staging` field), BC5 (the body,
  the trauma) and BC6 (the pulse, the gold rim quieted under it, the `blood` tint); BC7 places the beats. The cancel
  path is the kit's: the filter is removed and the rim given back when the action ends or is cancelled.

### On the film strip

```bash
node scripts/battle/filmstrip.mjs spell:soul_devourer:v1 --actor a1 --targets b1 --damage 7 --out <folder>/devour.png
node scripts/battle/filmstrip.mjs spell:soul_devourer:v1 --actor a1 --targets b1 --damage 7 \
  --frames 24 --interval 60 --columns 6 --out <folder>/devour-contact.png
```

- 0 and 60 ms (contact): no arc or mark on a1.
- 180 to 600 ms: pale bone shards on a ring around a1, closing on its crouched body; from 360 ms a soft green light
  on a1.
- 660 to 780 ms: no shard or light on a1 or where it stood.
- 840 and 900 ms: b1 filled flat in bone (these two only); a1 over b1's lower half, b1's head clear; a hard green light
  on b1; pale shards just outside b1's outline on every side, gone by 1080 ms.
- 960 to 1140 ms: a narrow stream of deep red drops and green sparks together on b1's side toward a1, over a1's back
  while it is on b1, none above b1's head; the bite motif on b1.
- 1200 to 1380 ms: the stream still leaving b1, thinning, none of it reaching a1's home; a1 returning.
- 1440 ms (whole): a1 home or all but, a green halo on its own outline, its gold rim quieted; a last few drops and
  sparks at b1.
- 1680 ms (whole): the halo at its fullest, a soft green light on a1; no drop or spark left at b1.
- 1920 ms (whole): the halo fading, the light faint. 2160 ms (whole): a trace of the halo at most.
- 2400 ms (whole) and every frame after: no effect object on the field, a1's gold rim back. Nothing in the effect is
  gold.
- Set beside Bite's sheet at 1440 ms: Bite's red drops are landing on a1; Devour's red stays at b1 and a1 wears a
  halo.

The sheets cannot show the stop, the shake, the feel of the halo's swell at full speed, or the sound.

### Left

- The halo means "healed", as on Vital Surge: the one `pulse` of the family is kept for the capstone, so neither the
  barb's stun nor a windup charge uses it. Settled by the owner, 2026-11-19: the halo is for a heal only.
- The stream is two flows aimed `back`, red and green, and fades before it reaches a1: a stream that lands on the
  caster is Bite's gather. The owner may want the siphon to arrive; then Devour repeats Bite's gulp.
- The windup's teeth are bone shards gathered onto a1, Latch's hooks drawn inward: the shape is shared, the anchor is
  not (Latch's close on b1 at contact, Devour's on a1 in the windup).
- The halo starts at 1380 ms, on a1 as it comes home: a pulse on a melee caster plays on its figure wherever it is.
- In a match the host may list a1 among the recipients of its heal; the scene then draws its own mark on a1, which
  goes with its body. The bench's film, on `--targets b1`, does not show it beside the halo.
