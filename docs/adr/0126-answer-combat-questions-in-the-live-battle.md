# 0126. Answer combat questions in the live battle view

Date: 2026-11-03
Status: Accepted for the opt-in combat integration

## Context

A4 reads one authenticated seat (ADR 0125), and promises to send no decision and no `shown`. This ADR
amends that promise for combat questions alone; it holds for inspection. A5 must let that player choose real spells and targets
without duplicating rules, abandoning the table, or attaching a command to an animation. The merged
A4 corrections also establish shared scene boot/hit targets, retained catalogues, nonredundant SSE
reads and return links that work without storage. Those boundaries remain in place.

## Decision

Keep `/battle` opt-in and require the existing explicit viewpoint selection. Inspection alone sends
neither a combat command nor a `shown` acknowledgement. Opening combat controls displays the current
question, then acknowledges its `waitingAsked` through the existing seat transport. Evolution, Tempo,
ties and the remainder of the game loop stay at `/play` until A7. Invitations still belong there.

Adapt Action questions through `decisionView`/`slotView`. Choose a spell locally, select figures, then
explicitly confirm the complete Action. Hidden Intent is declared explicitly without targets; its
later Target question binds them. Spell descriptions come from the catalogue. Offered spells, target
candidates and bounds come from the current seat options. Unaffordable hidden plans remain selectable
with a fizzle warning, as at the table. Known but unoffered cards are disabled. Empty casts require
confirmation too. Share target validation and combat command construction in `slot.js` with `/play`.

Keep the draft in `battle-combat.js`, independent of DOM and Phaser. Its identity includes the selected
seat and asking; changing either invalidates it. Acknowledgement responses must still match that
asking. Revalidate the draft before writing, suppress duplicate confirmations, and fence callbacks
from closed/reopened controls. A hidden page, covered controls, stale connection or failed renderer
cannot post. A rejected or uncertain write clears the draft and displays the error; never retry writes
automatically. Reopening obtains a fresh acknowledgement. The host remains the final legality check.

Extend the existing connection with explicit acknowledgement and command methods. Polls begun before
a write cannot publish after it; notifications during the write owe a fresh read. Refresh after every
write outcome, including failures. Preserve connection generation fencing, feedNext, first-stream-ready
behavior and catalogue reuse. Access refusal clears all private surfaces, including combat controls.

HTML owns spell cards, accessible field buttons and confirmation; Phaser receives highlights only.
Multiple targets have independent selected states. Repeated taps toggle selection and never submit.
Keyboard focus moves from the spell dialog to a legal field target (or the targetless confirmation).
Results use the next authoritative snapshot immediately. No action queue or attack animation is added;
A6 will consume confirmed event frames without becoming a prerequisite for sending the next command.

## Consequences

The preview supports combat, not a complete match. Both entry points share the wire command builders
and host legality but retain their own presentation lifecycles. No Domain, Application, API contract,
content or game rules change. No new dependency is needed.

Acknowledging on opening the controls, not on drawing the question, has two costs, accepted here. The
decision clock starts later than it does at the table, which draws and acknowledges at once: a duration
recorded from the battle view measures from the opening, and `playtest-timing` cannot tell the two apart. And
a push notice (ADR 0118) goes out after its grace to a device whose person is reading the battle view with the
controls closed; opening them later does not take it back. The service worker is what keeps that notice from
showing: it reads a battle view window that is visible and focused as the game in front of somebody, the way it
reads the table page, so the notice reaches only a device nobody is looking at. Acknowledging on draw would start clocks for
a person who only came to look, which ADR 0125 promised not to do.

Two clients can hold the same asking. Whichever posts second is refused with the next asking in hand, and
the refusal stays on the battle view's dock until the player's next move: the refresh the write triggers must
not take it down.

Deterministic tests cover target bounds, stale questions/options, duplicate confirmation, rejected and
uncertain writes, seat changes and late reads. Browser tests cover catalogue choices, keyboard input,
selection, mobile bounds and readonly inspection. Real-host journeys send both combat declarations
from the battle view while an external driver answers only the planning questions still outside A5.

## Alternatives considered

- Import the table controller wholesale: that would create competing polling, acknowledgement and
  historical-playback lifecycles. Share the pure builders and existing transport instead.
- Submit on a second target tap or on animation completion: both make presentation an implicit command.
  Keep a separate confirmation button, including casts with no targets.
- Disable unaffordable hidden plans: that changes an existing game choice. Follow the host's offerings.

## Follow-up

- `TableFiles` routes `battle-combat.js` with the other bundled modules; `TableFilesTests` holds every module beside the page.
- `.github/workflows/studio-browser.yml` budgets twenty minutes for the three projects plus the real-host journeys.
- A6 consumes confirmed event frames for playback without making an animation a prerequisite for the next command.
- A7 brings Evolution, Tempo and ties into the battle view, and with them the question of acknowledging on draw.
