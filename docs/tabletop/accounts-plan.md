# Plan: player accounts, a Google sign-in and a home page

Status: plan, accepted with [ADR 0122](../adr/0122-a-player-signs-in-with-google-and-owns-their-seats.md) on 2026-11-03,
with every recommendation under "Open questions" taken. Stages 1 and 2 are built; the rest is not.

## What the owner asked for

- A home page where a player signs in, and registers, with their Google account. The admin panel keeps its
  Microsoft Entra ID sign-in (ADR 0081); Google is for the public tables.
- Every player chooses a **username**, unique across the host, before they can play.
- Every player has an **internal id**, unique and never shown as a name. Their games are attached to it,
  not to a browser's `localStorage` (ADR 0115) — so a game follows the player to another device.
- Signing in is **required to play**. Reading the public lobby is not.
- Match cards show who plays whom: usernames, bots by their name, an open place as open.
- Later: a leaderboard, profiles, and whatever else an identity makes possible. This plan only makes sure the
  data a leaderboard needs is recorded from the first account on.

## What it changes in what exists

| Today | After |
| --- | --- |
| A seat is held by whoever holds its token; the browser keeps up to 128 tokens (ADR 0115). | A seat is bound to an account. The token is still the transport key, but a seat request must also carry the session of the account the seat is bound to. |
| "Your games" is what this browser saved. | "Your games" is what the host says the signed-in account holds, on any device. |
| Lobby cards say `Player vs greedy`. | Lobby cards say `mkf vs Greedy`, `mkf vs bastien`, `mkf vs — open —`. |
| Anyone can open a table or take an open place. | Only a signed-in account can; anyone can still read the summaries. |
| An invitation link or code is accepted with a tap (ADR 0116). | The same, after signing in; accepting binds the seat to the account. |
| A recorded session's `who` is initials the operator typed. | Each seat records the account id (and the username at the time); a person-vs-person result is kept for a ladder. |
| The operator signs in with Entra through the platform (ADR 0081). | Unchanged. The two sign-ins live side by side, so the owner can be the operator and a player at once. |

The engine, the domain and the game rules do not change: a match knows seats, not people.

## The sign-in: Google Identity Services, verified by the host

The page loads Google's sign-in script (`https://accounts.google.com/gsi/client`) and shows its button. Google
hands the page an **ID token** (a signed JWT). The page posts it to the host, the host verifies it, and the host
sets its **own session cookie**. Google is never asked again until that cookie expires.

The host verifies the token itself, with `System.Security.Cryptography` and no package:

- RS256 signature against Google's published keys (`https://www.googleapis.com/oauth2/v3/certs`), cached for as
  long as their `Cache-Control` says, refetched once on an unknown `kid`;
- `iss` is `accounts.google.com` or `https://accounts.google.com`;
- `aud` is the host's Google client id;
- `exp` is in the future (one minute of skew), `iat` is not.

The account is keyed by **Google's `sub`**, never by the email: an email can change owner, `sub` cannot.

Why this and not the platform's built-in Google provider (Container Apps authentication), which ADR 0122
weighs in full:

- **No secret.** Google's redirect flow needs a client secret; the ID-token flow does not. ADR 0082 is kept:
  nothing that deploys or runs the table holds a secret. A Google client id is public, like an Entra one.
- **The operator and the player are two sessions.** The platform keeps one signed-in identity per browser:
  signing in with Google would sign the owner out of Entra. The host's own cookie sits next to the platform's.
- **The laptop is the container.** Google accepts `http://localhost` as a JavaScript origin, so the same sign-in
  runs under `dotnet run` — no "only works when deployed".
- **It can be tested.** The verifier takes its keys from a port; a test signs its own token with its own RSA key.

What it costs: about a hundred lines of JWT verification that have to be right, and a session key the host
generates once and keeps in its store (blob `session.key` in the accounts container, behind the managed identity like
everything else). That key is made by the host, never typed by anyone, and rotating it signs everyone out.

### The session cookie

`downfall_session`, `HttpOnly`, `Secure` (except on a loopback `http` host), `SameSite=Lax`, `Path=/`, thirty
days, renewed on use. Its value is `v1.<accountId>.<issuedAt>.<generation>.<hmac>`: the host checks the HMAC,
the age, and that `generation` still equals the account's. **Sign out everywhere** bumps the generation.
The existing same-origin fence (`Sec-Fetch-Site`, `TableServer`) keeps protecting the `POST`s a cookie now
authorises.

### `--accounts`

| Value | Who uses it | What it does |
| --- | --- | --- |
| `off` (the default) | `table --rules <file>`, practice, tests that predate this | Today's behaviour: tokens and codes, no sign-in. |
| `google` | the container, and a laptop with a client id | Google sign-in required to play. Needs `--google-client-id` (or `DOWNFALL_GOOGLE_CLIENT_ID`). |
| `dev` | a laptop, the host tests | "Sign in as `<name>`" with no Google, refused unless the host binds loopback only. |

