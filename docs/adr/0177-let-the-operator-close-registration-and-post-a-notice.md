# 0177. Let the operator close registration and post a notice

Date: 2026-11-14
Status: Accepted

## Context

Since ADR 0122, anyone with a Google account can make an account: a sign-in whose subject has no account is asked
for a name, and the account exists once the name is taken. The operator has no way to stop that, for a spam wave or a
release that should not take new players yet, short of a redeploy without Google. Nor can they tell every player
something at once (a maintenance window, a new version) except through push notices, which reach only the devices
that asked for them. The admin plan (`docs/tabletop/admin-plan.md`, A4) gives the panel a Settings tab for both.

## Decision

The host keeps one record of settings, `HostSettings(RegistrationOpen, Notice, UpdatedBy, UpdatedAt)`, as
`settings/host.json` beside the accounts (the accounts' directory or container). A host that never saved one reads
the defaults: registration open and no notice.

- **Registration closed** means no new account at all, invitation or seat code included (the owner's answer of
  2026-11-13). A Google sign-in whose subject has no account is answered `409 Account.RegistrationClosed` ("New
  accounts are closed for now.") instead of the ticket that asks for a name, and a ticket handed out before the
  closing is refused the same way when its name is sent. Every existing account signs in as before. A dev host
  (`--accounts dev`, loopback only) is not affected: it is a developer's own machine.
- **The notice** is one line of at most 200 characters, trimmed, none when empty. `/api/me` carries it, and every
  page with the shared header shows it under the header, but for the game itself (the battle view and the classic
  table), where nothing is drawn over the board.
- `GET /api/admin/settings` and `PUT /api/admin/settings {"registrationOpen","notice"}`, behind the operator gate,
  read and replace the record, stamping who saved it and when.
- A process reads the record at most once a minute and keeps the last one it read, so a change reaches every process
  of a hosted table within a minute without a restart; the process that saved it uses it at once. A record that
  cannot be read keeps the last one read, or the defaults before any was.

## Consequences

- Good: the operator can stop new accounts and speak to every player from the panel, on every process, without a
  deploy.
- Good: closing changes nothing for an existing player: their sign-in, games and invitations go on.
- Bad: a person invited by a friend while registration is closed cannot take the seat until it opens again. That is
  the owner's choice; invitation codes that let one person through can follow in a plan of their own.
- Bad: a store out at a process's start leaves registration open there until it can be read. A closing is a
  convenience against a wave of sign-ups, not a security boundary; the gate in front of the operator is.
- Neutral: the record is the host's, not an account's: no account owns it, and deleting accounts leaves it.

## Alternatives considered

- A command-line flag or an environment variable: needs a redeploy and a restart to change, which is what the panel
  is meant to spare.
- Keeping the setting in memory only: a hosted table runs several processes (ADR 0162), and each would have its own.
- Closing registration but letting a holder of an invitation through: the owner chose closed as closed; a gate per
  invitation is its own design.

## Follow-up

- `docs/tabletop/admin-plan.md` (A4 built), `AGENTS.md` (the settings route and the notice), the glossary
  (Registration, Host notice).
