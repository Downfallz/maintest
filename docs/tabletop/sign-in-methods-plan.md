# Plan: more ways to sign in, and a guest

Status: accepted, 2026-11-23. The owner settled the four questions (at the end). P1 to P3 are built; P4 to P6 are
not.

## What the owner asked for

"How could I integrate Supabase (or something else)? The end goal is to support many sign-in mechanisms:
Discord, guest, with UX15 in view, Facebook, whatever."

The idea named UX14; the stage that needs a guest is UX15, *a lesson before an account*
([ux-craft-plan.md](../art/ux-craft-plan.md)). UX14 is the acceptance. This plan serves UX15.

## What exists today

| Concern | What the code and the docs hold |
| --- | --- |
| Sign-in | Google Identity Services. The page gets an ID token, `POST /api/account/google` posts it, the host checks it with no package and no secret (`Infrastructure/Accounts/GoogleIdTokenVerifier.cs`, [ADR 0122](../adr/0122-a-player-signs-in-with-google-and-owns-their-seats.md)). |
| Modes | `--accounts off\|google\|dev` (`Cli/Table/AccountsMode.cs`, `CliOptions.cs`). One provider at a time. |
| The identity | `ExternalIdentity(provider, subject)` (`Application/Accounts/ExternalIdentity.cs`): already provider-neutral. |
| The account | `Account(Id, Identity, Username, CreatedAt, Generation)`: **one** identity per account. No email, no real name, no picture. |
| Indexes | `by-subject/<provider>/<subject>.json` → the account id (`Infrastructure/Accounts/AccountDocuments.cs`). Already keyed by provider. |
| The port | `IIdentityTokenVerifier.VerifyAsync(token)` → an identity or null. One verifier, Google's. |
| Sessions | The host's own cookie `downfall_session`, HMAC with a key the host made (`Cli/Table/AccountSessions.cs`). Any provider ends in the same cookie. |
| The page | `table/account.js` loads `https://accounts.google.com/gsi/client` and shows Google's button. |
| Secrets | None for sign-in. The VAPID push keys are a Container App secret ([ADR 0118](../adr/0118-your-turn-reaches-a-phone-nobody-is-looking-at.md)): the precedent for one more. |
| Registration | The operator can close it ([ADR 0177](../adr/0177-let-the-operator-close-registration-and-post-a-notice.md)); suspension refuses a sign-in ([ADR 0178](../adr/0178-let-the-operator-suspend-an-account.md)). Both hold per account, whatever the provider. |
| Guests | None. Playing, practice and the tutorials need an account (accounts-plan question 5, onboarding-plan question 5). UX15 needs an ADR that amends it. |

Not measured: how many people stopped at the Google button. The host keeps no count of a sign-in that did not
happen. P2 adds one per provider, so the next plan reads it.

## Supabase, a broker, or the host

Three shapes fit "many providers". This plan recommends the third.

| | Supabase Auth | Entra External ID | The host, one provider at a time |
| --- | --- | --- | --- |
| How | The page signs in with `supabase-js`; the host checks Supabase's JWT against its JWKS (asymmetric keys since 2025) and keys the account by Supabase's `sub`. | The page redirects to Microsoft's hosted pages; the host checks its ID token. | Each provider gets its own small exchange in Infrastructure, behind one port, as Google has today. |
| Providers | Google, Discord, Facebook, Apple, GitHub, anonymous, email links: switches in a dashboard. | Google, Facebook, Apple, email codes built in; other providers through OIDC. Discord is not OIDC. | Google (built), Discord (PKCE, no secret), Microsoft (PKCE, no secret), Facebook (needs its app secret), Apple (needs a signing key). |
| Secrets on our side | None: Supabase holds every provider's secret. | None. | None, until Facebook or Apple: then one Container App secret each, like the push keys. |
| What a third party keeps | Supabase keeps each user's email and provider profile. Our "no email" (ADR 0122) becomes "not kept by us". | Microsoft keeps them. | Nothing beyond what the provider itself holds. |
| Cost | Free up to 50,000 monthly users, but a free project pauses after a week of low activity: a quiet week, nobody signs in. Pro is 25 USD a month. | 50,000 monthly users free. | Nothing. |
| Guest | Anonymous sign-in built in. | None. | The host makes the guest itself (below). |
| Existing accounts | Keyed by Google's `sub`; Supabase gives its own `sub`. Every account needs a migration or a second key. | The same migration. | Unchanged. |
| Down | Supabase down, nobody signs in. | Microsoft down, nobody signs in. | Only the provider chosen is down. |
| Laptop | A second project, or the dev mode. | A second tenant. | `localhost` works as Google's does. |