The container runs `table --lobby --platform-auth --accounts google --google-client-id <id> ...`; the id is a
repository variable passed to `infra/main.bicep`, like `signInClientId`.

## The account

```jsonc
// accounts/<accountId>.json
{
  "id": "acct_7kq2m9x4p2d8w3zc",   // internal id: random, 16 base32 characters after the prefix
  "provider": "google",
  "subject": "109876543210987654321", // Google's sub
  "username": "MarkF",              // as typed
  "createdAt": "2026-11-03T20:14:00Z",
  "generation": 1                   // bumped by "sign out everywhere"
}
```

Accounts live in a blob container of their own, `accounts`, next to the recordings' one and never inside it: the
session store reads every top-level prefix of its container as a session. On a laptop they are a directory.

Two index records make look-ups and uniqueness atomic, written with "create only if absent" (`If-None-Match: *`
on a blob, a move that does not overwrite on a file):

- `accounts/by-subject/google/<sub>.json` → the account id;
- `accounts/by-name/<username, lower-cased>.json` → the account id.

Registration writes the name first, then the account, then the subject. A crash between leaves a name pointing
at an account that does not exist; the next registration of that name finds it and takes it back after an hour.

**Kept: nothing else.** No email, no real name, no picture. A ladder needs a stable id and a name, and holding
less is the cheapest way to honour Quebec's Law 25 and PIPEDA. The home page says so in one line, and an
account can be deleted (stage 4).

### The username

- 3 to 20 characters: letters, digits, `_` and `-`; starts with a letter. Displayed as typed.
- Unique without regard to case (`MarkF` and `markf` are one name).
- Reserved: `admin`, `operator`, `pilot`, `system`, `guest`, `open`, `player`, `player1`, `player2`, `bot`,
  `downfall`, and every agent name the lobby or `learning/seatable.json` can seat (`greedy`, `random`,
  `lookahead`, `minimax`, `heuristic`, `policy`...), so a person is never mistaken for a bot on a card.
- Not changeable in the first version. Because results are attached to the id, a rename later moves nothing.

## Where the code goes

| Layer | What |
| --- | --- |
| SharedKernel | `Identifiers/AccountId`, beside the other strongly typed ids. |
| Application | `Accounts/`: `Username` (the rules above, returning `Result` with `Account.*` error codes), `Account`, the port `IAccountStore`, and `AccountRegistry`, the use cases: find the account of a sign-in (or none: it needs a username), check a name, register. The port `IIdentityTokenVerifier` so the use cases never see a JWT. |
| Infrastructure | `FileAccountStore` and `BlobAccountStore` (create-if-absent semantics), `GoogleIdTokenVerifier` with its key cache behind a fetcher. |
| Cli | `AccountApi` (routes below), the session cookie, `--accounts` and `--google-client-id`, the binding of seats in `LobbyApi` and `TableRecord`. |
| Domain | Nothing. |

Accounts belong to the people around a match, not to the game, so they stay out of Domain and SharedKernel.
No architecture rule moves.

## The routes

