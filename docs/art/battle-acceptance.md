# The battle's craft: acceptance record (BC10)

What [the battle's craft plan](battle-craft-plan.md) built in BC1 to BC9, measured as a whole: the frame time of
every staging with the processor slowed, the budgets, less motion across all 44 spells, and the gallery of film
strips. Measured on 2026-11-19 on `main` at `c10507f`, the bundle built in the session.

It is a record of one machine on one day. The tests named here hold what can be held on every change; the
numbers are kept to be compared with, by `node scripts/battle/frametime.mjs`.

## The answer in short

| Question | Answer |
| --- | --- |
| Does a staging's code fit in a frame on a slow processor? | Yes, all 44. Slowed four times, the worst step of the worst staging takes 11.8 ms of a 16.7 ms frame; the medians are 1.3 to 3.1 ms. |
| Does a staging drop frames while it plays? | 26 of the 44 drop none, slowed four times. 12 double a frame now and then (33 ms), Contagion the most: 12 frames of its play. The 6 that hold a `pulse` stall: two or three frames of about 300 ms each. |
| Is the stall a phone's? | Not known. It is the renderer's cost, and a session draws in software. The page's thread pays nothing for a pulse. A phone's graphics card decides, and that is the physical gate's. |
| The budgets | All five hold. The effects kit is 11,045 of its 49,152 bytes. |
| Less motion | Every one of the 44 goes straight to its settled frame: no beat, no motif, no figure moved, no view moved. |
| The gallery | The 44 film strips are on one page for the owner. |

## Frame time

### How it is measured

`scripts/battle/frametime.mjs` plays each effect on the effects bench (`/battle-lab?bench`) in Chromium, in a
viewport of 390 by 844, the size the film strips use. The browser's own CPU throttle slows the page's thread
four times, the rate Lighthouse uses for a mid-range phone. A spell is cast by `a1` on as many figures as its
content lets it reach, and each figure struck loses 5 health, a medium blow. Each effect is measured three times
and each number is the middle one of the three.

Two measures, because the throttle slows one thing and not the other:

- **The step**: what one 20 ms step of the game costs the page's thread, in milliseconds. The bench's film clock
  steps the whole action, 127 steps at 1×, and each step is timed where it runs: the scene's update and the
  drawing calls it issues. The throttle slows this, so this is the number that speaks for a slow phone's
  processor. A step is over when it takes more than one frame of a sixty-hertz screen, 16.7 ms.
- **The frame**: the time between two animation frames while the effect plays on the page's own loop, from the
  play to the resolved action. It holds everything: the page's thread, and what the renderer does with the
  drawing calls. A frame is long when it takes two frames or more (32.3 ms).

### The machine, and what the frame does not say

Chromium 157 headless on Linux, in a cloud session with four processors and no graphics card. WebGL is drawn by
SwiftShader, a software renderer (`ANGLE (Google, Vulkan 1.3.0 (SwiftShader Device (Subzero)))`). The device
pixel ratio is 1: at a phone's 3 the software renderer takes 67 ms to draw the field at rest, so nothing could
be read from it.

So the frame's number compares one staging with another on this machine. It is not a phone's frame rate, and
no line here promises one. The throttle does not slow the renderer: the stall of a `pulse` is the same with no
throttle at all (the second table). What a phone's graphics card does with these pixels is not measured.

### Every staging, slowed four times, at 1×

Times in milliseconds. ◌ marks a staging that holds a `pulse`. The three last rows are the bench's samples: the
motif alone, one pulse on each of three figures, and seven streams of particles on each of three figures, which
fill the field's 150.

| Effect | Figures | Beats | Step, median | Step, 95th | Step, worst | Steps over a frame | Frame, median | Frame, worst | Long frames |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| Alpha Roar ◌ | 1 | 10 | 3.1 | 6.4 | 11.8 | 0 | 16.7 | 300.0 | 3 |
| Ambush | 1 | 6 | 1.9 | 4.4 | 8.3 | 0 | 16.7 | 33.3 | 2 |
| Bite | 1 | 6 | 2.3 | 4.9 | 9.2 | 0 | 16.7 | 33.3 | 3 |
| Blood Hunt | 2 | 9 | 2.1 | 4.3 | 8.8 | 0 | 16.7 | 33.3 | 2 |
| Blood Price | 1 | 10 | 1.7 | 3.7 | 7.5 | 0 | 16.7 | 33.4 | 1 |
| Body Slam | 1 | 7 | 1.7 | 3.9 | 6.7 | 0 | 16.7 | 33.4 | 1 |
| Bonewall | 1 | 6 | 1.7 | 3.7 | 7.8 | 0 | 16.7 | 16.8 | 0 |
| Brace | 1 | 4 | 1.4 | 3.4 | 5.0 | 0 | 16.7 | 16.8 | 0 |
| Carapace | 1 | 6 | 2.4 | 4.5 | 6.8 | 0 | 16.7 | 16.8 | 0 |
| Contagion | 3 | 9 | 2.1 | 6.8 | 10.6 | 0 | 16.7 | 33.4 | 12 |
| Crash | 1 | 6 | 2.4 | 5.2 | 11.7 | 0 | 16.7 | 33.4 | 4 |
| Crushing Stomp ◌ | 1 | 11 | 2.9 | 6.3 | 8.8 | 0 | 16.7 | 300.1 | 3 |
| Death Wail | 3 | 10 | 2.1 | 5.4 | 8.9 | 0 | 16.7 | 33.4 | 3 |
| Deep Freeze | 1 | 9 | 1.7 | 3.2 | 7.0 | 0 | 16.7 | 16.8 | 0 |
| Deranged Charge | 1 | 9 | 1.7 | 3.9 | 6.4 | 0 | 16.7 | 16.8 | 0 |
| Devour ◌ | 1 | 10 | 2.7 | 5.1 | 8.1 | 0 | 16.7 | 316.6 | 3 |
| Dominate | 2 | 9 | 2.1 | 5.1 | 7.7 | 0 | 16.7 | 33.3 | 1 |
| Emberstorm | 3 | 7 | 1.6 | 3.4 | 6.1 | 0 | 16.7 | 16.8 | 0 |
| Eviscerate | 1 | 9 | 1.6 | 3.4 | 6.2 | 0 | 16.7 | 16.8 | 0 |
| Focus | 1 | 2 | 1.3 | 3.2 | 4.4 | 0 | 16.7 | 16.8 | 0 |
| Fury | 1 | 7 | 1.5 | 3.0 | 5.3 | 0 | 16.7 | 16.8 | 0 |
| Groundbreaker ◌ | 3 | 11 | 2.9 | 5.4 | 8.7 | 0 | 16.7 | 299.9 | 3 |
| Ice Grip | 1 | 6 | 1.5 | 3.5 | 4.5 | 0 | 16.7 | 16.8 | 0 |
| Incinerate | 1 | 9 | 1.5 | 3.2 | 4.9 | 0 | 16.7 | 16.8 | 0 |
| Infestation | 3 | 6 | 2.1 | 4.9 | 9.6 | 0 | 16.7 | 33.3 | 2 |
| Latch | 1 | 6 | 1.7 | 3.3 | 6.3 | 0 | 16.7 | 16.8 | 0 |
| Overdrive | 1 | 6 | 1.5 | 4.2 | 5.8 | 0 | 16.7 | 16.8 | 0 |
| Paralyzing Barb | 1 | 10 | 1.6 | 3.3 | 5.2 | 0 | 16.7 | 33.3 | 1 |
| Pummel | 1 | 4 | 1.6 | 3.7 | 6.4 | 0 | 16.7 | 16.8 | 0 |
| Pursuit | 1 | 6 | 1.9 | 3.6 | 5.5 | 0 | 16.7 | 16.8 | 0 |
| Quill | 2 | 4 | 1.6 | 4.4 | 9.7 | 0 | 16.7 | 16.8 | 0 |
| Renewal Burst ◌ | 1 | 9 | 1.5 | 3.7 | 4.9 | 0 | 16.7 | 283.3 | 3 |
| Revitalize | 1 | 4 | 1.4 | 3.5 | 5.7 | 0 | 16.7 | 16.8 | 0 |
| Seize | 1 | 6 | 1.4 | 3.4 | 5.0 | 0 | 16.7 | 16.8 | 0 |
| Shock | 1 | 4 | 1.4 | 2.9 | 7.3 | 0 | 16.7 | 16.8 | 0 |
| Strike | 1 | 2 | 1.9 | 3.7 | 6.4 | 0 | 16.7 | 16.8 | 0 |
| Toxic Mend | 1 | 6 | 1.8 | 4.7 | 5.8 | 0 | 16.7 | 16.8 | 0 |
| Venom Claw | 1 | 4 | 1.5 | 3.4 | 5.8 | 0 | 16.7 | 16.8 | 0 |
| Vital Echo | 1 | 7 | 1.8 | 3.9 | 4.9 | 0 | 16.7 | 16.8 | 0 |
| Vital Surge ◌ | 1 | 9 | 2.0 | 3.7 | 5.0 | 0 | 16.7 | 300.0 | 2 |
| Void Pulse | 3 | 9 | 2.3 | 4.8 | 9.1 | 0 | 16.7 | 33.3 | 2 |
| Whisper | 1 | 6 | 2.1 | 3.4 | 5.8 | 0 | 16.7 | 16.8 | 0 |
| Wild Swing | 1 | 6 | 1.4 | 2.9 | 4.7 | 0 | 16.7 | 16.8 | 0 |
| Wraithguard | 1 | 9 | 2.3 | 5.0 | 7.9 | 0 | 16.7 | 16.8 | 0 |
| Kit: no staging | 3 | 0 | 1.6 | 3.5 | 6.9 | 0 | 16.7 | 16.8 | 0 |
| Kit: pulse ◌ | 3 | 2 | 1.6 | 3.4 | 8.3 | 0 | 16.7 | 833.3 | 4 |
| Kit: budget | 3 | 8 | 2.0 | 4.9 | 7.9 | 0 | 16.7 | 33.3 | 1 |

### What the table says

- **The page's thread has room.** No step of any staging is over a frame. The costliest stagings (Alpha Roar,
  Crushing Stomp, Groundbreaker) step in about 3 ms at the median, twice the motif alone (1.6 ms). The number
  of beats and of figures moves the step by a millisecond or two, not by a frame.
