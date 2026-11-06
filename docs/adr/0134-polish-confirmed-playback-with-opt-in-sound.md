# 0134. Polish confirmed playback with opt-in sound

Date: 2026-11-04
Status: Accepted for the opt-in battle

## Context

ART-4/5 provide content-authored visual vocabulary and bounded mutation cues. The live player still uses
one speed, repeated timing literals can drift from the renderer, and only the lab has an impact sound.
Polish must preserve confirmed outcomes, reduced motion, privacy transitions and immediate cancellation.

## Decision

Keep choreography constants in the shared timing module and normalize live playback to 1×, 1.5× or 2×.
Pass the same rate to queue phases and renderer tweens. Changing it settles pending playback and fences
old callbacks, while reduced-motion outcomes retain 650 ms of reading time independent of the rate.
Add bounded motif-specific attack envelopes without moving electrical arc endpoints.

Share one small Web Audio owner between live and lab. It creates a context only after an explicit checkbox
gesture, starts disabled on each load, synthesizes one short family tone at confirmed impact, and owns at
most one oscillator/gain pair. No audio assets, ambient loop, network request or persisted opt-in. Repeated
polls, skipped events and manual history/replay do not create sounds. Reduced motion is independent.

Release voices whenever an animation is cancelled or replaced, without touching the opt-in: Skip and the
scene's own cancellations only silence. Fence asynchronous context resumes against mute and privacy
transitions (seat change, hidden page, lost connection, renderer failure), so a late completion cannot undo
them. A context the platform suspends under an opted-in player is asked back at the next impact. Unavailable audio does not affect rendering or decisions. Retain
ART-5's content-authored mappings, catalogue labels and per-figure depth; do not reintroduce page content IDs.

## Consequences

- Good: players shorten animation waits without the result moving: the outcome stays at the same logical
  impact boundary, the engine is not sped up, and reduced-motion reading time stays at 650 ms at any speed.
  Sound is an optional vocabulary with no asset or licensing pipeline.
- Bad: the tones are synthetic placeholders, not final recordings, and one tone per impact cannot tell
  apart several recipients. A tone cut by Skip ends abruptly rather than fading.
- Neutral: neither the speed nor the opt-in is persisted, so every load starts at 1× and silent. The lab
  shares the owner and its impact tone, now quieter (130 → 42 Hz at gain .055, was 150 → 42 Hz at .13).

## Alternatives considered

- Persist the speed or the opt-in in local storage: a returning player would be surprised by sound on a
  shared or public device, and the speed is a per-session choice; both stay one tap away in Settings.
- Ship recorded audio files: an asset, licensing and download budget before the vocabulary is approved.
- A voice per family or per recipient: overlapping tones on a multi-target cast are noise; one owner with
  one voice keeps the cleanup provable.
- Let a speed change re-time the action in flight: it would replay or skip part of a confirmed frame;
  settling to the latest board is what every other presentation change already does.

## Follow-up

- `battle-timing.js` holds the choreography constants and `playbackRate`; `battle-playback.js` scales its
  animation phases by the rate and leaves the reduced-motion receipt unscaled.
- `BattleAudio.stop()` fences and silences, `silence()` only releases voices; `scene.onCancel` silences.
- Queue tests cover each phase at each rate, the unscaled reduced receipt, settling, the callback fence and
  no replay. Browser tests cover the scene's rate, gesture-only audio, one tone per impact, cuts on Skip, a
  hidden page and a seat switch, the owner's fences against a controlled context, and the arc staying pinned.
- The [ART-6 handoff](../art/battle-polish.md) records the timings, lifecycle and limitations.