| Route | Signed in? | What |
| --- | --- | --- |
| `GET /api/me` | — | Adds `account: { id, username } \| null` and `accounts: "off" \| "google" \| "dev"` and the Google client id, next to what it already says about the operator. |
| `POST /api/account/google` `{ credential }` | — | Verifies the ID token. Known subject → sets the cookie, `{ account }`. New subject → `{ needsUsername: true, ticket }`, a ten-minute signed ticket holding the verified `sub`. |
| `GET /api/account/names/<name>` | — | `{ available, reason? }` for the live check while typing. |
| `POST /api/account` `{ ticket, username }` | — | Registers, sets the cookie. `409 Account.UsernameTaken`, `400 Account.UsernameInvalid` / `Account.UsernameReserved`. |
| `POST /api/account/dev` `{ username }` | — | `--accounts dev` only: signs in (registers if new) with no Google. |
| `POST /api/account/signout` `{ everywhere? }` | yes | Clears the cookie; `everywhere` bumps the generation. |
| `GET /api/account/games` | yes | The account's live tables with their seat tokens, which replaces `POST /api/lobby/resume`. |
| `POST /api/account/adopt` `{ tokens }` | yes | Binds the seats a browser saved before accounts existed (and no account holds) to this account. Run once by the page after the first sign-in on that browser. |
| `DELETE /api/account` | yes | Stage 4: leaves every live seat (ADR 0115's Leave), frees the name, removes the account and its indexes; recorded sessions keep only the id. |

`POST /api/lobby/tables`, `/tables/<id>/join` and `/claim` answer `401 Account.SignInRequired` without a session
when accounts are on, and bind the seat(s) they hand out to the account. `POST /api/lobby/leave` unbinds. A
table request whose seat is bound to another account (or to none, while the session exists) is `403`. Joining
the other seat of your own table online is refused (`Lobby.AlreadySeated`); a shared device is the way to hold
both.

`TableRecord`'s seat gains `account` (absent on records written before). The lobby summary's `players`
becomes `seats: [{ seat, kind: "person" | "bot" | "open", name }]`, the name resolved from the account at listing
time.

## The home page

The mockup: <https://claude.ai/artifact/7aDdifjT6H1kpNAmuGBqhN> (private to the owner until shared). Four states, all on `/`, in the table's own
visual language (felt-dark, Georgia headings, one gold action):

1. **Signed out.** The hero (*Downfall Arena — two players, one arena*), one action: **Continue with Google**
   (Google's own button, as their branding rules require). Under it, the live public tables, read-only: *MarkF vs
   Greedy · Round 4*, *bastien vs — open —*. Joining one, or typing an invitation code, opens the sign-in and
   comes back to what was asked (`sessionStorage` keeps the intent). A footer line: what is kept (a name and an
   id, never the email), and the operator's *Admin* link.
2. **Choose your name** (first sign-in only). One card: the field, a live *available / taken / reserved* line,
   the rules in one sentence, **Enter the arena →**. *This is the name other players see on every table.*
3. **Signed in: the lobby.** Today's lobby, with the account chip (username, *Sign out*, *Sign out everywhere*)
   in the masthead. **Your games** comes from the host, so it is the same on the phone and the laptop, and a card
   says *Your turn* when a question waits for one of your seats. **Open tables** and **New table** as today.
4. **Arriving with an invitation, signed out.** *bastien invited you to a 2v2 battle* — sign in to accept —
   then ADR 0116's confirmation.

The match card: the format and state on top, then the versus line, Player 1's side in the ally colour and
Player 2's in the enemy colour, a bot with a small *bot* tag, an open place as a dashed *open seat*. The table
page's masthead says the same: *You: MarkF · against Greedy*.

## Stages

Each stage is one pull request, built, tested, formatted, with the node tests when a page changes.

1. **Accounts, alone.** *Built*: `AccountId` (SharedKernel), `Username`, `Account`, `IAccountStore` and
   `AccountRegistry` (`FindAsync`, `CheckUsernameAsync`, `RegisterAsync`) in `Application/Accounts`,
   `FileAccountStore` and `BlobAccountStore` in `Infrastructure/Accounts`, with tests (uniqueness under a race, reserved names, case, the crash between the name and the
   account). No route, no page.
2. **Signing in.** *Built*: `GoogleIdTokenVerifier` and `GoogleSigningKeys` (tested with a key a test makes), the
   session key in the account store, `AccountSessions` (cookie and ticket), `AccountApi`, `--accounts` and
   `--google-client-id`, the account routes, `/api/me`. The home page's states 1 and 2, and the account chip.
   Nothing is required yet: a signed-in player still plays the way they do today.
3. **Seats belong to accounts.** Binding on create, join, claim and leave; the `403`; `GET /api/account/games`
   replacing the browser's history; `adopt`; usernames on the cards and on the table page; state 4. Here
   "signed in to play" becomes true. `table/README.md`, `playtest-app.md` and `AGENTS.md` updated.
4. **On Azure.** *Started*: the deployment passes the project's client id (or `TABLE_GOOGLE_CLIENT_ID`) as
   `--accounts google`, `infra/README.md` says how the client is made, and a loopback host answers to `localhost`,
   the one loopback origin Google accepts. The OAuth client exists (a *Web application*; JavaScript origins: the
   app's address and `http://localhost:5099`; no redirect URI, no secret). Still to do: `DELETE /api/account` and
   the privacy line before the address is shared.
5. **Results for a ladder.** When a match with at least one account ends, append
   `results/<yyyy-mm>/<matchId>.json`: when, format, declaration, content hash, each seat's account id or agent,
   the winner, the rounds. Recorded sessions carry the seat accounts instead of `who`; `train-clone`'s export keeps
   the ids and drops the usernames.

Later, each its own plan: the leaderboard (a rating for person-vs-person only — Glicko-2 suits few, irregular
games — and a separate record against each bot; shared-device and abandoned matches unranked), a profile page
with a player's history, "your turn" sent to every device of an account instead of each seat (ADR 0118), a
rename, and letting the operator open a table for a named player.

## Questions the owner settled (2026-11-03: every recommendation taken)

1. **The sign-in mechanism.** The host verifying Google's ID token (recommended, above) or the platform's
   Google provider (less code, but a client secret and one identity per browser). ADR 0122 decides; it is
   *Proposed* until the owner says.
2. **The email.** Recommended: not kept. Keeping it would only serve to contact a player.
3. **Renaming.** Recommended: not in the first version.
4. **Shared device.** Recommended: still allowed, held by one account, never ranked.
5. **Reading without an account.** Recommended: the public lobby stays readable signed out; only playing needs a
   sign-in.