- **The particles' cap costs little here.** The sample at the cap steps in 2.0 ms and doubles one frame.
- **A pulse stalls the software renderer, by the figure.** One pulse: two or three frames of about 300 ms. Three
  pulses, the sample: frames of 833 ms. The step of the same stagings stays under 12 ms, so the whole cost is
  the filter's passes in the renderer. This is what ADR 0196 warned of; it is now measured, and it grows with
  each figure that wears one.
- **Twelve stagings double a frame.** Half of them are cast on two or three figures. Contagion (three figures, nine
  beats, a cloud that hangs) doubles 12 frames, Crash 4, Bite and Death Wail 3, the others 1 or 2. With no throttle none of them does (the second table).

## At other settings

Eight effects measured again: with no throttle, slowed six times, and slowed four times at the 2× speed, where
an action lasts half as long. The rows "Slowed 4 times" repeat the first table.

| Effect | Setting | Step, median | Step, worst | Steps over a frame | Frame, worst | Long frames |
| --- | --- | ---: | ---: | ---: | ---: | ---: |
| Kit: no staging | No throttle | 0.6 | 2.0 | 0 | 16.8 | 0 |
| Kit: no staging | Slowed 4 times | 1.6 | 6.9 | 0 | 16.8 | 0 |
| Kit: no staging | Slowed 6 times | 4.6 | 16.5 | 0 | 33.4 | 8 |
| Kit: no staging | Slowed 4 times, played at 2× | 2.2 | 8.6 | 0 | 16.8 | 0 |
| Kit: pulse | No throttle | 0.4 | 4.2 | 0 | 816.6 | 4 |
| Kit: pulse | Slowed 4 times | 1.6 | 8.3 | 0 | 833.3 | 4 |
| Kit: pulse | Slowed 6 times | 3.8 | 17.6 | 1 | 883.3 | 5 |
| Kit: pulse | Slowed 4 times, played at 2× | 2.3 | 15.4 | 0 | 833.3 | 3 |
| Kit: budget | No throttle | 0.3 | 1.6 | 0 | 16.8 | 0 |
| Kit: budget | Slowed 4 times | 2.0 | 7.9 | 0 | 33.3 | 1 |
| Kit: budget | Slowed 6 times | 3.1 | 11.2 | 0 | 33.4 | 8 |
| Kit: budget | Slowed 4 times, played at 2× | 2.0 | 5.6 | 0 | 16.8 | 0 |
| Strike | No throttle | 0.3 | 1.4 | 0 | 16.8 | 0 |
| Strike | Slowed 4 times | 1.9 | 6.4 | 0 | 16.8 | 0 |
| Strike | Slowed 6 times | 3.3 | 14.1 | 0 | 33.4 | 3 |
| Strike | Slowed 4 times, played at 2× | 1.7 | 9.9 | 0 | 16.8 | 0 |
| Alpha Roar | No throttle | 0.5 | 1.8 | 0 | 266.7 | 2 |
| Alpha Roar | Slowed 4 times | 3.1 | 11.8 | 0 | 300.0 | 3 |
| Alpha Roar | Slowed 6 times | 3.3 | 10.5 | 0 | 300.0 | 6 |
| Alpha Roar | Slowed 4 times, played at 2× | 2.2 | 5.9 | 0 | 283.4 | 2 |
| Contagion | No throttle | 0.6 | 4.7 | 0 | 16.8 | 0 |
| Contagion | Slowed 4 times | 2.1 | 10.6 | 0 | 33.4 | 12 |
| Contagion | Slowed 6 times | 4.2 | 24.8 | 1 | 33.4 | 22 |
| Contagion | Slowed 4 times, played at 2× | 2.6 | 13.6 | 0 | 33.4 | 4 |
| Death Wail | No throttle | 0.4 | 2.6 | 0 | 16.8 | 0 |
| Death Wail | Slowed 4 times | 2.1 | 8.9 | 0 | 33.4 | 3 |
| Death Wail | Slowed 6 times | 3.3 | 12.4 | 0 | 50.0 | 12 |
| Death Wail | Slowed 4 times, played at 2× | 2.6 | 8.7 | 0 | 33.4 | 3 |
| Groundbreaker | No throttle | 0.4 | 1.6 | 0 | 283.4 | 2 |
| Groundbreaker | Slowed 4 times | 2.9 | 8.7 | 0 | 299.9 | 3 |
| Groundbreaker | Slowed 6 times | 3.5 | 11.2 | 0 | 316.6 | 13 |
| Groundbreaker | Slowed 4 times, played at 2× | 2.1 | 6.9 | 0 | 266.7 | 3 |

