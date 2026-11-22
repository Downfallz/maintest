# Storyboards: the astral family

The Shaman's line of the Sorcerer, drawn on bodies that hold more than space allows: vertical rings of vitality rising
on the one it mends (the `vital` motif), and a contracting aperture in space, dark at its heart (the `void` motif)
([the vocabulary of the family](../spell-vfx.md)), on the family's pale violet (0xb9a6e5). Its five spells climb
[the ladder](README.md#the-ladder): Stage I (Revitalize), Stage II (Vital Echo, Whisper), Stage III (Renewal Burst,
Void Pulse). All five are casts: the caster loads and lets go where it stands ([movement.md](movement.md)), and a
storyboard here names that in one row per window and spends its rows on the spell. Four of the five mend allies:
nothing shakes and nothing is struck on them. The fifth, Void Pulse, wounds three foes and must look like nothing else
in the family.

The family keeps one meaning per look, so a player learns it once ([One effect, one
meaning](../battle-craft.md#one-effect-one-meaning)). The `vital` rings carry the violet and stay the primary element of
the four heals; they are told apart by shape and rhythm first, then by count.

- A string of violet motes travelling from the caster to an ally is the pulse sent: who gave, who received
  (Revitalize, Vital Echo, Renewal Burst). Whisper sends nothing: its whispers are already everywhere.
- One strong rise of round violet motes off a figure, at once, is health given now (Revitalize, Vital Echo, Renewal
  Burst). The more it heals, the more motes.
- Regeneration, health that comes back each round, has one look and keeps it in both spells that give it (Vital Echo,
  Whisper): late, after any rise has gone, a faint violet ring lies on the ground at the figure's feet and a slow,
  thin column of small motes climbs from it. It is the echo: what returns, smaller and later.
- The one halo on a figure's outline (`pulse`) says it was healed, as in the physical family (Vital Surge) and the
  biological (Devour): it is Renewal Burst's, the game's biggest heal, and no other astral spell's.
- Violet sparks, pointed and quick, rising slowly off a figure are energy it gained, as Overdrive's are: Renewal
  Burst's Energy +3. A heal that gives no energy throws no spark.
- Pieces drawn in onto a foe, never thrown out of it, are the void closing on it (Void Pulse): the only astral pieces
  that move inward on a target. Its shards are dark-blended (`normal`), where every heal's motes are added light.
- Blood that stays on the struck figure is its Bleed, as in the physical and biological families (Void Pulse). No
  heal shows blood.

The pass (2026-11-19) ran two rounds. The designer read the first films of the five against these storyboards and
asked for 14 changes, with three main causes. The trails were too short: a trail's motes live half its length, so at
200 or 280 ms not one mote showed on a frame. Gathers and flows were too faint or too few to count. A rise of round
motes, add-blended, piled into one white cloud on the figure. Round 2 read the second films. Void Pulse and Whisper
play as written. The trails, lengthened to 360 and 400 ms, still show only one or two motes by the target, never a
line. The first rise still turns white on its first frame. Renewal Burst's sparks and halo cannot be picked out. The
designer read those films; the data was not changed after them. The coded rows below are the stagings as built, and
what is still to change is under each spell's **Left**, with its numbers.

## Revitalize (`spell:rejuvenate:v1`)

### Intent

The player reads "the Warped sent a pulse of life into that ally": one quick, directed pulse that lands and rises,
nothing more. Revitalize heals one ally of 6. Stage I, a staple of the Shaman's early rounds, so it stays short and
small: no windup beat, no light. Its signature is the arrival: a short fan of violet motes thrown from a1 toward a2 at
contact, a string of motes that reaches a2 at 1200 ms, and the rings and one strong rise of motes standing up on a2
together.

### Today

Filmed at `fc3ec94`, 390 by 844, 1×, a1 on a2: a whole action and a contact.

- 0 ms: the shared faint violet spiral on a1's body; a1 barely moves through the windup; a2 does not move on any frame.
- 960 to 1440 ms: the `vital` motif on a2, small pale violet rings stacked upright on its body, fading; gone by
  1680 ms.
- Nothing travels from a1 to a2, nothing rises, nothing lights: the same sheet as Vital Echo's and Renewal Burst's.

### Beats

| At | Lasts | Window | Layer | Beat | Pattern |
| --- | --- | --- | --- | --- | --- |
| 0 | 840 | windup | the caster | A cast's load where it stands, then the rise ([movement.md](movement.md), `CAST`). No shared spiral: the staging replaces it. | [Anticipation](../battle-craft.md#anticipation), [Squash and stretch](../battle-craft.md#squash-and-stretch) |
| 840 | 240 | impact | the effect | The pulse leaves: 6 violet motes (*house*) thrown from a1 along the line to a2 in a narrow fan, past a1's outline, gone by 1080 ms. Along, never up: a rise off a1 would say a1 was healed. `burst at 840, on caster, count 6, shape dot, tint family, direction along, spread 40, reach 220, strength 0.4, lasts 240, blend add` | [Burst](../battle-craft.md#burst), [Contact and follow-through](../battle-craft.md#contact-and-follow-through) |
| 840 | 360 | impact | the effect | The pulse sent, the signature's first half: a head leaves a1 at the release and reaches a2 at 1200 ms, leaving 8 violet motes (*house*) on its path. 360 ms, not 200: a trail's motes live half its length, and at 200 ms none was on a frame. `trail at 840, count 8, shape dot, tint family, strength 0.5, lasts 360, blend add` | [Trail](../battle-craft.md#trail), [Easing families](../battle-craft.md#easing-families) |
| 840 | 360 | impact | the caster | The release, as a cast lets go ([movement.md](movement.md)). a2 lost nothing: no flash, no recoil. | [Contact and follow-through](../battle-craft.md#contact-and-follow-through) |
| 1200 | 500 | impact | the effect | The `vital` motif on a2, as the pulse lands; gone by 1700 ms, Stage I's end. `motif at 1200, lasts 500` | [Shape before colour](../battle-craft.md#shape-before-colour), [The three parts of an effect](../battle-craft.md#the-three-parts-of-an-effect) |
| 1200 | 500 | impact | the effect | The pulse lands, the signature's second half: one strong rise of 14 round violet motes (*house*) off a2 at once, a 70° column so they do not pile in one cloud, slowing as they climb; gone by 1700 ms. `burst at 1200, on target, count 14, shape dot, tint family, direction up, spread 70, reach 300, strength 0.4, lasts 500, blend add` | [Burst](../battle-craft.md#burst), [Additive light](../battle-craft.md#additive-light) |
| 1700 | 840 | impact | the field | Nothing new. Both figures home, breathing. The result is read here. | [The hold before the number](../battle-craft.md#the-hold-before-the-number), [Breath at rest](../battle-craft.md#breath-at-rest) |

Four staged beats, the motif among them; 22 particles asked for on a2 (8 on the trail, 14) and 6 on a1, gone by
1080 ms; no windup beat, no glow, no pulse, no shake; the last gone by 1700 ms: inside Stage I. No flash, no change of
light.

### Reduced motion

Nothing. The frame after the action is shown at once with the result text: no trail, no rise, no rings.

### Cost

- At contact (840 to 1080 ms): two emitters (the fan on a1, the trail); two draw calls.
- Objects at the peak (1200 to 1380 ms): the motif's `Graphics`, one emitter (the rise), the trail's last motes; about
  three draw calls.
- Particles at the peak: 14 at 1200 ms, with the trail's last few; of the field's 150.
- Bytes: not measured. Built by BC4 (motif, burst, trail, the `staging` field) and BC5 (the cast's body); BC8 places
  the beats. The cancel path is the kit's: every emitter dies with the action.

### On the film strip

```bash
node scripts/battle/filmstrip.mjs spell:rejuvenate:v1 --actor a1 --targets a2 --out <folder>/revitalize.png
node scripts/battle/filmstrip.mjs spell:rejuvenate:v1 --actor a1 --targets a2 \
  --frames 24 --interval 60 --columns 6 --out <folder>/revitalize-contact.png
```

- 0 to 780 ms (contact): no spiral, mote or light on a1 or a2; a1 squats and rises.
- 900 to 1020 ms (contact): a small bright white spot on a1's front edge, toward a2, with a mote or two; none above
  a1's head.
- 1080 and 1140 ms (contact): nothing between a1 and a2; nothing on a2 yet.
- 1200 ms (contact and whole): one or two motes just short of a2, on a1's side; nothing on a2 yet.
- 1260 ms (contact): a small white cloud on a2's back. 1320 and 1380 ms: the rings on a2 and a cluster of motes above
  its back, white at the core, none sideways onto a1 or a3.
- 1440 ms (whole): the rings on a2, motes above its head, thinning. 1680 ms (whole): nothing left.
- 1920 ms (whole) and every frame after: no effect object on the field. a2 never filled, lit or moved. Nothing in the
  effect is gold.

The sheets cannot show the feel of the pulse at full speed, nor the sound.

### Left

- The pulse travels in the impact, 840 to 1200 ms, because Stage I may not spend the windup: the motif waits for it,
  so a2 shows nothing for 360 ms after the release. The owner may prefer the motif at 840; then the pulse arrives on
  rings already standing. The trail is 360 ms because the kit gives a trail's motes half its length to live: at
  200 ms the first film showed no line at all.
- The fan off a1 (the session's fourth beat, Stage I's floor) is thrown along the line to a2, not up: the session's
  rise off a1 piled into a white blot on its back, and a rise off a figure is the family's "health given now".
- The trail is the family's "sent from a1 to a2", shared with Vital Echo and Renewal Burst: the three are told apart by
  what lands, not by what travels.
- For a later pass. The trail did not play: at 360 ms and 8 motes no line showed on any frame, only one or two motes
  by a2 at 1200 ms. Built since (#947): every trail's head goes at an even pace and its motes live a fixed 240 ms.
  If the line is still faint on the next film, ask 14 motes at strength 0.7.
- For a later pass. The fan off a1 still shows as a white spot at 900 to 1020 ms: its 6 motes leave together and add
  up. Ask `blend screen` and strength 0.3.
- For a later pass. The rise turns white on its first frame (1260 ms): 14 add-blended motes start on one point. Ask
  `blend screen`, or strength 0.3 and spread 90.

## Vital Echo (`spell:healing_screech:v1`)

### Intent

The player reads "the Ethereal's spectral voice reached that ally, healed it a little, and the sound keeps coming
back": a heal, then its echo. Vital Echo heals one ally of 2 and gives it Regeneration 4 a round for two rounds. Stage
II: one windup beat, a light. Its signature is the repeat: a first rise at 1200 ms, smaller than Revitalize's, then,
after it has gone, the family's regeneration (a faint ring at a2's feet and a slow thin column from it) as its second,
smaller wave.

### Today

Filmed at `fc3ec94`, 390 by 844, 1×, a1 on a2: a whole action and a contact.

- 0 ms: the shared faint violet spiral on a1; a1 barely moves; a2 does not move on any frame.
- 960 to 1440 ms: the `vital` motif on a2, pale violet rings, fading; gone by 1680 ms.
- Revitalize's sheet frame for frame: nothing says "echo", nothing says "again".

### Beats

| At | Lasts | Window | Layer | Beat | Pattern |
| --- | --- | --- | --- | --- | --- |
| 0 | 840 | windup | the caster | A cast's load where it stands, then the rise ([movement.md](movement.md), `CAST`). No shared spiral. | [Anticipation](../battle-craft.md#anticipation), [Squash and stretch](../battle-craft.md#squash-and-stretch) |
| 360 | 400 | windup | the effect | The voice gathers, the windup's one beat: 10 violet motes (*house*) on a ring around a1 drawn onto its body by 760 ms. `gather at 360, on caster, count 10, shape dot, tint family, reach 240, strength 0.45, lasts 400, blend add` | [Gravity well and convergence](../battle-craft.md#gravity-well-and-convergence), [Anticipation](../battle-craft.md#anticipation) |
| 840 | 360 | impact | the effect | The voice sent: a head leaves a1 and reaches a2 at 1200 ms, leaving 8 violet motes (*house*) on its path; Revitalize's length, so the line is on a frame. `trail at 840, count 8, shape dot, tint family, strength 0.5, lasts 360, blend add` | [Trail](../battle-craft.md#trail), [Easing families](../battle-craft.md#easing-families) |
| 840 | 360 | impact | the caster | The release, as a cast lets go ([movement.md](movement.md)). a2 lost nothing: no flash, no recoil. | [Contact and follow-through](../battle-craft.md#contact-and-follow-through) |
| 1200 | 800 | impact | the effect | The `vital` motif on a2, as the voice lands; gone by 2000 ms, so the rings stand through the echo. `motif at 1200, lasts 800` | [Shape before colour](../battle-craft.md#shape-before-colour), [The three parts of an effect](../battle-craft.md#the-three-parts-of-an-effect) |
| 1200 | 320 | impact | the effect | The voice heard, the impact's light: a soft violet light on a2, gone by 1520 ms. `glow at 1200, on target, tint family, reach 220, strength 0.45, lasts 320, blend screen` | [Glow](../battle-craft.md#glow), [A light that answers a cast](../battle-craft.md#a-light-that-answers-a-cast) |
| 1200 | 360 | impact | the effect | The heal now, the first wave: 10 round violet motes (*house*) rise off a2 at once, fewer and shorter than Revitalize's, a 70° column so they do not pile in one cloud; gone by 1560 ms. `burst at 1200, on target, count 10, shape dot, tint family, direction up, spread 70, reach 260, strength 0.4, lasts 360, blend add` | [Burst](../battle-craft.md#burst), [Additive light](../battle-craft.md#additive-light) |
| 1560 | 440 | impact | the ground | The echo, regeneration's ring: a violet ring lies on the ground at a2's feet, gone by 2000 ms. `glow at 1560, on target, shape ring, tint family, reach 200, strength 0.55, lasts 440, blend screen` | [Glow](../battle-craft.md#glow), [A beat on the ground](../battle-craft.md#a-beat-on-the-ground) |
| 1560 | 440 | impact | the effect | The echo, the signature's second wave: 12 small motes alive at once climb slowly from a2's feet in a thin column, gone by 2000 ms. `flow at 1560, on target, count 12, shape dot, tint family, direction up, spread 30, reach 200, strength 0.55, lasts 440, blend add, level feet` | [Flow](../battle-craft.md#flow), [One effect, one meaning](../battle-craft.md#one-effect-one-meaning) |
| 2000 | 540 | impact | the field | Nothing new. Both figures home, breathing. The result is read here. | [The hold before the number](../battle-craft.md#the-hold-before-the-number), [Breath at rest](../battle-craft.md#breath-at-rest) |

Seven staged beats, one in the windup; 30 particles asked for on a2 (8, 10, 12) and 10 on a1, gone by 760 ms; two
glows, no pulse, no shake; the last gone by 2000 ms: inside Stage II. No flash. Two changes of light on a2: the voice
at 1200 ms and the echo's ring at 1560 ms, the second on the ground.

### Reduced motion

Nothing. The frame after the action is shown at once with the result text: no voice, no rise, no echo. The
regeneration is the status chip's to say.

### Cost

- In the windup: one emitter on a1, gone by 760 ms.
- Objects at the peak (1560 to 2000 ms): the motif's `Graphics`, the ring's image, one emitter (the column); about
  three draw calls. At 1200 ms: the motif, the light's image, one emitter.
- Particles at the peak: 10 at 1200 ms, with the trail's last few; 12 from 1560 ms; of the field's 150.
- Bytes: not measured. Built by BC4 (motif, burst, flow, gather, glow, trail, the `staging` field), BC5 (the cast's
  body), and BC6's follow-ups (`level`, the ring on the ground); BC8 places the beats. The cancel path is the kit's.

### On the film strip

```bash
node scripts/battle/filmstrip.mjs spell:healing_screech:v1 --actor a1 --targets a2 --out <folder>/vital-echo.png
node scripts/battle/filmstrip.mjs spell:healing_screech:v1 --actor a1 --targets a2 \
  --frames 24 --interval 60 --columns 6 --out <folder>/vital-echo-contact.png
```

- 0 to 360 ms (contact): no spiral or mote on a1.
- 420 to 660 ms (contact): violet motes round a1, above its back and on its body, closing in; a mote or two at 720
  and 780 ms; nothing on a2.
- 840 to 1140 ms (contact): a last mote on a1's body; nothing between a1 and a2.
- 1200 ms (contact and whole): one or two motes just short of a2, on a1's side; nothing on a2 yet.
- 1260 ms (contact): a small white cloud on a2's back. 1320 and 1380 ms: the rings on a2 and separate motes above
  its back, about Revitalize's height at the same time.
- 1440 ms (whole): the rings on a2, a few motes above its head.
- 1680 ms (whole): a faint violet ring flat at a2's feet, on its gold ring, not hiding it; two or three small motes on
  it, at the feet; the rings faint.
- 1920 ms (whole) and every frame after: no effect object. a1, a3 and the foes never lit or marked. Nothing in the
  effect is gold.
- Set beside Revitalize's sheet at 1680 ms: Revitalize shows nothing; Vital Echo shows the ring and its motes at the
  feet.

The sheets cannot show the rhythm of the echo at full speed, nor the sound, which is where a screech belongs.

### Left

- Regeneration is a ring and a column at the feet, after the heal: the owner may read it as a second heal. The look is
  kept the same in Whisper, so it is learnt as "comes back", and the status chip names it.
- The "spectral" of the approved execution is carried by the windup gather alone: the kit has no way to make the trail
  or the rings ghostly (no blur, no alpha flicker on a beat). Asking for it is a pattern the craft book's
  [Blur](../battle-craft.md#blur) would have to grow into a kit word first.
- For a later pass. The trail does not play, as in Revitalize: ask 14 motes at strength 0.7, the same as Revitalize's,
  so the two still travel alike.
- For a later pass. The first rise turns white at 1260 ms and stands as high as Revitalize's: ask 8 motes, reach 200,
  `blend screen`, so it is plainly the smaller heal.
- For a later pass. The echo's column does not climb: at 1680 ms two or three motes sit at the feet. Ask reach 280
  and strength 0.7, so more than three motes are seen rising inside a2's outline.

## Whisper (`spell:soothing_chant:v1`)

### Intent

The player reads "the Ethereal murmured, and all three of them will mend each round": soft, low, everywhere at once.
Whisper gives up to three allies Regeneration 3 a round for two rounds; it heals nothing now. Stage II: one windup beat,
one light at least. Its signature is the hush: no trail and no rise, only a low violet mist stirring at each ally's
feet, then the family's regeneration (the ring and the slow column) on all three together. Vital Echo sends a voice
to one; Whisper sends nothing and is already there.

### Today

Filmed at `fc3ec94`, 390 by 844, 1×, a1 on a1, a2 and a3: a whole action and a contact.

- 0 ms: the shared faint violet spiral on a1.
- 960 to 1440 ms: the `vital` motif on each of the three, the rings larger on a1 and a3, fading; gone by 1680 ms.
- Nothing low, nothing soft, nothing that comes back: a heal's sheet on three.

### Beats

| At | Lasts | Window | Layer | Beat | Pattern |
| --- | --- | --- | --- | --- | --- |
| 0 | 840 | windup | the caster | A cast's load where it stands, then the rise ([movement.md](movement.md), `CAST`). No shared spiral. | [Anticipation](../battle-craft.md#anticipation), [Squash and stretch](../battle-craft.md#squash-and-stretch) |
| 360 | 400 | windup | the effect | The breath drawn, the windup's one beat: a faint violet light on a1, gone by 760 ms. `glow at 360, on caster, tint family, reach 170, strength 0.3, lasts 400, blend screen` | [Glow](../battle-craft.md#glow), [Anticipation](../battle-craft.md#anticipation) |
| 840 | 360 | impact | the caster | The release, as a cast lets go ([movement.md](movement.md)). No one lost anything: no flash, no recoil. | [Contact and follow-through](../battle-craft.md#contact-and-follow-through) |
| 840 | 1000 | impact | the effect | The `vital` motif on each of the three, gone by 1840 ms. `motif at 840, lasts 1000` | [Shape before colour](../battle-craft.md#shape-before-colour), [The three parts of an effect](../battle-craft.md#the-three-parts-of-an-effect) |
| 840 | 600 | impact | the effect | The murmur, the signature: 8 violet motes alive at once drift out low and slow around each ally's feet, a mist on the ground, gone by 1440 ms. No burst. `flow at 840, on target, count 8, shape dot, tint family, direction out, spread 360, reach 140, strength 0.55, lasts 600, blend screen, level feet` | [Flow](../battle-craft.md#flow), [A beat on the ground](../battle-craft.md#a-beat-on-the-ground) |
| 900 | 500 | impact | the effect | The murmur heard: a faint violet light on each ally, faint so three do not light the field; gone by 1400 ms. `glow at 900, on target, tint family, reach 180, strength 0.25, lasts 500, blend screen` | [Glow](../battle-craft.md#glow), [A light that answers a cast](../battle-craft.md#a-light-that-answers-a-cast) |
| 1300 | 500 | impact | the ground | Regeneration's ring, Vital Echo's: a faint violet ring on the ground at each ally's feet, gone by 1800 ms. `glow at 1300, on target, shape ring, tint family, reach 200, strength 0.35, lasts 500, blend screen` | [Glow](../battle-craft.md#glow), [A beat on the ground](../battle-craft.md#a-beat-on-the-ground) |
| 1300 | 540 | impact | the effect | Regeneration's column, Vital Echo's with fewer motes: 8 small motes alive at once climb slowly from each ally's feet, gone by 1840 ms. `flow at 1300, on target, count 8, shape dot, tint family, direction up, spread 30, reach 200, strength 0.55, lasts 540, blend add, level feet` | [Flow](../battle-craft.md#flow), [One effect, one meaning](../battle-craft.md#one-effect-one-meaning) |
| 1840 | 700 | impact | the field | Nothing new. Every figure home, breathing. The result is read here. | [The hold before the number](../battle-craft.md#the-hold-before-the-number), [Breath at rest](../battle-craft.md#breath-at-rest) |

Six staged beats, one in the windup; 16 particles asked for on each ally (8, 8) and none on a1 as caster; at most 48 on
the field (at 1300 to 1440 ms, 8 and 8 on each of three), well under the 150; three glows, no pulse, no shake; the last
gone by 1840 ms: inside Stage II. No flash. Two changes of light, both faint: a1 in the windup, the three at 900 ms.

### Reduced motion

Nothing. The frame after the action is shown at once with the result text. The regeneration is the status chips'.

### Cost

- In the windup: one glow image on a1, gone by 760 ms.
- Objects at the peak (1300 to 1440 ms): on each ally the motif's `Graphics`, the ring's image, two emitters (the
  mist, the column); about twelve draw calls for 140 ms, the screen objects batched.
- Particles at the peak: 48 at 1300 ms; of the field's 150.
- Bytes: not measured. Built by BC4 (motif, flow, glow, the `staging` field), BC5 (the cast's body), and BC6's
  follow-ups (`level`, the ring on the ground); BC8 places the beats. The cancel path is the kit's.

### On the film strip

```bash
node scripts/battle/filmstrip.mjs spell:soothing_chant:v1 --actor a1 --targets a1,a2,a3 --out <folder>/whisper.png
node scripts/battle/filmstrip.mjs spell:soothing_chant:v1 --actor a1 --targets a1,a2,a3 \
  --frames 24 --interval 60 --columns 6 --out <folder>/whisper-contact.png
```

- 0 to 360 ms (contact): no spiral on a1. 420 to 660 ms: a faint violet tint on a1's body, gone at 720 ms.
- 840 to 1380 ms (contact): no line between figures; no mote rising above any head. From 900 ms the rings on a1, a2
  and a3; from 960 ms violet motes low at the feet of each, two to four at each, under their bellies; a faint light
  on each, no white anywhere.
- 1440 ms (whole): a faint violet ring flat at a1's and a3's feet, a2's on its gold ring, none hiding it; two or three
  small motes at each one's feet.
- 1680 ms (whole): the rings at the feet, a3's the widest and clearest; a mote or two on each, low; the upright rings
  gone.
- 1920 ms (whole) and every frame after: no effect object on the field. No foe lit, moved or marked. Nothing in the
  effect is gold.
- Set beside Vital Echo's sheet at 1680 ms: the same ring and column, on three; at 1320 ms Vital Echo's motes rise
  above a2's head and Whisper's stay at the feet.

The sheets cannot show the softness at full speed, nor the sound, where the whispers live.

### Left

- The regeneration starts at 1300 ms here and at 1560 ms in Vital Echo: the same look, later where a heal comes first.
  The owner may want them on one clock; then Vital Echo's echo crowds its heal.
- The low mist is a `flow` out at the feet, the kit's nearest to a murmur: no beat can draw a sound wave. The owner
  may find it reads as dust (physical's Pummel); then it goes to the screen-blended motes' strength before it goes.
- The caster is also a recipient: its light in the windup and its share at contact are on the same body.
- For a later pass. The regeneration's column does not climb here either: two or three motes stay at each one's feet
  at 1440 and 1680 ms. Ask what Vital Echo's asks, reach 280 and strength 0.7, so the two keep one look.

## Renewal Burst (`spell:restorative_burst:v1`)

### Intent

The player reads "the Transcendent poured everything it had into that ally: it is whole again, and charged": the
biggest heal in the game, seen a few times a game. Renewal Burst heals one ally of 14 and gives it 3 energy. Stage III:
a windup that charges, two lights, and the family's one halo. Its signature is the burst: concentrated light drawn
into a1 and sent, then on a2 a ring of motes thrown out on every side at once, the halo on its outline, and violet
sparks rising slowly after: healed, then charged. Revitalize rises; Renewal Burst bursts, then rises.

### Today

Filmed at `fc3ec94`, 390 by 844, 1×, a1 on a2: a whole action and a contact.

- 0 ms: the shared faint violet spiral on a1; a1 barely moves; a2 does not move on any frame.
- 960 to 1440 ms: the `vital` motif on a2, pale rings, fading; gone by 1680 ms.
- Revitalize's sheet: the game's largest heal looks like its smallest.

### Beats

| At | Lasts | Window | Layer | Beat | Pattern |
| --- | --- | --- | --- | --- | --- |
| 0 | 840 | windup | the caster | A cast's load where it stands, then the rise ([movement.md](movement.md), `CAST`). No shared spiral. | [Anticipation](../battle-craft.md#anticipation), [Squash and stretch](../battle-craft.md#squash-and-stretch) |
| 120 | 440 | windup | the effect | Concentration: 14 violet motes (*house*) on a wide ring around a1 drawn onto its body by 560 ms. `gather at 120, on caster, count 14, shape dot, tint family, reach 260, strength 0.45, lasts 440, blend add` | [Gravity well and convergence](../battle-craft.md#gravity-well-and-convergence), [Anticipation](../battle-craft.md#anticipation) |
| 360 | 400 | windup | the effect | a1 full: a violet light swells on it and goes by 760 ms. `glow at 360, on caster, tint family, reach 180, strength 0.55, lasts 400, blend screen` | [Glow](../battle-craft.md#glow), [Anticipation](../battle-craft.md#anticipation) |
| 440 | 400 | windup | the effect | The vitality sent: a head leaves a1 and reaches a2 at 840 ms, leaving 16 violet motes (*house*) on its path; the release meets it. 400 ms, not 280: a trail's motes live half its length, and at 280 ms none left a1 on a frame. `trail at 440, count 16, shape dot, tint family, strength 0.6, lasts 400, blend add` | [Trail](../battle-craft.md#trail), [Easing families](../battle-craft.md#easing-families) |
| 840 | 360 | impact | the caster | The release, as a cast lets go ([movement.md](movement.md)). a2 lost nothing: no flash, no recoil. | [Contact and follow-through](../battle-craft.md#contact-and-follow-through) |
| 840 | 1100 | impact | the effect | The `vital` motif on a2, gone by 1940 ms. `motif at 840, lasts 1100` | [Shape before colour](../battle-craft.md#shape-before-colour), [The three parts of an effect](../battle-craft.md#the-three-parts-of-an-effect) |
| 840 | 440 | impact | the effect | The burst's light, the impact's one change of light: a wide violet light on a2, screen-blended so its middle does not burn white over a2's body; gone by 1280 ms. `glow at 840, on target, tint family, reach 280, strength 0.45, lasts 440, blend screen` | [Glow](../battle-craft.md#glow), [A light that answers a cast](../battle-craft.md#a-light-that-answers-a-cast) |
| 840 | 420 | impact | the effect | The burst, the signature: 18 round violet motes (*house*) thrown out on every side of a2 at once, well past its outline; gone by 1260 ms. `burst at 840, on target, count 18, shape dot, tint family, direction out, spread 360, reach 380, strength 0.5, lasts 420, blend screen` | [Burst](../battle-craft.md#burst), [The three parts of an effect](../battle-craft.md#the-three-parts-of-an-effect) |
| 880 | 560 | impact | the effect | Health given now, the family's strong rise: 14 round violet motes (*house*) up off a2 in a 50° column; gone by 1440 ms. `burst at 880, on target, count 14, shape dot, tint family, direction up, spread 50, reach 320, strength 0.5, lasts 560, blend add` | [Burst](../battle-craft.md#burst), [One effect, one meaning](../battle-craft.md#one-effect-one-meaning) |
| 900 | 700 | impact | the target | The family's one halo: a violet halo on a2's own outline swells and goes, "healed"; gone by 1600 ms. `pulse at 900, on target, tint family, strength 0.5, lasts 700` | [Glow](../battle-craft.md#glow), [One hero shader](../battle-craft.md#one-hero-shader) |
| 1000 | 900 | impact | the effect | Energy +3: 12 violet sparks (*house*) alive at once climb slowly off a2, pointed and thin, gone by 1900 ms. Overdrive's reading: energy gained. `flow at 1000, on target, count 12, shape spark, tint family, direction up, spread 40, reach 280, strength 0.45, lasts 900, blend add` | [Flow](../battle-craft.md#flow), [One effect, one meaning](../battle-craft.md#one-effect-one-meaning) |
| 1940 | 600 | impact | the field | Nothing new. Both figures home, breathing. The result is read here; the hold from 2400 ms is empty. | [The hold before the number](../battle-craft.md#the-hold-before-the-number), [Breath at rest](../battle-craft.md#breath-at-rest) |

Nine staged beats, three in the windup; 60 particles asked for on a2 (16, 18, 14, 12) and 14 on a1, gone by 560 ms;
two glows and the one pulse, on a2, the one target; no shake; the last gone by 1940 ms: inside Stage III. No flash. Two
changes of light: a1 in the windup, a2 at contact, the halo riding on the second.

### Reduced motion

Nothing. The frame after the action is shown at once with the result text: no charge, no burst, no halo, no spark.
The heal and the energy are the result text's.

### Cost

- In the windup: one emitter and one glow image on a1, gone by 760 ms; the trail's emitter from 440 to 840 ms.
- Objects at the peak (900 to 1260 ms): the motif's `Graphics`, the light's image, three emitters (the burst, the
  rise, the sparks), and the halo: a filter, a framebuffer and a pass each frame for 700 ms (the craft book, "Glow");
  about five draw calls and the filter's pass.
- Particles at the peak: about 44 at 1000 ms (18, 14, 12), with the trail's last few; of the field's 150.
- Bytes: not measured. Built by BC4 (motif, burst, flow, gather, glow, trail, the `staging` field), BC5 (the cast's
  body) and BC6 (the pulse, the gold rim quieted under it); BC8 places the beats. The cancel path is the kit's: the
  filter is removed and the rim given back when the action ends or is cancelled.

### On the film strip

```bash
node scripts/battle/filmstrip.mjs spell:restorative_burst:v1 --actor a1 --targets a2 --out <folder>/renewal-burst.png
node scripts/battle/filmstrip.mjs spell:restorative_burst:v1 --actor a1 --targets a2 \
  --frames 24 --interval 60 --columns 6 --out <folder>/renewal-burst-contact.png
```

- 0 to 120 ms (contact): no spiral on a1.
- 180 to 360 ms (contact): violet motes on a wide ring around a1, above its back, closing on its body; from 420 ms a
  violet light on a1, its body tinted, through 600 ms.
- 600 to 780 ms (contact): one or two bright motes on a1's front, toward a2; nothing between the two figures; the
  light on a1 going.
- 900 to 1080 ms (contact): a2 not filled in bone or any flat colour; motes on every side of a2, past its outline
  and below its feet; the rings standing; a bright white blot on a2's body at 960 ms and over its head at 1020 and
  1080 ms.
- 1140 to 1380 ms (contact): separate motes high over a2's head, thinning; no halo to pick out on its outline.
- 1200 ms (whole): the column high, the rings on a2; no spark to tell from the round motes.
- 1440 ms (whole): the rings faint; no spark, no halo, no round mote. 1680 ms (whole): nothing left.
- 2160 ms (whole) and every frame after: no effect object on the field; a2's gold rim back. a1, a3 and the foes never
  lit, moved or marked after 840 ms. Nothing in the effect is gold.
- Set beside Revitalize's sheet at 960 ms: Revitalize shows nothing yet on a2; Renewal Burst shows motes thrown out
  all round a2 under a bright light.

The sheets cannot show the swell of the halo at full speed, nor the sound.

### Left

- The family's one halo is spent here and not on Vital Echo or Revitalize, because Renewal Burst is the game's
  biggest heal and the only Stage III heal of the family. The owner may want a halo on every heal; then it stops
  meaning anything a player can count on.
- The trail runs in the windup, 440 to 840 ms, so the burst meets the release at contact, as Vital Surge's wave does.
  It shows the path to a2 before the impact: a2 is the host's confirmed recipient (ADR 0131), so this shows nothing
  undecided.
- The energy sparks are the family's violet, not the biological green: a spark rising is the reading, the colour is
  the family's.
- For a later pass. The trail does not play: at 400 ms and 16 motes nothing shows between a1 and a2. As in
  Revitalize, the kit's trail first, then strength 0.7.
- For a later pass. The rise piles into a white blot over a2's head at 960 to 1080 ms, on the screen-blended burst:
  ask the rise `blend screen`, strength 0.35, spread 70, and start it at 960 ms, after the outward burst has left.
- For a later pass. Energy +3 is not seen: no pointed spark at 1200 or 1440 ms. Ask the sparks' strength 0.7 and reach
  360, from 1100 ms, so they rise after the round motes have gone. The halo at strength 0.5 cannot be told on the
  sheet: ask 0.7, and judge it on the sheet at 1080 ms.

## Void Pulse (`spell:toxic_waves:v1`)

### Intent

The player reads "the Transcendent tore a hole in space under each of them, the space around them was pulled into it
twice, and they bleed": a wound by distortion, on up to three foes. Void Pulse deals 4 damage and Bleed 3 for two
rounds to up to three enemies. Stage III, a few times a game. Its signature is the inward pull: violet shards drawn in
from all round each foe onto its centre, twice, and nothing thrown out until the blood. Every other astral spell sends
light out or up; this one swallows it.

### Today

Filmed at `fc3ec94`, 390 by 844, 1×, a1 on b1, b2 and b3, 4 health lost each: a whole action and a contact.

- 0 to 180 ms: the shared faint violet spiral on a1's body. a1 barely moves through the windup.
- 840 and 900 ms: b1, b2 and b3 filled flat in bone, the `void` motif's dark disc already showing through.
- 960 to 1140 ms: on each target a dark disc inside several pale violet rings, ringed wider than the body on b1 and
  b3; contracting and fading through 1380 ms; gone by 1680 ms.
- Nothing is drawn in, nothing bleeds, no light: the aperture alone carries it, and it reads as rings, like the heals.

### Beats

| At | Lasts | Window | Layer | Beat | Pattern |
| --- | --- | --- | --- | --- | --- |
| 0 | 840 | windup | the caster | A cast's load where it stands, then the rise ([movement.md](movement.md), `CAST`). No shared spiral. | [Anticipation](../battle-craft.md#anticipation), [Squash and stretch](../battle-craft.md#squash-and-stretch) |
| 120 | 480 | windup | the effect | Space folds onto a1: 12 violet shards (*house*), dark-blended, on a wide ring around a1 drawn onto its body by 600 ms. The heals gather round motes; this gathers splinters. `gather at 120, on caster, count 12, shape shard, tint family, reach 260, strength 0.5, lasts 480, blend normal` | [Gravity well and convergence](../battle-craft.md#gravity-well-and-convergence), [Anticipation](../battle-craft.md#anticipation) |
| 200 | 640 | windup | the camera | The slow push, the capstone's: the view moves in on a1 while it gathers, slowly at both ends, 3% closer by the contact (*house*), holding a1 where it stands on the screen; it lets go over 240 ms as the blow lands. Not under less motion. `push at 200, strength 0.5, lasts 640` | [Slow push on a capstone](../battle-craft.md#slow-push-on-a-capstone) |
| 360 | 360 | windup | the effect | a1 holds the fold: a dim violet light on it, gone by 720 ms. `glow at 360, on caster, tint family, reach 170, strength 0.35, lasts 360, blend screen` | [Glow](../battle-craft.md#glow), [Anticipation](../battle-craft.md#anticipation) |
| 600 | 240 | windup | the effect | Space starts to pinch on each foe: 6 violet motes (*house*) from far round each target drawn toward its centre, gone at 840 ms. `gather at 600, on target, count 6, shape dot, tint family, reach 320, strength 0.55, lasts 240, blend normal` | [Gravity well and convergence](../battle-craft.md#gravity-well-and-convergence), [Anticipation](../battle-craft.md#anticipation) |
| 840 | 360 | impact | the caster | The release, as a cast lets go ([movement.md](movement.md)). | [Contact and follow-through](../battle-craft.md#contact-and-follow-through) |
| 840 | 900 | impact | the effect | The `void` motif, the aperture, on each target; gone by 1740 ms. `motif at 840, lasts 900` | [Shape before colour](../battle-craft.md#shape-before-colour), [The three parts of an effect](../battle-craft.md#the-three-parts-of-an-effect) |
| 840 | 590 | impact | the target | Each struck figure's medium hit from a distance ([movement.md](movement.md)): its bone flash for 80 ms, a short stop, a recoil away from a1, home before 1440 ms. All three on the contact frame. | [Hit flash](../battle-craft.md#hit-flash), [Recoil](../battle-craft.md#recoil) |
| 840 | 420 | impact | the effect | The first pull, the signature: 14 violet shards (*house*) start well outside each foe and are drawn onto its centre, gone by 1260 ms. `gather at 840, on target, count 14, shape shard, tint family, reach 300, strength 0.3, lasts 420, blend normal` | [Gravity well and convergence](../battle-craft.md#gravity-well-and-convergence), [Contact and follow-through](../battle-craft.md#contact-and-follow-through) |
| 840 | 320 | impact | the effect | The impact's light, faint so three do not light the field: a violet light on each foe, gone by 1160 ms. `glow at 840, on target, tint family, reach 220, strength 0.4, lasts 320, blend screen` | [Glow](../battle-craft.md#glow), [Additive light](../battle-craft.md#additive-light) |
| 1100 | 400 | impact | the effect | The second pull, the pulse of the name: 10 violet motes from round each foe drawn in again, closer, gone by 1500 ms. `gather at 1100, on target, count 10, shape dot, tint family, reach 220, strength 0.45, lasts 400, blend normal` | [Gravity well and convergence](../battle-craft.md#gravity-well-and-convergence), [Stagger and overlap](../battle-craft.md#stagger-and-overlap) |
| 1300 | 300 | impact | the effect | The wound opens as the aperture shuts: 6 deep red drops (*house*) out of each foe, short, just past its outline, apart and not one red blot on the body; gone by 1600 ms. `burst at 1300, on target, count 6, shape dot, tint blood, direction out, spread 360, reach 220, strength 0.45, lasts 300, blend normal` | [Burst](../battle-craft.md#burst), [One effect, one meaning](../battle-craft.md#one-effect-one-meaning) |
| 1400 | 700 | impact | the effect | The Bleed that stays: 6 red drops alive at once seep off each foe and fall short, gone by 2100 ms. `flow at 1400, on target, count 6, shape dot, tint blood, direction out, spread 360, reach 130, strength 0.45, lasts 700, blend normal` | [Flow](../battle-craft.md#flow), [One effect, one meaning](../battle-craft.md#one-effect-one-meaning) |
| 2100 | 440 | impact | the field | Nothing new. Every figure home, breathing. The result is read here; the hold from 2400 ms is empty. | [The hold before the number](../battle-craft.md#the-hold-before-the-number), [Breath at rest](../battle-craft.md#breath-at-rest) |

Nine staged beats, three in the windup; 42 particles asked for on each foe (6, 14, 10, 6, 6) and 12 on a1, gone by
600 ms; at most 72 on the field (at 1100 to 1260 ms, 14 and 10 on each of three), under the 150; two glows, no pulse
(three targets), no staged shake: the trauma of three medium hits already moves the view; the last gone by 2100 ms:
inside Stage III. One flash a figure, the body's, on b1, b2 and b3; two changes of light, a1 in the windup and the
three at contact.

### Reduced motion

Nothing. The frame after the action is shown at once with the result text: no fold, no pull, no blood. The Bleed is
the status chip's to say.

### Cost

- In the windup: one emitter and one glow image on a1, gone by 720 ms; one emitter on each foe from 600 to 840 ms.
- Objects at the peak (1100 to 1260 ms): on each foe the motif's `Graphics` and two emitters (the two pulls); about
  nine draw calls. From 1300 ms two emitters a foe (the blood).
- Particles at the peak: 72 at 1100 ms; of the field's 150.
- Bytes: not measured. Built by BC4 (motif, burst, flow, gather, glow, the `staging` field), BC5 (the cast's body, the
  trauma) and BC6 (the `blood` tint); BC8 places the beats. The cancel path is the kit's.

### On the film strip

```bash
node scripts/battle/filmstrip.mjs spell:toxic_waves:v1 --actor a1 --targets b1,b2,b3 --damage 4 --out <folder>/void-pulse.png
node scripts/battle/filmstrip.mjs spell:toxic_waves:v1 --actor a1 --targets b1,b2,b3 --damage 4 \
  --frames 24 --interval 60 --columns 6 --out <folder>/void-pulse-contact.png
```

- 0 to 180 ms (contact): no spiral on a1.
- 240 and 300 ms (contact): a few violet shards above a1's back, closing on it; from 360 to 660 ms a dim violet tint on
  a1's body, the shards lost in it; gone at 720 ms. Nothing on the foes.
- 720 and 780 ms (contact): a few faint violet motes round the foes, clearest above b3.
- 840 and 900 ms: b1, b2 and b3 filled flat in bone, in these two frames and no other; violet shards outside each
  outline.
- 960 to 1200 ms (contact): the aperture on each; shards and motes nearer each centre frame by frame, none moving
  away; a faint violet light on each, gone by 1200 ms.
- 1320 and 1380 ms (contact): the aperture shrinking; at 1380 ms the first red on b1's body, a small patch.
- 1440 ms (whole): a few red drops apart round each foe, under its middle, the body showing between them, not one red
  blot over its centre; no violet piece moving outward on any frame before 1300 ms.
- 1680 and 1920 ms (whole): a last red drop or two at each foe at most.
- 2160 ms (whole) and every frame after: no effect object on the field. a2 and a3 never lit or marked. Nothing in the
  effect is gold.
- Set beside Renewal Burst's sheet at 960 ms: Renewal Burst's motes fly out from a2; Void Pulse's close in on each foe.

The sheets cannot show the stop, the feel of the two pulls at full speed, or the sound.

### Left

- The Bleed is shown in blood, as in the physical and biological families: blood that stays on a figure is a Bleed
  wherever it is cast, so the player reads one status one way. The owner may want the void's wound bloodless; then the
  Bleed has no picture and only the chip says it.
- The void is dark and the kit has no darkening tint or blend: the shards are the family's violet on `normal`, which
  reads dimmer than the heals' added light, and the motif's own disc carries the dark. A dark tint, or a light that
  subtracts, is a pattern the craft book lacks ([Colour matrix](../battle-craft.md#colour-matrix) would be its source).
- The windup pinch on the foes, 600 to 840 ms, shows the confirmed recipients before contact (ADR 0131), as Vital
  Surge's wave does: it decides nothing.
- No staged shake: three medium hits add their trauma already. The owner may want one; up to 0.35 is in the band.
- Built since (#942): a gather closes on a figure's outline, not on its centre, so the shards stop at the
  aperture's edge rather than sink into the body.
- For a later pass. The fold onto a1 is lost from 360 ms in its own light: ask the windup gather strength 0.7 and the
  glow 0.25, so at least four shards count on each frame to 540 ms. The pinch on the foes at 720 ms is faint: ask its
  strength 0.7.
