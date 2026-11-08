# 0153 — Play the same game again from the battle's end card, and call the lobby the Lobby

Date: 2026-11-08

Status: Accepted for the opt-in battle view. Builds on [ADR 0129](0129-a-player-keeps-friends-and-invites-one-of-them-to-a-table.md)
(stage 4's Rematch link) and [ADR 0145](0145-end-the-match-in-one-moment.md).

## Context

The battle's end card showed the result as two plain boxes and three unrelated controls: Replay the match, Console ·
History (already in the field's header) and a "Back to tables" link. The menu calls the same page "Play", and the
loading screen says "Open tables": three names for one place. The classic table's outcome card offers Rematch, which
opens the lobby's create dialog on the same opponent; the battle offered nothing.

A lobby table takes an opponent kind (another player, a friend, the bot, a shared device), a format and, for a friend,
their account. The bot is always the strongest one. So the same game again is fully known for a bot or a shared
device, and for a friend once this player is their friend.

## Decision

- **One tap where the host can open it at once.** `rematchPlan` (`table/outcome.js`) reads the table's seats, the
  seats this browser holds and the friendship the opponent card already reads:
  - **Against the bot or on a shared device:** **Rematch** creates the same table (`POST /api/lobby/tables` with
    the same opponent kind and format). It saves its seats as the lobby does and opens the new battle.
  - **Against a friend:** **Invite to rematch** keeps the second seat for them, the stage 3 invitation. They are told
    on their devices, and this player waits at the new table.
  - **Against a player who is not a friend,** or whose friendship could not be read: **Invite to rematch** opens the
    lobby on them (the stage 4 link), which says what is missing. Nothing is sent to a stranger.
  - **No rematch:** for a recording, a page holding no seat, or an opponent with no account.

  A refusal from the host, such as the five-games limit (ADR 0147), stays on the card in its own words.
- **The card reads like the other sheets.** The eyebrow names the round. Each team is a card: how many stand, in
  large figures, and the health kept as a bar. Each card's whole line is its accessible name. Close is a ×. The
  footer holds Rematch (primary), Replay the match and Lobby. History stays in the field's header.
- **The lobby is "Lobby" everywhere:** the menu entry, the end card and the loading screen's way back.

## Consequences

- Good: a game against the bot is one tap from the next one, with nothing to choose again.
- Good: one name for one place.
- Neutral: an invitation to a player who is not yet a friend still goes through the lobby, as stage 4 already did.
- Bad: a rematch against the bot plays the strongest bot even if a future lobby offers a choice; the plan must then
  carry that choice too.

## Alternatives considered

- Always open the lobby's create dialog, as the classic table does: one more screen for a choice already made.
- Send a friend request with the rematch: it would invite a stranger by surprise. Friendship stays its own step.
