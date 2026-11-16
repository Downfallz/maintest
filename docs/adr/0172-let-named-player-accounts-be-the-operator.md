# 0172. Let named player accounts be the operator

Date: 2026-11-12

Status: Accepted. Amends [0081](0081-a-host-of-tables-found-by-their-tokens.md)

## Context

The operator reaches the admin panel through one of two doors (ADR 0081): the token a laptop's console prints, or
the hosted platform's own sign-in (Microsoft Entra ID, ADR 0080). Players sign in separately, with Google (ADR 0122).
The menu on every page already shows an Admin link, but only when `/api/me` says the request is the operator's.
The owner plays signed in with Google, so the link never shows: they have to open `/admin` and sign in a second
time, with another provider.

## Decision

We will let a host name player accounts that are the operator once signed in: `--admins <id>[,<id>...]` on
`table`, or the `DOWNFALL_ADMINS` variable, and `TABLE_ADMIN_ACCOUNTS` for the hosted table. Each account is
named by its id (`acct_...`, the id `/api/me` gives a signed-in account), never by its player name. When a player
deletes their account, their name is freed for anybody to take, and that person would become the operator.

`OperatorGate` admits such an account before trying its other door, and names it by its player name in the console.
`/api/me` then says `operator: true`, so the menu shows Admin, and every admin route accepts the request on its
account cookie. The two doors stay as they were. A host that signs nobody in refuses to start with admin
accounts, and an id that is not one is refused at start rather than ignored.

## Consequences

- Good: the owner reaches the panel from the menu, with the sign-in they play with.
- Good: Entra stays a second way in if the Google account cannot sign in.
- Bad: the panel's routes now accept a cookie as well as a header. The cookie is `SameSite=Lax`, so another site
  cannot make a signed-in admin's browser send a POST or a DELETE. A cross-site GET can start an export download,
  but that site never reads what was downloaded.
- Neutral: an account is made admin by redeploying with its id. No page grants the role.

## Alternatives considered

- Name admins by player name: easier to read, but a freed name could be taken by anybody.
- A role stored on the account, granted from the panel: needs a first admin anyway, and a page that grants the
  most powerful role, for a host with one owner.
- Replace Entra with admin accounts: one way in, and none if Google refuses the owner's account.

## Follow-up

- `OperatorGate`, `AdminApi`, `CliOptions` (`--admins`), `TableHost` (`DOWNFALL_ADMINS`).
- `infra/main.bicep` (`adminAccounts`), `.github/workflows/deploy.yml` (`TABLE_ADMIN_ACCOUNTS`), `infra/README.md`.
- `AGENTS.md`, `docs/adr/README.md`.