- Slowed six times, two steps go over a frame: one of Contagion's (24.8 ms) and one of the pulse sample's
  (17.6 ms). The motif alone reaches 16.5 ms there: at that rate the scene itself is at its edge, with or
  without a staging.
- At 2× no step is over a frame and no more frames are long than at 1×: a staging plays the same beats in half
  the time.

## What follows for the kit

- **The pulse stays on one figure.** ADR 0196 asked for this measure before a staging used a pulse on more than
  one. The answer is no: the cost adds up by the figure. `studio/staging-ladder.test.js` already refuses a pulse
  on a spell with several targets, and keeps one pulse to a Stage III spell. Six spells hold one today.
- **The 24 particles a beat and the 150 on the field stand.** The measure gives no reason to lower them, and
  none to raise them: the pixels they fill on a phone are not measured.
- **No staging is changed by this stage.** Nothing measured here is a fault on a phone until a phone says so.

## The budgets

`battle/budget.mjs`, run at the end of every build of the bundle (ADR 0136, ADR 0196).

| Resource | Measured bytes | Limit | Left |
| --- | ---: | ---: | ---: |
| Art a visit loads (the ground and four creature bodies) | 1,528,370 | 2,097,152 | 568,782 |
| Live JavaScript | 1,739,524 | 1,769,472 | 29,948 |
| Live JavaScript, gzip | 493,802 | 524,288 | 30,486 |
| The effects kit, `fx.js` | 11,045 | 49,152 | 38,107 |
| The effects kit, gzip | 4,287 | 16,384 | 12,097 |

