# 0206. Write the promotion's media to a private container, as an identity of their own

Date: 2026-11-23
Status: Accepted
Rulebook: none

## Context

A film of a shot is 5 to 20 MB, and the repository's pictures are 9.6 MB in all: the films, their sheets and the
generated pictures cannot be committed ([the promotion plan](../art/promotion-plan.md), question 3). The owner
chose a `promo` container in the table's storage account. That account holds the recordings, the index and the
queues, and only identities reach it: shared keys and public access are off (ADR 0160). A session has no Azure,
and the identities that exist either deploy (Contributor on the group) or read (ADR 0186).

## Decision

**The container.** `infra/main.bicep` adds a private `promo` container to the table's account. The processes of
the table hold no role on it that they did not hold before, and nothing of the table reads it.

**Who writes it.** A fourth registration, `downfall-table-promo`, made by `infra/bootstrap.sh setup`, trusts the
repository's `promo` environment alone, which only `main` may run in. It holds Storage Blob Data Contributor on
the `promo` container and nothing else: no role on the group, the account, the recordings or the queues. The
template assigns it from the `AZURE_PROMO_PRINCIPAL_ID` variable, a role the deploying identity may already assign.
`.github/workflows/promo-film.yml`, started by hand, films a shot of `promo/shots/` and writes the film and its
sheet as that identity.

**Who reads it.** The operators of `TABLE_OPERATORS` hold the same role on the container alone, to open and tidy
the files in the portal; the owner posts by hand (question 4).

**What the repository keeps.** Each file is written once, named by its SHA-256 under its shot
(`<shot>/<sha256>.mp4`), and `promo/media.json` records it: the shot, the ratio, the file, the hash, the size and the
blob. A file is what its hash says, whoever wrote it.

## Consequences

- Good: a workflow that films cannot touch a player's recording, and a leaked token of it writes promotion files
  and nothing else, for a run's lifetime.
- Good: no secret: the workflow signs in by federation, as the deploy and the report do.
- Bad: the owner runs the setup again, sets three variables and deploys once before the first upload
  (`infra/runbooks/promo-identity.md`).
- Bad: a film is shot on a runner, so its bytes are that runner's: the repository records the hash of what was
  written, not of what a session's machine would draw.
- Neutral: storage costs cents a month at these sizes; reads and writes are a handful a week.

## Alternatives considered

- Commit the films with Git LFS: the repository's quota and bandwidth are small, and the files are not code.
- Give the deploying identity the upload: it may change the whole group; a filming job does not need that.
- A shared access signature in a secret: shared keys are off on the account (ADR 0160), and a SAS outlives its run.
- A second storage account: one more resource to watch for a few files, and the owner chose the table's.

## Follow-up

- `infra/README.md` names the variables; `infra/runbooks/promo-identity.md` the owner's steps.
- `promo/README.md` holds the record's format; `docs/reference/commands.md` the commands.
