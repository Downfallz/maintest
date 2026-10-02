# 0080. Host the table on Azure Container Apps, with the studio left on GitHub Pages

Date: 2026-09-27

Status: Accepted

## Context

Two hosts exist and both are built for one machine. The studio is on the loopback address by design
([ADR 0015](0015-content-studio.md)); its hosted form is a static page on GitHub Pages whose backend is the
GitHub API, one commit per save on `studio/content` ([ADR 0023](0023-a-hosted-studio-with-github-as-its-backend.md)),
and that ADR refused to expose the local studio API because it writes and deletes files and takes agent
paths from requests. The table is one process per match, bound to the loopback address or one LAN address
([ADR 0054](0054-a-table-for-two-people-at-one-screen.md)), with the match in memory and the session written
to `runs/playtest/<id>/`. It is hosted nowhere: a playtest is a laptop on a home network.

The owner wants the whole thing reachable from anywhere, on an Azure subscription they already have, at the
lowest cost that works. The batch work, tuning passes, weight searches and learning-loop turns, runs for two
to six hours at a time on GitHub Actions today, free.

## Decision

**The table runs as one container on Azure Container Apps, in the consumption plan, with its state in one
Storage account. The studio stays on GitHub Pages with GitHub as its backend. The batch work stays on GitHub
Actions.**

- One Container App in `canadacentral`, consumption plan, **0 to 1 replica**. The platform scales it to
  zero after minutes without a request, and it costs nothing then. One replica at most, because a seat blocks
  a thread while a person thinks (ADR 0054) and a second replica would hold a different match.
- The container is the CLI's `table` command bound to every interface (`--bind 0.0.0.0`). The host speaks
  plain HTTP inside the container, as it does today; the platform's ingress terminates TLS. The `Sec-Fetch-Site`
  guard and the seat tokens stay as they are.
- **A match lives in memory, as it does today.** A `Match` is an aggregate with a private constructor and a
  random source of its own; it has no memento, and writing one is a domain change this record does not make.
  A page that is open polls its seat, so a match being played keeps the replica awake; a match nobody has open
  is lost when the replica goes, as it is lost today when the laptop closes. What survives is the recording.
- One Storage account, Standard LRS, hot, and **Blob only**: it holds what `runs/playtest/` holds today, the
  session recordings and traces, written as they are played and read back by the viewer. Table Storage has
  no job in this design; the account can grow one (a session index) the day the table has sessions to list.
  The content, the weights and the models ship inside the image, because they are in git.
- The container reaches the Storage account with its managed identity and a data-plane role. No storage key
  exists anywhere.
- The platform's built-in authentication (Microsoft Entra ID, the owner's tenant) protects the pilot page and
  the creation of a session. A seat is still joined by its code, without an account, because the people who
  sit down at a table are not the people who have one.
- GitHub Actions deploys with an identity federated to Azure (OIDC): the workflow holds a client id, a tenant
  id and a subscription id, and no secret.
- The infrastructure is Bicep, in the repository, and a deployment is a workflow run.

## Consequences

- Good: the cost is the storage, cents a month, and the container's CPU-seconds while a match is being
  played. Idle, it is nothing.
- Good: nothing in the studio moves. Its hosted form works, is free, and was designed for exactly this.
- Good: the batch work keeps its free compute. On Azure, a five-hour tuning pass would be the largest line
  on the bill.
- Bad: the table has to become a multi-session host to be worth hosting: today it is one match per process,
  with its tokens in memory and its end tied to Ctrl+C. That is its own decision and its own record.
- Bad: a container scaled to zero takes some seconds to answer the first request. The first player waits.
- Bad: a match is only as durable as the replica. Two people who close their pages for an hour come back to
  no match. Persisting one is a memento of the aggregate and of its random source, and is left open here.
  *Settled by [ADR 0091](0091-a-table-survives-the-host-that-played-it.md): a table is rebuilt from its seed
  and its recorded decisions when the next replica starts.*
- Bad: `HttpHost` now accepts the wildcard address outside Windows. On Windows it still refuses it, for the
  reason ADR 0054 gave (a URL reservation), so a laptop playtest is unchanged.
- Neutral: the seat tokens travel over TLS now, which is better than the home-network HTTP they were designed
  for; the fence stays the token.
- Neutral: the region is `canadacentral`, the closest to the owner; nothing depends on it.

## Alternatives considered

- **App Service, Basic B1.** The owner's first suggestion. It is billed around the clock, about 13 USD a
  month, whether or not anyone plays, and the free tier sleeps and caps CPU at an hour a day, which the bots
  spend in a few matches. Container Apps at zero replicas is the same container for nothing when idle.
- **Host the studio API too.** ADR 0023 refused it, and the reasons hold: it writes files, and its run API
  takes agent paths. The hosted studio already exists and costs nothing.
- **Move the batch work to Azure.** It is free on Actions and would be the whole bill on Azure.
- **A static site plus serverless functions.** The table is a long-lived process with seats that block; it is
  not a set of short requests.

## Follow-up

- `HttpHost.Bindable` accepts `0.0.0.0` outside Windows; `HttpHost.AnyInterface`.
- A Blob adapter for `IArtifactWriter`, and a reader port for what `PlaytestRun.Artifacts()` reads back with
  `File.*` today, both tested against Azurite. The Azure SDK (`Azure.Storage.Blobs`, `Azure.Identity`) is
  referenced by Infrastructure and nothing inward of it, which the architecture tests already enforce.
- A multi-session table, with its own ADR: create a session, join a seat by code, tokens persisted, the pilot
  behind the platform's authentication.
- A `Dockerfile`, the Bicep under `infra/`, a `deploy.yml` workflow, and the one-time bootstrap the owner runs
  (an app registration with a federated credential, and three repository secrets).
