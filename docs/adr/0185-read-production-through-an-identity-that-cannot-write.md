# 0185. Read production through an identity that cannot write

Date: 2026-11-15
Status: Accepted

## Context

A session cannot see the hosted table: it has no Azure and no portal. The owner reads the portal aloud from a
phone, and a fault (a revision that restarts, a tending job that fails, a turn the worker gave up on, a bill that
grows) is found by a player or not at all (docs/engineering/ai-workflow-plan.md, W7). The one identity GitHub
signs in with is the deployer's, which may change the resource group and assign roles (ADR 0082). The
repository is public, so a workflow's log is public too.

## Decision

A second app registration, `<prefix>-table-ops`, made by `infra/bootstrap.sh setup`, is federated to this
repository's `ops` environment alone and holds Reader, Log Analytics Reader and Cost Management Reader on the
resource group, and nothing else: it can read no blob, table or queue, and write nothing. `ops-report.yml` signs in
with it every morning, and on demand, and prints a report to its log: the table's answer to `GET /api/lobby`, the
revisions, the restarts and 5xx answers of the last 24 hours, the tending job's runs, the messages waiting in the
queues, the turns given up on, the console's error lines and the month's cost against the budget. It prints counts
and masked lines, never a record: an error line loses its emails, ids and long tokens and is cut to 160
characters. `/ops` runs it and reads the log. `rollout.yml` asks the deployed table for `GET /api/lobby` once the
deployment is up, and a deployment that does not answer is red. `main.bicep` adds an action group (an email and
the Azure mobile app, to the address in the `TABLE_ALERT_EMAIL` secret) and the alerts it sends: 5xx answers,
restarts of the API and of the worker, a turn given up on, a failed tending pass, and a budget of 5 a month in
the billing currency (80 % and 100 % spent, 100 % forecast). Without the secret none of these is deployed.

## Consequences

- Good: a session reads production with a door that cannot write, and the owner relays nothing.
- Good: the owner hears of a fault by email and on the phone, without opening the portal.
- Good: no probe from outside keeps the app awake: the API scales to zero, the alerts read what it already reports,
  and the report's one request a day costs one cold start.
- Bad: one more registration and one more `bootstrap.sh setup` run for the owner, and an alert that never fires
  is not proven to work until it does.
- Bad: the report is public. The month's cost and the counts are readable by anyone; the masking is a filter, not a
  guarantee, and a player's name in an error line would pass it.
- Neutral: the two log alerts cost about $0.50 a month each at list price, the metric alerts about $0.10 each.

## Alternatives considered

- Sign the report in as the deployer: no step for the owner, but a daily job holding the right to write and to
  assign roles.
- An availability test from Application Insights: a request every few minutes keeps a replica running, which
  costs more than the whole table does today.
- Write the report to the run's summary: a session can read a job's log and cannot read a summary.

## Follow-up

`infra/bootstrap.sh`, `infra/main.bicep`, `infra/README.md` and `infra/runbooks/`, `.github/workflows/rollout.yml`,
`ops-report.yml`, `scripts/ops/`, `studio/ops.test.js`, the `/ops` skill, `docs/reference/commands.md`.
