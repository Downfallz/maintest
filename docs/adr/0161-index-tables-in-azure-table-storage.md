# 0161. Index the tables in Azure Table Storage, for the codes and the lobby

Date: 2026-11-10

Status: Accepted. Follows [0159](0159-advance-a-table-by-submitted-decisions-over-its-journal.md) (phase 4.3b);
amends [0080](0080-host-the-table-on-azure-container-apps.md) and [0082](0082-deploy-from-github-to-azure-without-a-secret.md)
for the role the app holds on the storage account

## Context

ADR 0159 turns the host's registry into a cache: a host has to serve a table it does not hold. A seat's key
names its table (`<table>.<secret>`, phase 4.3a), but two lookups have nothing to read off the request. A join
code (`/j/ABCD1234`) is typed by a person and too short to carry a table's id, and the lobby lists the open
tables, eight at a time and newest first, which today is a walk over the registry. Over blobs, the first is a
document per code and the second is a listing of the runs and a read of every open table's record per page:
a cost that grows with the store.

## Decision

We will keep an index of the tables in Azure Table Storage, in the storage account that already holds them. An
entity per join code names its table and seat; an entity per open table, in one partition and keyed so the
newest sorts first, carries the summary the lobby shows. Entities are written on their entity tag, as the
journal and the record are on theirs, and an entry the index lost is written again from the record it
summarizes: the record and the journal stay the table, the index only finds it. A port owned by Application
has two adapters in Infrastructure: Table Storage for the hosted table, files for a host on a directory. The
app's identity gets the Storage Table Data Contributor role beside the blob one, and nothing holds a key.

## Consequences

- Good: a typed code and a page of the lobby are one query each, whatever the store holds.
- Good: no new resource and nothing that does not scale to zero; the same account, the same identity, cents.
- Bad: a second kind of storage to emulate and to grant. CI starts Azurite's table service beside its blob
  service, and the bootstrap of an existing deployment has to grant the new role before the first deploy.
- Bad: two writes where there was one. A record and its index entry can disagree for a moment; the record
  wins, and whatever reads the index checks the record before it trusts it.
- Neutral: queries are efficient on the partition and row keys only, so the keys are designed for these two
  lookups and no others.

## Alternatives considered

- A blob per code, and the lobby read off the records: no new service, but every page of the lobby reads every
  open table's record.
- Scan the open records for a code: nothing to keep in step, and a read per open table per typed code.
- A database (Cosmos DB, SQL): queries of any shape, and a resource that does not scale to zero, which ADR
  0159 set aside.

## Follow-up

- `Directory.Packages.props`: `Azure.Data.Tables`.
- `infra/main.bicep`: the Storage Table Data Contributor role for the app's identity; `infra/README.md`.
- `.github/workflows/ci.yml`: Azurite's table service.
- `docs/architecture/stateless-table-inventory.md`: 4.3b, question 4.
