# 0082. Deploy the table from GitHub to Azure without a secret

Date: 2026-09-27

Status: Accepted

## Context

[ADR 0080](0080-host-the-table-on-azure-container-apps.md) put the table on Azure Container Apps, reached by a
workflow federated to Azure and with no secret in the repository, and left open where the image lives and
how the platform's sign-in is registered. [ADR 0081](0081-a-host-of-tables-found-by-their-tokens.md) made the
platform's sign-in the operator's door. Each of the three obvious answers to those questions brings a
credential with it: a private registry needs a pull secret or a paid registry with an identity, a sign-in
registration is usually given a client secret, and a deploying identity is usually a service principal with
a password.

## Decision

**Nothing that deploys or runs the table holds a secret.**

- **The image lives on GitHub's container registry, public.** The repository is public, so the image holds
  nothing that is not already published. The workflow pushes it with its own run token; the container app
  pulls it anonymously. Azure Container Registry, at about five dollars a month, would be the largest line of
  a bill that is otherwise cents.
- **The image is tagged with the commit**, never `latest`, and the same twelve characters are stamped into the
  engine version every recording carries.
- **The workflow reaches Azure through an identity federated to the repository's `azure` environment.** It
  may change one resource group, and may assign exactly one role there, the one the template assigns to the
  app (a conditioned Role Based Access Control Administrator). Its ids are repository variables, not secrets:
  an id grants nothing.
- **The sign-in registration has no client secret.** The platform signs the operator in with the implicit
  flow, which needs ID tokens issued, and its authorization policy admits only the object ids listed as
  operators. Anonymous requests still pass, because a player joins by a code.
- **The storage account has shared-key access off, and refuses every network but the app's.** The app
  reaches it with its managed identity, so no storage key exists to leak; and the Container Apps environment
  runs in a virtual network whose one subnet is the only place the account accepts a request from, through a
  service endpoint. Either fence would hold alone. Both cost nothing.
- **The infrastructure is one Bicep template** in `infra/`, compiled and linted on every run, and the one-time
  setup is one idempotent script the owner runs.

## Consequences

- Good: there is nothing to rotate, and nothing in the repository, the workflow or the image that grants
  anything if it is read.
- Good: the cost is what ADR 0080 said: the storage, the log workspace under its daily cap, and the CPU
  seconds while a page is open.
- Bad: a new package on GitHub's registry is private whatever its repository is. Making it public is one
  click, once; the deploy job checks an anonymous pull first and says so in a sentence when it fails.
- Bad: the sign-in's redirect URI names the app's address, which exists only after the first deployment.
  That is a second, one-line bootstrap step.
- Bad: the implicit flow puts an ID token in a browser redirect. It is the flow the platform uses without a
  secret, and the token admits the operator to a lobby, nothing more.
- Neutral: anyone can pull the image and run a table of their own, as anyone can build one from the repository.
- Neutral: reading the recordings from anywhere but the app means adding one's own address to the account for
  the occasion; `infra/README.md` says how.

## Alternatives considered

- **Azure Container Registry with the app's identity pulling.** No secret either, and five dollars a month for
  an image that is public anyway.
- **A client secret on the sign-in registration.** The authorization-code flow, and a secret to store in the
  container app and rotate every year or two.
- **A deploying service principal with a password in repository secrets.** The credential federation exists
  to remove.

## Follow-up

- `Dockerfile`, `.dockerignore`, `infra/main.bicep`, `infra/bootstrap.sh`, `infra/README.md`.
- `.github/workflows/deploy.yml` and `.github/scripts/try-table-image.sh`, which runs the image both ways
  before it is pushed.
- `AGENTS.md`: the repository map and the commands.
