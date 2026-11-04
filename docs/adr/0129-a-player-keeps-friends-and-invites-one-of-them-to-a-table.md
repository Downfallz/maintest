# 0129. A player keeps friends, and invites one of them to a table

Date: 2026-11-04

Status: Accepted. Builds on [0116](0116-a-seat-invitation-is-accepted-once.md), [0118](0118-your-turn-reaches-a-phone-nobody-is-looking-at.md) and [0122](0122-a-player-signs-in-with-google-and-owns-their-seats.md).

## Context

A player who wants a game with a particular person has an open seat anyone can take, or a link and a code to pass
around out of band (ADR 0115, 0116). Accounts exist (ADR 0122), so a player can be named; a notice can reach a seat's
phone (ADR 0118), but only a seat's, and the person being invited holds no seat yet. The owner asked for a friends
list, an invitation sent from it with one tap, visibly pending, one at a time, cancellable, and pushed to the friend's
phone, with the acceptance and the match pushed back.

## Decision

- **A friendship is mutual.** One player asks another by their exact username; the other accepts or declines. Until
  then it is a request, outgoing for one and incoming for the other. Declining and removing a friend are silent to the
  other side. There is no directory and no search: a name seen on a card is enough. An account keeps at most a
  hundred friends and a hundred pending requests. Why mutual: a friend can wake your phone, and the request is the
  consent.
- **An invitation is a seat addressed to an account.** A table opened for a friend keeps ADR 0116's invitation on its
  second seat and adds who it is for; the seat is not an open place, so the public lobby never lists it, and only a
  request signed in as that account accepts it, under the claim gate, with a fresh token and the invitation spent. So a
  pending invitation is a table with one seat waiting for a named account: it survives a restart with the table (ADR
  0091), and whether it is pending is read off the registry.
- **One outstanding invitation per account, for 24 hours.** An account may hold at most one table whose seat still
  waits for an invited account. Cancelling, declining and expiry each clear the invited account and leave the inviter's
  unbegun table with three ways out: invite another friend, open the seat to everyone, or close the table. Closing an
  unbegun table is allowed to whoever holds every taken seat of it.
- **An account has devices.** A signed-in browser's push subscription registers as one of the account's devices,
  five at most, kept beside the accounts and forgotten with "sign out everywhere" and with the account. A notice that
  is not about a seat (a friend's request, an invitation, its acceptance or refusal, the end of a match, the opponent
  leaving) goes to every device of the account, once per endpoint; "your turn" stays the seat's (ADR 0118, 0120). No
  separate "the game has started" notice: the acceptance, then the first "your turn", is the match starting (ADR 0092).
- Friends and devices live in Application (rules, ports), their stores in Infrastructure beside the account store, the
  routes, the notices and the invited seat in the host. The domain does not learn about people.

The stages, the routes and the lobby are in [docs/tabletop/friends-plan.md](../tabletop/friends-plan.md).

## Consequences

- Good: a game with a particular friend is one tap and one notice, and the invitation is as durable as the table.
- Good: nothing new is stored for an invitation, and the one-at-a-time rule is one query on the registry.
- Bad: a pending invitation holds one of the sixteen tables for up to a day. Accepted: it is the inviter's own table,
  and expiry frees it.
- Bad: the host now holds one more thing about a person: who their friends are, and where to reach their browsers. Both
  go with the account; the lobby's "What we keep" says so.
- Neutral: a host with `--accounts off` has no friends, no devices and no invitation to an account; links and codes stay.

## Alternatives considered

- **A one-sided list (follow)**: anyone who knows a name could wake its owner's phone.
- **Open the table only when the friend accepts**: no table held while pending, but the inviter is not at the table
  when the friend arrives, and the acceptance can fail for lack of a table.
- **An invitation store of its own**: a second place to keep what the table's record already keeps, and a second thing
  to rebuild on a restart.
- **Presence ("online now")**: a poll for every friend of every player, for a signal a notice replaces.

## Follow-up

- `table/README.md`, `docs/tabletop/playtest-app.md`, `AGENTS.md` (the `table` command's routes), each stage.
- `docs/domain/glossary.md` is untouched: a friend and an invitation are words of the lobby, not of the match.
