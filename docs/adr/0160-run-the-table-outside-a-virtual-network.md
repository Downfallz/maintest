# 0160. Run the hosted table outside a virtual network

Date: 2026-11-10

Status: Accepted

## Context

[ADR 0082](0082-deploy-from-github-to-azure-without-a-secret.md) put two fences around the storage account:
shared-key access off, and a network rule admitting only the app's subnet. It said both cost nothing. The
second does not: a Container Apps environment placed in a virtual network of ours brings a standard load
balancer and a public address into the subscription (the `ME_...` resource group), billed every hour whether
or not a replica runs. The owner's bill shows 30 to 40 dollars a month for the load balancer, on a host
whose every other line is cents ([ADR 0080](0080-host-the-table-on-azure-container-apps.md)).

## Decision

We will run the environment on the platform's own network and open the storage account's network. The
account keeps the fence that names who may read: shared-key access stays off, public blob access stays off,
and only an identity holding a role on the account reaches a blob, which is the app's managed identity. This
amends ADR 0082's storage point; everything else in it stands, and still nothing holds a secret.

## Consequences

- Good: the load balancer and the public address are gone, and the cost is again what ADR 0080 said.
- Good: reading the recordings from elsewhere takes a role and no network rule.
- Bad: one fence instead of two. The account's endpoint answers the internet, and a mistaken role
  assignment or a stolen Entra token is no longer stopped by the network.
- Bad: moving is not a deployment. An environment cannot leave its network, so the environment and the app
  are deleted and made again (`infra/README.md`), and the table's address changes: the sign-in's redirect
  URI and the Google client's origin are set again, links already sent stop working, and what a browser kept
  under the old address (a saved seat, a notice subscription) does not follow. Recordings, accounts and open
  tables stay in the storage account ([ADR 0091](0091-a-table-survives-the-host-that-played-it.md)).
- Neutral: the app's outbound address is no longer ours, which nothing depended on.

## Alternatives considered

- **Keep the virtual network.** Two fences, for more than the rest of the host costs together, around
  recordings of playtest matches.
- **A private endpoint on the account.** It needs the same virtual network, so the same load balancer, plus
  the endpoint's own fee.
- **Network rules by address.** The platform's outbound addresses are shared and not ours to list, and
  address rules do not apply to requests from the account's own region.

## Follow-up

- `infra/main.bicep`: no virtual network, no `vnetConfiguration`, `networkAcls.defaultAction: 'Allow'`.
- `infra/README.md`: the resource table, the download, and the one-time move.
- `docs/architecture/stateless-table-inventory.md`: the note on Functions and the account's network.