**Recommended: the host.** The model is already provider-neutral, the index is already keyed by provider, and
Discord, the first one asked, takes no secret. Supabase pays off at five providers and more, at the price of a
monthly bill or a pausing project, emails held by a third party, and a migration of every account. If the owner
later wants email links or Apple, Supabase can still be added as **one more provider** behind the same port: its
`sub` becomes an identity like Discord's, nobody migrates.

## The shape

### One account, several ways in

`Account.Identity` becomes `Account.Identities`: one or more `ExternalIdentity`. Each has its `by-subject` index,
already the case for the first. A stored account with `provider` and `subject` reads as one identity; a new one
writes `identities: [...]`.

- **Signing in** with an identity the host knows signs in its account, whatever the provider.
- **A new identity, signed out**: the name window, as today. The window adds *Already have an account? Sign in
  with it, then add this way in from Settings.*
- **Linking**, signed in: Settings, *Ways to sign in*, lists the account's providers and offers the others.
  Adding one writes its index, create-only-if-absent: an identity another account holds is refused
  (`409 Account.IdentityTaken`). Removing one is refused when it is the last.
- **Never by email.** The host keeps no email, so it never links two identities because they share one. That is
  also what keeps a stranger who controls a Discord account with your address out of your account.

### Providers behind one port

`IIdentityTokenVerifier` becomes `ISignInProvider`, one per provider, each naming itself (`google`, `discord`) and
turning what the page sends into an identity or null:

| Provider | What the page sends | What the host does | Secret |
| --- | --- | --- | --- |
| `google` | The ID token. | Checks it, as today. | None. |
| `discord` | `{ code, verifier, redirect }` after Discord's authorize page, code flow with PKCE, the app marked *public client*. | Exchanges the code at `discord.com/api/v10/oauth2/token` with the verifier, reads `GET /oauth2/@me` (the app and the user), keeps the user's `id`. Scope `identify` only. | None. |
| `microsoft` | The same shape, against personal Microsoft accounts. | Exchanges, checks the ID token as Google's. | None. |
| `facebook` (not planned) | The access token from Facebook's SDK. | Asks `debug_token` whether it was issued to this app, keeps the user id. | The app secret, as a Container App secret. |
| `guest` | Nothing. | Makes one (below). | None. |

The route becomes `POST /api/account/<provider>`; `/google` keeps its body. `--accounts google` becomes
`--sign-in google,discord,...` (the old flag kept as an alias), each provider with its client id
(`--discord-client-id`, a repository variable passed to `infra/main.bicep` like `googleClientId`). `GET /api/me`
lists the providers this host offers, in order, and the page draws a button for each. Discord's return lands on
a small page, `/signin/discord`, that posts the code and goes on to `next`. The PKCE verifier and a `state`
live in the tab's `sessionStorage` and nowhere else.

### The guest

A guest is an account the host makes without any provider: identity `guest:<random>`, a name the host draws
(`Guest-7KQ2`, reserved, never typed), the same cookie. What it may reach and how long it lives are questions 3
and 4. The recommended answer:

- It plays the tutorials and nothing else: no lobby table, no friends, no ladder, no feedback outside a lesson.
  The routes it may call are a list in the host, and every other one answers `403 Account.GuestNotAllowed`.
- It lives as long as the browser keeps its cookie: thirty days, renewed on use. Once no cookie can reach it,
  `table-tend` removes it and its tutorial progress, so the host keeps no guest nobody can use.
- Signing in with a provider **upgrades** it: the identity is added to the guest's account, the player chooses
  their name, the account keeps its id and its tutorials. That is UX15's "asked to sign in when it ends".
- Making a guest is counted per network address, as feedback counts a guest's reports (ADR 0189), so a script
  cannot make thousands. Closed registration closes guests too.

## Where the code goes

| Layer | What |
| --- | --- |
| Application | `Account.Identities`; `AccountRegistry.LinkAsync`, `UnlinkAsync`, `MakeGuestAsync`, `UpgradeAsync`; `ISignInProvider` replacing `IIdentityTokenVerifier`; `Account.IdentityTaken`, `Account.LastIdentity`, `Account.GuestNotAllowed`. |
| Infrastructure | The stores read and write several identities; `DiscordSignIn`, `MicrosoftSignIn`, `FacebookSignIn`, each over an HTTP port a test replaces. |
| Cli | `--sign-in` and the client ids, `POST /api/account/<provider>`, the identity routes, the guest's route list, `/signin/<provider>`. |
| Pages | `table/account.js` draws the buttons from `/api/me`; Settings gains *Ways to sign in*. |
| Domain, SharedKernel | Nothing. |

