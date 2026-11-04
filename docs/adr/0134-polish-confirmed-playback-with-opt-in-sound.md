# 0134. Polish confirmed playback with opt-in sound

Date: 2026-11-04
Status: Accepted for the opt-in arena

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

Stop and disconnect voices on cancellation; fence asynchronous context resumes so a late completion cannot
undo mute, Skip or a privacy transition. Unavailable audio does not affect rendering or decisions. Retain
ART-5's content-authored mappings, catalogue labels and per-figure depth; do not reintroduce page content IDs.

## Consequences

The result remains authoritative at the same logical impact boundary while players can shorten animation
waits. Faster playback does not speed the engine or shrink reduced-motion reading time. Synthetic tones
establish an optional sound vocabulary without an asset/licensing pipeline; they are not final recordings.
The [ART-6 handoff](../art/arena-polish.md) records timings, lifecycle behavior, limitations and validation.
