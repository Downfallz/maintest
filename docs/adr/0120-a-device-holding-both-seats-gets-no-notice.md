# 0120. A device holding both seats of a table gets no notice

Date: 2026-11-01

Status: Accepted. Refines ADR 0118 (a notice goes when nobody drew the question).

## Context

ADR 0118 sends "your turn" to a seat whose page did not acknowledge the question within two seconds, and a
hidden page acknowledges nothing. Two seats of one table on one phone is hotseat (ADR 0115 lets a browser
hold several seats): the pass screen hides one seat's question while the other player holds the device, so
every opportunity asks a question nobody draws in time, for a player who is in the room. The worker shows
none of it while the game is visible and focused, and all of it the moment the phone is put down or another
app is opened: one "your turn" a seat, an opportunity, for the two people who just passed it to each other.

## Decision

A phone that holds both seats of a table is told nothing. The host sends no notice to a seat whose other
seat carries the same subscription (`PushNotices`): the person holding that phone is the other player. The
page offers no notices button while it holds both seats of its table. A subscription the browser already
has is still told to the host for every seat it holds, so the host sees the same endpoint on both and keeps
quiet; a seat that later goes to another phone (a claim, a new invitation) takes a new subscription with its
new keys, and both phones are told from then on.

## Consequences

- Good: hotseat on one phone is as quiet as it was before notices.
- Neutral: the test notification is not offered on a hotseat page either; the device in hand is the test.
- Neutral: two seats of two different tables in one browser are not hotseat, and each is told.

## Alternatives considered

- Forget the subscription on a hotseat page: loses it for the seat that stays when the other leaves, and
  asks the player to subscribe again for nothing.
- Keep sending and let the worker suppress: the worker sees only whether the game is in front of somebody,
  not who is holding the phone, and shows the notice as soon as it is not.

## Follow-up

- `table/README.md` says a device holding both seats gets no notice.
