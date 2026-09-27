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

- One Container App in `canadacentral`, consumption plan, **0 to 1 replica**: it scales to zero when nobody
  is playing, and costs nothing then. One replica at most, because a seat blocks a thread while a person
  thinks (ADR 0054) and a second replica would hold a different match.
- The container is the CLI's `table` command bound to every interface (`--bind 0.0.0.0`). The host speaks
  plain HTTP inside the container, as it does today; the platform's ingress terminates TLS. The `Sec-Fetch-Site`
  guard and the seat tokens stay as they are.
- One Storage account, Standard LRS, hot: **Blob** holds what `runs/playtest/` holds today, the session
  recordings and traces; **Table** holds what the process holds in memory today, the match state, the
  sessions, the join codes and the seat tokens, so a replica that scales to zero or restarts loses no match.
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
- Blob and Table adapters for `IArtifactWriter` and `IMatchRepository`, tested against Azurite.
- A multi-session table, with its own ADR: create a session, join a seat by code, tokens persisted, the pilot
  behind the platform's authentication.
- A `Dockerfile`, the Bicep under `infra/`, a `deploy.yml` workflow, and the one-time bootstrap the owner runs
  (an app registration with a federated credential, and three repository secrets).
