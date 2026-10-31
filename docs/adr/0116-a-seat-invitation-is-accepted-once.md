# 0116. A seat's invitation is accepted once, with a tap

Date: 2026-10-31

Status: Accepted. Amends ADRs 0081 and 0115.

## Context

A seat's link and code carried the seat's own token, so whoever opened them sat down. The owner opened the
link meant for the other player, from the waiting room, in the browser that already held Player 1: that browser
silently became a shared device holding both seats, the match began, and when the other player opened the same
link a second device held Player 2 too. Both pages answered for one seat. ADR 0115 rotated the token only when a
place was taken from the lobby, not when it was reached by its link.

## Decision

- A person's seat is offered under an **invitation**, a key of its own that is not the seat's token. Links,
  codes, the admin panel, the waiting room and the console carry the invitation; an invitation reaches no seat.
- `POST /api/lobby/invitations` says what one or two keys are (an outstanding invitation, a seat's own token,
  or nothing) without spending anything. The table page asks it before it keeps or plays anything, and shows a
  confirmation. A browser that already holds a seat at the invited table is offered that seat back first, and
  playing the invited seat there too is an explicit second choice.
- `POST /api/lobby/claim` accepts the invitations, for one seat or both seats of one table, under the claim gate
  of ADR 0115: the seats get fresh tokens, their invitations and codes are spent, the places stop being open, and
  `table.json` says so before the tokens are returned. The second browser to ask is refused; a spent code
  answers 404.
- The seats the lobby hands to the browser that opened the table are taken from the start. Leaving offers the
  place again under a fresh invitation and code. The operator can give a taken seat a new invitation from the
  admin panel (`POST /api/tables/<id>/seats/<seat>/invite`), which cuts off the device that held it.
- `table.json` records each seat's outstanding invitation and whether it is taken. A record written before this
  has neither, and its links keep carrying the seat's token, as they did.

## Consequences

- Good: one link opened twice, or opened by the wrong person, can no longer put two devices on one seat or make a
  browser a shared device without saying so. The admin panel shows which seats a browser holds.
- Bad: a taken seat has no link, so a player who loses the device holding it needs the operator's new invitation
  or another device that kept the seat. A browser that keeps no storage holds its seat in the address bar
  instead, so a reload works, and the address is a key to the seat while that page is open.
- Bad: one more tap before the first board.
- Neutral: an older page that polls with the link's key is refused, since an invitation is not a token. A host
  that cannot answer `invitations` (an older host) makes the page read the link as a seat, as before.

## Alternatives considered

- Rotate the seat's token when its link is first used: the first browser to load the page would take the seat
  with no confirmation, which is the owner's case exactly.
- Refuse a second device on a seat already reached: the owner's own browser would still have sat down as Player
  2, and a player whose phone reloaded would be locked out.
- Accounts: a sign-in for every player is what ADR 0115 chose not to need.
