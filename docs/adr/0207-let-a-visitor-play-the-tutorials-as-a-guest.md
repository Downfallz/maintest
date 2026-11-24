# 0207. Let a visitor play the tutorials as a guest

Date: 2026-11-24

Status: Accepted. Amends [0122](0122-a-player-signs-in-with-google-and-owns-their-seats.md) and
[0204](0204-let-one-account-hold-several-ways-to-sign-in.md) (an account can be a guest, which no provider vouches
for), and question 5 of [accounts-plan.md](../tabletop/accounts-plan.md) (playing a tutorial needs no sign-in).

Rulebook: none

## Context

Playing, practice and the tutorials need an account (accounts-plan question 5, onboarding-plan question 5). UX15,
*a lesson before an account* ([ux-craft-plan.md](../art/ux-craft-plan.md)), has a visitor play tutorial 1 before
signing in, and asks for an ADR that says what a guest is on the host, what it may reach, how long it lives and how
its lesson joins the account. The owner settled the last two in the sign-in methods plan
([sign-in-methods-plan.md](../tabletop/sign-in-methods-plan.md), questions 3 and 4): the tutorials only, and as long
as the browser keeps its cookie. The tutorials already run at the host as a practice of the account that opened it,
in the process's memory (ADR 0179, ADR 0187), and their progress is kept by account id (ADR 0140).

## Decision

- **A guest is an account** the host makes without any provider (`POST /api/account/guest`,
  `AccountRegistry.MakeGuestAsync`). Its only identity is `guest:<random>`, which the host draws and no route signs in
  with: the guest's session cookie, the same `downfall_session` as any account's, is the whole of its proof. Its name
  is drawn too, `Guest-` and four characters with none that reads as another (`Guest-7KQ2`). A player cannot type a
  name that starts with `Guest-` or `guest_`. A visitor already signed in is answered the account it has.
- **It plays the tutorials and nothing else.** The routes a guest may call are a list in the host (`GuestRoutes`):
  its account (read, sign out, delete), the sign-ins that upgrade it and the name check, its game settings, the
  tutorials' list and their done and skipped marks, a tutorial's description and its practice, and the seat of its
  own tutorial, found by the practice's token. The host checks the list before any route answers, so a route added
  later is closed to guests until it is listed. Everything else answers `403 Account.GuestNotAllowed`: the lobby,
  tables and join codes, friends, the ladder and the statistics, other scenarios, notices and push, the ways to
  sign in, the admin panel. Feedback too, even from the lesson, so no feedback names an account the hourly job
  removes. The registry refuses a guest a linked way in as well (`LinkAsync`).
- **It lives as long as the browser keeps its cookie.** A session lives thirty days from its last renewal, which
  follows its use. The hourly job (`table-tend`, `IdleGuests`) removes a guest last used thirty-one days ago: by
  then no browser holds a session of it. It takes the last use from the account's activity (ADR 0174), which lags by
  up to an hour. When no activity was kept, it takes the time the guest was made. With the account go its binding, its name, its
  game settings and tutorials, and its activity. A guest holds no seat, friend, device, notice or feedback, so
  nothing else is kept for it. The job runs in its own process (ADR 0162). Removing a guest is safe there because
  no session of a guest that old exists for the API process to act on.
- **Signing in with a provider upgrades it.** A guest who signs in with an identity no account holds is asked a name
  as any new player is. Choosing it (`POST /api/account` with the ticket) upgrades the guest
  (`AccountRegistry.UpgradeAsync`). The account keeps its id and its making time, so its tutorials stay with it. The provider's
  identity replaces the guest's, the chosen name replaces the drawn one, and the generation is raised, so every other
  copy of the guest's cookie ends. The writes go as a registration's do, the new name first. A guest who signs in
  with an identity an account already holds is signed in to that account. The guest is removed at once: its cookie
  is replaced, so nothing can reach it. Its tutorials do not join that account.
- **Making a guest is counted.** Ten guests an hour from one network address, an IPv6 address by its /64
  (`RequestAddress`), and three hundred an hour from every address together. Past either, the answer is
  `429 Account.TooManyGuests` with `Retry-After`. As the guests' feedback is counted (ADR 0189), the count is held
  in the process's memory only, each address hashed with a salt the process draws. Closed registration (ADR 0177)
  makes no guest. A suspended guest is refused as any account is (ADR 0178).

## Consequences

- Good: a visitor plays a lesson in one tap, and what they learned is still theirs once they sign in: nobody
  migrates, the upgrade is one account changing its identity and name.
- Good: the list is closed by default. A route nobody thought of for guests is refused to them, not open.
- Good: the host keeps no email and no provider identity for a guest; what it keeps goes after a month unused.
- Bad: anyone can now make an account of one more kind. The counts bound it to three hundred an hour, about
  seven thousand a day at worst, each a few small documents. The operator's list of players shows them, by their
  `Guest-` names.
- Bad: the host-wide count can be spent by one actor with thirty addresses, or thirty IPv6 /64s, for an hour: no
  visitor becomes a guest until it passes, and they are told to sign in. That is the price of a bound on what the
  store is asked to keep; the provider sign-ins are not affected.
- Neutral: an upgrade that stops between binding the identity and writing the account, then is asked again with
  another name, holds the first name for an hour. After that hour anyone may take it (#1227). A rename that stops
  between its writes is the same.
- Neutral: a player cannot type a name that starts with `Guest-` or `guest_`, and so cannot rename to one either,
  even to change the case of a name they already hold; a player who registered one before keeps it.
- Neutral: a request with a session is checked against the list before its route, which reads the account. While
  the account store is out of service and the account is not in the 30-second cache, such a request answers
  `503 Account.Unavailable` rather than be let past the list on a guess.
- Bad: a guest who signs in to an account they already have loses the guest's tutorial progress. Merging it is left
  out: the account's own progress is the one its player made.
- Neutral: an upgrade that stops after the account is written leaves the guest's binding, which reaches nothing,
  on the account until its next removal, link or deletion (#1216). Its drawn name stays held for an hour (#1227).
- Neutral: no page changes here. UX15 builds the journey on these routes; `/api/account` and every sign-in answer
  say `guest` on the account so the page can tell. A guest's lobby page reads `403 Account.GuestNotAllowed` until
  UX15 decides what it shows.
- Neutral: the panel's *Run now* pass does not remove guests; the hourly job does.

## Alternatives considered

- **A guest with no account**, its tutorial played under a token alone: the progress, the settings and the
  practice's owner all key on an account id, and the upgrade would have to move them. An account that changes its
  identity moves nothing.
- **A deny-list of what a guest may not call**: every route added later would be open to guests until somebody
  remembered to close it.
- **Practice against bots for a guest too** (the plan's question 3): the owner chose the tutorials only.
- **Keep the guest's identity beside the provider's after the upgrade**: it signs in through nothing, and the account
  would read as a guest still.

## Follow-up

- UX15 (`docs/art/ux-craft-plan.md`): the journey, from either door, and what the page shows a guest outside a lesson.
- `docs/tabletop/sign-in-methods-plan.md`: P5 built on this.
