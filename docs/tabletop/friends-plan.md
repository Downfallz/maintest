# Plan: friends, and inviting one of them to a table

Status: plan, settled with the owner on 2026-11-04 (every recommendation under "Questions the owner settled"
taken, and every notice of stage 4 wanted) and recorded by [ADR 0129](../adr/0129-a-player-keeps-friends-and-invites-one-of-them-to-a-table.md).
Builds on [accounts-plan.md](accounts-plan.md) (ADR 0122), the seat invitations of ADR 0116 and the push notices
of ADR 0118. Every stage is built.

## What the owner asked for

- Add another account to **your friends**.
- **Invite a friend** to a game from that list, and see whether the invitation is still **pending**.
- **One outstanding invitation at a time.**
- **Cancel** the invitation.
- The friend's phone gets a **push notice** for the invitation; yours gets one when they **accept** and when the
  **game starts**, and for whatever else would otherwise be missed.

## What it changes in what exists

| Today | After |
| --- | --- |
| A table against another person has an open seat anyone in the lobby can take, or a link and a code to pass around (ADR 0115, 0116). | A table can also be opened **for one friend**: its second seat is reserved for that account, is not public, and is taken by that friend with one tap from their lobby. |
| The other player is whoever took the seat. | The card says *Waiting for bastien…* while the invitation is pending, with *Cancel*. |
| A push notice is bound to a **seat**: it says "your turn", and only a browser holding a seat's token can subscribe (ADR 0118). | An **account** also has devices. A notice can reach a player who holds no seat yet: a friend request, an invitation, an acceptance, a decline, the end of a match. "Your turn" stays per seat for now. |
| A player's name is public on every card, and that is all another player can do with it. | A player can ask another, by name, to be their friend. The other accepts or declines. |
| Nothing about an account can be deleted but the account. | Friends, requests, devices and the one outstanding invitation go with the account, on both sides. |

The engine, the domain and the game rules do not change: a match knows seats, not people. Hosts with
`--accounts off` have no friends and no account devices; their lobby hides the panel and the routes answer 404.

## The model

### A friendship is mutual

One player asks, the other accepts. Until then it is a **request**, outgoing for one and incoming for the other.
Why not one-sided ("follow"): the point of a friend is that they can send your phone a notice. A stranger must
not be able to do that, and a one-sided list would let anyone who knows your name wake your phone. A request is
the consent.

- A request is made **by exact username**. There is no directory of players and no search: a name you were
  given, or saw on a card, is enough; a name nobody holds answers *No player has that name*.
- One pending request per pair. Asking someone who already asked you accepts their request.
- Declining is silent to the other side: their outgoing request simply disappears. Removing a friend is silent
  too, and cancels the outstanding invitation between the two if there is one.
- Reserved and bot names cannot be asked (they cannot be registered either). You cannot befriend yourself.
- At most 100 friends and 100 pending requests per account. Enough for this host, and a bound on a document.

### An invitation is a seat addressed to an account

The invitation reuses ADR 0116: a seat's single-use key. What is new is **who it is addressed to**. A table
opened for a friend has its second seat `Taken: false`, no public place (`OpenSeats` stays empty, so the lobby's
*Tables* list never shows it), and `InvitedAccount = <the friend's id>`. Only a request signed in as that account
can accept it; accepting is `TakeAsync` as it is today: a fresh token, the invitation spent, written to
`table.json` before the token is returned, under the claim gate.

So a pending invitation is **a table** with one seat waiting for a named account. That is where the owner's
rules come from for free:

- **Pending or not** is read off the registry, which already rebuilds every open table from `table.json` on a
  restart (ADR 0091): the invitation survives a deploy with nothing new to store.
- **One at a time**: an account may hold at most one table whose seat still waits for an invited account. The
  rule is one query on the registry, under the claim gate, when a table is opened or a seat re-invited.
