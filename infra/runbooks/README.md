# Runbooks

The steps only the owner can do, written to be done from a phone: numbered, one block to paste each, and the way
through the portal. A session that needs one names its file; a step it had to dictate by hand belongs here.

| When | Runbook |
| --- | --- |
| Once, so a session can read production (ADR 0186) | [ops-identity.md](ops-identity.md) |
| Once, to be told when something is wrong (ADR 0186) | [alerts.md](alerts.md) |
| Once, so a workflow can write the promotion's films (ADR 0206) | [promo-identity.md](promo-identity.md) |
| An alert arrived | [alerts.md, "When one arrives"](alerts.md#when-one-arrives) |
| A deployment is red on *The table answers* | [alerts.md, "When one arrives"](alerts.md#when-one-arrives), as for `table-5xx` |
| A change needs a new role or provider | [roles.md](roles.md) |
| The table's own domain, its certificate, DNS and sign-ins (ADR 0175) | [custom-domain.md](custom-domain.md) |
| The table has a new address: the admin panel's redirect and the Google client's origins | [sign-ins.md](sign-ins.md) |

The first setup (the resource group, the deploying identity, the first deployment) is in
[../README.md, "Setting it up, once"](../README.md#setting-it-up-once).

Each runbook starts in [Azure Cloud Shell](https://shell.azure.com) (Bash), signed in as the subscription's owner,
in an up-to-date clone:

```bash
[ -d maintest ] || git clone https://github.com/Downfallz/maintest.git; cd maintest && git pull --ff-only
```
