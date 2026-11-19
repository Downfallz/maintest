# Storyboards: the necromantic family

The Necromancer's line of the Sorcerer, drawn on the body the art calls the Warped, in its Necrotic and Revenant
mutations: death magic, not infection ([the art direction](../creature-art-direction.md)), its blue-green ghostlight,
floating bones and dead spirits ([the vocabulary of the family](../spell-vfx.md)), on the family's blue-green
(0x85c9c2). Its 4 spells climb [the ladder](README.md#the-ladder) from Stage II: Bonewall and Infestation (Necrotic),
then Wraithguard and Death Wail (Revenant). The family has no Stage I spell. Every one of them is a cast: the caster
loads where it stands and stays home ([movement.md](movement.md)), and every one of them costs it blood. Two of the
four are wards, and the family must get the price and the wards right.

The family keeps one meaning per look ([One effect, one
meaning](../battle-craft.md#one-effect-one-meaning)). The `spirits` and `bones` motifs carry the family and stay the
primary element. Soft blue-green dots on a screen blend are the dead: spirits called up, hovering, feeding. The look
of each reading:

- **The price.** Deep red drops (`blood`) torn up off the caster's own back in the windup are the blood it pays,
  and the wound the scene draws on it after the contact (`--wound`) is the same price; a caster never reacts to its
  own cost and the view never shakes for it ([movement.md](movement.md)). The look is one in all four; its size
  follows the price: 6 drops for a Bleed of 1 (Bonewall), 8 for 2 damage (Infestation), 14 for a Bleed of 4
  (Wraithguard, Death Wail). A Stage II spell spends its one windup beat on it; a Stage III spell adds a deep red
  light on the caster before the drops and again at home after the wound, as Blood Price does in the biological
  family. The price is carried to where it is spent only when the approved execution says so: Wraithguard's guards
  are "fed by caster's blood", so a `trail` of red drops runs from the caster to each guarded ally. Nothing else
  carries blood across the field.
- **A Bleed.** A few red drops seeping slowly off an enemy and staying on it are its Bleed (Infestation, Death
  Wail): red on an enemy is never the caster's price.
- **The ward of bone.** Pale `bone` shards rising straight up off an ally's feet, a wall going up, are Bonewall. No
  ring, no gather: physical's Brace closes plates onto the body, Bonewall raises them from the ground.
- **The ward of spirits.** A blue-green ring standing on the ground at an ally's feet for more than a second, and
  blue-green dots rising slowly from it, are spirits standing guard (Wraithguard). Rounded dots on a ring, not
  shards: the two wards are told apart by shape first, then tint.
- A ward neither shakes the view nor plays a `pulse`: the halo means healed, and no spell of this family heals.
- **Feeding and screaming.** Infestation's spirits close in on each victim and settle on it (a `gather`, then a
  slow `flow` close to the body), with no hit. Death Wail's scream goes through: an arc of light and sparks thrown
  on beyond each enemy, `along` the line from the caster, over a hit and a shake.

The three-target spells share the field's 150 particles: at any moment three times what lives on one target, plus
what lives on the caster, stays under it. Wraithguard counts its caster among its targets, so a1 carries the
price's beats and the ward's.

The pass (2026-11-19) ran two rounds. The designer read the first films of the four against these storyboards and
asked for 15 changes, most from three causes: drops and dots under a strength of about 0.45 piled into one red cloud
on the back or were not seen at all; pieces of the size of the motif's were lost in it; and the rings and arcs opened
too wide, into a neighbour. A fourth finding, Wraithguard's ward unseen on a1, was the bench's: it gave a caster among
its own recipients the wound alone, and now gives it the spell as well, as the host does. The second films show the
price, the trail, the rings, the scream and a1's ward; what they still miss is Bonewall's shards clearing the motif
and Infestation's Bleed reading under its light, each under its spell's **Left**. The designer read the second films;
the data was not changed after them.

## Bonewall (`spell:bone_ward:v1`)

### Intent

The player reads "the Necromancer bled a little, and bones of the dead rose round its ally as a wall": who paid,
who is warded, and that the ward is bone. Bonewall gives one ally +6 defense for 2 rounds and leaves its caster a
Bleed of 1 for 1 round. Stage II, cast now and then: one windup beat, no shake. Its signature is the wall going up:
pale bone shards rising straight off a2's feet, around the `bones` motif, with grave dust broken off the ground.

### Today

Filmed at `fc3ec94`, 390 by 844, 1×, a1 on a2, a1 paying its Bleed: a whole action and a contact.

- 0 to 300 ms: the shared blue-green arc on a1's body; a1 barely moves through the windup.
- 840 ms: faint upright bones on a2; 900 to 1380 ms: the `bones` motif, four upright bones over a pale blue-green
  ring at a2's feet, fading, gone by 1680 ms (whole).
- 900 to 1440 ms: deep red strokes on a1's body, the scene's wound.
- Nothing before contact says the price, and nothing rises: the bones appear where they stand.

### Beats

| At | Lasts | Window | Layer | Beat | Pattern |
| --- | --- | --- | --- | --- | --- |
| 0 | 840 | windup | the caster | A cast's load where it stands, then the rise ([movement.md](movement.md), `CAST`). No shared arc: the staging's windup beat replaces it. | [Anticipation](../battle-craft.md#anticipation), [Squash and stretch](../battle-craft.md#squash-and-stretch) |
| 420 | 340 | windup | the effect | The price, the one windup beat: 6 deep red drops (*house*), the smallest price of the family, thrown up clear of a1's back in a 90° fan, gone by 760 ms. Small drops thrown far: larger ones piled into one red cloud on the back on the first film. `burst at 420, on caster, count 6, shape dot, tint blood, direction up, spread 90, reach 240, strength 0.15, lasts 340, blend normal` | [Burst](../battle-craft.md#burst), [Anticipation](../battle-craft.md#anticipation) |
| 840 | 900 | impact | the effect | The `bones` motif on a2, gone by 1740 ms. `motif at 840, lasts 900` | [Shape before colour](../battle-craft.md#shape-before-colour), [The three parts of an effect](../battle-craft.md#the-three-parts-of-an-effect) |
| 840 | 900 | impact | the caster and the target | The release, as a cast lets go ([movement.md](movement.md)). a2 gains and does not react. The scene draws a1's wound: the price's second half, not a staged beat. | [Contact and follow-through](../battle-craft.md#contact-and-follow-through) |
| 840 | 420 | impact | the effect | The wall rising, the signature: 14 bone shards (*house*) thrown straight up off a2's feet in a narrow 60° fan, past the motif's bones to above its head: smaller and shorter, they were lost in the motif on the first film. `burst at 840, on target, count 14, shape shard, tint bone, direction up, spread 60, reach 300, strength 0.6, lasts 420, blend normal, level feet` | [Burst](../battle-craft.md#burst), [One effect, one meaning](../battle-craft.md#one-effect-one-meaning) |
| 860 | 380 | impact | the ground | Grave dust: 8 pale grains (*house*) thrown out flat on every side of a2's feet as the ground gives the bones up. `burst at 860, on target, count 8, shape dot, tint bone, direction out, spread 360, reach 220, strength 0.4, lasts 380, blend normal, level feet` | [Burst](../battle-craft.md#burst), [A beat on the ground](../battle-craft.md#a-beat-on-the-ground) |
| 900 | 700 | impact | the effect | The wall standing: 10 shards (*house*) alive at once keep rising off a2's feet, gone by 1600 ms. `flow at 900, on target, count 10, shape shard, tint bone, direction up, spread 40, reach 220, strength 0.5, lasts 700, blend normal, level feet` | [Flow](../battle-craft.md#flow), [Stagger and overlap](../battle-craft.md#stagger-and-overlap) |
| 900 | 600 | impact | the effect | The ward's light: a soft pale light on a2, gone by 1500 ms. Not gold. `glow at 900, on target, tint bone, reach 220, strength 0.4, lasts 600, blend screen` | [Glow](../battle-craft.md#glow) |
| 1740 | 800 | impact | the field | Nothing new. Both figures home, breathing. The result is read here. | [The hold before the number](../battle-craft.md#the-hold-before-the-number), [Breath at rest](../battle-craft.md#breath-at-rest) |

Six staged beats, one in the windup; 32 particles asked for on a2 (14, 8, 10) and 6 on a1; one glow, no pulse, no
shake, the last gone by 1740 ms: inside Stage II. No flash: nobody is struck. One change of light, a pale light on a2;
a1's wound is the scene's, not a light of the staging.

### Reduced motion

Nothing. The frame after the action is shown at once with the result text: no drops, no wall, no light. The ward is
the status chip's to say, the price the result text's.

### Cost

- In the windup: one emitter on a1, gone by 760 ms.
- Objects at the peak (900 to 1240 ms): one `Graphics` for the motif, the light's image, three emitters (the rise,
  the dust, the wall), the scene's wound on a1; about six draw calls.
- Particles at the peak: 32 at 900 ms; of the field's 150.
- Bytes: not measured. Built by BC4 (motif, burst, flow, glow, the `staging` field), BC5 (the cast's body, the
  wound) and BC6 (the `level`, the `blood` tint); BC8 places the beats. The cancel path is the kit's.

### On the film strip

```bash
node scripts/battle/filmstrip.mjs spell:bone_ward:v1 --actor a1 --targets a2 --wound --out <folder>/bonewall.png
node scripts/battle/filmstrip.mjs spell:bone_ward:v1 --actor a1 --targets a2 --wound \
  --frames 24 --interval 60 --columns 6 --out <folder>/bonewall-contact.png
```

- 0 to 360 ms (contact): no arc or mark on a1.
- 480 ms: a small red patch on a1's back; 540 to 660 ms: a few small deep red drops apart from one another, above
  a1's back and outline; none on a2. 720 ms: none left.
- 900 to 1080 ms: pale shards rising straight up from a2's feet, none sideways onto a1, among the motif's bones and
  to about the top of a2's back; pale grains on the ground round a2's feet from 960 ms; a soft pale light on a2. a2
  does not flash, move or recoil.
- 960 to 1380 ms: the bones motif on a2; shards still rising off its feet; red strokes on a1 (the scene's wound),
  and no red anywhere else.
- 1440 ms (whole): a last shard or two at a2's feet, the light faint. 1680 ms (whole): no shard, no light.
- 1920 ms (whole) and every frame after: no effect object on the field. The view does not shake on any frame.
  Nothing in the effect is gold.
- Set beside Brace's sheet at 960 ms: Brace's shards close onto the body; Bonewall's rise from the ground.

The sheets cannot show the feel at full speed, nor the sound.

### Left

- The price is seen before the ward, in the one windup beat a Stage II spell may have. The host has confirmed the
  whole action when the windup starts (ADR 0131); the owner may want the cost seen only after contact, with the
  scene's wound alone. Then the windup is empty and Bonewall keeps five beats, under Stage II's six.
- On the second film the rising shards stay inside a2's outline, mixed with the motif's bones, from 900 to 1080 ms:
  the wall does not yet stand clear of the motif. A later pass: the rise at a reach of 420 (from 300) and the wall
  standing at 300 (from 220), both *house*, so the tallest shards pass a2's head.
- The wall is shards rising in a fan, not a wall drawn standing: the kit has no standing shape but the ring. A wall
  of bones that stands for a second is a pattern the book lacks.
- The ward's light is bone-tinted, not the family's: the family's blue-green is the dead, and this ward is bone. The
  motif keeps its blue-green ring, so the family still reads first.

## Infestation (`spell:summon_minions:v1`)

### Intent

The player reads "the Necromancer cut itself and loosed hungry spirits of the dead on up to three foes, and they are
feeding": spirits that arrive, settle on each victim and stay, and blood that will keep coming. Infestation leaves a
Bleed of 2 a round for 3 rounds on up to three enemies, with no damage now, and deals its caster 2. Stage II: one
windup beat, no shake, and no hit, since nobody loses health on the contact. Its signature is the swarm settling:
blue-green spirits drawn in from all round each victim onto its body, then hovering close to it, with a few red drops
seeping off it. Death Wail goes through its foes; Infestation stays on them.

### Today

Filmed at `fc3ec94`, 390 by 844, 1×, a1 on b1, b2 and b3, a1 paying 2: a whole action and a contact.

- 0 to 300 ms: the shared blue-green arc on a1's body.
- 900 to 1380 ms: the `spirits` motif, three small blue-green skull-like drops over each of b1, b2 and b3, sinking
  and fading; faint at 1440 ms (whole), gone by 1680 ms. No target flashes or moves.
- 960 to 1440 ms: deep red strokes on a1's body, the scene's wound.
- Nothing arrives and nothing feeds: the spirits appear on each body, and no red says the Bleed.

### Beats

| At | Lasts | Window | Layer | Beat | Pattern |
| --- | --- | --- | --- | --- | --- |
| 0 | 840 | windup | the caster | A cast's load where it stands, then the rise ([movement.md](movement.md), `CAST`). No shared arc. | [Anticipation](../battle-craft.md#anticipation), [Squash and stretch](../battle-craft.md#squash-and-stretch) |
| 420 | 340 | windup | the effect | The price, the one windup beat: 8 deep red drops (*house*) thrown up clear of a1's back in a 100° fan, gone by 760 ms. `burst at 420, on caster, count 8, shape dot, tint blood, direction up, spread 100, reach 260, strength 0.15, lasts 340, blend normal` | [Burst](../battle-craft.md#burst), [Anticipation](../battle-craft.md#anticipation) |
| 840 | 900 | impact | the effect | The `spirits` motif on each target, gone by 1740 ms. `motif at 840, lasts 900` | [Shape before colour](../battle-craft.md#shape-before-colour), [The three parts of an effect](../battle-craft.md#the-three-parts-of-an-effect) |
| 840 | 900 | impact | the caster and the targets | The release, as a cast lets go ([movement.md](movement.md)). No target loses health now: none flashes or recoils. The scene draws a1's wound. | [Contact and follow-through](../battle-craft.md#contact-and-follow-through) |
| 840 | 400 | impact | the effect | The swarm arriving, the signature's first half: 12 blue-green dots (*house*) on a wide ring around each target drawn onto its body by 1240 ms. `gather at 840, on target, count 12, shape dot, tint family, reach 300, strength 0.5, lasts 400, blend screen` | [Gravity well and convergence](../battle-craft.md#gravity-well-and-convergence), [Stagger and overlap](../battle-craft.md#stagger-and-overlap) |
| 840 | 700 | impact | the effect | The ghostlight: a faint blue-green light on each target, gone by 1540 ms. Faint, so three do not light the field. `glow at 840, on target, tint family, reach 220, strength 0.3, lasts 700, blend screen` | [Glow](../battle-craft.md#glow) |
| 1000 | 900 | impact | the effect | The feeding, the signature's second half: 10 dots (*house*) alive at once hover close round each body, slow, gone by 1900 ms. `flow at 1000, on target, count 10, shape dot, tint family, direction out, spread 360, reach 130, strength 0.45, lasts 900, blend screen` | [Flow](../battle-craft.md#flow), [One effect, one meaning](../battle-craft.md#one-effect-one-meaning) |
| 1100 | 800 | impact | the effect | The Bleed: 6 deep red drops (*house*) alive at once seeping off each target and staying on it, gone by 1900 ms. Larger drops: at 0.4 they were a faint smudge under the motif and the light on the first film. `flow at 1100, on target, count 6, shape dot, tint blood, direction out, spread 360, reach 110, strength 0.7, lasts 800, blend normal` | [Flow](../battle-craft.md#flow), [One effect, one meaning](../battle-craft.md#one-effect-one-meaning) |
| 1900 | 640 | impact | the field | Nothing new. Every figure home, breathing. The result is read here. | [The hold before the number](../battle-craft.md#the-hold-before-the-number), [Breath at rest](../battle-craft.md#breath-at-rest) |

Six staged beats, one in the windup; 28 particles asked for on each target (12, 10, 6) and 8 on a1; 84 on the field
at most (at 1100 ms, 28 on each of three, none on a1); one glow, no pulse, no shake, the last gone by 1900 ms: inside
Stage II. No flash: nobody is struck on the contact. One change of light, at contact, on the three.

### Reduced motion

Nothing. The frame after the action is shown at once with the result text: no drops, no swarm, no light. The Bleed is
the status chip's to say, the price the result text's.

### Cost

- In the windup: one emitter on a1, gone by 760 ms.
- Objects at the peak (1100 to 1240 ms): on each target one `Graphics` for the motif, the light's image and three
  emitters; the scene's wound on a1; about fifteen draw calls, the screen objects batched after the normal ones.
- Particles at the peak: 84 at 1100 ms; of the field's 150.
- Bytes: not measured. Built by BC4 (motif, gather, flow, burst, glow, the `staging` field), BC5 (the cast's body,
  the wound) and BC6 (the `blood` tint); BC8 places the beats. The cancel path is the kit's.

### On the film strip

```bash
node scripts/battle/filmstrip.mjs spell:summon_minions:v1 --actor a1 --targets b1,b2,b3 --wound --out <folder>/infestation.png
node scripts/battle/filmstrip.mjs spell:summon_minions:v1 --actor a1 --targets b1,b2,b3 --wound \
  --frames 24 --interval 60 --columns 6 --out <folder>/infestation-contact.png
```

- 0 to 360 ms (contact): no arc or mark on a1.
- 480 to 720 ms: small deep red drops apart from one another, rising off a1's back to above its outline, a few more
  than Bonewall's, not one red cloud; 780 ms: none left.
- 900 to 1140 ms: blue-green dots scattered wide round each of b1, b2 and b3, most above it, closing on its body; a
  faint blue-green light on each. No target is filled in bone or moves on any frame.
- 900 to 1380 ms: the spirits motif on each; blue-green dots hovering close to each body; from 1200 ms a faint red
  smudge inside each outline, under the motif and the light, its drops not told apart. Red strokes on a1 (the
  scene's wound).
- 1440 ms (whole): faint dots and red on each target. 1680 ms (whole): a red drop or two at most. 1920 ms (whole):
  nothing.
- 2160 ms (whole) and every frame after: no effect object on the field. a2 and a3 are not lit or marked. The view
  does not shake. Nothing in the effect is gold.
- Set beside Death Wail's sheet at 1080 ms: Infestation's dots close in on each body; Death Wail's sparks fly on
  beyond it.

The sheets cannot show the feel at full speed, nor the sound.

### Left

- On the second film the Bleed's drops are still a faint red smudge inside each target from 1200 to 1380 ms: they play
  under the ghostlight (to 1540 ms) and the motif, where Death Wail's, after a light gone by 1120 ms, read as drops. A
  later pass: the ghostlight to 400 (from 700), gone by 1240 ms, and the Bleed at reach 150 (from 110), both *house*.
- The caster's 2 damage now is shown with the same drops as the Bleeds the other three leave: one look for the price,
  whatever form the catalogue gives it (`docs/art/spell-vfx.md`, "Timing and integrity"). The owner may want the
  immediate damage told apart; then a1 needs a hit of its own, which the body does not give a cost.
- The Bleed is shown at once, in red drops on the victims, though it bleeds them only on the coming rounds. The owner
  may want the targets to show only spirits; then the Bleed is the status chip's alone.
- Spirits that circle a victim would say "feeding" better than dots flowing out and back: the kit's flow has no orbit,
  and the book lacks the pattern.

## Wraithguard (`spell:revenant_guards:v1`)

### Intent

The player reads "the Revenant paid a heavy price in blood, and its blood called the dead to stand guard round its
allies": the price paid first and carried to each ward, then a spirit standing on each. Wraithguard gives up to three
allies +3 defense for good and +5 more for 2 rounds, and leaves its caster a Bleed of 4 for 1 round. Stage III, a few
times a game: a windup of three beats, and no shake, since a ward does not shake. Its signature is the guard ring: a
blue-green ring standing at each ally's feet, spirits rising slowly from it, each fed by a thread of a1's blood.
Bonewall's bone rises in shards; Wraithguard's spirits stand in a ring.

### Today

Filmed at `fc3ec94`, 390 by 844, 1×, a1 on a1, a2 and a3, a1 paying its Bleed: a whole action.

- 0 to 240 ms: the shared blue-green arc on a1's body; a1 barely moves through the windup.
- 960 to 1440 ms: the `spirits` motif, three blue-green skull-like drops over a2 and over a3, sinking and fading,
  gone by 1680 ms (whole). On a1, the scene's red wound strokes and no spirit seen.
- Nothing before contact says the price, nothing goes from a1 to its allies, and the ward reads as Infestation's
  spirits on allies.

### Beats

| At | Lasts | Window | Layer | Beat | Pattern |
| --- | --- | --- | --- | --- | --- |
| 0 | 840 | windup | the caster | A cast's load where it stands, then the rise ([movement.md](movement.md), `CAST`). No shared arc. | [Anticipation](../battle-craft.md#anticipation), [Squash and stretch](../battle-craft.md#squash-and-stretch) |
| 160 | 440 | windup | the effect | The ritual opens: a deep red light swells on a1 and goes, gone by 600 ms. `glow at 160, on caster, tint blood, reach 200, strength 0.6, lasts 440, blend screen` | [Glow](../battle-craft.md#glow), [Anticipation](../battle-craft.md#anticipation) |
| 360 | 340 | windup | the effect | The price, Bleed 4: 14 deep red drops (*house*) thrown up clear of a1's back in a 120° fan, gone by 700 ms. `burst at 360, on caster, count 14, shape dot, tint blood, direction up, spread 120, reach 280, strength 0.15, lasts 340, blend normal` | [Burst](../battle-craft.md#burst), [Anticipation](../battle-craft.md#anticipation) |
| 520 | 300 | windup | the effect | The dead answer the blood: 16 blue-green dots (*house*) on a wide ring around a1 drawn onto it by 820 ms. Larger and more: at 12 and 0.45 the first film showed a few faint dots. `gather at 520, on caster, count 16, shape dot, tint family, reach 320, strength 0.8, lasts 300, blend screen` | [Gravity well and convergence](../battle-craft.md#gravity-well-and-convergence), [Anticipation](../battle-craft.md#anticipation) |
| 840 | 900 | impact | the effect | The `spirits` motif on each ward, gone by 1740 ms. `motif at 840, lasts 900` | [Shape before colour](../battle-craft.md#shape-before-colour), [The three parts of an effect](../battle-craft.md#the-three-parts-of-an-effect) |
| 840 | 900 | impact | the caster and the targets | The release, as a cast lets go ([movement.md](movement.md)). The wards gain and do not react. The scene draws a1's wound. | [Contact and follow-through](../battle-craft.md#contact-and-follow-through) |
| 840 | 400 | impact | the effect | The price carried, "fed by caster's blood": a thread of blood runs along the ground from a1's feet to each ally's at an even pace and stays open while it runs, 16 deep red drops (*house*), the last gone by 1640 ms. On the ground, as a crack: at body height the head eased in, its drops lived 180 ms and nothing of it was seen on the first film. `trail at 840, count 16, shape dot, tint blood, strength 1, lasts 400, blend normal, level feet` | [Trail](../battle-craft.md#trail), [One effect, one meaning](../battle-craft.md#one-effect-one-meaning) |
| 880 | 1200 | impact | the ground | The guard ring, the signature's first half: a blue-green ring opens slowly on the ground at each ward's feet to just past its outline and fades there, gone by 2080 ms. At 260 it opened to twice a3's width and was still growing at 1380 ms on the first film; the kit's ring always opens over its whole beat, so a short reach is what keeps it near standing. `glow at 880, on target, shape ring, tint family, reach 150, strength 0.6, lasts 1200, blend screen` | [Glow](../battle-craft.md#glow), [A beat on the ground](../battle-craft.md#a-beat-on-the-ground) |
| 900 | 1100 | impact | the effect | The guards standing, the signature's second half: 10 blue-green dots (*house*) alive at once rise slowly from each ring, gone by 2000 ms. `flow at 900, on target, count 10, shape dot, tint family, direction up, spread 40, reach 220, strength 0.5, lasts 1100, blend screen, level feet` | [Flow](../battle-craft.md#flow), [Stagger and overlap](../battle-craft.md#stagger-and-overlap) |
| 900 | 800 | impact | the effect | The ghostlight: a faint blue-green light on each ward, gone by 1700 ms. `glow at 900, on target, tint family, reach 220, strength 0.35, lasts 800, blend screen` | [Glow](../battle-craft.md#glow) |
| 1500 | 500 | impact | the effect | The price felt: a deep red light on a1 at home, under its ring, gone by 2000 ms. `glow at 1500, on caster, tint blood, reach 160, strength 0.4, lasts 500, blend screen` | [Glow](../battle-craft.md#glow), [A light that answers a cast](../battle-craft.md#a-light-that-answers-a-cast) |
| 2080 | 460 | impact | the field | Nothing new. Every figure home, breathing. The result is read here; the hold from 2400 ms is empty. | [The hold before the number](../battle-craft.md#the-hold-before-the-number), [Breath at rest](../battle-craft.md#breath-at-rest) |

Nine staged beats, three in the windup; 26 particles asked for on each ward (16, 10) and 30 on a1 in the windup, all
gone by 840 ms; 78 on the field at most (at 900 ms, 26 on each of three, a1 among them); four glows (one a ring on the
ground), no pulse, no shake, the last gone by 2080 ms: inside Stage III. No flash. Three changes of light: red on a1
before, blue-green on the three at contact, red on a1 at home.

### Reduced motion

Nothing. The frame after the action is shown at once with the result text: no blood, no ring, no spirit. The ward is
the status chips' to say, the price the result text's.

### Cost

- In the windup: one glow image and two emitters on a1, gone by 820 ms.
- Objects at the peak (900 to 1200 ms): on each ward one `Graphics` for the motif, the ring's `Graphics`, the light's
  image, two emitters (the trail, the guards); the scene's wound on a1; about fifteen draw calls.
- Particles at the peak: 78 at 900 ms; of the field's 150.
- Bytes: not measured. Built by BC4 (motif, trail, gather, flow, burst, glow, the `staging` field), BC5 (the cast's
  body, the wound) and BC6 (the ring of `glow`, the `level`, the `blood` tint); BC8 places the beats. The cancel path
  is the kit's.

### On the film strip

```bash
node scripts/battle/filmstrip.mjs spell:revenant_guards:v1 --actor a1 --targets a1,a2,a3 --wound --out <folder>/wraithguard.png
node scripts/battle/filmstrip.mjs spell:revenant_guards:v1 --actor a1 --targets a1,a2,a3 --wound \
  --frames 24 --interval 60 --columns 6 --out <folder>/wraithguard-contact.png
```

- 0 and 60 ms (contact): no arc or mark on a1.
- 180 to 420 ms: a deep red light on a1, not gold; 420 to 600 ms: deep red drops above a1's back, more than
  Bonewall's; none on a2 or a3.
- 600 to 720 ms: a dozen or more blue-green dots, each seen, round a1 closing on its body.
- 960 to 1260 ms: a line of red drops on the ground from a1's feet toward a2's and on toward a3's, the head at a3's
  feet by about 1260 ms; none toward b1, b2 or b3. 1440 ms (whole): the line gone.
- 960 to 1380 ms: a blue-green ring on the ground at the feet of a1, a2 and a3, small at first and opening slowly to
  about each figure's width, not reaching a neighbour; blue-green dots rising from each ring; the motif on each, a1
  included, drawn over its wound; a faint blue-green light on each. Red strokes on a1 (the scene's wound). No ward
  flashes or moves.
- 1440 and 1680 ms (whole): the rings still there, a3's the widest, at about its outline; dots still rising; a faint
  red light on a1 at 1680 ms. 1920 ms (whole): a3's ring faint, nothing else.
- 2160 ms (whole) and every frame after: no effect object on the field. The view does not shake on any frame. No halo
  on any outline. Nothing in the effect is gold.
- Set beside Bonewall's sheet at 960 ms: Bonewall's pale shards rise; Wraithguard's blue-green ring stands with dots
  rising from it.

The sheets cannot show the feel at full speed, nor the sound.

### Left

- On the first film a1, its own ward, showed no ring, no rising dot, no light and no motif. That was the bench's
  fixture, not the scene: with `--wound` it gave a caster among its recipients the wound alone, and the scene draws
  only the cost on a caster that received only the cost. The bench now gives such a caster the spell as well, as the
  host does for a ward cast on oneself ([spell-vfx.md](../spell-vfx.md)), and the second film shows a1's motif,
  ring, dots and light over its wound from 960 ms. Nothing is left for the scene.
- The guard ring opens over its whole beat; a ring that opens fast and then stands is a pattern the book lacks.
- The trail on a1, its own ward, has no length: a1 is fed where it stands. The trail plays once a target, so a1's
  gives nothing to see; the owner may want a1's ward told apart, which the kit cannot do by target.
- The price is carried to the wards by a trail, the only blood that crosses the field in this family; the others
  bleed at home. A person could read the red line as a1 hurting its allies; the film says whether it does.
- The permanent +3 and the +5 for 2 rounds are one ward on the field: the split is the status chips'.
- The motif is drawn over the scene's wound on a1 and seen there on the second film, the wound's strokes behind it:
  the price and the ward read together on one figure. The owner may want the wound kept clear; then the motif on a
  caster that also pays is a change of the scene's.

## Death Wail (`spell:crazed_specter:v1`)

### Intent

The player reads "the Revenant bled, and the voices of the dead screamed through its foes and left them bleeding": the
price paid first, a cry that goes through three enemies and out beyond them, and blood that stays. Death Wail deals 4
damage and a Bleed of 4 a round for 2 rounds to up to three enemies, and leaves its caster a Bleed of 4 for 1 round.
Stage III, a few times a game, the family's one blow: the only shake and the brightest light. Its signature is the
scream going through: a blue-green arc of light opening on the ground beyond each foe and blue-green sparks thrown on
past it, along the line from a1. Infestation's spirits close in and stay; Death Wail's go through.

### Today

Filmed at `fc3ec94`, 390 by 844, 1×, a1 on b1, b2 and b3, 4 health lost each, a1 paying its Bleed: a contact.

- 0 to 300 ms: the shared blue-green arc on a1's body.
- 840 and 900 ms: b1, b2 and b3 filled flat in bone; the spirits motif already faint on them at 900 ms.
- 960 to 1380 ms: the `spirits` motif, three blue-green skull-like drops over each, sinking; Infestation's sheet with
  a hit. Red strokes on a1, the scene's wound, from 900 ms.
- Nothing screams or goes through, nothing says the Bleed, and nothing before contact says the price.

### Beats

| At | Lasts | Window | Layer | Beat | Pattern |
| --- | --- | --- | --- | --- | --- |
| 0 | 840 | windup | the caster | A cast's load where it stands, then the rise ([movement.md](movement.md), `CAST`). No shared arc. | [Anticipation](../battle-craft.md#anticipation), [Squash and stretch](../battle-craft.md#squash-and-stretch) |
| 160 | 440 | windup | the effect | The ritual opens: a deep red light swells on a1 and goes, gone by 600 ms. `glow at 160, on caster, tint blood, reach 200, strength 0.6, lasts 440, blend screen` | [Glow](../battle-craft.md#glow), [Anticipation](../battle-craft.md#anticipation) |
| 360 | 340 | windup | the effect | The price, Bleed 4: 14 deep red drops (*house*) thrown up clear of a1's back in a 120° fan, gone by 700 ms. `burst at 360, on caster, count 14, shape dot, tint blood, direction up, spread 120, reach 280, strength 0.15, lasts 340, blend normal` | [Burst](../battle-craft.md#burst), [Anticipation](../battle-craft.md#anticipation) |
| 480 | 360 | windup | the effect | The breath drawn: 16 blue-green sparks (*house*) on a wide ring around a1 drawn onto it by 840 ms. Wraithguard's are soft dots; these are sharp. At 12 and 0.25 none was seen on the first film. `gather at 480, on caster, count 16, shape spark, tint family, reach 300, strength 0.6, lasts 360, blend add` | [Gravity well and convergence](../battle-craft.md#gravity-well-and-convergence), [Anticipation](../battle-craft.md#anticipation) |
| 840 | 900 | impact | the effect | The `spirits` motif on each target, gone by 1740 ms. `motif at 840, lasts 900` | [Shape before colour](../battle-craft.md#shape-before-colour), [The three parts of an effect](../battle-craft.md#the-three-parts-of-an-effect) |
| 840 | 600 | impact | the caster and the targets | The release ([movement.md](movement.md)); each foe's hit from a distance by the 4 health it lost: its bone flash, a short stop, a recoil away from a1. The scene draws a1's wound. | [Hit flash](../battle-craft.md#hit-flash), [Recoil](../battle-craft.md#recoil) |
| 840 | 300 | impact | the camera | The family's one staged shake, over the trauma of three hits, decaying over about 300 ms (*house*). `shake at 840, strength 0.3` | [Shake from trauma](../battle-craft.md#shake-from-trauma) |
| 840 | 280 | impact | the effect | The cry's light, the family's brightest: a hard blue-green light on each target, gone by 1120 ms, still the family's brightest. At 260 and 0.75 it washed b1 white to the head at 960 and 1020 ms on the first film, after the flash. `glow at 840, on target, tint family, reach 200, strength 0.55, lasts 280, blend add` | [Glow](../battle-craft.md#glow), [Additive light](../battle-craft.md#additive-light) |
| 840 | 480 | impact | the ground | The scream going through, the signature's first half: a blue-green arc opens on the ground on each target's far side from a1, gone by 1320 ms. At 320 and 140° the three arcs ran into one another and read as one line across the row on the first film. `glow at 840, on target, shape ring, tint family, direction along, spread 90, reach 180, strength 0.7, lasts 480, blend screen` | [Glow](../battle-craft.md#glow), [An impact that keeps the direction](../battle-craft.md#an-impact-that-keeps-the-direction) |
| 900 | 420 | impact | the effect | The voices carried on, the signature's second half: 14 blue-green sparks (*house*) thrown on beyond each target along the line from a1 in a 50° fan, past its body, gone by 1320 ms. Started after the bone fill. `burst at 900, on target, count 14, shape spark, tint family, direction along, spread 50, reach 400, strength 0.3, lasts 420, blend add` | [Burst](../battle-craft.md#burst), [An impact that keeps the direction](../battle-craft.md#an-impact-that-keeps-the-direction) |
| 1100 | 900 | impact | the effect | The Bleed: 8 deep red drops (*house*) alive at once seeping off each target and staying on it, gone by 2000 ms. Infestation's look, two more drops for the deeper Bleed. `flow at 1100, on target, count 8, shape dot, tint blood, direction out, spread 360, reach 110, strength 0.7, lasts 900, blend normal` | [Flow](../battle-craft.md#flow), [One effect, one meaning](../battle-craft.md#one-effect-one-meaning) |
| 1500 | 500 | impact | the effect | The price felt: a deep red light on a1 at home, gone by 2000 ms. `glow at 1500, on caster, tint blood, reach 160, strength 0.4, lasts 500, blend screen` | [Glow](../battle-craft.md#glow), [A light that answers a cast](../battle-craft.md#a-light-that-answers-a-cast) |
| 2000 | 540 | impact | the field | Nothing new. Every figure home, breathing. The result is read here; the hold from 2400 ms is empty. | [The hold before the number](../battle-craft.md#the-hold-before-the-number), [Breath at rest](../battle-craft.md#breath-at-rest) |

Ten staged beats, three in the windup; 22 particles asked for on each target (14, 8) and 30 on a1 in the windup, all
gone by 840 ms; 42 on the field at most (at 900 ms, 14 on each of three); four glows (one an arc on the ground), no
pulse, a shake of 0.3, the last gone by 2000 ms: inside Stage III. One flash a figure, the body's, on b1, b2 and b3.
Three changes of light: red on a1 before, blue-green on the three at contact, red on a1 at home.

### Reduced motion

Nothing. The frame after the action is shown at once with the result text: no blood, no scream, no shake. The Bleeds
are the status chips' to say.

### Cost

- In the windup: one glow image and two emitters on a1, gone by 840 ms.
- Objects at the peak (900 to 1120 ms): on each target one `Graphics` for the motif, the light's image, the arc's
  `Graphics` and one emitter; the scene's wound on a1; the bodies' tweens; about fifteen draw calls.
- Particles at the peak: 42 at 900 ms; of the field's 150.
- Bytes: not measured. Built by BC4 (motif, burst, flow, gather, glow, shake, the `staging` field), BC5 (the cast's
  body, the hit, the trauma, the wound) and BC6 (the arc of `glow`, `along`, the `blood` tint); BC8 places the beats.
  The cancel path is the kit's.

### On the film strip

```bash
node scripts/battle/filmstrip.mjs spell:crazed_specter:v1 --actor a1 --targets b1,b2,b3 --damage 4 --wound --out <folder>/death-wail.png
node scripts/battle/filmstrip.mjs spell:crazed_specter:v1 --actor a1 --targets b1,b2,b3 --damage 4 --wound \
  --frames 24 --interval 60 --columns 6 --out <folder>/death-wail-contact.png
```

- 0 and 60 ms (contact): no arc or mark on a1.
- 180 to 420 ms: a deep red light on a1, not gold; 420 to 600 ms: deep red drops above a1's back; none on b1, b2 or
  b3.
- 600 to 720 ms: sharp pale blue-green sparks, each seen, round a1 closing on its body.
- 840 and 900 ms: b1, b2 and b3 filled flat in bone, in these two frames and no other; a hard blue-green light on
  each. 960 ms: each target's head and outline read through the light.
- 960 to 1260 ms: on each target's far side from a1, a blue-green arc on the ground, three arcs apart, none reaching
  a neighbour's feet; blue-green sparks flying on past its outline; none on a1's side. The spirits motif on each. Red strokes on a1 (the scene's wound).
- 1200 to 1380 ms: a few red drops close on each target's outline, each drop told apart from the body; no spark
  left by 1380 ms.
- 1440 and 1680 ms (whole): red drops on each target, a faint red light on a1. 1920 ms (whole): a last drop at most.
- 2160 ms (whole) and every frame after: no effect object on the field. a2 and a3 are not lit or marked. Nothing in
  the effect is gold.
- Set beside Infestation's sheet at 1080 ms: Death Wail's sparks fly on beyond each foe; Infestation's dots close in
  on each body.

The sheets cannot show the shake, the stop, the feel at full speed, nor the sound.

### Left

- The scream goes on beyond each foe, along its own line from a1: three arcs, not one wave crossing the three. One
  wave through all three is a chain between targets, which the kit does not have.
- The windup's sharp sparks drawn onto a1 say the breath drawn in; a person may read them as the dead called, as
  Wraithguard's dots are. They are told apart by shape only.
- The red light on a1 at home starts at 1500 ms, after the scene's wound has faded (about 1440 ms on the film), so the
  price is seen twice; the owner may want only the wound, and then the staging still holds nine beats.
