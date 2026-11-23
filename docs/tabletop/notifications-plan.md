# Plan: notifications, the bell that keeps everything and the push a player chooses

Status: built, 2026-11-23 (the owner settled the questions at the end). N1 to N6 are built; each stage below was
one pull request.

## What the owner asked for

Are the notifications optimal, in infrastructure and in architecture? The bell should always hold the
notifications, and the switch should only turn off the push on the phone. Today it feels as if the switch turns
off every notification. Put the push in the onboarding too: on Android, a message that invites the player to turn
the pop-ups on, with a choice like "don't ask again", and the switch stays in the menu for a player who changes
their mind. Clean, best practice, able to grow if the game gets big, cheap to start.

## What exists today

| Concern | What the code and the docs held before N1 |
| --- | --- |
| The bell | `table/inbox.js`, `docs/tabletop/home-navigation.md#the-bell`. It lists what **waits**: a turn against a person, an invitation, a friend request, a declined invitation. It is computed from `GET /api/lobby/mine` and `GET /api/friends`. The host stores no notification. No switch turns it off. |
| What the bell never shows | What happened and calls for nothing: a friend accepted, an invited friend accepted or left, a match over or voided, a turn about to time out. These exist only as a push. A player without push never learns of them: this is what reads as "everything is off". |
| Push to an account | `AccountNotices`, `AccountDevices` (five devices an account, `devices/<accountId>.json`), `PUT`/`DELETE /api/account/push`. Seven send sites: `FriendsApi` (2), `LobbyApi` (1), `TableComposer` (3), `TurnTimeouts` (1). |
| Push to a seat | "Your turn" (ADR 0118, 0120): `PushNotices`, to the subscription the seat's page registered (`PUT /api/lobby/push`), two seconds after an asking no page drew. It does not go to the account's other devices (`docs/tabletop/friends-plan.md`, "not in this plan"). |
| The switches | Two, in two places. Settings, Notifications: one button, *Notify me on this device* (`table/devices.js`). Match settings, *Turn notifications*: one button a seat (`table/battle-services.js`). Stopping the first keeps the second, and the reverse. No choice by kind of notice. |
| The offer | Only on the classic `/play` page: *Get told when it's your turn*, **Not now** kept in the browser (`downfall.table.notices-offer`, `table/table.js`). The battle view (`/battle`), where every seat plays since ADR 0157, offers nothing. The lobby and the tutorials (`docs/tabletop/onboarding-plan.md`) offer nothing. |
| Install | `table/manifest.webmanifest` is served. No page handles `beforeinstallprompt`. An iPhone is told, in the button's hint, that a push needs the home screen. |
| Sending | `WebPushSender` (`Lib.Net.Http.WebPush`, VAPID keys in `DOWNFALL_PUSH_KEYS`), straight to the browser's push service. An account's notice is awaited inside the request that caused it, device after device, ten seconds a device at worst. "Your turn" is a delay in the API's process (`docs/architecture/stateless-table-inventory.md`): a process that stops loses it. |
| Dead subscriptions | Forgotten from the store on a 404 or 410. The endpoints answered 410 afterwards are a dictionary in memory, lost on a restart. |
| Processes | The API runs zero to one replica (`infra/main.bicep`, ADR 0080). The worker (`TableWorker`) and the tending job (`TableTend`) send account notices too, when a match ends in their hands. `AccountDevices` writes under a lock of its own process, which three processes do not share. |
| Cost | A push costs nothing: the browsers' push services are free, and no Azure service sits between. The devices are a few bytes of blob an account. |
| Measures | None. A send that fails is one console line. Nobody can say how many notices left, arrived or died. Not measured here for that reason. |

The answer to the first question: the transport is right and stays. Web Push with the host's own keys is the
standard, is free at any size, and needs no notification service. Three things are not right: the bell holds no
history, so push is the only carrier of half the notices; the switches are two and say nothing of the bell; the
send rides on the request and on the process's memory.

## The shape

### One door, two channels

Every notice to an account goes through one door, `AccountNotifier.NotifyAsync(account, notice)`. A notice is a
kind, a title, a body, where a tap goes, and a tag. The door does two things, in this order:

