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
  first, then its entry removed; an entry left behind reaches nothing.
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
