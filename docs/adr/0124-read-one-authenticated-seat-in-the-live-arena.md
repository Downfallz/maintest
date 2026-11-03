# 0124. Read one authenticated seat in the live arena

Date: 2026-11-03
Status: Accepted for the opt-in read-only integration

## Context

The isolated Phaser experiment (ADR 0123) and the shared presentation boundary now exist. The next
slice must render a real game without adding another authority for rules, exposing the other seat's
private data or changing the established table's command and acknowledgement lifecycle.

## Decision

Serve a separate `/arena` entry point alongside `/arena-lab` and `/play`. Keep the laboratory on its
sample reducer. The live bundle may share the renderer, geometry and styles, but cannot import the
sample fixture or state machine. Both clients use `boardPresentation` to read the host's timeline,
activation cursor and team projection. There is no simulation, inferred legality or action playback.

Discover held seats through the existing lobby resume API. Read browser storage without replacing
the active table's saved pair. Always ask who is holding the device before loading a seat, including
a device holding both seats. An explicit table or linked capability never falls back to an unrelated
table. Invitations are reviewed and accepted at `/play`; this preview never calls claim. Account
refusals lead back through the existing sign-in flow, retaining the destination.

Use the existing token-header transport for the selected seat only. Refresh full snapshots on SSE
notifications, with one in-flight poll and a remembered refresh when changes arrive during it. A
five-second poll covers unavailable notifications; closed streams reconnect with capped backoff.
Advance the feed cursor from the host's `feedNext`, not the visible feed's length or last sequence.
Do not send decisions, leave, concede, subscriptions or `shown` acknowledgements. The existing host
still records arrival on authenticated reads: opening an accepted seat may start a waiting match
once all humans have arrived. Read-only describes decisions and presentation, not absence of that
existing arrival behavior.

Fence asynchronous responses by connection identity. Seat changes and hidden/closed pages stop the
stream and discard the old display. A 401 or 403 clears the canvas, HUD and inspection and stops the
connection. A transient failure labels the retained snapshot stale until a successful poll replaces
it. A resumed page obtains a fresh snapshot. Asset failure retains labelled vector figures; graphics
context loss offers reload and the normal table as recovery.

HTML owns accessible inspection targets, status, viewpoint choice and dialogs; Phaser owns the
figures and ambient art. Handle one, two or three creatures per side. Figure forms remain explicitly
provisional positions, not class identities. Creature stats, conditions, spell names and outcomes
come from the seat projection and catalogue. Reuse condition labels/countdown semantics from the
table instead of inventing another interpretation of fresh or permanent effects.

## Consequences

The existing table remains the place to play. The preview does not time questions or animate attacks,
and skipping or inspecting cannot advance the game. A5 adds commands; A6 adds confirmed-action
playback. Desktop/mobile browser tests cover viewpoints, state updates, access loss, reconnect and
storage-disabled links. A real-host journey checks projection parity and confirms the arena issues
no command or acknowledgement while another client advances the game.
