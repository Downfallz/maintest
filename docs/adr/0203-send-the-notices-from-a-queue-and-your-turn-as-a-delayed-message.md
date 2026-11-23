# 0203. Send the notices from a queue, and "your turn" as a delayed message

Date: 2026-11-23
Status: Accepted. Amends [0118](0118-your-turn-reaches-a-phone-nobody-is-looking-at.md) ("your turn" waits on the queue, not in the process) and [0201](0201-send-every-account-notice-through-one-door-and-keep-the-news-in-the-bell.md) (the door puts the push on the queue)
Rulebook: none

## Context

The door (ADR 0201) wrote the bell's entry and then sent the push inside the request that caused it, one device
after another, ten seconds a device at worst. Some of those requests held the lobby's claim gate while they waited,
so every other claim waited too. "Your turn" (ADR 0118) was a delay in the API's process: a process that stopped
within the grace, two seconds, lost the notice. The endpoints a push service had answered 410 for (gone) were kept in
a dictionary in the memory of the process that heard it, so a device the worker had forgotten was registered again
by the API on the next page load (`docs/tabletop/notifications-plan.md`, "Sending off the request").

## Decision

- **A queue, `notices`, beside the turns' (ADR 0159).** The door still writes the bell's entry in the request. The
  push goes on the queue: one item a device, naming the account and the endpoint, never the device's keys. The
  sender reads the keys with the account's devices when it sends, so a browser turned off or signed out since the
  item was queued is told nothing.
- **Sent by whichever process reads the queue.** The API and `table-worker` (ADR 0162) both run the notices' worker.
  An item whose push service is out comes back after 2 seconds, then twice as long each time, up to a minute. After
  five deliveries it goes on `notices-poison` with its error, and the dead-letter worker keeps it on `notices-dlq`. What is set aside keeps the account and the kind of notice, never the endpoint nor the words, and
  is kept a week. A push service that does not answer in time is a failure like any other. The items of one look are
  sent side by side, so a slow endpoint holds only its own. A 410 is not a failure: the device is forgotten and the
  item is finished. The worker wakes on `notices` and `notices-poison` as it wakes on the turns' queues.
- **"Your turn" is a delayed message, for a signed-in seat.** When a seat that belongs to an account is asked a
  question, the host puts a message on the queue, hidden for the grace, and keeps the receipt the queue returns. If
  the seat's page reports within the grace that it drew the question (`?shown=`), the host deletes that message. When
  it comes due, the process that reads it loads the table from its record and tells the account's devices only if the
  match is not over, the seat is still on that asking, and no page there drew it. A process that stops in between
  loses nothing: the next one that reads the queue sends it. A seat without an account keeps ADR 0118's wait in the
  API's process, where the page that subscribed it is answered 410.
- **The 410 list moves to the store.** A gone endpoint is kept in the account's devices document
  (`devices/<accountId>.json`) as an entry marked `gone`, without its keys, among the five latest. Whichever process
  heard it, `PUT /api/account/push` and a signed-in seat's `PUT /api/lobby/push` answer 410 for it. An entry without
  keys reads as gone, so an older image that drops the flag leaves no device that cannot be told. Nothing is written
  for an endpoint the account no longer holds. A seat with no account keeps ADR 0118's list in the API's memory.
- **No notice under the claim gate.** The notices a lobby route gives are held until the route let go of the gate,
  then given before the request is answered. So are those of the work that takes the gate outside a route: the
  release of a deleted or suspended account's seats (`LobbyApi.ReleaseAsync`) and an operator's change under
  `LobbyApi.ExclusivelyAsync`, a void's notices among them (#1133). A notice that cannot go is a line, never a 500.
  A queue that is out sends the push in the request instead.
- **Only the browsers' push services.** A subscription names Firebase, Mozilla's, Windows' or Apple's push service,
  or it is refused with a 400 (`Account.UnknownPushService`, `Lobby.UnknownPushService`), and one send gives up after
  five seconds: a server a player controls holds neither the worker's time nor makes the host call it (#1132).
- **Where no queue is, nothing changes.** A host whose bots' turns are taken in its own process (`--turns
  process`), or with no push keys, sends in the request as before. The hourly job sends in its own process: no
  request rides on it, and it runs to its end.

## Consequences

- Good: a request that notifies answers after one queue write a device, and the claim gate waits on no push service.
- Good: a notice outlives the replica that gave it, a deploy included, and a push service that is out for a minute
  is tried again rather than dropped.
- Good: a gone device stays gone across processes, so the worker and the API stop handing one another dead endpoints.
- Bad: about six queue operations for each notice to a device, and at most one receive a second while a process is
  idle. At thousands of notices a day this is cents a month. No new service, no new role: the API and the worker
  already hold the queue role.
- Bad: a seat without an account is told its turn as before, and loses it as before when the API stops within the
  grace.
- Bad: when the API is down, a notice waits for the worker to wake on its queue, up to a minute.
- Bad: a "your turn" whose page draws the question on another replica than the one that queued it is not taken
  back. The API runs one replica at most (ADR 0080); the service worker still hides a notice over a page in front.
- Neutral: the payload, the title and the line a phone shows, is on the queue. It is kept for ten minutes, as long as
  a push service keeps a notice. No key and no seat token travels.
- Neutral: the way back is a revert. Items left on the three queues expire unread. To the previous image, a `gone`
  entry reads as a device without keys, which fails once and is forgotten.

## Alternatives considered

- Azure Notification Hubs or Firebase: a service and a cost for what Web Push with the host's own keys already does.
- One item an account rather than a device: a retry would tell again the devices that already got it.
- Keep the shown mark of each seat in the table's files, for the worker to read: a write each asking on the seat
  read, for a check the receipt does without.
- The worker alone sends: a turn would wait on the worker's cold start, often half a minute, while the API is up.

## Follow-up

- `docs/tabletop/notifications-plan.md` (N5 built), `docs/architecture/stateless-table-inventory.md`,
  `infra/README.md` (the queues), `docs/tabletop/home-navigation.md` if it describes the send.
- N6 (built): the worker's counts in the ops report and the admin's Jobs tab.
