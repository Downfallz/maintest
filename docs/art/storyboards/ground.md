# Storyboards: the ground

The place the battle stands in, and no spell's look: the air that moves over it at rest, the depth its layers give
when the view trembles, and the eclipse's light answering a cast. It is BC9 of
[the battle's craft plan](../battle-craft-plan.md). Like [the movement](movement.md) it stages no spell: it plays
under every action and between them, and a staging (BC6 to BC8) never names it.

What the ground stands on, read in `battle/src/scene.ts`, `battle/src/battle-horizon.ts` and
`battle/src/horizon-arch.ts` at `9b87cde`:

| Layer | Depth | What it is | By round |
| --- | --- | --- | --- |
| The painting, `eclipse-battle.webp` | -10 | The sky, the eclipse and the floor the figures stand on, in one picture, tinted `0xb5bbc2`. Laid to cover the field, the crop centred across and 35% from the top. | always |
| The fallen ledge | -9.9 | A plate over the painting's far edge. | from 4 |
| The breach | -9.8 | A plate of the whole sky, feathered into the floor across source rows 399 to 479. | 6 to 11 |
| The broken floor | -9.6 | A plate of the floor from source row 399. | from 12 |
| The shattered sky | -9.5 | A plate of the whole sky, the eclipse cracked. | from 10 |
| The faults | -9.25 | A `Graphics`: one crack more each even round, in ember orange `0xd18d51`. | from 14 |
| The arch, pier and span | -9 | Cut out of the painting at boot, tinted `0xa5b1b3`; the span falls at round 2, the pier at 8. | 1 to 8 |
| The figures | their y | Positive depths. | always |
| The motes | 1000 | 24 circles of radius 1 or 2 px in the target marker's gold `0xeac88e`, alpha .24, in front of the figures, moved by hand each frame from the ambient clock, hidden under less motion. | always |

Where the eclipse is: in the source painting (1,672 by 941) its disc is centred near x 846, y 135, radius about 112;
the horizon is near row 400. On the bench's field (390 by about 320, the sheets' frames) the painting is laid at a
scale of about 0.34: the disc's centre near x 198, y 46, radius about 38 px; the horizon near y 136. The numbers
below are given in source rows or in shares of the field, so they hold on a phone and a desktop alike.

The film clock: the bench's film restarts the scene's ambient clock with the play, so a still film is deterministic,
and the ambient clock is the only clock the air reads. Nothing here draws from `Math.random`. A film lets the scene
settle 500 ms before its frame 0, and the ambient clock runs through it: at a frame's millisecond the air's clock reads
that millisecond plus 500. The film steps the game by ticks of 20 ms (`FILM_TICK_MS`), and a tween added on a tick is
first moved on the next: a beat that starts at contact shows its first value one tick later. Every duration here is
at 1× and is divided by the playback rate only.

## The air at rest

### Intent

The field is alive between two actions: a slow band of fog lies on the horizon and a few embers rise from the cracked
floor, so the place does not read as a picture. It is seen for the whole match, behind every action, so it takes no
attention at all: dim, small, slow, behind every figure, and never in a colour that means something on the page.

### Today

Filmed at `9b87cde`, 390 by 844, 1×, `--still --interval 1000`, round 1 and round 12 (the actor a1 and its halo
are the bench's defaults; nothing plays).

- Round 1, 0 to 11000 ms: the twelve frames are the same picture. The painting, the arch on the left, the painted
  teal mist across the horizon, the eclipse at the top centre. Nothing in the ground moves.
- The motes are the one thing that changes, and they can hardly be found: a few gold dots of one or two pixels, for
  example over b1's back at 9000 ms. They drift on a slant across the whole field, over the sky, over the eclipse and
  in front of the figures (depth 1000).
- Their gold is the target marker's (`GOLD` in `scene.ts`, the colour of the acting rings on the same frames).
- Round 12: the shattered sky, the broken floor glowing orange in its cracks, the same twelve identical frames but
  for the motes. The glow in the cracks is painted: it does not move.

### Beats

The air is not an action's: it plays in every window, at the resting rate between actions (15 frames a second,
`battle/README.md`, "The resting loop") and at the full rate during one. The rows are one action's length; at rest
the same beats go on.

