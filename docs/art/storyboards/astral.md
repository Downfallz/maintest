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

## Revitalize (`spell:rejuvenate:v1`)

### Intent

The player reads "the Warped sent a pulse of life into that ally": one quick, directed pulse that lands and rises,
nothing more. Revitalize heals one ally of 6. Stage I, a staple of the Shaman's early rounds, so it stays short and
small: no windup beat, no light. Its signature is the arrival: a string of violet motes leaves a1 at contact, reaches
a2 at 1040 ms, and the rings and one strong rise of motes stand up on a2 together.

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
| 840 | 200 | impact | the effect | The pulse sent, the signature's first half: a head leaves a1 at the release and reaches a2 at 1040 ms, leaving 8 violet motes (*house*) on its path. `trail at 840, count 8, shape dot, tint family, strength 0.5, lasts 200, blend add` | [Trail](../battle-craft.md#trail), [Easing families](../battle-craft.md#easing-families) |
| 840 | 360 | impact | the caster | The release, as a cast lets go ([movement.md](movement.md)). a2 lost nothing: no flash, no recoil. | [Contact and follow-through](../battle-craft.md#contact-and-follow-through) |
| 1040 | 640 | impact | the effect | The `vital` motif on a2, as the pulse lands; gone by 1680 ms. `motif at 1040, lasts 640` | [Shape before colour](../battle-craft.md#shape-before-colour), [The three parts of an effect](../battle-craft.md#the-three-parts-of-an-effect) |
| 1040 | 560 | impact | the effect | The pulse lands, the signature's second half: one strong rise of 14 round violet motes (*house*) off a2 at once, a 50° column, slowing as they climb; gone by 1600 ms. `burst at 1040, on target, count 14, shape dot, tint family, direction up, spread 50, reach 300, strength 0.45, lasts 560, blend add` | [Burst](../battle-craft.md#burst), [Additive light](../battle-craft.md#additive-light) |
| 1680 | 860 | impact | the field | Nothing new. Both figures home, breathing. The result is read here. | [The hold before the number](../battle-craft.md#the-hold-before-the-number), [Breath at rest](../battle-craft.md#breath-at-rest) |

Three staged beats and the motif, four; 22 particles asked for on a2 (8 on the trail, 14), none on a1; no windup beat,
no glow, no pulse, no shake; the last gone by 1680 ms: inside Stage I. No flash, no change of light.

### Reduced motion

Nothing. The frame after the action is shown at once with the result text: no trail, no rise, no rings.

### Cost

- Objects at the peak (1040 to 1240 ms): the motif's `Graphics`, one emitter (the rise), the trail's last motes; about
  three draw calls.
- Particles at the peak: 14 at 1040 ms, with the trail's last few; of the field's 150.
- Bytes: not measured. Built by BC4 (motif, burst, trail, the `staging` field) and BC5 (the cast's body); BC8 places
  the beats. The cancel path is the kit's: every emitter dies with the action.

### On the film strip

```bash
node scripts/battle/filmstrip.mjs spell:rejuvenate:v1 --actor a1 --targets a2 --out <folder>/revitalize.png
node scripts/battle/filmstrip.mjs spell:rejuvenate:v1 --actor a1 --targets a2 \
  --frames 24 --interval 60 --columns 6 --out <folder>/revitalize-contact.png
```

- 0 to 780 ms (contact): no spiral, mote or light on a1 or a2; a1 squats and rises.
- 900 and 960 ms (contact): violet motes in a line between a1 and a2, the line's head past the halfway point at
  960 ms; nothing on a2 yet.
- 1080 and 1140 ms (contact): the rings on a2 and a column of violet motes rising off its back, none sideways onto a1
  or a3; the line between them gone or all but.
- 1200 ms (whole): the motes above a2's head, thinning; the rings clear. 1440 ms (whole): a last mote or two, the rings
  faint.
- 1680 ms (whole) and every frame after: no effect object on the field. a2 never filled, lit or moved. Nothing in the
  effect is gold.

The sheets cannot show the feel of the pulse at full speed, nor the sound.

### Left

- The pulse travels in the impact, 840 to 1040 ms, because Stage I may not spend the windup: the motif waits for it,
  so a2 shows nothing for 200 ms after the release. The owner may prefer the motif at 840; then the pulse arrives on
  rings already standing.
- The trail is the family's "sent from a1 to a2", shared with Vital Echo and Renewal Burst: the three are told apart by
  what lands, not by what travels.

## Vital Echo (`spell:healing_screech:v1`)

### Intent

The player reads "the Ethereal's spectral voice reached that ally, healed it a little, and the sound keeps coming
back": a heal, then its echo. Vital Echo heals one ally of 2 and gives it Regeneration 4 a round for two rounds. Stage
II: one windup beat, a light. Its signature is the repeat: a first rise at 1040 ms, smaller than Revitalize's, then,
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
| 840 | 200 | impact | the effect | The voice sent: a head leaves a1 and reaches a2 at 1040 ms, leaving 8 violet motes (*house*) on its path. `trail at 840, count 8, shape dot, tint family, strength 0.5, lasts 200, blend add` | [Trail](../battle-craft.md#trail), [Easing families](../battle-craft.md#easing-families) |
| 840 | 360 | impact | the caster | The release, as a cast lets go ([movement.md](movement.md)). a2 lost nothing: no flash, no recoil. | [Contact and follow-through](../battle-craft.md#contact-and-follow-through) |
| 1040 | 900 | impact | the effect | The `vital` motif on a2, as the voice lands; gone by 1940 ms, so the rings stand through the echo. `motif at 1040, lasts 900` | [Shape before colour](../battle-craft.md#shape-before-colour), [The three parts of an effect](../battle-craft.md#the-three-parts-of-an-effect) |
| 1040 | 360 | impact | the effect | The voice heard, the impact's light: a soft violet light on a2, gone by 1400 ms. `glow at 1040, on target, tint family, reach 220, strength 0.45, lasts 360, blend screen` | [Glow](../battle-craft.md#glow), [A light that answers a cast](../battle-craft.md#a-light-that-answers-a-cast) |
| 1040 | 400 | impact | the effect | The heal now, the first wave: 10 round violet motes (*house*) rise off a2 at once, fewer and shorter than Revitalize's; gone by 1440 ms. `burst at 1040, on target, count 10, shape dot, tint family, direction up, spread 50, reach 260, strength 0.45, lasts 400, blend add` | [Burst](../battle-craft.md#burst), [Additive light](../battle-craft.md#additive-light) |
| 1440 | 500 | impact | the ground | The echo, regeneration's ring: a faint violet ring lies on the ground at a2's feet, gone by 1940 ms. `glow at 1440, on target, shape ring, tint family, reach 200, strength 0.35, lasts 500, blend screen` | [Glow](../battle-craft.md#glow), [A beat on the ground](../battle-craft.md#a-beat-on-the-ground) |
| 1440 | 540 | impact | the effect | The echo, the signature's second wave: 10 small motes alive at once climb slowly from a2's feet in a thin column, gone by 1980 ms. `flow at 1440, on target, count 10, shape dot, tint family, direction up, spread 30, reach 200, strength 0.4, lasts 540, blend add, level feet` | [Flow](../battle-craft.md#flow), [One effect, one meaning](../battle-craft.md#one-effect-one-meaning) |
| 1980 | 560 | impact | the field | Nothing new. Both figures home, breathing. The result is read here. | [The hold before the number](../battle-craft.md#the-hold-before-the-number), [Breath at rest](../battle-craft.md#breath-at-rest) |

Seven staged beats, one in the windup; 28 particles asked for on a2 (8, 10, 10) and 10 on a1, gone by 760 ms; two
glows, no pulse, no shake; the last gone by 1980 ms: inside Stage II. No flash. Two changes of light on a2: the voice
at 1040 ms and the echo's ring at 1440 ms, the second fainter and on the ground.

### Reduced motion

Nothing. The frame after the action is shown at once with the result text: no voice, no rise, no echo. The
regeneration is the status chip's to say.

### Cost

- In the windup: one emitter on a1, gone by 760 ms.
- Objects at the peak (1440 to 1940 ms): the motif's `Graphics`, the ring's image, one emitter (the column); about
  three draw calls. At 1040 ms: the motif, the light's image, one emitter.
- Particles at the peak: 10 at 1040 ms, with the trail's last few; 10 again from 1440 ms; of the field's 150.
- Bytes: not measured. Built by BC4 (motif, burst, flow, gather, glow, trail, the `staging` field), BC5 (the cast's
  body), and BC6's follow-ups (`level`, the ring on the ground); BC8 places the beats. The cancel path is the kit's.

### On the film strip

```bash
node scripts/battle/filmstrip.mjs spell:healing_screech:v1 --actor a1 --targets a2 --out <folder>/vital-echo.png
node scripts/battle/filmstrip.mjs spell:healing_screech:v1 --actor a1 --targets a2 \
  --frames 24 --interval 60 --columns 6 --out <folder>/vital-echo-contact.png
```

- 0 to 300 ms (contact): no spiral or mote on a1.
- 420 to 720 ms (contact): violet motes on a ring around a1, closing on its body; none left at 780 ms; nothing on a2.
- 900 and 960 ms (contact): violet motes in a line between a1 and a2.
- 1080 to 1380 ms (contact): the rings on a2, a soft violet light on it, a short column of motes rising off it, lower
  than Revitalize's at the same time.
- 1440 ms (whole): the first motes gone; a faint violet ring flat on the ground at a2's feet, its ellipse inside or on
  the gold target ring, not hiding it; the light gone.
- 1680 ms (whole): a thin column of small motes rising from a2's feet, inside its outline; the rings still standing.
- 1920 ms (whole): the ring and the rings faint, a last mote. 2160 ms (whole) and every frame after: no effect object.
  a1, a3 and the foes never lit or marked. Nothing in the effect is gold.
- Set beside Revitalize's sheet at 1680 ms: Revitalize's field is empty; Vital Echo shows the ring and the column.

The sheets cannot show the rhythm of the echo at full speed, nor the sound, which is where a screech belongs.

### Left

- Regeneration is a ring and a column at the feet, after the heal: the owner may read it as a second heal. The look is
  kept the same in Whisper, so it is learnt as "comes back", and the status chip names it.
- The "spectral" of the approved execution is carried by the windup gather alone: the kit has no way to make the trail
  or the rings ghostly (no blur, no alpha flicker on a beat). Asking for it is a pattern the craft book's
  [Blur](../battle-craft.md#blur) would have to grow into a kit word first.

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
| 840 | 600 | impact | the effect | The murmur, the signature: 6 violet motes alive at once drift out low and slow around each ally's feet, a mist on the ground, gone by 1440 ms. No burst. `flow at 840, on target, count 6, shape dot, tint family, direction out, spread 360, reach 140, strength 0.4, lasts 600, blend screen, level feet` | [Flow](../battle-craft.md#flow), [A beat on the ground](../battle-craft.md#a-beat-on-the-ground) |
| 900 | 500 | impact | the effect | The murmur heard: a faint violet light on each ally, faint so three do not light the field; gone by 1400 ms. `glow at 900, on target, tint family, reach 180, strength 0.25, lasts 500, blend screen` | [Glow](../battle-craft.md#glow), [A light that answers a cast](../battle-craft.md#a-light-that-answers-a-cast) |
| 1300 | 500 | impact | the ground | Regeneration's ring, Vital Echo's: a faint violet ring on the ground at each ally's feet, gone by 1800 ms. `glow at 1300, on target, shape ring, tint family, reach 200, strength 0.35, lasts 500, blend screen` | [Glow](../battle-craft.md#glow), [A beat on the ground](../battle-craft.md#a-beat-on-the-ground) |
| 1300 | 540 | impact | the effect | Regeneration's column, Vital Echo's with fewer motes: 8 small motes alive at once climb slowly from each ally's feet, gone by 1840 ms. `flow at 1300, on target, count 8, shape dot, tint family, direction up, spread 30, reach 200, strength 0.4, lasts 540, blend add, level feet` | [Flow](../battle-craft.md#flow), [One effect, one meaning](../battle-craft.md#one-effect-one-meaning) |
| 1840 | 700 | impact | the field | Nothing new. Every figure home, breathing. The result is read here. | [The hold before the number](../battle-craft.md#the-hold-before-the-number), [Breath at rest](../battle-craft.md#breath-at-rest) |

Six staged beats, one in the windup; 14 particles asked for on each ally (6, 8) and none on a1 as caster; at most 42 on
the field (at 1300 to 1440 ms, 6 and 8 on each of three), well under the 150; three glows, no pulse, no shake; the last
gone by 1840 ms: inside Stage II. No flash. Two changes of light, both faint: a1 in the windup, the three at 900 ms.

### Reduced motion

Nothing. The frame after the action is shown at once with the result text. The regeneration is the status chips'.

### Cost

- In the windup: one glow image on a1, gone by 760 ms.
- Objects at the peak (1300 to 1440 ms): on each ally the motif's `Graphics`, the ring's image, two emitters (the
  mist, the column); about twelve draw calls for 140 ms, the screen objects batched.
- Particles at the peak: 42 at 1300 ms; of the field's 150.
- Bytes: not measured. Built by BC4 (motif, flow, glow, the `staging` field), BC5 (the cast's body), and BC6's
  follow-ups (`level`, the ring on the ground); BC8 places the beats. The cancel path is the kit's.

### On the film strip

```bash
node scripts/battle/filmstrip.mjs spell:soothing_chant:v1 --actor a1 --targets a1,a2,a3 --out <folder>/whisper.png
node scripts/battle/filmstrip.mjs spell:soothing_chant:v1 --actor a1 --targets a1,a2,a3 \
  --frames 24 --interval 60 --columns 6 --out <folder>/whisper-contact.png
```

- 0 to 300 ms (contact): no spiral on a1. 420 to 720 ms: a faint violet light on a1, gone at 780 ms.
- 840 to 1380 ms (contact): no line between figures; no mote rising above any head. Violet motes low around the feet
  of a1, a2 and a3, inside a body's width of each, under their bellies; the rings on each; a faint light on each,
  fainter than Vital Echo's at 1080 ms.
- 1440 ms (whole): a faint violet ring flat at each ally's feet, beside or on its gold ring, not hiding it; a thin
  column of small motes starting up from each.
- 1680 ms (whole): the three columns, thin, inside each outline; the rings fading.
- 1920 ms (whole) and every frame after: no effect object on the field. No foe lit, moved or marked. Nothing in the
  effect is gold.
- Set beside Vital Echo's sheet at 1680 ms: the same ring and column, on three; at 1080 ms Vital Echo's motes rise
  above a2's head and Whisper's stay at the feet.

The sheets cannot show the softness at full speed, nor the sound, where the whispers live.

### Left

- The regeneration starts at 1300 ms here and at 1440 ms in Vital Echo: the same look, later where a heal comes first.
  The owner may want them on one clock; then Vital Echo's echo crowds its heal.
- The low mist is a `flow` out at the feet, the kit's nearest to a murmur: no beat can draw a sound wave. The owner
  may find it reads as dust (physical's Pummel); then it goes to the screen-blended motes' strength before it goes.
- The caster is also a recipient: its light in the windup and its share at contact are on the same body.

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
| 560 | 280 | windup | the effect | The vitality sent: a head leaves a1 and reaches a2 at 840 ms, leaving 16 violet motes (*house*) on its path; the release meets it. `trail at 560, count 16, shape dot, tint family, strength 0.6, lasts 280, blend add` | [Trail](../battle-craft.md#trail), [Easing families](../battle-craft.md#easing-families) |
| 840 | 360 | impact | the caster | The release, as a cast lets go ([movement.md](movement.md)). a2 lost nothing: no flash, no recoil. | [Contact and follow-through](../battle-craft.md#contact-and-follow-through) |
| 840 | 1100 | impact | the effect | The `vital` motif on a2, gone by 1940 ms. `motif at 840, lasts 1100` | [Shape before colour](../battle-craft.md#shape-before-colour), [The three parts of an effect](../battle-craft.md#the-three-parts-of-an-effect) |
| 840 | 440 | impact | the effect | The burst's light, the impact's one change of light: a wide, bright violet light on a2, gone by 1280 ms. `glow at 840, on target, tint family, reach 280, strength 0.65, lasts 440, blend add` | [Glow](../battle-craft.md#glow), [Additive light](../battle-craft.md#additive-light) |
| 840 | 420 | impact | the effect | The burst, the signature: 18 round violet motes (*house*) thrown out on every side of a2 at once, past its outline; gone by 1260 ms. `burst at 840, on target, count 18, shape dot, tint family, direction out, spread 360, reach 300, strength 0.5, lasts 420, blend screen` | [Burst](../battle-craft.md#burst), [The three parts of an effect](../battle-craft.md#the-three-parts-of-an-effect) |
| 880 | 560 | impact | the effect | Health given now, the family's strong rise: 14 round violet motes (*house*) up off a2 in a 50° column; gone by 1440 ms. `burst at 880, on target, count 14, shape dot, tint family, direction up, spread 50, reach 320, strength 0.5, lasts 560, blend add` | [Burst](../battle-craft.md#burst), [One effect, one meaning](../battle-craft.md#one-effect-one-meaning) |
| 900 | 700 | impact | the target | The family's one halo: a violet halo on a2's own outline swells and goes, "healed"; gone by 1600 ms. `pulse at 900, on target, tint family, strength 0.5, lasts 700` | [Glow](../battle-craft.md#glow), [One hero shader](../battle-craft.md#one-hero-shader) |
| 1000 | 900 | impact | the effect | Energy +3: 10 violet sparks (*house*) alive at once climb slowly off a2, pointed and thin, gone by 1900 ms. Overdrive's reading: energy gained. `flow at 1000, on target, count 10, shape spark, tint family, direction up, spread 40, reach 280, strength 0.25, lasts 900, blend add` | [Flow](../battle-craft.md#flow), [One effect, one meaning](../battle-craft.md#one-effect-one-meaning) |
| 1940 | 600 | impact | the field | Nothing new. Both figures home, breathing. The result is read here; the hold from 2400 ms is empty. | [The hold before the number](../battle-craft.md#the-hold-before-the-number), [Breath at rest](../battle-craft.md#breath-at-rest) |

Nine staged beats, three in the windup; 58 particles asked for on a2 (16, 18, 14, 10) and 14 on a1, gone by 560 ms;
two glows and the one pulse, on a2, the one target; no shake; the last gone by 1940 ms: inside Stage III. No flash. Two
changes of light: a1 in the windup, a2 at contact, the halo riding on the second.

### Reduced motion

Nothing. The frame after the action is shown at once with the result text: no charge, no burst, no halo, no spark.
The heal and the energy are the result text's.

### Cost

- In the windup: one emitter and one glow image on a1, gone by 760 ms; the trail's emitter to 840 ms.
- Objects at the peak (900 to 1260 ms): the motif's `Graphics`, the light's image, three emitters (the burst, the
  rise, the sparks), and the halo: a filter, a framebuffer and a pass each frame for 700 ms (the craft book, "Glow");
  about five draw calls and the filter's pass.
- Particles at the peak: about 42 at 1000 ms (18, 14, 10), with the trail's last few; of the field's 150.
- Bytes: not measured. Built by BC4 (motif, burst, flow, gather, glow, trail, the `staging` field), BC5 (the cast's
  body) and BC6 (the pulse, the gold rim quieted under it); BC8 places the beats. The cancel path is the kit's: the
  filter is removed and the rim given back when the action ends or is cancelled.

### On the film strip

```bash
node scripts/battle/filmstrip.mjs spell:restorative_burst:v1 --actor a1 --targets a2 --out <folder>/renewal-burst.png
node scripts/battle/filmstrip.mjs spell:restorative_burst:v1 --actor a1 --targets a2 \
  --frames 24 --interval 60 --columns 6 --out <folder>/renewal-burst-contact.png
```

- 0 and 60 ms (contact): no spiral on a1.
- 180 to 480 ms (contact): violet motes on a wide ring around a1, closing on its body; from 420 ms a violet light on
  a1.
- 600 to 780 ms (contact): a line of violet motes from a1 toward a2, its head at a2 by 780 ms; the light on a1 going.
- 840 and 900 ms (contact): a2 not filled in bone or any flat colour; a bright violet light on a2; motes on every
  side of a2 past its outline; the rings standing.
- 960 to 1140 ms (contact): a violet halo on a2's outline, its gold rim quieted; a column of motes rising above a2's
  head; the outward motes thinning.
- 1200 ms (whole): the halo, the column high, the first thin sparks rising; a2's head not covered by a bright blot.
- 1440 ms (whole): the halo fading; violet sparks, pointed, rising slowly off a2; no round mote left.
- 1680 ms (whole): sparks only, a few; the rings faint. 1920 ms (whole): a last spark at most.
- 2160 ms (whole) and every frame after: no effect object on the field; a2's gold rim back. a1, a3 and the foes never
  lit, moved or marked after 840 ms. Nothing in the effect is gold.
- Set beside Revitalize's sheet at 960 ms: Revitalize shows a line still travelling; Renewal Burst shows motes thrown
  out all round a2 under a bright light.

The sheets cannot show the swell of the halo at full speed, nor the sound.

### Left

- The family's one halo is spent here and not on Vital Echo or Revitalize, because Renewal Burst is the game's
  biggest heal and the only Stage III heal of the family. The owner may want a halo on every heal; then it stops
  meaning anything a player can count on.
- The trail runs in the windup, 560 to 840 ms, so the burst meets the release at contact, as Vital Surge's wave does.
  It shows the path to a2 before the impact: a2 is the host's confirmed recipient (ADR 0131), so this shows nothing
  undecided.
- The energy sparks are the family's violet, not the biological green: a spark rising is the reading, the colour is
  the family's.

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
| 120 | 480 | windup | the effect | Space folds onto a1: 12 violet shards (*house*), dark-blended, on a wide ring around a1 drawn onto its body by 600 ms. The heals gather round motes; this gathers splinters. `gather at 120, on caster, count 12, shape shard, tint family, reach 260, strength 0.3, lasts 480, blend normal` | [Gravity well and convergence](../battle-craft.md#gravity-well-and-convergence), [Anticipation](../battle-craft.md#anticipation) |
| 360 | 360 | windup | the effect | a1 holds the fold: a dim violet light on it, gone by 720 ms. `glow at 360, on caster, tint family, reach 170, strength 0.35, lasts 360, blend screen` | [Glow](../battle-craft.md#glow), [Anticipation](../battle-craft.md#anticipation) |
| 600 | 240 | windup | the effect | Space starts to pinch on each foe: 6 violet motes (*house*) from far round each target drawn toward its centre, gone at 840 ms. `gather at 600, on target, count 6, shape dot, tint family, reach 320, strength 0.4, lasts 240, blend normal` | [Gravity well and convergence](../battle-craft.md#gravity-well-and-convergence), [Anticipation](../battle-craft.md#anticipation) |
| 840 | 360 | impact | the caster | The release, as a cast lets go ([movement.md](movement.md)). | [Contact and follow-through](../battle-craft.md#contact-and-follow-through) |
| 840 | 900 | impact | the effect | The `void` motif, the aperture, on each target; gone by 1740 ms. `motif at 840, lasts 900` | [Shape before colour](../battle-craft.md#shape-before-colour), [The three parts of an effect](../battle-craft.md#the-three-parts-of-an-effect) |
| 840 | 590 | impact | the target | Each struck figure's medium hit from a distance ([movement.md](movement.md)): its bone flash for 80 ms, a short stop, a recoil away from a1, home before 1440 ms. All three on the contact frame. | [Hit flash](../battle-craft.md#hit-flash), [Recoil](../battle-craft.md#recoil) |
| 840 | 420 | impact | the effect | The first pull, the signature: 14 violet shards (*house*) start well outside each foe and are drawn onto its centre, gone by 1260 ms. `gather at 840, on target, count 14, shape shard, tint family, reach 300, strength 0.3, lasts 420, blend normal` | [Gravity well and convergence](../battle-craft.md#gravity-well-and-convergence), [Contact and follow-through](../battle-craft.md#contact-and-follow-through) |
| 840 | 320 | impact | the effect | The impact's light, faint so three do not light the field: a violet light on each foe, gone by 1160 ms. `glow at 840, on target, tint family, reach 220, strength 0.4, lasts 320, blend screen` | [Glow](../battle-craft.md#glow), [Additive light](../battle-craft.md#additive-light) |
| 1100 | 400 | impact | the effect | The second pull, the pulse of the name: 10 violet motes from round each foe drawn in again, closer, gone by 1500 ms. `gather at 1100, on target, count 10, shape dot, tint family, reach 220, strength 0.45, lasts 400, blend normal` | [Gravity well and convergence](../battle-craft.md#gravity-well-and-convergence), [Stagger and overlap](../battle-craft.md#stagger-and-overlap) |
| 1300 | 300 | impact | the effect | The wound opens as the aperture shuts: 6 deep red drops (*house*) out of each foe, short, just past its outline; gone by 1600 ms. `burst at 1300, on target, count 6, shape dot, tint blood, direction out, spread 360, reach 160, strength 0.45, lasts 300, blend normal` | [Burst](../battle-craft.md#burst), [One effect, one meaning](../battle-craft.md#one-effect-one-meaning) |
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

- 0 and 60 ms (contact): no spiral on a1.
- 180 to 540 ms (contact): violet shards on a wide ring around a1, closing on its body, none left at 660 ms; from
  420 ms a dim violet light on a1, gone at 720 ms. Nothing on the foes.
- 660 to 780 ms (contact): violet motes well outside each of b1, b2 and b3, closer to each centre at 780 ms than at
  660 ms.
- 840 and 900 ms: b1, b2 and b3 filled flat in bone, in these two frames and no other; violet shards outside each
  outline.
- 960 to 1200 ms (contact): the aperture on each; shards and motes nearer each centre frame by frame, none moving
  away; a faint violet light on each, gone by 1200 ms.
- 1320 and 1380 ms (contact): deep red drops just outside each foe's outline, none above its head; the aperture
  shrinking.
- 1440 ms (whole): a few red drops close round each foe, under its middle; no violet piece moving outward on any frame
  before 1300 ms.
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
- A gather lands on a figure's centre, not on its outline (biological.md): the shards may sink into the body rather
  than into the aperture.
