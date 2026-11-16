# 0156. Replay a finished game in the battle view for the players who sat at it

Date: 2026-11-10
Status: Accepted. Amends [ADR 0147](0147-list-every-finished-game-and-cap-the-ones-in-progress.md), whose
finished games the host no longer holds open their recorded session.

## Context

A finished game the host no longer holds opened `/session/<table>`, the learning viewer's page for a trace.
Players read that as the old viewer. The battle view already replays a finished match from one seat's side:
its end card, the final scorecard, and a whole-match replay back to that card. It read the replay only off
`GET /api/sessions/<id>/replay`, the admin panel's route, so only the operator could open it.

## Decision

- **The lobby serves a player's own replay.** `GET /api/lobby/tables/<table>/replay` answers what the panel's
  replay answers: the outcome, each seat's final board, the catalogue and the public events. It answers only an
  account that a kept result names at that table. Signed out is a 401; another account is a 403.
- **The finished card opens the battle view.** A finished game's `replay` is
  `/battle?replay=<table>&seat=<the player's seat>`. It replaces `session`, the viewer's link. This holds
  whatever the presentation preference (ADR 0136): the recap exists only in the battle view.
- **The pages ask that one route.** It admits the operator too, by the panel's token or the platform's sign-in
  (`--platform-auth`), for any game. A refusal asks to sign in or says the match is not the reader's.

## Consequences

- Good: a player watches their own finished game again from their side, with the end card and the replay,
  without the operator.
- Neutral: the replay serves only public events and the final boards, which both players saw by the end. The
  viewer's `/session/<table>` page still exists; the lobby no longer links to it.
- Bad: a player without the battle view chosen is sent to it for a replay all the same.

## Alternatives considered

- **Pick the route by the token the page holds:** behind `--platform-auth` the operator signs in through the
  platform and the page holds no token, so the operator's replays would have been refused.
- **Open the panel's route to accounts:** the panel's routes would then admit players, which none of the others does.
- **Follow the presentation preference:** the classic page's replay has no end card and no recap.

## Follow-up

- `battle/README.md` (AU13) and `AGENTS.md` name the player's route.
