# 0122. A player signs in with Google, and their seats belong to their account

Date: 2026-11-03

Status: Accepted. Amends ADRs 0081, 0115 and 0116.

## Context

ADR 0115 gave the players a lobby without accounts: a seat is whoever holds its token, and "your games" is what
one browser saved. A game does not follow a player to another device, a cleared browser loses its shortcuts, and
a card cannot say who plays whom, which also leaves nothing for a leaderboard to stand on. The owner wants a home
page where players sign in with Google, choose a unique username, and own their games through an internal id;
signing in becomes required to play. The operator's own door is Microsoft Entra ID through the platform (ADR
0081), and nothing that deploys or runs the table holds a secret (ADR 0082).

## Decision

- **Players sign in with Google Identity Services, and the host verifies the ID token itself**: RS256 against
  Google's published keys, the issuer, the audience (the host's client id, public) and the expiry, with
  `System.Security.Cryptography` and no package. The host then sets its own `HttpOnly`, `SameSite=Lax` session
  cookie, HMAC-signed with a key it generates once and keeps in its store.
- **An account is an internal id** (`acct_` and sixteen random base32 characters) keyed by Google's `sub`, with a
  username chosen at the first sign-in: 3 to 20 letters, digits, `_` or `-`, starting with a letter, unique without
  regard to case, never an agent's name or a reserved word, not changeable for now. Uniqueness is a create-if-absent
  write of a name index. Nothing else is kept: no email, no real name, no picture.
- **A seat is bound to the account that took it**: creating, joining or claiming one requires a session and binds
  it; leaving unbinds it. A seat request must carry the session of the seat's account as well as its token. The
  host, not the browser, lists a player's games. Seats saved by a browser before this are adopted by the first
  account that signs in there.
- **The operator's door does not change.** Entra stays the platform's sign-in, beside the host's cookie.
- `--accounts off|google|dev` chooses it; `off`, today's behaviour, stays the default outside the container.
- Accounts live in Application (rules, port, use cases), their stores and the verifier in Infrastructure, the
  routes and the cookie in Cli. The domain does not learn about people.

The stages, the routes and the home page are in [docs/tabletop/accounts-plan.md](../tabletop/accounts-plan.md).

## Consequences

- Good: a game follows its player to any device; a card says `MarkF vs Greedy`; results can be attached to a stable
  id from the first match, which is what a ladder needs.
- Good: still no secret in the repository, the workflow or the image (ADR 0082 holds); the same sign-in runs on a
  laptop (`http://localhost` is an allowed origin) and in the container; the verifier is tested with a test's own
  key.
- Bad: the host now owns security-critical code: the token's verification and the cookie's signature. Both are
  small and fully tested, and a wrong one admits anybody as anybody.
- Bad: a sign-in before the first game, and a Google account to have one. A table with `--accounts off` remains for
  a laptop and a playtest with friends.
- Bad: the host holds personal data for the first time — a Google subject and a chosen name. The home page says
  what is kept, and an account can be deleted before the address is shared.
- Neutral: invitations and codes stay (ADR 0116); accepting one now needs a sign-in and binds the seat.

## Alternatives considered

- **The platform's built-in Google provider (Container Apps authentication).** The least code: the platform signs
  the player in and stamps `X-MS-CLIENT-PRINCIPAL-*`. It lost on three counts: Google's redirect flow needs a client
  secret, which ADR 0082 refuses; the platform keeps one identity per browser, so the owner could not be operator
  and player at once; and it does not exist on a laptop. Had it won, `OperatorGate` would have had to check
  `X-MS-CLIENT-PRINCIPAL-IDP` is `aad`, because the Entra authorization policy does not apply to a Google identity.
- **A JWT package (`Microsoft.IdentityModel.*`)** for the verification: a dependency for one algorithm and four
  claims. Kept as the fallback if the hand-written verifier proves hard to get right.
- **The email as the key**: an address can change owner; `sub` cannot.
- **Keep browser-held seats and only add a username**: a name without an identity is a label anybody can type, and
  nothing for a ladder.

## Follow-up

- `OperatorGate`'s remark ("the players are never asked") and ADR 0081's line on anonymous players.
- `table/README.md`, `docs/tabletop/playtest-app.md`, `AGENTS.md` (the `table` command's options).
- `infra/main.bicep` and `infra/README.md`: the Google client id, the one-time OAuth client.
- `docs/domain/glossary.md` if *account* reaches the language a player reads.
