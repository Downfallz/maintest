# 0129 — Gate the arena release behind a reversible preference

Date: 2026-11-04

Status: Accepted for the opt-in release candidate; physical-device acceptance remains open.

## Context

A7 (#432) completes the game loop. Its merged fixes preserve private simultaneous choices, clear a
mutation when changing creatures, make the dice-order choice visible, prevent repeated accepted
concessions, share the table's outcome wording, and keep read-only round review from discarding a draft.
A working match alone does not establish mobile performance, audio policy or accessibility.

## Decision

Keep the classic table as the default. The lobby offers a browser-local Table/Arena preference for
opening accepted seats, including newly created games. Invitations and sign-in retain their existing
flows. No automatic redirects or account/seat migrations are introduced. `/play` remains directly usable;
the arena's Table link retains the selected table and viewpoint, and a renderer failure exposes the same
link inside its recovery panel. Invalid or unavailable preference storage defaults to the table.

Enable synthesized action cues only after a checkbox gesture, for the current visible visit. A late
AudioContext resume cannot override mute, visibility suspension or a renderer failure. No audio files
are loaded; every outcome is still textual. Tone differences distinguish confirmed cue categories without
inventing damage. Disabling sound stops current voices, and sound never calls a game command.

Enforce build budgets for the live bundle (1,600 KiB raw, 512 KiB gzip) and initial WebP art (2 MiB).
The gzip value is a reproducible size estimate, not a promise that the host compresses responses.
Browser release tests retain resource sizes and sampled frame intervals; runtime telemetry is not added.
A separate CI job plays complete matches in desktop Chromium, a touch Android profile and iOS WebKit.
These are engine/emulation gates, not physical-device certification.

Keep provisional art explicit. ART-2 owns new sprites and catalogue identity; A8 does not replace that
parallel work with another asset scheme. Missing images still leave textual, selectable figures.

## Consequences

The opt-in candidate can be reviewed and rolled back per browser without changing a match. Engine rules,
authorization, recorded history and command fencing are unchanged. Preference persistence is best effort;
storage-disabled browsers can still choose a view for the current lobby visit.

Physical Android/iOS GPU, browser chrome, thermal/battery behavior and assistive technology remain release
checks in [the acceptance record](../tabletop/arena-release-acceptance.md). A public default switch requires
that evidence; merging this candidate does not assert those checks passed.

## Validation

Pure tests cover opt-in defaults, invalid/blocked storage, audio lifecycle, unavailable audio and late resume.
Browser tests cover lobby persistence and both destinations, keyboard focus, the 720×500 CSS viewport of a
200%-zoom desktop, renderer recovery to the same seat, silent startup and visibility mute. Existing suites
retain missing-art, reduced-motion, legal command, privacy, replay and lifecycle coverage. Real-host matches
exercise enabled sound and settings without an early decision, in both declaration modes and two seats.