- **Cancel** clears the invited account from the seat. The table is then the inviter's alone, not begun, and the
  card offers three ways out: *Invite another friend*, *Open the seat to everyone* (today's open place), or
  *Close the table*. Closing an unbegun table you opened is new, and useful on its own: today such a table waits
  twenty hours for nobody.
- **Decline** does the same as cancel, from the other side, and marks the seat *declined by bastien* so the
  inviter's card says why they are alone. The three ways out are the same.
- **Expiry**: a pending invitation expires **24 hours** after it was sent, as if declined. The existing rule that
  closes a table nobody has begun after twenty hours without a request does not do this: the inviter's open
  page keeps asking. The expiry is checked when the table is listed and by the hourly sweep that already closes
  unbegun tables.
- A friend whose account is deleted, or who removes you, is a decline.

Why the table is opened at once rather than at acceptance: the inviter is at the table from the start, the
waiting room of ADR 0092 is the waiting room, acceptance has nothing to compose (so capacity cannot fail at the
moment the friend says yes), and the one-at-a-time rule costs one table of sixteen for one day at most.

### An account has devices

A **device** is a push subscription (endpoint and two keys, the same shape as ADR 0118's `PushSubscription`)
registered by a signed-in browser for its account, up to **5** per account, oldest replaced. A seat's own
subscription stays what it is: it is the token's, and goes with it. An account's devices are the account's, and
go with *Sign out everywhere* and with the account. The same browser will usually be both; a notice is sent once
per endpoint.

What reaches a device, each a one-line notice with a title, a body naming the other player, and where a tap goes:

| Notice | To | Tap opens |
| --- | --- | --- |
| *bastien wants to be your friend* | the asked | `/lobby#friends` |
| *bastien accepted your friend request* | the asker | `/lobby#friends` |
| *MarkF invites you to a 2v2 battle* | the invited friend | `/lobby#invitations` |
| *bastien accepted. Take your seat.* | the inviter | `/play?table=<id>` |
| *bastien declined your invitation* | the inviter | `/lobby` |
| *Your turn* (ADR 0118, unchanged) | the seat | `/play?table=<id>` |
| *bastien left the table* (ADR 0115's Leave) | the other seat's account | `/play?table=<id>` |
| *You won against bastien* / *bastien won* / *A draw* | both seats' accounts | `/play?table=<id>` |

**"The game has started" is not a separate notice.** ADR 0092 begins the match the moment both people are at
their seats and asks both their first pick at once; the invited friend's page goes to the table as it accepts.
So the inviter hears *accepted* and, seconds later, *Your turn*, which is the match starting. A third notice in
between would say the same thing again (settled, question 5).

The rules of ADR 0118 and 0120 keep applying: a notice is not shown when a window of this host is visible and
focused on the page the notice opens; a device holding both seats of a table gets no notice about that table;
a push service that says a subscription is gone makes the account forget that device. A notice carries names
(public on every card already), a table's public id and nothing else: no token, no board.

## Where the code goes

| Layer | What |
| --- | --- |
| SharedKernel | Nothing: `AccountId` exists. |
| Application | `Accounts/Friends/`: `Friendship`, `FriendRequest`, `FriendErrors` (`Friend.Self`, `Friend.NoSuchPlayer`, `Friend.AlreadyFriends`, `Friend.AlreadyAsked`, `Friend.NotFriends`, `Friend.TooMany`), the port `IFriendStore`, and `Friends`, the use cases (list, ask, accept, decline or withdraw, remove, forget an account). `Accounts/PushDevice`, `AccountDevices` (the rules: five at most, one per endpoint) and the port `IDeviceStore` (list, save, forget an account). Pure, tested with an in-memory store. |
| Infrastructure | `FileFriendStore` / `BlobFriendStore` and `FileDeviceStore` / `BlobDeviceStore`, beside the account store, same container (`friends/<accountId>.json`, `devices/<accountId>.json`), same staged write. |
| Cli | `AccountNotices` (one notice to one account's devices through `IPushSender`, dedup by endpoint, dead endpoints forgotten), `FriendsApi`, the invitation routes in `LobbyApi`, `TableSeat.InvitedAccount` and `DeclinedBy`, `RecordedSeat.InvitedAccount`, `TableRegistry.InvitedBy(account)`. The host wires them when `--accounts` is on. |
| `table/` | `friends.js` (pure functions and the panel), the lobby's *Friends* and *Invitations* sections, the *A friend* choice in the create dialog, `sw.js` reading the notice's kind, the notices button on the lobby through `push.js`. |
| Domain | Nothing. |

Two documents per account rather than one record per pair: a friendship is written on both sides under a
process lock, as `AccountRegistry` writes are (one host runs one replica, ADR 0080), and a change that stopped
between its two writes is put right by the next change between the two accounts. Friends and devices stay out of `IAccountStore`, whose
contract is the sign-in's and is tested as such.

## The routes

All signed in (`401 Account.SignInRequired` otherwise), all behind the same-origin fence, all 404 with
`--accounts off`.

| Route | What |
| --- | --- |
| `GET /api/friends` | `{ friends: [{ id, username }], incoming: [{ id, username, since }], outgoing: [...] }`, names resolved at listing time as cards are. |
| `POST /api/friends/requests` `{ username }` | Asks. `404 Friend.NoSuchPlayer`, `409 Friend.AlreadyFriends` / `Friend.AlreadyAsked`, `400 Friend.Self`. Accepts instead when the other already asked. Sends *wants to be your friend*. |
| `POST /api/friends/requests/<accountId>/accept` | Sends *accepted your friend request*. |
| `DELETE /api/friends/requests/<accountId>` | Declines an incoming request, or withdraws an outgoing one. Silent. Both are `Friends.UnlinkAsync`, as removing is. |
| `DELETE /api/friends/<accountId>` | Removes a friend. Cancels an invitation between the two. Silent. |
| `PUT /api/account/push` `{ endpoint, keys }` / `DELETE /api/account/push` `{ endpoint }` | This browser is, or is no longer, one of the account's devices. `410` for an endpoint a push service said was gone. `POST /api/account/push/test` `{ endpoint }` sends a test notice to this device. |
| `POST /api/lobby/tables` `{ opponent: "friend", friend: "<accountId>", format }` | Opens the table with the second seat addressed to the friend. `403 Friend.NotFriends`; `409 Lobby.InvitationOutstanding` while another one of yours is pending. Sends *invites you*. |
| `GET /api/lobby/mine` | Adds `invitations: [{ table, from: { id, username }, sentAt }]`, the tables waiting for you; each of your own tables gains `invited: { id, username, sentAt, declined }` while a seat waits or was declined. |
| `POST /api/lobby/tables/<id>/accept` | The invited account takes the seat: today's claim by account instead of by key. Answers the token, as claim does. Sends *accepted* to the inviter. |
| `POST /api/lobby/tables/<id>/decline` | Sends *declined* to the inviter. |
| `DELETE /api/lobby/tables/<id>/invitation` | Cancel, by the inviter. |
| `POST /api/lobby/tables/<id>/invitation` `{ friend }` | Invite another friend to the same seat, after a cancel or a decline. Same two refusals as opening. |
| `POST /api/lobby/tables/<id>/open` | Offer the seat to everyone instead: today's open place. |
| `GET /api/lobby/opponents` | `{ opponents: [{ id, name, lastPlayedAt, games, won, lost, drawn, format, casual, relation }] }`: the eight players met most recently, from the kept results, newest first; an account deleted since is left out. `relation` is `friend`, `asked`, `asking` or `none`, null where the host keeps no friends. |
| `DELETE /api/lobby/tables/<id>` | Close a table that has not begun and whose every taken seat is yours. |

## The lobby

Signed in, the lobby gains two things in the table's visual language:

- **Invitations**, above *Your games*, only when there is one: an incoming card, *MarkF invites you · 2v2 ·
  Accept · Decline*, and your outgoing one among *Your games*, *Waiting for bastien… · Cancel*, or *bastien
  declined · Invite another · Open to everyone · Close*.
- **Friends**, in the sidebar: the list, each name with *Invite* (disabled, with the reason, while your
  invitation is pending), the incoming requests with *Accept* and *Decline*, *Add a friend* by name, and
  *Remove* behind each name's menu.
- The create dialog's *Who are you playing?* gains *A friend*, which shows the list and keeps the format.
- A **Notify me** button for the account, with `push.js`'s rules (https or localhost, installed on iOS), and the
  same *Test* as the game page. The *What we keep* panel adds one line: your friends list, and where to reach
  this browser if you asked to be notified.
- A table's outcome gains *Rematch*, which opens the create dialog with the same friend and format.
- **Recent opponents**, under *Your games*, only when there is one: the eight players met most recently, from the
  kept results (`GET /api/lobby/opponents`), in one row that scrolls sideways (the section keeps one card's height;
  on a phone the next card shows at the edge; friends first, the ones a tap challenges, then the others, each group
  met most recently first), each with the score between you (*2–1*, read as *Won 2 · Lost 1*), the last game's format and rating,
  and one way to play again: *Challenge* for a friend (the create dialog on them, in that format and rating), *Add
  friend* or *Accept friend* otherwise, *Friend request sent* while one waits. As with *Rematch* (ADR 0153), nothing
  is sent to a player who is not a friend.

A page on the lobby refreshes its lists when the service worker tells it a notice arrived, and on its usual
refresh otherwise.

## Stages

Each stage is one pull request: built, tested, formatted, the node tests when a page changes, docs updated
(`table/README.md`, `docs/tabletop/playtest-app.md`, `AGENTS.md`'s `table` line).

1. **An account has devices.** *Built*: `PushDevice`, `AccountDevices` and the port `IDeviceStore` in
   `Application/Accounts`, `FileDeviceStore` and `BlobDeviceStore` (`devices/<accountId>.json`), `AccountNotices`
   in the host, `PUT`/`DELETE /api/account/push` and `POST /api/account/push/test`, the lobby's *Notify me on this
   device* and *Send a test* (`devices.js`), `sw.js` opening the notice's own `url` and staying quiet when a window
   is focused on that page, and `IPushSender` taking a topic so a friend's notice never replaces a "your turn" on
   the push service. No notice but the test one is sent yet. Tested: five devices and the sixth replaces the
   oldest; the same endpoint registered twice is one device; a device the push service let go of is forgotten and
   its page answered `410` until it makes a new subscription; *Sign out everywhere* and deletion forget them all;
   the worker opens the lobby for a lobby notice and the game for a game notice.
2. **Friends.** *Built*: ADR 0129. `FriendList`, `FriendLink`, `FriendErrors`, `Friends` and the port
   `IFriendStore` in `Application/Accounts/Friends`, `FileFriendStore` and `BlobFriendStore`
   (`friends/<accountId>.json`), `FriendsApi` and its routes, the lobby's *Friends* panel (`friends.js`), the two
   friend notices, and the worker telling every open page a notice arrived so the lobby reads its lists again.
   Tested: a request made twice is one; asking back accepts; declining, withdrawing and removing are silent and leave
   neither side a link; a hundred and first is refused; one side written and the other missing is put right by the
   next change between the two; a deleted account leaves no request and no friendship behind, on either side.
3. **Invite a friend.** *Built*: `InvitedAccount`, `InvitedAt` and `DeclinedBy` on the seat, in its keys and in
   `table.json` (a seat kept for a friend has no code and no link: `TableSeat.Key` is null); `opponent: "friend"`
   on `POST /api/lobby/tables`, the invitation routes, the one-at-a-time rule, the 24-hour expiry and the
   unfriending both read as the lists are, the three ways out, closing an unbegun table, the *Invitations* cards
   and the inviter's card, *Invite* on the Friends panel and *A friend* in the create dialog, the waiting room
   saying it waits for a friend, and the three invitation notices. Tested: an invited seat is listed as *invited*
   and never as open, a join and a claim by its key are refused and `/api/seat/*` answers its key nothing; only the
   invited account accepts, once, and the inviter is told; a second invitation while one waits is `409`; cancel,
   decline and expiry each leave the inviter's table with no seat kept and the card saying who declined; removing
   the friend cancels it; a restart rebuilds a waiting invitation; a begun table is not closed from the lobby.
4. **What else would be missed.** *Built*: the end of a match to each seat's account (*Victory · You won against
   bastien*, *Defeat · bastien won*, *A draw*), the opponent leaving a begun match (*bastien left the table*),
   *Rematch* on the outcome card (the lobby's dialog with the same opponent and format, a friend, the bot or the
   shared device), and the account deletion freeing the seats kept for it and the seats it kept for friends (offered
   to everyone, as ADR 0122 offers its own), on top of its devices and friendships. Tested: each player is told against the other by name; a bot's seat is told nothing
   and the person is told against the bot by its name; one account holding both seats (a shared device) is told
   once, in the room's words; a finished table nobody with an account played tells nobody; leaving a begun match
   tells the other seat's account; a deleted account's seats kept for friends are offered to everyone and the seats
   kept for it are let go.

Not in this plan, each its own later decision: *your turn* to every device of an account instead of the seat's
browser (the follow-up ADR 0118 already names; stage 1 is what it needs), presence (*online now*), a block list
beyond declining, notification preferences, invitations to a group or a tournament, and chat.

## Questions the owner settled (2026-11-04: every recommendation taken)

1. **Mutual friendship or one-sided list?** Mutual: a request the other accepts, because a friend can wake your
   phone.
2. **Add by exact name, or search players?** Exact name. Names are already public on cards; a directory of every
   player is one more thing to explain in *What we keep*.
3. **Open the table when inviting, or when the friend accepts?** When inviting, as above; the cost is one of
   sixteen tables for at most a day.
4. **How long does an invitation wait?** 24 hours, then it counts as declined.
5. **A separate "the game has started" notice?** No: *accepted*, then the first *Your turn* is the game starting.
6. **Who may close an unbegun table?** Whoever holds every taken seat of it. Today only the operator can, and only
   from the admin panel.
7. **Which other notices in the first version?** All four of stage 4: the end of a match, the opponent leaving the
   table, a declined invitation, and *Rematch* on the outcome.
8. **In what order?** Devices, friends, inviting, the rest: one pull request each, as the stages say.
