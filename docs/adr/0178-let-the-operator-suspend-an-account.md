# 0178. Let the operator suspend an account

Date: 2026-11-14
Status: Accepted

## Context

The operator's player sheet (ADR 0174) can sign an account out everywhere, rename it or delete it. Signing out lasts
only until the player signs in again, and deleting is final: the account, its name, its friends and its history go.
Against a player who abuses the game (spam, slurs in a name, a bug exploited on purpose) the operator needs a step in
between: keep the player out, say why, and be able to let them back. The owner settled on 2026-11-13 that this is
suspension, with its own ADR (admin plan, A2b).

## Decision

An account can carry an `AccountSuspension(Reason, By, At, SeatsReleasedAt)`, kept in the account's own record.

- **Suspending** needs a reason of 10 to 300 characters, which the player reads. One write sets the suspension and
  raises the account's generation, so every session it holds stops working at once; its devices are forgotten, as
  with "sign out everywhere". An account already suspended keeps the suspension it has.
- **A suspended account cannot sign in.** Google or a dev host alike answer `403 Account.Suspended` with "This account
  is suspended: <reason>", which the lobby's sign-in panel shows. Nothing else of the account changes: its name stays
  taken, its friends, results, rating and settings stay.
- **Its seats are kept a day.** The account's seats in unfinished games are left alone for a day, in case the
  operator restores it. After that, the hourly tending pass (ADR 0162) offers them to other players, the way deleting
  the account does (a seat left, ADR 0170), once, and notes when on the suspension. Its time to answer runs on
  meanwhile (ADR 0169).
- **Restoring** lifts the suspension: the player signs in again and holds whatever seats are still theirs. Sessions
  ended by the suspension stay ended.
- `POST /api/admin/players/<id>/suspension {"reason"}` and `DELETE /api/admin/players/<id>/suspension`, behind the
  operator gate; the player sheet's Suspend and Restore, and the list says who is suspended.

## Consequences

- Good: the operator can keep a player out without losing the account, and the player is told why.
- Good: a suspension lifted the same day leaves the player's games as they were.
- Bad: a game against a suspended player waits up to a day before the seat is offered, unless the time to answer
  ends it first.
- Neutral: a suspended player stays on the leaderboard and in other players' recent opponents and finished games;
  suspension keeps a player out, it does not rewrite what they played. Voiding a match (ADR 0176) is the tool for
  that.

## Alternatives considered

- Releasing the seats at once: a suspension made by mistake, or lifted after a word with the player, would cost the
  player every game in progress.
- Ending the account's games as losses: punishes the opponent's record too, and decides a game nobody finished.
- A suspension with an end date: the operator restores by hand; an automatic end can follow if it is needed.

## Follow-up

- `docs/tabletop/admin-plan.md` (A2b built), `AGENTS.md` (the suspension routes), the glossary (Suspension).
