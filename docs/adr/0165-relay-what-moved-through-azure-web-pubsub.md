# 0165. Relay what moved through Azure Web PubSub

Date: 2026-11-11

Status: Accepted. Takes the step [0164](0164-read-on-what-the-host-says-changed-never-on-a-clock.md) left to its own ADR;
amends [0119](0119-a-seat-page-is-told-when-its-table-changed.md) (a WebSocket, with a service's token) and
[0162](0162-run-the-table-as-an-api-a-worker-and-an-hourly-job.md) (no process looks at its tables every second);
answers the third step of question 9 of [the stateless table inventory](../architecture/stateless-table-inventory.md)

## Context

Since ADR 0164 no page reads on a clock, but two things still tie the pages to one process. A page's stream is
held by the API replica it reached, counted in that replica's memory: a second replica would not tell it what the
first one moved, and an open page keeps the API awake. And the API learns what the worker and the hourly job wrote
by reading the journal end and the record of every table it holds, once a second (ADR 0162): the last poll.

## Decision

- **The relay is Azure Web PubSub**, one hub, `downfall`, reached by each process's own identity: local auth is
  off, so no access key exists (ADR 0160), and each identity holds Web PubSub Service Owner on it alone.
- **Every process says what moved** (`IChangeRelay`): a table's change, from wherever it is bumped (the API, the
  worker, the job), goes to the group `table-<id>` as `changed` and to the group `hosts` as `<origin>|<id>`; the
  public lists to the group `lobby`; an account's own lists to that account as the user. The messages are words,
  never state: a page reads its own routes, and what it may read is still the API's to decide (ADR 0119).
- **Sends are coalesced by key**: one on its way and one more for whatever came meanwhile, so a bot's turn costs a
  table two messages, not ten. A send that fails is a console line; the next move sends again.
- **A page connects to the service, not to the API.** `GET /api/seat/{seat}/negotiate` (under the seat's token) and
  `GET /api/lobby/negotiate` answer an address the API signs for an hour: the table's group only, or the lobby's
  group as the signed-in account. The page opens a WebSocket to it; its opening is `ready`, each message `changed`,
  exactly what the stream said, and every rule of ADR 0164 holds on it (read on `ready`, reconnect when it closes).
  The token in that address is the service's, short-lived and good for one group, not the seat's.
- **A page falls back to the API's own stream** when the host has no relay (404), or when the service refuses the
  connection before it opens (its connections are all taken): for the rest of that page's life.
- **A process hears the hosts' group** (`RelayListener`) instead of looking at its tables every second: a table
  another process moved is looked at once, and each connection that opens is a look at every table held, for what
  moved while it was down. Without a relay the one-second look stays (ADR 0162).
- **Opted into**: `DOWNFALL_RELAY` names the endpoint for the three processes; the deployment adds the service when
  the repository variable `TABLE_RELAY_SKU` says `Free_F1` or `Standard_S1`, after the bootstrap has granted the role.

## Consequences

- Good: no process and no page reads on a clock. An open page keeps no replica awake, and the API can run more
  than one replica as far as being told what moved goes.
- Good: the worker's turn reaches the page as it is taken, not within the second.
- Bad: Free is 20 connections and 20 000 messages a day, enough for a few tables; a page past the twentieth falls
  back to the API's stream. Standard is 1 000 connections and a million messages a day a unit, about $1.61 a unit a
  day by the preview-era price, to check in the pricing calculator before choosing it.
- Bad: one more service and one more package (`Azure.Messaging.WebPubSub`), and a relay that is out is pages that
  are not told: they still read on `ready`, on coming back and on every tap, and fall back to the stream at their
  next reconnect only if the service refuses them.
- Neutral: other things still bind a table to one process (the claims gate, the practice tables; the inventory's
  other questions): this decision does not raise the API's replica count.

## Alternatives considered

- Azure SignalR Service: the same service family, built around a client library and its own protocol, where a word
  per change is all a page needs.
- Event Grid, Service Bus, Redis: Event Grid pushes to an address and replicas have none of their own, Service Bus
  holds no browser connection, Redis does not scale to zero (inventory, question 9).
- Keep the API's stream and have replicas tell each other through a queue: an open page would still keep a replica
  awake, and a queue read by a replica is a poll.