| At | Lasts | Window | Layer | Beat | Pattern |
| --- | --- | --- | --- | --- | --- |
| 0 | 840 | windup | the ground (fog) | One band of fog on the horizon, source rows 330 to 470, the field's whole width, feathered to nothing at its top and bottom: one `Phaser.GameObjects.TileSprite` of a texture drawn at boot. Slate taken from the painting's mist (`0x61857d`, rows 300 to 360) lifted to `0x8aa29c`, alpha .40 (*house*; .18 on the first film lifted the floor by 5 levels at most, .30 on the second by 12 at its densest rows on a phone's scale, and neither band could be told from the painted mist). It drifts left to right, `tilePositionX` = ambient clock × 0.008 px a ms, 8 px a second (*house*; 3 on the second film moved a pattern of ±4 levels too slowly to be seen as moving): 0.5 px a resting frame, a crossing of the field in about 50 s. Depth -8.9: over the arch's foot, under every figure. | [Fog and embers](../battle-craft.md#fog-and-embers), [Textures drawn at boot](../battle-craft.md#textures-drawn-at-boot) |
| 0 | 840 | windup | the ground (embers) | 24 embers in place of the 24 gold motes: one soft dot drawn at boot, as images, additive, depth -8.8, behind every figure. 16 far: 3 px across, peak alpha .50, rising 1.2% of the field's height a second from source row 600 to row 420. 8 near: 6 px across, peak alpha .70, rising 2.5% a second from row 900 to row 480 (*house*). Each sways ±3 px across, on a period of 5 to 9 s by its index, and fades in and out along its rise (alpha × sin(π × progress)), so none appears or vanishes at once. | [Fog and embers](../battle-craft.md#fog-and-embers), [Additive light](../battle-craft.md#additive-light) |
| 0 | 840 | windup | the ground (embers) | Their colour is the faults' and the cracks' ember orange, `0xd18d51`, not the gold. They never rise above row 420: none crosses the sky, the eclipse or the bright clouds, where additive light turns white. Their alpha grows with the round as the floor breaks: × (0.6 + 0.4 × min(1, (round − 1) / 11)), 0.6 at round 1, 1 from round 12 (*house*). | [Shape before colour](../battle-craft.md#shape-before-colour), [One effect, one meaning](../battle-craft.md#one-effect-one-meaning) |
| 840 | 1700 | impact | the ground | The same, through the impact. The air does not answer the hit: the light does ("The light that answers a cast"). | [Fog and embers](../battle-craft.md#fog-and-embers) |
| 2540 | 500 | settle | the ground | The same. The result is read over a field where only the air moves. | [Fog and embers](../battle-craft.md#fog-and-embers), [The hold before the number](../battle-craft.md#the-hold-before-the-number) |

- Every position is a pure function of the ambient clock, the ember's index and the field's size: no emitter, no
  state carried from frame to frame. The book's one flowing emitter draws from `Math.random`, which the bench pins
  only for the length of a play: a film of the field at rest would not repeat. Nothing it gives (a lifespan, a zone, a
  cap) is out of reach of a sine and a remainder for 24 dots, so no exception is asked for.
- Each ember's place, its column across and its phase along its rise, is read from a fixed table of 24 pairs in
  `table/battle-ground.js` (*house*), chosen once so that, at clock 0 on the bench's field, no
  two embers of one kind stand within 20 px and no three of one kind in one row or one column within 3 px, and held
  by a test (`PLACES`). No three near any line at all was asked first; 16 far embers in a band 61 px high cannot
  meet it, so rows and columns are what is held. Embers of one kind rise at one pace,
  so their figure on the floor never changes but for the wrap: what holds at one instant holds at all. Two films
  tried a step for each: the golden ratio for both drew one slant across the floor, and Roberts' R2 (0.7549 across,
  0.5698 along) drew a lattice, as every constant step does: the far embers 0, 4, 8 and 12 stood in one column
  (4 × 0.7549 is 0.02 of the width), and 0, 7 and 14 in one row at round 12 (7 × 0.5698 is 0.011 of a cycle).
  A near ember's rise takes about 18 s, a far one's about 16 s.
- Depth at rest is in the air itself, not in a camera: far embers are smaller, dimmer, slower and nearer the horizon
  than near ones, and the fog, the furthest, is the slowest of all. The painting and its plates never move at rest.
- The numbers live in `table/battle-ground.js`, as the movement's live in `table/battle-motion.js`.

### Reduced motion

No ember and no drift. The fog band stays, still, at `tilePositionX` 0, so a player who asked for less motion sees the
same place, the same haze on the horizon, and every screenshot under the setting is the same. The gold motes are gone
in both settings.

### Cost

- Objects: one `TileSprite` and 24 images, made at boot, in place of 24 `Arc` shapes. Draw calls: one for the fog,
  one for the embers (additive, drawn after the fog and before every figure, so they do not split another batch).
  The 24 shapes they replace batched with the vectors.
- Textures drawn at boot, in the live bundle (the ground is seen from the first frame, and the kit's `fx-kit` texture
  arrives after it): the fog, 256 by 64, tileable across, made of seven soft ellipses from fixed numbers in
  `table/battle-ground.js`; the ember's soft dot, 16 by 16. Two textures, about 65 KiB of
  texture memory. No picture file.
- Pixels: the fog band is about 390 by 50 px on the bench's field, one layer; 24 dots of at most 6 by 6. The embers
  are not the kit's particles and are not counted in the field's 150.
- A frame at rest: one `tilePositionX`, 24 positions and alphas, from sines. Nothing is added to `busy()`: the air
  is ambience and plays at the resting rate.
- A hidden tab: the game's loop is paused and the ambient clock stands still; on return the air resumes where it
  stood. There is no emitter to pause.
- Bytes: with the depth and the light, the live bundle grew by about 8 KiB, to 1,738,691 of 1,769,472 bytes raw.
- Built by BC9: the two textures, the fog, the embers, `table/battle-ground.js`; the motes and their gold go.

### On the film strip

Filmed at 390 by 844, 1×, from the repository with the bundle built, the sheets written outside it. Nothing plays; the
actor is named so the halo stays where it was on the sheets of today:

```bash
node scripts/battle/filmstrip.mjs --still --actor a1 --interval 1000 --motion --out <folder>/ground-rest.png
node scripts/battle/filmstrip.mjs --still --actor a1 --interval 1000 --round 12 --motion \
  --out <folder>/ground-rest-round12.png
node scripts/battle/filmstrip.mjs --still --actor a1 --interval 1000 --round 12 --scale 2 \
  --out <folder>/ground-rest-round12-large.png
```

- Every frame: a soft grey-green haze across the horizon, between the back row's rings and the front row, over the
  arch's foot at round 1; the back row's rings drawn over it. Against the sheet of today at the same millisecond, the
  band lifts the floor at its densest rows by 15 to 25 levels a channel (`--scale 2`), never more than 30.
- Every frame: no gold dot anywhere on the field. Small orange dots between the horizon and the bottom edge only:
  none over the sky, the eclipse or the clouds, none drawn over a figure's body; spread over the floor, no three of
  one kind in one row or one column. At round 1, on the sheet at 1:1 (`ground-rest`), each near ember's brightest
  pixel lifted by 60 to 100 levels on red against the sheet of today (the second film: 37 at most, and no dot could be
  found); at round 12 none near white.
- 0 and 1000 ms (`--scale 2`, which lays about 4 sheet px to a pixel of the field): a near ember has risen about 8 px
  of the field (about 32 on the sheet), a far one about 4 (about 15); each is in the same column within 3 px of the
  field.
- Round 12 against round 1, at the same millisecond: the dots brighter (the record: alpha factor 1 against 0.6).
- 0 to 11000 ms: the haze's pattern has moved right, slowly (the record: drift 8 px a second, 88 px from 0 to
  11000 ms). The painting, the plates and the arch the same on all twelve frames.
- The record (`--motion`), at each frame: `ground.fog.drift` = 0.008 × (ms + 500), 4 at 0 ms and 92 at
  11000 ms (the film's settling, under "The film clock"); `ground.embers.shown` 24;
  `ground.embers.alpha` 0.6 at round 1 and 1 at round 12; every `ground.layers` shift 0 and every scale 1;
  `ground.light.strength` 0.
- Two films of one command are the same file byte for byte (`studio/browser/battle-bench.spec.js`).

A sheet cannot show the pace at the resting rate (the film steps the game by hand), the feel of the drift at full
speed, reduced motion (the film strip has no flag for it) or a hidden tab: the browser specs check those three (no
ember shown and the fog's drift 0 under less motion; the ambient clock unchanged across a hidden tab).

### Left

- The motes go, and their gold with them: the craft book's **Here** for "Fog and embers" asks for the motes "in the
  same gold", which is the target marker's (`docs/art/spell-vfx.md`). The embers take the faults' orange. The book's
  entry is amended in this stage.
- No emitter, against the same **Here** ("BC9 makes them one emitter"): an emitter draws from `Math.random`, and a
  film of the field at rest would not repeat. The entry's **Phaser** line keeps the emitter as the general pattern;
  its **Here** now says the ground draws its air from the ambient clock, and that a hidden tab stops it by stopping
  that clock, so the plan's test "a hidden tab stops the emitters" becomes "the ambient clock stands still while
  hidden, and resumes with no jump".
- The embers move behind the figures, where the motes were in front: the book asks for "behind the figures", and a
  dot over a body is noise on the one thing the player reads.
- The embers' orange is near the fire motif's `0xd58b75`. They are told apart by shape and place: 3 to 6 px, dim,
  slow, everywhere at once, behind the figures, against a spell's large bright pieces on a figure. The owner may find
  that a fire spell's embers and the ground's read as one; the fallback is the painting's ash grey.
- The fog's slate is a greyer cousin of the necromantic `0x85c9c2`; a wide soft band is not a spirit's shape, but the
  film says whether it reads as one.
- A still fog band stays under reduced motion: it is a colour, not a motion. The owner may want the ground under the
  setting to be today's exactly; then the band goes too.
- The embers brighten with the round, as the floor breaks. It shows nothing the field does not already show (the
  round's plates), but it is a choice.
- At round 1 on the 1:1 sheet the haze is measured (16 levels at its densest rows, 20 at `--scale 2`) but the eye
  hardly tells it from the painted mist on the horizon, and a near ember is a faint dot (75 levels on red at its
  brightest). Both sit at the low end of what was asked, on purpose: the air must not
  compete with the figures. The owner's phone decides whether they are found; if not, the fog's alpha and the
  round-1 factor are the knobs.

## The depth

### Intent

When the view trembles under a heavy blow, the floor and the figures shake and the sky shakes less: the player reads
"the ground shook", not "the picture shook". It is a small thing, felt and not seen, and it moves only when the view
already moves.

### Today

Filmed at `9b87cde`, 390 by 844, 1×, `spell:pummel:v1 --actor a1 --targets b1 --damage 8 --defeat --round 12`, the
whole action with `--motion`, and the contact at a frame every 20 ms.

- The contact sheet covers 0 to 460 ms (24 frames every 20 ms): the contact is not on it. It shows the draw back and
  nothing of the ground.
- The record, whole action: the view's offset is 0 and its zoom 1 on every frame but 960 ms, where the trauma is
  0.308, the offset −0.56 px across and 0.67 px down, the zoom 1.003. By 1200 ms everything is back at 0.
- So at the bench's size (the field's short side about 320 px, a full shake 8 px) a killing blow moves the view by
  under 1 px on the frame that catches it, and 3 px at most at its peak (the ceiling 0.6 squared × 2.5% × 320). Every
  layer moves with the camera: the ground has no depth.

### Beats

The rows are a defeat, the only action with both the tremor at its ceiling and the punch. A blow that does not defeat
plays the first row alone, by its loss ([the camera](movement.md#the-camera)).

| At | Lasts | Window | Layer | Beat | Pattern |
| --- | --- | --- | --- | --- | --- |
| 0 | 840 | windup | the ground | Nothing. The camera does not move before contact, and no layer moves by itself: no sway of a picture at rest. | [Parallax](../battle-craft.md#parallax) |
| 840 | 288 | impact | the ground | While the view trembles, each layer is shifted against the view's offset by hand, by (1 − its factor) × the offset, so on the screen it moves its factor × the offset. Factors (*house*, after Godot's 0.1 to 0.7): the painting, the fallen ledge, the broken floor, the faults and the arch 1; the sky plates (the breach and the shattered sky) 0.5; the fog 0.75; the far embers 0.8; the near embers 1. | [Parallax](../battle-craft.md#parallax), [Shake from trauma](../battle-craft.md#shake-from-trauma) |
| 840 | 200 | impact | the ground | While the zoom punches in, each layer of factor under 1 is counter-scaled about the camera's centre so it zooms 1 + its factor × the punch: the sky plates 1.5% at the peak where the floor takes 3%. | [Parallax](../battle-craft.md#parallax), [Punch zoom](../battle-craft.md#punch-zoom) |
| 1128 | 1412 | impact | the ground | Every shift 0 and every scale 1: the layers are where they rest, as the camera is. | [The hold before the number](../battle-craft.md#the-hold-before-the-number) |

Why these factors:

- 1 for everything glued to the floor or to a painted twin. The painting carries the floor under the figures' feet,
  so it moves with them. The arch is cut out of the painting and the painting still holds it underneath: shifted, it
  would show its own double. The fallen ledge, the broken floor and the faults lie on the floor.
- 0.5 for the sky plates only. They cover the painting's sky and feather into the floor across rows 399 to 479, so a
  relative shift of 1.5 px at most slides inside the feather, where it cannot be seen as a seam. Their edges lie
  outside the crop on every aspect: the painting is wider than the field.
- So before round 6 the sky is the painting's and moves with the floor: only the air has depth then. From round 6 the
  sky plate gives the sky its own.
- What the eye gets, honestly, at the ceiling on the bench's field: the floor 2.9 px, the sky 1.4 px, for under
  300 ms; on the punch, at the top of the field (160 px from the centre) the floor moves 4.8 px and the sky 2.4 px,
  for 200 ms. On a phone's seat, whose field is taller, about one and a half times that. It is felt, not seen.

### Reduced motion

Nothing. The camera does not move, so nothing shifts; every layer stays at shift 0 and scale 1.

### Cost

- Objects: none. A position and a scale on each layer whose factor is under 1 (two sky plates, the fog, the far
  embers), written in `Phaser.Scene#update` from the numbers `ViewShake.state` already holds, only while the view
  moves. `setScrollFactor` is not used: the fitted camera has a zoom and a non-zero scroll, and a scroll factor would
  displace a plate for good.
- Nothing is added to `busy()`: the view's tremor and punch already keep the field at its full rate.
- The cancel path (`clearMotion`) gains: every layer's shift back to 0 and its scale to its own, as `layout` places
  it. A resize lays the plates again from the backdrop and never keeps a shift.
- Built by BC9: the factors in `table/battle-ground.js`, the shift and the counter-scale, the record's lines.

### On the film strip

Filmed at 390 by 844, 1×, from the repository with the bundle built, the sheets written outside it:

```bash
node scripts/battle/filmstrip.mjs spell:pummel:v1 --actor a1 --targets b1 --damage 8 --defeat --round 12 \
  --frames 36 --interval 40 --columns 6 --motion --out <folder>/depth-defeat.png
node scripts/battle/filmstrip.mjs spell:pummel:v1 --actor a1 --targets b1 --damage 8 --defeat \
  --frames 36 --interval 40 --columns 6 --motion --out <folder>/depth-defeat-round1.png
node scripts/battle/filmstrip.mjs spell:heavy_strike:v1 --actor a1 --targets b1 --damage 3 --round 12 \
  --frames 36 --interval 40 --columns 6 --motion --out <folder>/depth-light.png
```

- Depth-defeat, record, 0 to 800 ms: `ground.layers` every shift 0, every scale 1.
- Depth-defeat, record, 840 to 1120 ms, every frame: the sky plates' shift is 0.5 × the view's offset (within
  0.01 px), the fog's 0.25 ×, the far embers' 0.2 ×; the painting's, the broken floor's and the near embers' 0.
- Depth-defeat, record, 840 to 920 ms: the sky plates' scale below 1, (1 + 0.5 × (zoom − 1)) / zoom of the view's
  zoom over its fitted one, so that on the screen they zoom half of the view's punch (the first film: view zoom 1.024
  at 840 ms, the sky's scale 0.988, 1.012 on the screen; the punch's peak of 1.03 falls between two frames).
- Depth-defeat, record, from 1160 ms: every shift 0 and every scale 1, on every later frame.
- Depth-defeat-round1: the same lines for the fog and the far embers; no sky plate in the record at round 1.
- Depth-light (Strike, 3 lost): the shifts follow a view offset under 1 px, and every scale stays 1 (no punch).
- The sheets, 840 to 1080 ms: no seam on the horizon, no sliver of another colour at any edge of the field, the arch
  and the painting in register (no doubled edge).

A sheet cannot show a shift of 1 to 3 px for a few frames, nor the feel of it at full speed: read the record, and do
not judge the depth from the pictures.

### Left

- The camera parallax is small, by the camera's own measure: under 3 px of difference on a phone, for under 300 ms,
  and only from round 6 for the sky. It costs a few lines and no draw call, so it is staged; the owner may judge it
  not worth keeping.
- Not taken: the eclipse cut out of the painting at boot as its own far plate (the disc and its corona, about 400 by
  300 source px, 470 KiB of texture memory) at a factor of 0.3, so the sun holds still while the field shakes from
  round 1. Against it: shifted over its painted twin, its bright rim would double for a few frames. A film could try
  it in a later pass.
- Not taken: the painting's whole sky cut out at boot as a plate from round 1. It would give the sky its depth at every
  round, for 2.8 MB of texture memory (1,672 by 420), to move 1.5 px.
- No layer sways by itself at rest: a picture that slides under standing figures reads as a fault of the picture.
  Depth at rest is the air's ("The air at rest").
- The craft book's **Here** for "Parallax" says BC9 gives the plates scroll factors and that the camera work of BC5
  makes them show. Both are amended: the shift is made by hand, for the reason under Cost, and the camera's
  amplitudes make it felt rather than shown.

## The light that answers a cast

### Intent

When a blow lands hard, the eclipse answers: its corona brightens in the family's colour and the whole ground warms or
cools toward it, once, and goes back before the result is read. The place reacts to harm done in it, in proportion to
the harm, so a killing blow is felt in the sky and an ordinary round of jabs is not.

### Today

Filmed at `9b87cde`, 390 by 844, 1×: Emberstorm (`spell:meteor:v1`, elemental, a1 on b1, 5 lost), the whole action
and the contact at a frame every 60 ms; Pummel (8 lost, a defeat, round 12), the whole action.

- Emberstorm, 0 to 2640 ms: the painting, the eclipse and the sky the same on every frame. At 840 and 900 ms b1 is
  filled flat in bone (its hit flash); the fire's strokes play on b1 from 960 ms. The ground does not answer.
- Pummel, 960 ms: the strokes on b1 and its fall begin; the shattered sky and the eclipse are the same as at 720 ms
  and at 2640 ms. The ground never changes.

### Beats

The rows are a defeat, at full strength. The strength is read from what the host confirmed, as the camera's
trauma is: the health lost by recipients other than the caster, added up.

| At | Lasts | Window | Layer | Beat | Pattern |
| --- | --- | --- | --- | --- | --- |
| 0 | 840 | windup | the ground | Nothing. The light answers the contact; nothing anticipates it, so there is one change of light a cast. | [A light that answers a cast](../battle-craft.md#a-light-that-answers-a-cast) |
| 840 | 100 | impact | the ground (the corona) | The rise, eased `Quad.Out` (*house*, the book's 100 ms): one ring of light on the eclipse's rim, an image of a soft annulus drawn at boot, additive, centred on the disc (source x 846, y 135): clear inside 0.9 × the disc's radius, at its peak at 1.08 ×, faded to nothing at 1.7 × (*house*). Tinted the family's colour (`PALETTE` in `battle/src/spell-effects.ts`), its alpha from 0 to 0.8 × strength (*house*; at 0.35 the second build's ring could not be found on the sheets, at 0.8 a medium blow shows a thin cool rim and a heavy one a pale corona round a disc that stays dark). From the shattered sky (round 10) the ring is laid 1.5 times larger, clear inside 1.35 × the disc's radius, at its peak at 1.62 ×, gone at 2.55 × (*house*): the cracked halves stand out to 1.24 × the radius on the left and 1.33 × on the right, and the round-12 film at 0.8 laid the ring's band over their dark rock as a grey veil (the rock's outer rim lifted from about 40 to about 120 levels). The disc stays dark: the corona brightens, the eclipse is not lit from within. Depth -9.45: over the sky plates, under the arch, the faults and every figure, so it lights the sky behind a figure and never covers one. | [A light that answers a cast](../battle-craft.md#a-light-that-answers-a-cast), [Additive light](../battle-craft.md#additive-light) |
| 840 | 100 | impact | the ground (the tint) | With the same counter, the painting's, the plates' and the arch's tint go from their rest (`0xb5bbc2`, the arch `0xa5b1b3`) toward the family's colour, half the way × strength (*house*), through `Phaser.Display.Color.Interpolate.ColorWithColor`. | [A light that answers a cast](../battle-craft.md#a-light-that-answers-a-cast), [Flash limits](../battle-craft.md#flash-limits) |
| 940 | 200 | impact | the ground | The fall, eased `Sine.In` (*house*): the ring to 0 and hidden, every tint back to its rest. Gone at 1140 ms, 300 ms after contact; on a film, which moves the counter from the tick after contact, at 1160 ms. | [A light that answers a cast](../battle-craft.md#a-light-that-answers-a-cast), [The hold before the number](../battle-craft.md#the-hold-before-the-number) |
| 1140 | 1400 | impact | the ground | Nothing new: the ground as it rests, the air going on. The result is read here. | [The hold before the number](../battle-craft.md#the-hold-before-the-number) |

What each action gets, by what the ground knows (the family, the health lost, a fizzle; never the spell):

- 0 to 3 lost by others, in all (Strike's 3, Pummel's 2, Quill's 3; a film may tell a larger loss with `--damage`): none. The base actions are seen every round: a
  light on each would be a flash a turn, and the light would mean nothing.
- 4 to 6 lost (most family spells): 0.5. A medium blow: the corona brightens a little.
- 7 or more lost, or a defeat (the capstones, any killing blow): 1. A heavy blow: the one change of light the field
  is staged for.
- A heal, a ward, a figure struck with no loss: none. The place answers harm, as the view's tremor does; a heal's
  light is the spell's own.
- A blood cost alone: none. The caster's own price is the spell's (BC8).
- A fizzle: none. Nothing landed.
- A neutral action (the laboratory's sample, Focus): none. No family colour to answer in.

- The strength scales on the health lost, on the movement's own bands (light, medium, heavy), not on the Stage: the
  ground does not know the spell. A Stage III spell gets full strength because it deals heavy harm, and a Stage I
  blow that kills gets it too, for the same reason the camera punches on it.
- The colours: physical `0xd2bbac`, biological `0xadc777`, elemental `0x8dcfe5`, necromantic `0x85c9c2`, astral
  `0xb9a6e5`. None is the target marker's gold.
- One change of light a cast: the ring and the tint are one counter, one rise and one fall: a single
  `addCounter` from 0 to the rise and the fall together, its shape (the rise's ease, then the fall's) computed in its
  `onUpdate`. Two counters chained by `onComplete` lose a tick between them, and at 2× that tick is a fifth of the fall.
  It is the field's only one: a staging's `glow` is on a figure, its `flash` on a figure.
- The flash limits: halfway to the family's colour changes the field's relative luminance by 1% (necromantic,
  biological) to 7% (astral), under the 10% the W3C counts as a flash; the ring covers the corona, a band about 30 px
  wide around a disc of radius 38 on the bench's field, about 9,400 px² of the field's 125,000 (8%), well under a quarter of a 10° field even on a phone, where the whole field is about 10°. At 0.8 × strength the round-1 defeat at 960 ms turns 29 pixels near white, where the ring crosses the bright clouds under the disc behind b1, and none on the disc: one change of light, once, so the alpha holds to the limits. At 2× an action lasts about 1.5 s and plays one rise and fall: under one a
  second.
- The craft book's fall is 400 ms; here it is 200, so the light is gone 300 ms after contact, and 320 on a film that
  moves it from the tick after contact, as every bright beat must be ([the camera](movement.md#the-camera)). The first
  film, with a fall of 220, still showed strength 0.14 at 1160 ms. See **Left**.

### Reduced motion

Nothing: no light, no tint change. The ground rests at its tint.

### Cost

- Objects: one image, the ring, made at boot, hidden and at alpha 0 at rest, and one counter tween a cast
  (`Phaser.Tweens.TweenManager#addCounter`) that drives the ring and the tints. Its texture is drawn at boot, white, a
  soft annulus on a canvas of 128 by 128 (64 KiB of texture memory; no picture file), and tinted the family's colour.
  Draw calls: one more, the ring's own (additive), during the 300 ms it plays. The tints cost nothing. The
  `PointLight` of the first build goes: it peaks at its centre, and a light at the disc's centre fills the eclipse.
- The tween counts in `busy()` as every tween does: the light plays at the full rate.
- The cancel path (`clearMotion`), which a skip, a rotation, a seat change and a hidden tab all run: `killAll` stops
  the counter, and the path gains the ring's alpha set to 0 and the ring hidden, and every tint set back to its
  rest (the painting and the plates `0xb5bbc2`, the arch `0xa5b1b3`). A plate that loads during a light is tinted at
  its rest and stays so.
- The ring's place and size follow the backdrop's layout: it is laid again on a resize, from the source point and
  the disc's radius, and leans with the sky plate (factor 0.5) while the view trembles, as the light did.
- Built by BC9: the ring and its texture, the counter, what the scene reads at the impact (the family, the health lost by others, a
  defeat), the strength's bands in `table/battle-ground.js`, the record's lines.

### On the film strip

Filmed at 390 by 844, from the repository with the bundle built, the sheets written outside it:

```bash
node scripts/battle/filmstrip.mjs spell:meteor:v1 --actor a1 --targets b1 --damage 5 \
  --frames 36 --interval 40 --columns 6 --motion --out <folder>/light-medium.png
node scripts/battle/filmstrip.mjs spell:pummel:v1 --actor a1 --targets b1 --damage 8 --defeat --round 12 \
  --frames 36 --interval 40 --columns 6 --motion --out <folder>/light-defeat.png
node scripts/battle/filmstrip.mjs spell:heavy_strike:v1 --actor a1 --targets b1 --damage 3 \
  --motion --out <folder>/light-strike.png
node scripts/battle/filmstrip.mjs spell:pummel:v1 --actor a1 --targets b1 --fizzle \
  --motion --out <folder>/light-fizzle.png
node scripts/battle/filmstrip.mjs spell:rejuvenate:v1 --actor a1 --targets a2 \
  --motion --out <folder>/light-heal.png
node scripts/battle/filmstrip.mjs spell:meteor:v1 --actor a1 --targets b1 --damage 5 --speed 2 \
  --frames 36 --interval 20 --columns 6 --motion --out <folder>/light-medium-2x.png
```

- Light-medium (Emberstorm, 5 lost), 800 ms: the eclipse as at 0 ms. 920 and 960 ms: a cool rim around the disc,
  the corona paler and cooler than at 800 ms, the sky just around it a little lighter; the disc itself as dark as at
  800 ms (against the sheet of today, its middle lifted by under 10 levels a channel), and no pixel of the disc
  near white; the ring centred on the disc within 2 px; b1 and b2 not covered or brightened
  (the ring is behind them). 1160 ms: the eclipse as at 0 ms.
- Light-medium, record: `ground.light.strength` 0 up to 840 ms, rising at 880 and 920 ms, its peak 0.5 at 960 ms,
  falling from 1000 ms, 0 from 1160 ms on every frame; `ground.light.colour` `0x8dcfe5`; the painting's tint
  `0xb5bbc2` at 800 ms and from 1160 ms.
- Light-defeat (Pummel, 8 lost, a defeat, round 12): the same shape at strength 1, colour `0xd2bbac`; the ring lies
  outside the shattered sky's cracked halves: their rock, out to 1.2 × the disc's radius, as dark as at 800 ms
  (within 10 levels a channel at the median; the round-3 film: 3 to 7), only its last pixels at the outer edge
  touched by the ring's soft inner rise, and the bright crack inside the disc no brighter than at 800 ms;
  at 920 to 960 ms the whole floor a shade warmer than at 800 ms; from 1160 ms every tint at its rest. The command is
  depth-defeat's: one film reads for both.
- Light-strike, light-fizzle, light-heal: `ground.light.strength` 0 on every frame; the ground the same on every
  frame.
- Light-medium-2x: the peak at 480 to 500 ms (contact at 420 ms, the counter moved from 440), strength 0 again from
  600 ms (150 ms of counter after the tick that starts it).
- Every sheet: on no frame from 1160 ms at 1× is the light's strength above 0, and no frame shows the field in one
  flat colour.

A sheet cannot show the rise's ease or how the change of light feels at full speed, and a 7% change of the floor's
tint is hard to see between two frames: read the record for the strength and the colour.

### Left

- The fall is 200 ms, not the book's 400: the light is a bright beat, and since BC5 every bright beat is gone 320 ms
  after contact so the result is read on a quiet field. The book's **Here** for "A light that answers a cast" is to
  say so. If the film shows 200 ms reads as a blink, the other way is a 400 ms fall with the hold's rule amended for a
  light that only dims.
- The light is on the eclipse, not under the cast or the target: the figure already has its hit flash and the
  spell's own glow, and one figure gets one flash an action. The eclipse is the place's one source of light, so it is
  what answers. The owner may want the light where the blow lands.
- The light answers harm only. A heal, a ward and the astral family's own vitality do not move it, though the astral
  spells speak of the eclipse; the owner may want them to.
- The strength is read from the health lost, so it says nothing the frame does not hold (ADR 0131), but at contact it
  shows a heavy blow's weight before the number is read, as the movement's anticipation does.
- A ring, not the book's `PointLight`. The first film put a `PointLight` at intensity 0.5 × strength on the disc: a
  point light is brightest at its centre, so it filled the dark disc with a pale ball, near white at its middle at
  strength 0.5 (the disc's pixels lifted by about 70 levels) and blown out over the shattered sky's crack at
  strength 1. It read as a second sun lit in the sky, a flash of its own beside the target's, and no longer as an
  eclipse whose corona answers. The ring is an image drawn at boot ("Textures drawn at boot") and drawn additive
  ("Additive light"), both in the book; the entry's **Phaser** line names a point light at the cast, and its **Here** now says
  that on a dark disc the light is a ring. Over the brightest clouds of the corona the ring may still turn white: the
  second film, at 0.8, turned 29 pixels so, under the disc, on a heavy blow at round 1.
- Not built, after the last round (BC9, round 3): at round 12, at the defeat's peak (960 ms), the ring's soft inner
  rise still lifts the outermost few pixels of the cracked halves' rim (at 1.2 to 1.33 × the disc's radius, by about
  50 levels at the 75th percentile); the rock's body holds. If the owner sees the rims lit, the shattered ring's clear
  radius goes from 1.35 to 1.45 × (*house*), its peak and its end with it.
- Not staged: "A scar that fades". The craft book's entry names BC9, but the plan's line for BC9 does not, and a
  scar is a mark under the target, the spell's place, not the ground's answer. It waits for its own pass; the book's
  **Here** no longer names BC9 for it.