The plan started from a live bundle of 1,711,869 bytes: nine stages of movement, ground and 44 stagings added
27,655 bytes to it and no picture file. The 311 beats of the 44 stagings are content, in
`data/presentation/spell-art.json`, and ride on each spell's card. The live bundle has 30 KiB left: the next
large feature of the battle view meets that limit before any other.

## Less motion

With less motion asked for, by the system or by the page's setting, an action is shown as its settled frame
(`battle/AGENTS.md`). Held for the whole set by `studio/browser/battle-stagings.spec.js`: each of the 44 entries
is played on the bench on the figures it reaches, each struck for 5 health, and after each one

- the kit holds no object, no particle, and has changed no figure;
- no motif is drawn;
- every figure stands at its home, upright, at its own size, untinted;
- the view has no trauma, no offset and no zoom.

The body and the breath (`battle-motion.spec.js`) and the ground (`battle-ground.spec.js`) hold their own part:
no breath, no drifting fog, no rising ember, no light on the eclipse.

## The rules held across the 44

| Rule | Held by |
| --- | --- |
| Every entry of the spell art is staged, and the page reads its staging | `studio/battle-frametime.test.js` |
| One `flash`, one `shake` and one `motif` a staging at most | `table/battle-staging.js`, which reads no staging that breaks it |
| A staging stays inside its band of the ladder: beats, particles, lights, pulses, shake | `studio/staging-ladder.test.js` |
| A pulse is on the caster, or on a spell with one target | `studio/staging-ladder.test.js` |
| Every beat is seen, the field stays inside 150 particles, nothing is left after the action or after a stop | `studio/browser/battle-stagings.spec.js` |
| This record measures every entry of the spell art | `studio/battle-frametime.test.js` |

