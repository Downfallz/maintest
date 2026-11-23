# 0203. Send the notices from a queue, and "your turn" as a delayed message

Date: 2026-11-23
Status: Accepted. Amends [0118](0118-your-turn-reaches-a-phone-nobody-is-looking-at.md) ("your turn" waits on the queue, not in the process) and [0201](0201-send-every-account-notice-through-one-door-and-keep-the-news-in-the-bell.md) (the door puts the push on the queue)
Rulebook: none

## Context

The door (ADR 0201) wrote the bell's entry and then sent the push inside the request that caused it, one device
after another, ten seconds a device at worst. Some of those requests held the lobby's claim gate while they waited,
so every other claim waited too. "Your turn" (ADR 0118) was a delay in the API's process: a process that stopped
within the two seconds of grace lost the notice. The endpoints a push service answered 410 for were a dictionary in
the memory of the process that heard it, so a device forgotten by the worker was registered again by the API on the
next page load (`docs/tabletop/notifications-plan.md`, "Sending off the request").

## Decision

- **A queue, `notices`, beside the turns' (ADR 0159).** The door still writes the bell's entry in the request. The
  push goes on the queue: one item a device, naming the account and the endpoint, never the device's keys. The
  sender reads the keys with the account's devices when it sends, so a browser turned off or signed out since the
  item was queued is told nothing.
- **Sent by whichever process reads the queue.** The API and `table-worker` (ADR 0162) both run the notices' worker.
  An item whose push service is out comes back after 2 seconds, then twice as long each time, up to a minute. After
  five deliveries it goes on `notices-poison` with its error, and the dead-letter worker keeps it for good on
  `notices-dlq`. A 410 is not a failure: the device is forgotten and the item is done with. The worker wakes on
  either queue as it wakes on the turns'.
- **"Your turn" is a delayed message.** A question nobody drew goes on the queue, hidden for the grace. The send
  answers a receipt, and when the seat's page says it drew the question within the grace (`?shown=`), the host
  deletes that item. When the item comes due, the process that reads it picks the table up and sends the notice only
  if the match is not over, the seat is still on that asking, and no page here drew it. A process that stops in
  between loses nothing: the next one that reads the queue sends it.
- **The 410 list moves to the store.** A gone endpoint is kept in the account's devices document
  (`devices/<accountId>.json`) as an entry marked `gone`, without its keys, among the five latest. Whichever process
  heard it, `PUT /api/account/push` and a signed-in seat's `PUT /api/lobby/push` answer 410 for it. A seat with no
  account, on a host with accounts off, keeps ADR 0118's list in memory: its turn is the only notice it gets.
- **No notice under the claim gate.** The notices a lobby route gives are held until the route let go of the gate,
  then given before the request is answered.
- **Where no queue is, nothing changes.** A host whose bots' turns are taken in its own process (`--turns
  process`), or with no push keys, sends in the request as before. The hourly job sends in its own process: no
  request rides on it, and it runs to its end.

## Consequences

- Good: a request that notifies answers after one queue write a device, and the claim gate waits on no push service.
- Good: a notice outlives the replica that gave it, a deploy included, and a push service that is out for a minute
  is tried again rather than dropped.
- Good: a gone device stays gone across processes, so the worker and the API stop handing one another dead endpoints.
- Bad: queue operations, about six for each notice to a device, and one receive a second at most while a process
  is idle. At thousands of notices a day this is cents a month. No new service, no new role: the API and the worker
  already hold the queue role.
- Bad: when the API is down, a notice waits for the worker to wake on its queue, up to a minute.
- Bad: a "your turn" whose page draws the question on another replica than the one that queued it is not taken
  back. The API runs one replica at most (ADR 0080); the service worker still hides a notice over a page in front.
- Neutral: the payload is on the queue, the title and the line a phone shows, kept as long as a push service keeps a
  notice, ten minutes. No key and no seat token travels.

## Alternatives considered

- Azure Notification Hubs or Firebase: a service and a cost for what Web Push with the host's own keys already does.
- One item an account rather than a device: a retry would tell again the devices that already got it.
- Keep the shown mark of each seat in the table's files, for the worker to read: a write each asking on the seat
  read, for a check the receipt does without.
- The worker alone sends: a turn would wait on the worker's cold start, often half a minute, while the API is up.

## Follow-up

- `docs/tabletop/notifications-plan.md` (N5 built), `docs/architecture/stateless-table-inventory.md`,
  `infra/README.md` (the queues), `docs/tabletop/home-navigation.md` if it describes the send.
- N6: the worker's counts in the ops report and the admin's Jobs tab.
