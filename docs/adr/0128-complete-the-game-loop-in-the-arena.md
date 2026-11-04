# 0128 — Complete the game loop in the arena

## Status

Accepted. Implements A7 of the [browser arena plan](../tabletop/browser-arena-plan.md).

## Context

A6 presents confirmed combat but still sends players to `/play` for planning and a match's conclusion.
The arena needs to complete a match without acquiring a second set of game rules or confusing a historical
frame with a question that can be answered now. Simultaneous choices and recorded sessions also require
keeping the selected seat's private information separate from public history.

## Decision

Keep `/arena` opt-in and keep the existing host contracts. No engine rule changes.

- `arena-planning.js` owns the selected seat's Evolution, Speed and TieOrder drafts. Opening visible controls
  acknowledges the asking; selecting does not write. Confirm sends one complete command with `asked`.
  A changed asking, seat, hidden page, failed acknowledgement or changed offer fences the draft. Refusals
  survive the refresh and require a new explicit attempt. Mutation purchases use only `availableTiers`.
- `src/planning.ts` displays the shared catalogue's package forest, prerequisites (including any-of), spells,
  passives, initiative and known/picked/available/future status. Creature selection and scroll survive ordinary
  polls. Mutation selection and passing each need confirmation. Tempo confirms **one asked creature at a time**;
  the host continues to reveal the team's choices together. Tie ordering uses the existing group helpers.
- `arena-match.js` retains two rounds of public events for upkeep and aftermath review. These notes describe
  applied ticks and expired conditions without subtracting snapshots or inventing intermediate boards.
  The current battlefield stays current. The round-review overlay does not acknowledge a covered question.
- Concession has an explicit, cancellable confirmation. It shares the connection's write fence with decisions,
  refreshes the host result and never computes a winner locally. Elimination, round cap, draw and concession
  are displayed from `outcome`.
- Finished-match replay explicitly rereads this selected seat from sequence zero, even on an unrecorded live
  host. It uses the existing `matchReplay` and `playbackBoard` helpers, adds public aftermath steps, and offers
  Previous, Next, a round selector and Return to result. Casts have separate before/after frames. Upkeep and
  purchase reference frames are labelled as the start of that round's combat; frameless events show no
  battlefield. No historical state can submit a decision. This replay is manual and has no animation timer.
- `/arena?replay=<session>&seat=player2` uses `replayTransport` and the existing operator authorization for the
  finished-session route. It loads no live seat, claims no invitation, and writes nothing. The recording's
  existing event filter can omit detailed condition cleanup; a recorded RoundEnded still gives an aftermath
  boundary. The UI never reconstructs omitted events. Session IDs and seat perspectives are explicit.

## Alternatives considered

- Embedding `/play` would complete navigation but leave A7 outside the scene shell.
- A client-side simultaneous planning protocol or inferred legal mutations would duplicate host rules.
- Replaying the last two retained rounds as a whole match would silently omit earlier actions.
- Drawing final stats behind an event with no historical frame would misrepresent the recording.

## Consequences

The complete command/combat/result loop is available in the arena, with the table still a recovery route.
The Mutation Tree is scrollable inside its overlay; its close and confirmation controls remain reachable on
short screens. The Phaser mount refreshes its cached parent bounds when replay changes the shell height,
so the canvas and creature hit targets stay inside the battlefield without a window resize. The scene remains provisional art. Invitation acceptance stays on the table, and recorded
session access keeps the existing operator gate. A8 still owns release polish, real-device performance,
screen-reader testing, high zoom and rollout.

## Validation

Pure tests cover host offers, tempo values, group ordering, duplicate confirmations, acknowledgement races,
seat changes, hidden/offline states, refusals, history boundaries, missing frames and concession fencing.
The browser suites cover planning, tie selection, concession, round review, recorded perspectives and short
portrait/landscape bounds. `arena.loop.js` completes 3v3 games in both declarations through visible arena
controls, reaches a configured round cap, reads the real recorded session, and checks simultaneous private
choices on two independent browser contexts. Existing A4–A6 browser suites remain required.
