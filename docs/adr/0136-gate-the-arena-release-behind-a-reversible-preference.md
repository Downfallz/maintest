# 0136 — Gate the arena release behind a reversible preference

Date: 2026-11-05

Status: Accepted for the opt-in release candidate; physical-device acceptance remains open.

## Context

A7 (#432) completed the game loop, and AU1–AU14 (#440–#464) brought the arena to the classic table's
parity, recorded in [the AU14 acceptance record](../tabletop/arena-ux-acceptance.md). A working,
parity-checked match does not establish how a player reaches it, mobile performance, download size or
behaviour across browser engines.

## Decision

Keep the classic table as the default. The lobby offers a browser-local Table/Arena preference for
opening accepted seats, including newly created games. Invitations and sign-in retain their existing
flows. No automatic redirects or account/seat migrations are introduced. `/play` remains directly usable;
the arena's Table link retains the selected table and viewpoint, and a renderer failure exposes the same
link inside its recovery panel. Invalid or unavailable preference storage defaults to the table.

Sound is not part of this decision: ART-6's opt-in playback sound ([ADR 0134](0134-polish-confirmed-playback-with-opt-in-sound.md))
already starts silent, needs a gesture and stops on access loss. A release candidate adds no second engine.

Enforce build budgets for the live bundle (1,600 KiB raw, 512 KiB gzip) and the art a visit loads (2 MiB):
the creature sprites the scene loads and the files the live bundle names. A file still served but no
longer loaded is not counted. The gzip value is a reproducible size estimate, not a promise that the host
compresses responses. Browser release tests retain resource sizes and sampled frame intervals; runtime
telemetry is not added. A separate CI job plays complete matches in desktop Chromium, a touch Android
profile and iOS WebKit. These are engine/emulation gates, not physical-device certification.

## Consequences

The opt-in candidate can be reviewed and rolled back per browser without changing a match. Engine rules,
authorization, recorded history and command fencing are unchanged. Preference persistence is best effort;
storage-disabled browsers can still choose a view for the current lobby visit. A change that crosses a
budget fails the build and has to raise the budget explicitly, with its reason.

Physical Android/iOS GPU, browser chrome, thermal/battery behavior and assistive technology remain release
checks in [the acceptance record](../tabletop/arena-release-acceptance.md). A public default switch requires
that evidence; merging this candidate does not assert those checks passed.

## Validation

Pure tests cover the preference's default, invalid and blocked storage. Browser tests cover lobby
persistence and both destinations, keyboard focus, the 720×500 CSS viewport of a 200%-zoom desktop,
renderer recovery to the same seat and the art a cold scene fetches. Real-host matches play with sound
enabled in both declaration modes and two seats, and the release job repeats them in three browser profiles.
