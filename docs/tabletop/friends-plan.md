# Plan: friends, and inviting one of them to a table

Status: plan, settled with the owner on 2026-11-04 (every recommendation under "Questions the owner settled"
taken, and every notice of stage 4 wanted). Builds on [accounts-plan.md](accounts-plan.md) (ADR 0122), the seat
invitations of ADR 0116 and the push notices of ADR 0118. Nothing here is built; the ADR that records it is the
first deliverable of stage 2.

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
| Application | `Accounts/Friends/`: `Friendship`, `FriendRequest`, `FriendErrors` (`Friend.Self`, `Friend.NoSuchPlayer`, `Friend.AlreadyFriends`, `Friend.AlreadyAsked`, `Friend.NotFriends`, `Friend.TooMany`), the port `IFriendStore`, and `Friends`, the use cases (list, ask, accept, decline or withdraw, remove, forget an account). `Accounts/PushDevice` and the port `IAccountDevices` (list, add, remove, forget an account). Pure, tested with an in-memory store. |
| Infrastructure | `FileFriendStore` / `BlobFriendStore` and `FileAccountDevices` / `BlobAccountDevices`, beside the account store, same container (`friends/<accountId>.json`, `devices/<accountId>.json`), same staged write. |
| Cli | `AccountNotices` (one notice to one account's devices through `IPushSender`, dedup by endpoint, dead endpoints forgotten), `FriendsApi`, the invitation routes in `LobbyApi`, `TableSeat.InvitedAccount` and `DeclinedBy`, `RecordedSeat.InvitedAccount`, `TableRegistry.InvitedBy(account)`. The host wires them when `--accounts` is on. |
| `table/` | `friends.js` (pure functions and the panel), the lobby's *Friends* and *Invitations* sections, the *A friend* choice in the create dialog, `sw.js` reading the notice's kind, the notices button on the lobby through `push.js`. |
| Domain | Nothing. |

Two documents per account rather than one record per pair: a friendship is written on both sides under the
process lock `AccountRegistry` already uses for its writes (one host runs one replica, ADR 0080), and a reader
that finds one side without the other repairs it on read. Friends and devices stay out of `IAccountStore`, whose
contract is the sign-in's and is tested as such.

## The routes

All signed in (`401 Account.SignInRequired` otherwise), all behind the same-origin fence, all 404 with
`--accounts off`.

| Route | What |
| --- | --- |
| `GET /api/friends` | `{ friends: [{ id, username }], incoming: [{ id, username, since }], outgoing: [...] }`, names resolved at listing time as cards are. |
| `POST /api/friends/requests` `{ username }` | Asks. `404 Friend.NoSuchPlayer`, `409 Friend.AlreadyFriends` / `Friend.AlreadyAsked`, `400 Friend.Self`. Accepts instead when the other already asked. Sends *wants to be your friend*. |
| `POST /api/friends/requests/<accountId>/accept` | Sends *accepted your friend request*. |
| `DELETE /api/friends/requests/<accountId>` | Declines an incoming request, or withdraws an outgoing one. Silent. |
| `DELETE /api/friends/<accountId>` | Removes a friend. Cancels an invitation between the two. Silent. |
| `PUT /api/account/push` `{ endpoint, keys }` / `DELETE /api/account/push` | This browser is, or is no longer, one of the account's devices. `410` for an endpoint a push service said was gone. `POST /api/account/push/test` sends a test notice to this device. |
| `POST /api/lobby/tables` `{ opponent: "friend", friend: "<accountId>", format }` | Opens the table with the second seat addressed to the friend. `403 Friend.NotFriends`; `409 Lobby.InvitationOutstanding` while another one of yours is pending. Sends *invites you*. |
| `GET /api/lobby/mine` | Adds `invitations: [{ table, from: { id, username }, sentAt }]`, the tables waiting for you; each of your own tables gains `invited: { id, username, sentAt, declined }` while a seat waits or was declined. |
| `POST /api/lobby/tables/<id>/accept` | The invited account takes the seat: today's claim by account instead of by key. Answers the token, as claim does. Sends *accepted* to the inviter. |
| `POST /api/lobby/tables/<id>/decline` | Sends *declined* to the inviter. |
| `DELETE /api/lobby/tables/<id>/invitation` | Cancel, by the inviter. |
| `POST /api/lobby/tables/<id>/invitation` `{ friend }` | Invite another friend to the same seat, after a cancel or a decline. Same two refusals as opening. |
| `POST /api/lobby/tables/<id>/open` | Offer the seat to everyone instead: today's open place. |
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

A page on the lobby refreshes its lists when the service worker tells it a notice arrived, and on its usual
refresh otherwise.

## Stages

Each stage is one pull request: built, tested, formatted, the node tests when a page changes, docs updated
(`table/README.md`, `docs/tabletop/playtest-app.md`, `AGENTS.md`'s `table` line).

1. **An account has devices.** `PushDevice`, `IAccountDevices`, its two stores, `AccountNotices`, the push
   routes under `/api/account`, the lobby's *Notify me* and *Test*, `sw.js` opening the notice's own `url` and
   staying quiet when a window is focused on it. No notice but the test one is sent yet. Tests: five devices and
   the sixth replaces the oldest; the same endpoint registered twice is one device; a `410` forgets it; *Sign out
   everywhere* and deletion forget them all; the worker opens the lobby for a lobby notice and the game for a
   game notice.
2. **Friends.** The ADR. `Friends` and `IFriendStore`, the stores, `FriendsApi`, the panel, the two friend
   notices. Tests: a request made twice is one; asking back accepts; declining is silent; removing a friend is
   silent; a hundred and first is refused; one side written and the other missing is repaired on read; a deleted
   account leaves no request and no friendship behind, on either side.
3. **Invite a friend.** `InvitedAccount` and `DeclinedBy` on the seat and in `table.json`, the invitation routes,
   the one-at-a-time rule, expiry, the three ways out, closing an unbegun table, the cards, the three invitation
   notices. Tests: an invited seat never appears in `/api/lobby`'s tables nor opens to a join; only the invited
   account accepts, once, and the second device to ask is refused; a second invitation while one is pending is
   `409`; cancel, decline and expiry each leave the inviter's table with no invited account; a restart rebuilds
   a waiting invitation; removing the friend cancels it; the inviter is told once at acceptance; the
   `/api/seat/*` route still answers the invited seat nothing before it is taken (the hidden-information
   boundary of `playtest-app.md` §4 is asserted on the serialised bytes).
4. **What else would be missed.** *Left the table* and the end-of-match notices to each seat's account,
   *Rematch* on the outcome, and the account deletion cleaning up everything stage 2 and 3 added. Tests: a
   shared-device table sends nothing to its one account about itself (ADR 0120); a bot's seat is told nothing;
   a match whose two seats are one account gets one notice.

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
