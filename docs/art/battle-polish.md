# ART-6 — Playback rhythm, motion and sound

Built from merged ART-5 (`354eede`, #445), including the content-authored spell/mutation visuals, catalogue
names in inspection, table-driven motif painters and per-figure depth ordering. These boundaries remain:
no content IDs return to page code, and no timing or audio changes a domain result.

## Playback

The live settings now offer 1×, 1.5× and 2× playback. One normalized rate drives the queue and renderer;
projectile launch/arrival, melee anticipation and impact cannot choose independent clocks. The standard
420 ms windup, 850 ms outcome and 250 ms interval remain the default. The caster returns home over 360 ms
after a 60 ms impact hold. These timings and the 600 ms effect lifetime live in `battle-timing.js`.

A speed change during playback settles the entire pending queue to its latest authoritative board and
invalidates pending callbacks. It never repeats consumed events, predicts targets or delays a command.
Reduced motion skips anticipation/displacement, particles and camera motion; its outcome remains visible
for 650 ms even at 2×. Explicit Skip, help/history and opening the next command retain existing behavior.
The isolated lab retains its own 1×/2× control and now shares the same audio owner and choreography constants.

## Choreography

Motifs arrive with a short 100 ms attack then dissipate within the remaining impact window. Bones and
spirits rise slightly into place, vitality lifts, ground waves expand from a compact origin, and void
contracts. Electrical arcs retain fixed caster/recipient endpoints with no translation or scale. Unknown
motifs keep the neutral fallback. All movement scales with playback rate, uses confirmed recipients, and
is owned by cancellable Phaser tweens. Reduced motion retains the authoritative after-frame and DOM text.
Persistent mutation cues retain ART-5's transform/depth behavior and remain independent of temporary VFX.

This polishes shared motifs; it does not add unique skeletal poses for all spells, sound recordings, music,
persistent summons, or new anatomical assets. Those would require their own asset review and budget.

## Sound

Spell sounds start **off on each page load** and enable only through the settings checkbox. One lazily
created Web Audio context synthesizes quiet, short tones by presentation family: low physical impacts,
biological plucks, elemental tones, necromantic bass and astral rising tones. Unknown profiles and fizzles
use a neutral cue. No audio file, network request, microphone, autoplay or stored preference is involved.

One confirmed impact produces one tone, regardless of target count or repeated polls. Manual replay and
baseline/catch-up snapshots do not play audio. Reduced motion and sound remain independent preferences.
Only one oscillator/gain pair can be alive at once; each ends within 320 ms and disconnects. Skip and scene
cancellation release the voice without touching the opt-in, so ticking the box while a cast plays sticks.
Mute, seat switches, hidden pages, a lost connection and renderer failures also fence a pending resume with
a generation counter, so it cannot enable sound after them. A context the platform suspends (a backgrounded
tab, a phone call) is asked back at the next impact. Unavailable or denied audio leaves the checkbox off and
never blocks gameplay. The lab's impact tone is the shared physical one, quieter than the prototype's.

## Validation

- Queue tests cover exact phase timings at all rates, minimum reduced-motion reading time, invalid rates,
  duplicate polls and cancelled callback fences.
- Browser tests exercise the production scene's emergence/cleanup, live speed changes and reduced-motion
  results. Real Web Audio instrumentation checks zero contexts before opt-in, one sound per confirmed
  impact, skipped/duplicate events and muting. A production-class test covers delayed resume cancellation,
  unavailable contexts and deterministic voice cleanup.
- Run all seven battle browser suites, including AU2 guidance, plus the static page tests, build, .NET tests
  and formatting gate. Capture actual desktop, narrow-phone and landscape rendering/settings.
