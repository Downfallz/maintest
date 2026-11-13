# 0174. Keep when each account last signed in and was last seen

Date: 2026-11-13

Status: Accepted

## Context

The admin panel's Players tab (`docs/tabletop/admin-plan.md`, stage A2) lists every account with its games and its
rating. The owner also wants to see who still plays: when each player last signed in and last used the site. The
host keeps neither. `Account` holds a name, an id, the provider's subject, its creation time and a generation; it is
read on every request that carries a session, and changing it changes what a session is checked against.

The panel also lets the operator act on an account: sign it out everywhere, rename it, delete it. Its player could
already do the first and the last. Nothing could rename an account.

## Decision

We will keep one small record per account beside it, `activity/<accountId>.json` in the accounts' store:
`AccountActivity(Account, LastSignIn, LastSeen)`. `Account` does not change.

- `LastSignIn` is written when the account signs in (Google, a registration, or the dev sign-in). It is also a visit.
- `LastSeen` is written when a request carries the account's session, at most once an hour an account. The host
  remembers what it last wrote, so a playing account costs one write an hour and no read between.
- The record is forgotten with the account, whoever deletes it.

The list of players reads every account, every activity record and every kept result once, and pages, sorts and
searches in memory. Nothing is counted twice: games and wins come from `IMatchResults`, the rating from the ladder.
`IAccountStore` gains `ListAsync`, which reads every account.

The operator's three actions reuse the player's own paths: sign out everywhere raises the generation and forgets
the devices; deletion releases the seats, frees the name and forgets devices, friends, settings and activity. A
rename holds the new name, writes the account, then frees the old name. Sessions name the account by id, so they
keep working. Deletion needs the name typed, as it does for the player.

The *What we keep* panel says that the two times are kept, and that the operator sees them and can act on the
account.

## Consequences

- Good: the operator sees who still plays, and can act on an account without a shell.
- Good: a session check stays one cached read; activity never touches it.
- Bad: new personal data, kept until the account is deleted. It is two times, to the hour, and the privacy panel
  says so.
- Bad: `LastSeen` is up to an hour behind, and two replicas could each write once in the same hour.
- Neutral: listing reads every account. That is fine at tens to hundreds of accounts. Past a few thousand, the counts
  should move into the activity record, written when a result is kept.
- Neutral: a table in progress keeps the name it was opened with until it is read again; finished results name
  accounts by id, so a rename changes nothing there.

## Alternatives considered

- Add the two times to `Account`: one record, but every visit would rewrite what sessions are checked against.
- Write `LastSeen` on every request: exact, but a write for each poll of a playing page.
- Derive "last seen" from the tables' journals: no store write, but it misses a player who only browses, and reads
  every table.
- Keep the counts in the activity record now: faster lists, but a second copy of the results to keep in step.

## Follow-up

- `AccountActivity`, `IAccountActivityStore`, `AccountActivityLog`, `PlayerDirectory`, `AccountRegistry.RenameAsync`,
  `IAccountStore.ListAsync`.
- `FileActivityStore`, `BlobActivityStore`, `ArtifactStores.OpenActivityAsync`.
- `AccountApi` (activity, shared sign-out, deletion and rename), `PlayersApi` (`/api/admin/players`), `AdminApi`.
- `table/admin-players.js`, `table/admin.html`, `table/navigation.js` (*What we keep*).
- Suspension is its own decision (A2b).