1. **The bell, always.** A notice that is news is written to the account's notice log.
2. **Push, when the player chose it.** The notice is sent to the account's devices if the account's preferences
   keep that kind.

The seven send sites call the door instead of `AccountNotices.SendAsync`. No site decides a channel.

| Kind | What it says | Bell | Push |
| --- | --- | --- | --- |
| `turn` | Your turn; your turn is about to time out | Waiting (as today) | yes |
| `invitation` | A friend invites you | Waiting (as today) | yes |
| `friend-request` | A player wants to be your friend | Waiting (as today) | yes |
| `declined` | A friend declined your invitation | Waiting (as today) | yes |
| `friend-accepted` | A player accepted your request | News | yes |
| `seat-taken` | Your invited friend accepted | News | yes |
| `left` | The other player left the table | News | yes |
| `match-over` | Won, lost, a draw, voided | News | yes |

A reaction still notifies nobody (ADR 0173).

### The notice log

`AccountNotices` keeps, an account, the latest news: `notices/<accountId>.json`, each entry an id, the kind, the
title, the body, the URL, the tag, when, and whether it was read. A notice with the tag of an entry replaces it.
The log keeps the 100 latest entries of the last 90 days. It goes with the account, and *Delete account* forgets it.

- `GET /api/account/notices`: the entries, newest first, and how many are unread.
- `POST /api/account/notices/read`: marks them read, up to an id.

The bell's window keeps *Your turn*, *Invitations*, *Friend requests* and *Declined*, then gains **News**. The
badge counts what waits plus the unread news. Opening the window marks the news read. The player's stream
(ADR 0164) already says "your lists changed"; a new entry says so too, so the bell reads on no clock.

Three processes write an account's log: the API, the worker and the tending job. A lock inside one of them
protects nothing. So the log is written on a condition from its first day: read with the blob's ETag, written
only if it has not changed, read again and tried again on a conflict. The devices' list
(`devices/<accountId>.json`) gets the same write in N1, since a 410 from the worker already removes a device
there.

The port (`INoticeStore`) is the Application's, beside `IDeviceStore`; the adapters (memory, file, blob) are the
Infrastructure's. `Settings`, *Privacy* and the showcase's privacy page gain one sentence on what is kept.

### One switch that says what it does

Settings, Notifications, for a signed-in player:

- A first line, always there: *The bell keeps everything. These choices are about this device's pop-ups only.*
- **Pop-ups on this device**: the switch of today, a checkbox now, with **Send a test**.
- **What pops up**, for the account: *Your turn*, *Invitations and friends*, *Results*. Kept with the
  account's preferences (`/api/account/preferences`).

"Your turn" goes to the account's devices when the seat's player is signed in. The match settings'
*Turn notifications* then shows the same switch and says where the rest is. A seat without an account, on a host
with accounts off, keeps ADR 0118's subscription as it is.

### The offer

One part, `table/push-offer.js`, shared by the lobby and the battle view. It is a card of the page, never the
browser's own prompt: the browser's prompt opens only after a tap on **Turn on**. A refusal in the browser's
prompt is final for the site, so the card comes first, at a moment the player has a reason to say yes.

- **Turn on**: asks the browser, registers the device, says *Done*.
- **Not now**: closes; asked again later.
- **Don't ask again**: never offered again in this browser. Settings, Notifications, still has the switch.

The choice is the browser's, kept beside its other choices (`localStorage`), because the permission is the
browser's too. The words follow the platform: on Android, *Turn on pop-ups to be told when it is
your turn, even with the screen off*; on an iPhone outside the home screen, the steps to add the app first; in a
browser that cannot push, nothing. On Android the card can also offer **Install** when the browser raised
`beforeinstallprompt`. The journey is briefed first (`/ux-pass`, `docs/art/journeys/`).

### Sending off the request

The door writes the bell's entry in the request, then puts the push on a storage queue, `notices`. The worker
app (ADR 0162) sends it, tries again with the queue's own back-off, and gives up to a dead-letter queue. "Your
turn" becomes a message visible after the grace, checked against what the seat's page acknowledged, so a stopped
process loses nothing. The 410 list moves to the store. A local host with no queue sends in process, as today.

What this costs: queue operations, cents a month at thousands of notices a day. What it does not need, at any
size: Azure Notification Hubs, Firebase or a native app.

