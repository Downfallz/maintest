# 0162. Run the table as an API app, a turn worker app and an hourly job, each seeing what the others write

Date: 2026-11-11

Status: Accepted. Follows [0159](0159-advance-a-table-by-submitted-decisions-over-its-journal.md) (phase 6);
amends [0080](0080-host-the-table-on-azure-container-apps.md), [0082](0082-deploy-from-github-to-azure-without-a-secret.md)
and [0161](0161-index-tables-in-azure-table-storage.md) for what runs and the roles each holds

## Context

Since phase 5 a table's bots answer through a queue a worker can take (`--turns queue`). The hosted table still
runs both workers in its own process, and two kinds of work still run on requests: the sweep, which closes a
table that never began after twenty hours, and the expiry of a seat kept for a friend after a day. ADR 0159 asks
for time-dependent work to leave the request path for a command a schedule runs. Moving either the worker or the
schedule out of the table's process makes it the first second writer of a table. Today a host that holds a table
in memory reads its journal before each step, but serves its seats from memory and writes its record from
memory, so another process's write would go unseen and then be written over.

## Decision

We will run three things from the one image:

- **The API app.** It runs `table` with `--turns queue` and no workers. It is the Container App that exists, 0 to
  1 replica.
- **The worker app.** It runs `table-worker`: the turn worker and the dead-letter worker of phase 5, over the
  same store, picking tables up as the API does. It is a second Container App, 0 to 1 replica, scaled on the
  length of `agent-turns`, with no ingress.
- **The tending job.** It runs `table-tend`, one pass of the sweep and of the invitations' expiry over the index
  and the records, and exits. It is a Container Apps Job on an hourly schedule. Tables it picks up take no bot
  turn.

Each process sees what the others write. A process that holds a table watches the table's journal end and its
record's revision. When the journal moves, the process reads the match again and tells the pages, which
answers question 9. When the record is written by somebody else, the process lets the table go, and the next
request picks it up again. A record write that only marks how a table ended is made over the stored record,
never from memory, so it cannot put back keys another process has changed. The app's identity gains the
Storage Queue Data Contributor role, the bootstrap's ABAC condition allows it, and the deployment updates all
three.

## Consequences

- Good: no time-dependent work and no bot thinking runs on a request, and the API's single replica does nothing
  but answer requests and stream.
- Good: the worker scales to zero with the queue: nothing runs while no table owes a bot turn.
- Good: two processes holding one table stay correct. The journal's append condition already keeps their lines
  apart, and the watch keeps each one's view of the table current.
- Bad: a page waits up to the watch interval, about a second, for a line the worker wrote, plus a cold start
  when the worker app was at zero.
- Bad: three things to deploy, and a role to grant by running the bootstrap again before the first deployment
  that carries them.
- Neutral: an invitation can expire up to an hour late, and a table that never began can close an hour late.

## Alternatives considered

- The job posts a tick on a queue, and the API does the work as the only writer: it needs the API awake, and it
  keeps the API as the one process that can hold a table, which is what ADR 0159 is leaving.
- A timer in the API's process: nothing runs while the app is at zero replicas.
- Session affinity, or one process for everything: it cannot scale the API past one replica, and a bot's turn
  would still take the API's processor.

## Follow-up

`infra/main.bicep` (the worker app, the job, the queue role), `infra/bootstrap.sh` (the condition),
`.github/workflows/deploy.yml`, `infra/README.md`, `docs/architecture/stateless-table-inventory.md` (phase 6 and
question 9), and `AGENTS.md` (the `table-worker` and `table-tend` commands).
