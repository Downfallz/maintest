# The alerts

The table tells you, by email and on the Azure mobile app, when it answers 5xx, when the API or the worker
restarts, when the worker gives up on a bot's turn, when a tending pass fails, and when the month's
spending reaches 80 % or 100 % of a budget of 5 (billing currency), or is forecast to pass it (ADR 0186). No
alert stops anything. Without the address, none of it is deployed.

## Turning them on, once

1. **The providers and the role.** Run the setup of [ops-identity.md](ops-identity.md), step 2, first: it
   registers `Microsoft.Insights` and `Microsoft.Consumption`, and lets the deployer give the log alerts Log
   Analytics Reader on the workspace. Without it the deployment of the alerts fails.

2. **The address**, as a secret, so the log never shows it. GitHub: the repository, *Settings*, *Secrets and
   variables*, *Actions*, *Secrets*, *New repository secret*, name `TABLE_ALERT_EMAIL`, value your address.

   ```text
   TABLE_ALERT_EMAIL
   ```

3. **The budget's month**, set once and never moved: Azure takes a past start only within its own month. GitHub:
   *Variables*, beside *Secrets*, *New repository variable*, name `TABLE_BUDGET_START`, value the first day of the
   current month (`2026-11-01` in November 2026). Unset, the alerts are deployed without the budget, for a
   subscription that takes none. A budget made again later (the resource group recreated, the budget deleted)
   needs the variable moved to that month first, or the deployment fails.

   ```text
   TABLE_BUDGET_START
   ```

4. **The phone.** Install *Microsoft Azure* from the App Store or Google Play, sign in with the same address,
   and allow its notifications.

5. **Deploy.** GitHub: *Actions*, *Deploy the table*, *Run workflow*, on `main` (or wait for the next merge).

6. **Check.** Azure sends an email, "You've been added to an Azure Monitor action group". To see one arrive: Azure
   portal, *Monitor*, *Alerts*, *Action groups*, `downfall-alerts`, *Test*, *Email* and *Azure app push*.

A deployment that goes red right after step 2 is the providers: step 1 was skipped. Run it, then re-run the
deployment.

To turn them off: delete the `TABLE_ALERT_EMAIL` secret and deploy. A deployment does not delete what it no
longer declares: Azure portal, the `downfall-table` resource group, select the alert rules, `downfall-alerts`
and, under *Cost Management*, *Budgets*, `downfall-monthly`, and delete them.

## When one arrives

Ask a session `/ops` first: the report says what the last 24 hours held, error lines included.

| Alert | What it usually is | Where to look |
| --- | --- | --- |
| `downfall-table-5xx` | A route that throws, or a store the API cannot reach | Azure portal, *Container Apps*, `downfall-table`, *Monitoring*, *Log stream*; the report's error lines |
| `downfall-table-restarts`, `downfall-worker-restarts` | A revision that cannot start: a missing role or setting, often right after a deployment | The same app, *Revisions and replicas*, the active revision's *Running status*; then [roles.md](roles.md) |
| `downfall-turn-given-up` | A bot's turn the worker failed five times; the table waits for it | The admin panel's *Overview*, which counts the poison and dead-letter queues |
| `downfall-tend-failed` | The hourly pass could not reach the store | The admin panel's *Jobs* tab; *Container Apps jobs*, `downfall-tend`, *Execution history* |
| `downfall-monthly` (budget) | A replica that stays up, or the Web PubSub relay on the Standard tier | Azure portal, *Cost Management*, *Cost analysis*, scope `downfall-table`, grouped by resource |

When *The table answers* is red, the new revision is still serving: read it as `table-5xx`. The way back is *Deploy the table* run on the last good commit.
