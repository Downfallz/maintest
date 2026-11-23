# 0204. Let one account hold several ways to sign in

Date: 2026-11-23
Status: Accepted. Amends [0122](0122-a-player-signs-in-with-google-and-owns-their-seats.md) (an account is keyed by one or more identities, not by Google's `sub` alone).
Rulebook: none

## Context

ADR 0122 keyed an account by one Google `sub`. The owner wants more ways to sign in (Discord, then Microsoft, and a
guest for UX15), with the host signing people in one provider at a time
([sign-in-methods-plan.md](../tabletop/sign-in-methods-plan.md), question 1). A player who signs in with Discord
must reach the account they made with Google, not a second one. The model was already close: `ExternalIdentity`
names its provider, and the `by-subject` index is keyed by provider and subject.

## Decision

- **An account holds one or more identities** (`Account.Identities`), each an `ExternalIdentity` from a provider
  the host lists, in the order they were added. Each has its own `by-subject` entry naming the account, and any of
  them signs in to it. An account never holds none: removing the last is refused (`Account.LastIdentity`);
  deleting the account is how the last one goes.
- **Only a signed-in player links an identity** (`AccountRegistry.LinkAsync`). Its `by-subject` entry is written
  first, create-only-if-absent, so an identity another account holds is refused however close the race
  (`Account.IdentityTaken`); then the account. A link or a removal that stops between its two writes is finished
  by asking again. A binding to an account that no longer exists is taken back, as registration does.
- **Never by email.** The host keeps no email (ADR 0122 holds), so it never joins two identities because a provider
  says they share an address. That is also what keeps someone who controls another provider's account with your
  address out of yours.
- **A record written before this reads as one identity.** A new record lists `identities` and keeps the first one
  as `provider` and `subject`, so the image before this one still reads every account written after it.

## Consequences

- Good: the providers of P2 to P6 add identities, not accounts: nobody migrates, and an account keeps its id, its
  seats, its friends and its results whatever door it came in by.
- Good: the identity that registered the account can be removed once another is linked; a player who loses their
  Google account keeps theirs through Discord.
- Bad: one more way for an account to be reached means one more provider whose own security matters. The host
  links only for a signed-in player, never on a provider's say-so.
- Bad: the revision before this one, running beside it during a deploy, writes an account back without its
  `identities`. Until P4 offers linking there is nothing to lose. After it, a linked identity dropped that way
  still signs in through its index, and removing it again tidies the index.
- Neutral: no route and no page change here (P1); the routes come with P2 and P4.

## Alternatives considered

- **One account per identity, merged on request**: a merge moves seats, friends, results and ratings between
  accounts, and every store would need to learn it. Linking before there is a second account avoids it.
- **Link by email**: the host keeps none, and an address can change owner (ADR 0122).
- **Supabase Auth as the one identity**: every account would need a second key or a migration, and a third party
  would keep every email (the plan's question 1).

## Follow-up

- `docs/tabletop/sign-in-methods-plan.md`: P2 to P6 build on this.
- `table/README.md` and `docs/reference/commands.md` gain the identity routes with P4.