## Stages

1. **P1, one account, several ways in.** ADR that amends ADR 0122: an account holds one or more identities from
   the providers the host lists, linked only by a signed-in player, never by email. `Account.Identities`, the
   stores reading the old shape, `LinkAsync` and `UnlinkAsync`. No route, no page. Tests: an old account reads as
   one identity; a link writes its index; an identity held elsewhere is refused under a race; the last one
   cannot be removed.

   *Built (2026-11-23)*: [ADR 0204](../adr/0204-let-one-account-hold-several-ways-to-sign-in.md) amends ADR 0122.
   `AccountIdentities` (never empty, never one twice, in the order added) replaces `Account.Identity`. An identity
   signs in only when its entry names the account and the account lists it. `AccountRegistry.LinkAsync` binds the
   index first, create-only-if-absent. It refuses an identity another account lists (`Account.IdentityTaken`) and
   a suspended account, and takes back an entry that holds nobody. `UnlinkAsync` writes the account first and
   raises its generation, so every session is signed out. It refuses the last identity (`Account.LastIdentity`) and
   one the account does not hold (`Account.IdentityNotLinked`). Deleting an account unbinds every identity it
   lists. The stores write `identities` and keep the first as `provider` and `subject`, so the previous release
   reads them; a record without `identities` reads as one. Left to P4: the routes over `LinkAsync` and
   `UnlinkAsync`, shipped once this release is the only one running (ADR 0204, Consequences), and a cap on the
   number of identities an account lists.

