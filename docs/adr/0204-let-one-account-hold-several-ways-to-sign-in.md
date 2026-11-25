# 0204. Let one account hold several ways to sign in

Date: 2026-11-23

Status: Accepted. Amends [0122](0122-a-player-signs-in-with-google-and-owns-their-seats.md) (an account is keyed by one or more identities, not by Google's `sub` alone).

Rulebook: none

## Context

ADR 0122 keyed an account by one Google `sub`. The owner wants more ways to sign in: Discord, then Microsoft, and a
guest for the lesson before an account (UX15). The host offers each provider on its own, with no broker
([sign-in-methods-plan.md](../tabletop/sign-in-methods-plan.md), question 1). A player who signs in with Discord
must reach the account they made with Google, not a second one. The model was already close: `ExternalIdentity`
names its provider, and the `by-subject` index is keyed by provider and subject.

## Decision

- **An account holds one or more identities** (`Account.Identities`), each an `ExternalIdentity` from a provider
  the host lists, in the order they were added. Each has its own `by-subject` entry naming the account. An
  identity signs in to an account only when the entry names it **and** the account lists it: the list, which the
  player sees, is the whole of the ways in. An account never holds none: removing the last is refused
  (`Account.LastIdentity`); deleting the account is how the last one goes.
- **Only a signed-in player links an identity** (`AccountRegistry.LinkAsync`), and the identity comes from a
  provider's sign-in completed in the same request, never from what the request says. A suspended account links
  nothing. The `by-subject` entry is written first, create-only-if-absent, so an identity another account lists is
  refused however close the race (`Account.IdentityTaken`); then the account. An entry that holds nobody (its
  account is gone, or does not list the identity: a link or a removal that stopped between its writes) is taken
  back by the next link or registration, under the lock links and removals take.
- **Removing an identity signs the account out everywhere** (`UnlinkAsync`): the generation is raised in the same
  write as the removal, so whoever came in through the identity removed is signed out too. The account is written
  first, listing the identity as `unbinding` instead of among its identities. Then its entry is removed. Then the
  account is written without `unbinding`. An entry left behind reaches nothing. The next removal, link or deletion
  of the account removes it, and so does a `DELETE` of the same provider once the player has signed in again,
  though Settings does not list it (#1216). A release older than this one drops `unbinding` when it writes the
  account; an entry left then waits for the next link or registration of that identity, as before.
- **An account read before is written or deleted only if it is unchanged since the read** (the blob's ETag, the
  file's hash). During a deploy the release beside this one writes the same accounts, and so does the hourly job,
  in a process of its own; the registry's lock reaches neither. A refused write reads the account again and decides
  again. If the account is gone, it is not written back. When a later try refuses what an earlier one began, the
  binding and the name that try wrote are freed. Five tries lost in a row throw, as a store out of service does, and
  free what the tries wrote, as a refusal does (#1263). A host that stops between two writes leaves what it wrote.
  The sweep of idle guests deletes an account only while it is still a guest (#1217).
- **Every write of a `by-subject` entry gives it a new version.** This includes a link that takes up an entry
  another link of the same account made: it rewrites that entry over the version it read. So a refused link removes
  its entry only if the entry is still the version that link wrote. It leaves an entry another link has taken up
  since (#1261). A registration takes back an entry that holds nobody the same way, only as the version it judged.
  A file's entry carries a mark of its own, so its hash changes even when the account it names does not.
- **What versions do not cover.** An older release takes an entry up by writing only the account, so a refused
  link reads the account once more before it removes its entry. An older release's refused link still removes by
  account. When both links of one account are refused, the entry the second took up stays: it reaches nothing,
  since the account does not list it, and the next link or registration of that identity takes it back.
- **Never by email.** The host keeps no email (ADR 0122 holds), so it never joins two identities because a provider
  says they share an address. So someone who opens a provider account with your address cannot reach yours.
- **A record written before this reads as one identity.** A new record lists `identities` and keeps the first one
  as `provider` and `subject`, so the previous release of the host still reads every account written by this one.

## Consequences

- Good: the providers of P2 to P6 add identities, not accounts: nobody migrates, and an account keeps its id, its
  seats, its friends and its results whatever door it came in by.
- Good: the identity that registered the account can be removed once another is linked; a player who loses their
  Google account keeps theirs through Discord, and removing the lost one signs out whoever holds it.
- Bad: one more way into an account means one more provider whose own security matters. The host links only for
  a signed-in player, never on a provider's say-so.
- Bad: the previous release, running beside this one during a deploy, writes an account back without its
  `identities` and deletes only its first entry. Until P4 offers linking there is nothing to lose. A link made
  while the previous release still runs could be dropped from the list: it then signs in to nothing, and its
  entry, which holds the provider's identifier, outlives a deletion of the account until a link or a
  registration takes it back. P4 therefore ships once this release is the only one running.
- Bad: an account whose registration stopped before its entry was written can list an identity another account
  then links. It signs in to nothing, and its name is taken back after an hour like any abandoned registration.
- Neutral: no route and no page change here (P1); the routes come with P2 and P4.

## Alternatives considered

- **One account per identity, merged on request**: a merge moves seats, friends, results and ratings between
  accounts, and every store would need to learn it. Linking avoids it: a player never has a second account.
- **Link by email**: the host keeps none, and an address can change owner (ADR 0122).
- **Supabase Auth as the one identity**: every account would need a second key or a migration, and a third party
  would keep every email (the plan's question 1).

## Follow-up

- `docs/tabletop/sign-in-methods-plan.md`: P2 to P6 build on this; P4 ships after this release is alone.
- `table/README.md` and `docs/reference/commands.md` gain the identity routes with P4.