## The gallery

The 44 film strips, one sheet a spell, by family and by Stage, are on one page for the owner:
[the gallery](https://claude.ai/artifact/M81YCyHog3TBW5pAe54nha), private to the owner, with each spell's step and frame beside its sheet. Each spell is filmed on the figures it reaches, each struck for 5 health, twelve frames 240 ms apart.
The sheets are not committed. `node scripts/battle/filmstrip.mjs --all` films them again, and a sheet is compared
only with one filmed on the same machine.

A sheet shows frames, not time. It cannot say how an effect feels at full speed, and neither can this record.

## What stays open

- **A phone.** The physical-device gate of [the battle's release record](../tabletop/battle-release-acceptance.md)
  is still pending, and this stage adds two things to watch there: a spell with a pulse (Alpha Roar, Crushing
  Stomp, Devour, Groundbreaker, Renewal Burst, Vital Surge), and Contagion on three figures. If a phone stalls on
  a pulse, the cheap halo of the craft book (a soft sprite behind the figure) replaces the filter in
  `battle/src/fx/pulse.ts`, and no staging changes.
- **The breath and the air at the resting rate.** BC5 and BC9 left "nobody has seen it on a phone". Nobody has:
  it is the same gate.
- **A number held by a test.** None is: a frame time in a shared runner moves too much to gate a pull request.
  The script is run by hand when a staging adds a pulse or many particles.