### Seeing it work

The worker counts, a kind, what was sent, refused and gone. The ops report (`/ops`, ADR 0186) and the admin's
Jobs tab show the counts and the queue's length.

## Stages

1. **N1, the door and the log.** ADR: every account notice goes through one door, the bell keeps the news,
   push is a channel a player chooses (amends ADR 0118). `AccountNotifier`, `INoticeStore` and its three
   adapters, the two routes, the seven sites moved. The log and the devices are written on the blob's ETag.
   Tests: Application tests on the log (the cut, the tag that replaces, read, two writers that both keep their
   entry), Cli tests on the routes and on each site writing its entry with push off, the storage test on
   Azurite.

   *Built (2026-11-22)*: ADR 0201. `AccountNotifier` (Cli) is the door: news to the account's `AccountNoticeLog`
   (Application, port `INoticeStore`; memory, file and blob adapters beside the devices), then the push through
   `AccountNotices`, which stays the push channel and keeps its name (the log is `AccountNoticeLog`, not
   `AccountNotices` as the shape above says). The seven sites call the door with a `NoticeKind`; each kind keeps
   the push topic it had. The API, the worker and the hourly job (its lobby too, for a suspended player's seat)
   build the door whether or not they hold push keys, so the log is kept with push off; the hourly job's turn
   warning still goes only when push is on, because a turn is not news. Keeping an entry also sends "your lists
   changed" on the account's stream. An id is greater than both the newest entry's id and the current time in
   milliseconds, so a page's "read up to" never covers a later entry. The door checks that the account exists
   before and after the write, so a deleted account's log does not come back. The devices' list lost its process
   lock: `IDeviceStore` reads a version and saves over it, and `AccountDevices` reads again on a conflict, in both
   adapters (`ConditionalBlob`, `ConditionalFile`, shared with the notice stores). Left to N2: drawing the news,
   the privacy sentences (an entry keeps the other player's name for up to 90 days), the Settings line on what is
   kept, and a mark-read that tells the player's other tabs. Left to N5: the log's write leaves the lobby's claim
   gate. Left to #1109: a test that each process wires the door. A device registered while its account signs out
   everywhere is forgotten again (the route reads the session after the write), a fix that came after #1108
   merged.

2. **N2, the bell shows the news.** `table/inbox.js` gains News and the unread count; the window marks read;
   the privacy sentences. Tests: `inbox.test.js`, `terminology.test.js`, the lobby's browser spec with push off.

   *Built (2026-11-22)*: the bell's window lists **News** below what waits, newest first, each with its line, how long
   ago and **Open →** (a link inside the site only), read from `GET /api/account/notices` beside the lobby's and the
   friends' reads; a host that answers 404 keeps no notices and is not asked again. The badge counts what waits and
   the unread news; the bell's name says both (`Notifications · 1 waiting · 2 new`). Opening the window marks the
   news read up to the newest entry; what it showed stays drawn *New* until it closes, and a refused read is not
   retried until the next opening. `POST /api/account/notices/read` now tells the account's other pages through the
   lobby's stream when it marked any. The privacy sentences: Settings, Privacy, and the showcase's policy (English and
   French), on what is kept, for how long, and the other player's name kept as it was. Left to N3: the Settings
   line *The bell keeps everything…* belongs with the redrawn Notifications tab. Left to N4: the push offer.
   `terminology.test.js` reads the new words (*News*, *New*, *Open →*) unchanged. An entry older than 90 days is
   hidden, not erased at rest, until the account's next write: the privacy sentences say so, and a sweep is an
   issue. The test that the API wires the read's stream joins #1109.
3. **N3, one switch.** The account's notice preferences, "your turn" to the account's devices, Settings,
   Notifications redrawn, the match settings' section pointing to it. ADR (amends ADR 0118
   and 0120: a device holding both seats still gets nothing). Tests: Cli tests on a kind turned off (the bell
   has it, the device does not), `devices.test.js`, `battle-services.test.js`.

   *Built (2026-11-22)*: ADR 0202. The account's choices are `NoticePreferences` (turns, invitations and friends,
   results; every kind until chosen), a third document beside its settings (`preferences/<id>.notices.json`, port
   `IPreferenceStore`), read under `notices` by `GET /api/account/preferences` and saved by
   `PUT /api/account/preferences/notices`. The door reads them before each push, in the API, the worker and the hourly
   job; a kind turned off is still kept in the bell, and a choice that cannot be read lets the push go. A seat that
   belongs to an account is told its turn through the door on every device of the account (`turn:<table>`, which the
   timeout warning replaces), not on the seat's own subscription; no device is told when the other seat is the same
   account's or the device is the other seat's subscription. A signed-in seat's `PUT /api/lobby/push` registers the
   subscription as one of its account's devices too, and its `DELETE` forgets it there, so the match settings' switch
   and Settings' are one switch a device. Settings, Notifications: the line on the bell, **Pop-ups on this device** (a
   checkbox; off now lets go of the browser's subscription, and a browser turned off under the old switch lets go of
   its subscription once on its next visit), **Send a test**, and **What pops up**. The match settings say the switch
   is the device's and where the rest is chosen. A seat's subscription is told only while the account holds it
   (Codex's review: joining it at send time would bring back a browser signed out); a subscription either sender heard is gone is answered 410 to the seat page; a
   replaced one leaves the account; signing out on a browser forgets it as a device. Turning *Your turn* off also
   silences the timeout warning. Left to N4: the offer. Left to #1109: a test that each process wires the choices
   and the account's turn (`push.Accounts`, `Lobby.Devices`, `Preferences`), as for the door.
4. **N4, the offer.** The journey's brief, `table/push-offer.js`, the card in the lobby and in the battle view,
   the three answers, the words by platform, Install on Android. The classic page's own offer is removed.
   Tests: `push-offer.test.js` (when it shows, each answer, each platform), the browser specs (`table.push.js`).

   *Built (2026-11-23)*: the brief is `docs/art/journeys/lobby.md`, *The pop-ups offer*. `table/push-offer.js` is
   the card, added by the page to its body and styled once in `identity.css` (`.push-offer`, a card of the kit with
   its cut corner). It stands at the foot of the lobby and at the top of the battle view, so the dock and lesson 1's
   end card stay free. It shows the eyebrow *POP-UPS*, the title *Away from the game?* and one line by platform:
   Android in the owner's words, a desktop browser, an iPhone at the home screen, and an iPhone outside it, which
   gets the steps to add the app and no Turn on. Its answers are **Turn on**, **Not now**, **Don't ask again**, and
   **Install the app** on Android once the browser raised `beforeinstallprompt`.

   The page's own switch does the asking. In the lobby it is Settings, Notifications (`devices.js`, which now tells
   the page where it stands). In the battle view it is the match settings' switch (`battle-services.js`, through the
   seats, which N3 made the account's devices too). The end of a tutorial is asked through the account's switch,
   because a practice seat answers no push route (the host reviewer's finding); a practice seat now shows no switch,
   and a seat the host refuses no longer lets go of a subscription the browser already held. The lobby offers when a
   game against a person waits on the other player (`waitsOnAPerson`); the battle view on the other player's move
   against a person and at lesson 1's end (`battleMoment`). The card goes, unanswered and unkept, when the moment
   passes, and the page does not ask again.

   The answers are the browser's (`downfall.push-offer`). Not now counts as an asking, and so does a Turn on that
   leaves pop-ups off. Askings are a week apart at least, three at most. Don't ask again, or pop-ups turned on, ends
   the offer for good. After Turn on the card says Done, stays off or could not, and where the switch is, with a
   Close; no clock. The classic page's offer, and its `offerShown`, are removed.

   Left: the card is not on the journey's walk, because its host has no push keys and headless Chromium refuses the
   permission; the spec shows it with a browser never asked. A host with accounts off makes no offer in the lobby,
   whose switch is the account's; the battle view still does. The brief's **Left** holds the owner's choices on the
   card over the lobby's featured space at 320 px.

5. **N5, the queue.** ADR: notices are sent by the worker from a queue; "your turn" is a delayed message.
   `infra/main.bicep` gains the queue. Tests: the worker's tests on a retry, a 410 and a give-up, on Azurite;
   the `infra-reviewer`.

   *Built (2026-11-23)*: ADR 0203. The queue is `notices`, beside the turns', with `notices-poison` and `notices-dlq`.
   The door writes the bell's entry in the request and puts the push on the queue, one item a device, with no keys.
   `NoticeWorker` sends it, in the API and in `table-worker`: retried with a back-off, given up on at five deliveries,
   and kept a week as a dead letter with the account and the kind only. A 410 forgets the device, and the item is
   finished.

   A signed-in seat's "your turn" is a delayed message. The host deletes it when the page draws the question in time,
   and the process that reads it when it comes due tells the account's devices only if the seat is still on that
   asking. A seat without an account keeps ADR 0118's wait in the API.

   The 410 list moved to the account's devices document, a `gone` entry without keys. The lobby's notices leave the
   claim gate (left by N1). `main.bicep` wakes the worker on `notices` and `notices-poison`, and the give-up alert
   hears a notice too. No new role. Without a queue, and in the hourly job, notices are sent in process, as before.

   Tests: `NoticeWorkerContract` in memory and on Azurite (a retry, a timeout, a 410, a give-up kept without its
   words, a device let go of), the queued turn in `PushNoticesTests`, the door with a queue that is out, the gone
   list and its stores, the claim gate, and the notices worker the host starts.

   Left: a "your turn" drawn on another replica than the one that queued it is not taken back (the API runs one at
   most). An operator's release of seats and a void still notify under the claim gate (#1133). Any https endpoint is
   still accepted (#1132). The cross-process cases have no test yet (#1134), nor the hosts' wiring (#1109). The
   counts are N6.

6. **N6, the counts.** The worker's counts in the ops report and in the admin's Jobs tab. Tests: Cli tests on
   the counts, `ops.test.js`, `admin.test.js`.

   *Built (2026-11-23)*: `NoticeWorker` counts each look's pushes for each kind (the push topic: `turn`,
   `invitation`, `friend`, `match`): **sent**, **refused** (every try a push service refused or did not answer, so a
   notice retried twice counts two, and one given up on counts five; a store or a queue that fails around a send is
   not a refusal) and **gone** (a device its push service dropped, or that the account no longer holds). After the
   look it adds them to the day's document through `NoticeCounts` (Application, port `INoticeCountStore`; memory,
   file and blob adapters in `Infrastructure/Work`), written over the version read and read again on a conflict,
   since the API and `table-worker` both count; the first count of a day lets go of the days past seven, and a
   clean-up that fails is a line, not lost counts. The store is `notice-counts/` beside the recordings (a directory
   or a container; no new role, the processes' roles are the account's); a host that records nothing counts in
   memory. A count that cannot be kept is a console line, never a notice sent again. `GET /api/admin/jobs/notices`
   answers the three queues' lengths and the counts, today (UTC) and the seven days; the Jobs tab draws them in a
   section *Notices* (`table/admin-notices.js`), marked when a notice waits given up on. The worker's lines now start
   `Notice sent (kind)`, `Notice refused (kind)` and `Notice gone (kind)`; `scripts/ops/read.sh` counts them in Log
   Analytics over 24 hours, and the report prints `Notifications 24 h : … envoyées, … refusées (dont … abandonnées),
   … disparues`; a notice given up on, a push or a "your turn", is a concern, counted by the words the alert hears.

   Left: the ops report reads the storage account's queue length as a whole, since Azure's metrics do not split it by
   queue; the Jobs tab has each queue's. The notices sent in the request (no queue, or the hourly job) are not counted,
   as the stage asked for the worker's counts. A notice to a seat without an account (ADR 0118) is not counted either.

## Questions the owner settled (2026-11-22)

1. **What the bell keeps.** The 100 latest news of the last 90 days.
2. **What the player chooses.** One switch a device, and three kinds for the account: turns, invitations and
   friends, results. The bell keeps everything whatever the choices.
3. **"Your turn" reaches every device of the account**, for a signed-in player. N3 writes the ADR.
4. **When the offer appears.** When a player's first game against a person waits on the other player, and at the
   end of the first tutorial.
5. **After "Not now".** Asked again at the next game against a person, a week later at the earliest, three times
   at most. **Don't ask again** ends it at once.
6. **Who sees the offer.** Every browser that can push, in its platform's words; an iPhone outside the home
   screen gets the steps to install.
7. **The queue.** N5 and N6 are built last in this plan.
