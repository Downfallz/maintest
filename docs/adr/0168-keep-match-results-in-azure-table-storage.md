# 0168. Keep match results in Azure Table Storage, by match and by account

Date: 2026-11-11

Status: Accepted. Amends [0122](0122-a-player-signs-in-with-google-and-owns-their-seats.md) (stage 5)

## Context

Accounts, their preferences, devices and friends are small documents read by key, and blobs serve them well:
a name and a sign-in stay unique because their entries are created only when absent. Match results are not read
that way. Every reader lists all of them: one blob download per finished match, for each request. The
leaderboard caches the list for 30 seconds, but nothing else does. The finished games, the recent opponents and
the check that a player sat at a replay all read every player's history to keep one player's. Player and
creature statistics, which come next, would read the same history again, in more ways.

## Decision

We will keep results in Azure Table Storage: a table `matchresults` in the account that already holds the
index of the tables (ADR 0161). Each result is written as entities whose row key sorts by when the match
finished, then by its id:

- one entity in the partition `match`, which the leaderboard reads whole, in order;
- one entity per account that held a seat, in that account's own partition, which a player's pages read alone.

Each entity holds the result as one JSON property, in the shape the blobs used. A write is an upsert, so a
match recorded twice is kept once, as before. The first process to open the table copies the result blobs into
it, then marks the copy done. The blobs stay where they are. A directory keeps its files, as an index on a
laptop does.

## Consequences

- Good: a player's finished games, recent opponents and replay check read one partition. The leaderboard
  reads one partition in pages of a thousand, not one blob per match.
- Good: no new resource and no new role. The processes that record results already hold the table role on the
  account (ADR 0161, ADR 0162).
- Good: per-player and per-creature statistics can be written the same way, keyed by account or by creature,
  when a match closes.
- Bad: a result is two to three writes in different partitions, which Table Storage cannot make atomic. A
  process that stops between them leaves the result in some lists and not others until the match is recorded
  again. The closing claim (ADR 0162) makes that rare, and an upsert makes the retry safe.
- Bad: the copy reads every blob once, at the first start after the deploy.
- Neutral: the result blobs stay where they are until someone deletes them.

## Alternatives considered

- Keep blobs and cache the list in each process: every replica still downloads the whole history once per
  expiry, and the statistics would each need their own cache.
- One blob per account listing its matches: a second index to keep in step by hand, with an ETag on every write.
  That is what Table Storage gives by partition.
- A database (SQL, Cosmos DB): queries we do not need yet, and the first resource here that does not scale to
  zero.

## Follow-up

- `IMatchResults.ListForAsync`; `StorageMatchResults` in Infrastructure; `ArtifactStores.OpenResultsAsync`.
- `infra/README.md` (what the storage account holds), `docs/tabletop/accounts-plan.md` (stage 5),
  `docs/adr/README.md`.