2. **P2, providers behind one port.** `ISignInProvider`, Google moved behind it unchanged, `--sign-in` with
   `--accounts` kept as an alias, `POST /api/account/<provider>`, `/api/me` listing the providers, the page
   drawing a button per provider, a count of sign-ins started and finished per provider. Tests: every existing
   account test passes unchanged; an unknown provider is `404`; the page draws what `/api/me` lists.

   *Built (2026-11-23)*:

   - The port: `ISignInProvider` (a name, the public client id its button is drawn for, and a proof in, an identity
     or null out) replaces `IIdentityTokenVerifier`. Google's verifier implements it unchanged and reads the posted
     `credential`. A proof is the string fields of the posted object (`SignInProof`), their names read in any case
     as before; a body that is not one object is a 400.
   - The route: `POST /api/account/<provider>` signs in with each provider the host lists. Any other provider is a
     404 `Account.NoProvider`, which replaces `Account.NoGoogle`. A provider that returns an identity of another
     provider is refused, and a provider whose name is not lower-case letters is refused when the host is built.
   - The flag: `--sign-in google[,...]` lists the providers in order. `SignInProviders.Known` and `Build` hold
     what this release knows. `--accounts google` is the older spelling and is refused beside it.
   - `/api/me` lists `signIn: [{provider, clientId}]`. It still says `accounts: "google"` and `googleClientId`,
     which the page of the release before reads; the new page reads an older host's answer as Google alone. Renaming
     `accounts` waits for P3, once every page reads `signIn`.
   - The page draws the button of each listed provider it knows (`DRAWN_WAYS`, the row `#sign-in-ways`) and hides
     the others.
   - The count: each sign-in's steps are counted by provider. A sign-in is finished once a session is issued, after
     the name window for a new player, and refused when the provider says no, the account is suspended or
     registration is closed. P2 wrote a console line per step; since #1171 the host counts the steps in memory and
     writes one line a minute per provider, and when it stops, `Sign-ins (<provider>): … started, … finished, …`.
   - The account tests pass with the mode renamed (`AccountsMode.SignIn`) and their fake Google behind the new
     port. A credential that is not text is now a 401 rather than a 400.
   - Since built: the ops report sums the count, and a forged proof no longer writes a line each (#1171).
     `infra/main.bicep` still passes `--accounts google`; P3 changes it with `discordClientId`.
3. **P3, Discord.** `DiscordSignIn` (PKCE, public client, scope `identify`), `/signin/discord`, the state check,
   `discordClientId` in `infra/main.bicep`, and the owner's steps in `infra/README.md` (the app, the redirect
   addresses, *public client* on). Tests: a fake Discord answers the exchange and `@me`; a wrong state, a reused
   code, a token for another app are refused. `security-reviewer` and `host-reviewer` read it.

   *Built (2026-11-23)*:

   - The provider: `DiscordSignIn` (Infrastructure) reads `code`, `verifier` and `redirect`. It exchanges them at
     `discord.com/api/v10/oauth2/token` with the client id and no secret, asks `oauth2/@me`, which names the app the
     token was issued to and the user, and revokes the token. A token of another app, an answer without a user or a
     snowflake, and a refused or reused code are each a refusal (401). A proof of the wrong shape is refused before
     Discord is asked: a verifier outside RFC 7636, or a redirect that is not this host's `/signin/discord` over https
     (or over http on a loopback table only).
   - Discord that cannot be asked is not a refusal: out of reach, slower than ten seconds, a 5xx or a 429 throws
     `SignInUnavailableException`, which the route answers `503 Account.ProviderUnavailable` with the line
     `Sign-in unavailable (discord): <why>.`, said once a minute (#1171). The host asks Discord at most 30 times a minute and 4 at once, and not at
     all until a 429's `Retry-After` has passed, so a script cannot get the table's address banned by Discord.
   - The host: `--sign-in google,discord`, `--discord-client-id` or `DOWNFALL_DISCORD_CLIENT_ID`; a client id given
     without its provider is refused. `/api/me` now says `accounts: "signin"` and no longer `googleClientId`. The P2
     page reads `signIn` instead. The service worker does not serve a cached page while the network answers, so no
     old page meets the new host.
   - The page: `discord.js` makes the verifier and the state and keeps them with `next` in the tab's
     `sessionStorage`, for ten minutes at most. It then leaves for Discord's authorize page (`identify`, S256,
     `prompt=none`). The button is the table's plain button, under Google's, and comes back after Back at Discord.
   - The return: `/signin/discord` (`signin.html`, `signin.js`) takes the code off the address and refuses a state the
     tab did not send. It posts the code and goes back to the lobby with `next`. A new player's ticket reaches the
     lobby's name window through the tab's storage. The player saying no goes straight back; any other error is said.
   - The privacy policy names Discord in both languages.
   - Infra: `discordClientId` in `infra/main.bicep` from the `TABLE_DISCORD_CLIENT_ID` variable; unset, nothing
     changes. The owner's steps are in `infra/README.md` (*Player sign-in*), and the redirect per address in
     `infra/runbooks/sign-ins.md`.
   - Since built: the Discord journey in the browser suite against a mock of Discord's pages
     (`studio/browser/signin-discord.spec.js`, #1194); a count of sign-ins per network address, so one address
     cannot spend the minute's 30 exchanges for everybody, while a room behind one address still signs in (#1193).
   - Left: creating the Discord application and the variable (the owner's steps).

4. **P4, ways to sign in.** Ships once P1's release is the only one running (ADR 0204). Settings lists the
   account's providers, adds one (the provider's own flow, then `POST /api/account/identities/<provider>`) and
   removes one. Tests: the routes; the journey at 320 px.
5. **P5, the guest.** The ADR UX15 asks for, amending accounts-plan question 5: what a guest is, the routes it
   reaches, its lifetime, the per-address count, the upgrade. `MakeGuestAsync`, `UpgradeAsync`,
   `POST /api/account/guest`, the route list, `table-tend` removing old guests. No page: UX15 builds the journey
   on it. Tests: a guest reaches the tutorials and is refused everything else; upgrading keeps the id and the
   tutorials; an old guest is removed; the address count.
6. **P6, Microsoft.** `MicrosoftSignIn` (personal accounts, PKCE, no secret), as P3, with `microsoftClientId`
   and the owner's steps in `infra/README.md`.

Not planned (question 2): Facebook, which needs the app secret and Meta's app review, and Apple, which needs
a signing key and a paid developer account. Each comes back as its own stage with an ADR when a player asks.

## Questions for the owner

1. **Who signs people in?** The host, one provider at a time (recommended); Supabase Auth for every provider;
   Entra External ID.
2. **Which providers after Discord?** Any of: Microsoft, Facebook (a secret, and Meta's app review), Apple (a
   signing key and a paid Apple developer account). Recommended: Discord now, the rest when a player asks.
3. **What may a guest reach?** The tutorials only (recommended, UX15); the tutorials and practice against bots;
   everything but the ladder and friends.
4. **How long does a guest live?** 30 days after its last use (recommended); 7 days; until the browser forgets
   its cookie.

## Questions the owner settled (2026-11-23)

1. **Who signs people in:** the host, one provider at a time. No Supabase; it can come back as one provider.
2. **After Discord:** Microsoft (P6). Not Facebook, not Apple.
3. **What a guest reaches:** the tutorials only.
4. **How long a guest lives:** as long as the browser keeps its cookie (thirty days, renewed on use); then
   `table-tend` removes it.
