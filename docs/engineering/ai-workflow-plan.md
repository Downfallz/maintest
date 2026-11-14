# Plan: the AI workflow, from an idea to a merged pull request

Status: proposal, 2026-11-14. Nothing here is built, and the owner has settled none of the questions at the end.
Each stage below is one pull request, or one settings change that only the owner can make.

## What the owner asked for

A plan of recommendations to make analysis, planning, design and coding go better with what Claude Code and
Cowork offer today: instruction files, skills, agents, and whatever else helps an agent understand the project
and its environment; agents that propose features and operations work; reviewers of every kind, of language as
well as of code. Grounded in the past sessions and in the last 300 pull requests.

## What was read

- The last 300 pull requests, #284 to #584 (2026-10-05 to 2026-11-14): the history of `main` and each one's dates.
- 20 of the 25 cloud sessions held on this repository, each at its tail: 169 turns, 47 of them started by the owner.
- The kit as it stands: `AGENTS.md`, `CLAUDE.md`, `.claude/`, `.github/`, the two cloud environments.
- The documentation of Claude Code and Cowork as it reads on 2026-11-14. A preview or a beta is marked as one.

Not measured: the pass rate and the duration of CI on `main`, and a sample of review threads. Both readings
stopped at the account's usage limit, which this plan's own reading reached. Stage W0 carries the script that
measures them, and every stage that adds agents or scheduled runs spends from the same limit.

## What exists today

| Concern | Measured | What it says |
| --- | --- | --- |
| Pace | 300 pull requests in six weeks, 191 of them since 2 November; up to 22 a day; median 385 changed lines over 12 files | The owner directs and releases. Agents write. |
| Authors | Of the last 330 commits on `main`, 298 carry a Claude session trailer, 17 are Dependabot's, 15 carry none (art and battle work) | One main author, and a second agent beside it. |
| Open to merge | Median 56 minutes; 70 of 287 merged in under 15 | Faster than the browser suite, which sessions report at 30 to 45 minutes. |
| Where the work is | `table/` in 166 pull requests, `studio/` with its browser specs 142, `src/DownfallArena.Cli` 125, `docs/tabletop` 103, `battle/` 49 and `arena/` before it 30, `learning/` 44, `infra/` 19, the domain 15 | The hosted app, not the engine. |
| The kit | `.claude/` changed in 4 of the 300. Rules for the domain, the application layer, tests and docs. 5 of the 8 subagents serve the tabletop translation | Tuned for September's work. |
| `AGENTS.md` | 43 KB, loaded into every session and every subagent. 68% of it is Commands. One line is 15,127 characters. Changed in 101 of the 300 | A changelog and an API reference inside the contract. By a text match, 23 of the 41 routes it names are not in `table/README.md`. |
| Files several branches edit | `AGENTS.md` 101, `docs/adr/README.md` 86, `TableFiles.cs` 62, `docs/learning/journal.md` 54, `table/README.md` 54 | Sessions conflicted with each other in 5 of the 8 latest. An ADR number was already taken four times (#297, #512, #516, #569). |
| Session size | 4 of the 8 latest above 700,000 tokens, 7 of the 11 before them at 470,000 or more. A fresh session starts near 81,000 | 10 of 19 were doing work their title does not describe. |
| What a long session costs | About $3,400 at list price over those 19; $681 and $593 for the two largest. $4 to $10 to answer one line after an idle gap. 72 of 121 notification turns had nothing to do, at $0.13 to $0.82 each | The whole context is read again on every request. After one compaction, from 909,000 to 100,000 tokens, a 600-word answer cost less than an idle wake-up had. |
| What the owner types again | "Ouvre pr", "abonne toi", "Codex", "Conflit resolve": in 5, 5, 4 and 3 of the 8 latest | Standing orders, given by hand each time. |
| Merge | Before the checks finished, or on a red one: in 5 of the 8 latest sessions, and in 4 to 6 of the 11 before | The owner merges from a phone, and nothing on GitHub refuses. `deploy.yml` deploys `main` without the browser suite. |
| CI | `ci.yml` runs on pull requests and on `main`. The browser suite runs on pull requests only, behind path filters. Neither is required | A browser test red on `main` is found by the next pull request: seen in 5 sessions. |
| Local gate | About 3 minutes in a session. 97 of 3,183 tests skipped for want of a storage emulator; 4 with Azurite running | A session reports a gate that leaves the storage adapters out. |
| Review | Codex, asked with `@codex review`, in 9 of 11 sessions. Every finding a session examined was accepted and fixed. The repository's own subagents ran once in the windows read | The second opinion works. The first, inside the session, is not taken. |
| Out of a session's reach | Sonar's findings (`sonarcloud.io` blocked), Bicep, re-running a job, GitHub variables, Azure, Cloudflare, a real phone | The owner relays, by hand, from a phone. |
| Environment | This session runs on `Default`: no host allowed, no setup script. A `maintest` environment allows `sonarcloud.io` and is not the one sessions start on | The right environment exists and is not used. |
| Loose ends | They end a session's last message: a flaky d20 test, two Rematch specs red in the sandbox, stale reference screenshots, three sessions waiting on an answer | Nothing tracks them. Whether the repository uses issues was not read. |
| Scheduled work | None that recurs. Sessions arm their own one-off re-checks | |
| Account | No plugin, no skill of its own, no connector connected | |

## What the reading shows

1. **The contract became a log.** `AGENTS.md` grows with every feature because the only description of a route
   or a flag is a comment in its Commands block. It is the file two sessions most often edit at once, and
   every session and subagent pays for all of it.
2. **The kit points at September.** The rules and reviewers cover the engine, which 15 of 300 pull requests
   touched. Nothing guides a change to a route, a page, the battle view, the infrastructure or a workflow.
3. **A session outlives its stage, and its context is the cost.** A stage that costs $14 at 800,000 tokens is
   the same work at a fraction in a fresh session. Idle wake-ups and cold caches are most of the rest.
4. **The owner gives the same four orders.** Opening the pull request, subscribing, working through Codex and
   resolving a conflict are asked for one word at a time, from a phone.
5. **Merging runs ahead of checking, and production follows `main`.** The suite is slower than the owner's
   patience, so the merge comes first. Nothing refuses it, and the deploy does not wait either.
6. **A session cannot see what it is asked to judge.** Sonar, the storage tests, Bicep, the deployed app and a
   real phone are all out of reach, so "not verified" ends most reports.
7. **Parallel sessions meet in the same files.** Indexes edited by hand, a sequential ADR number and one long
   line guarantee a conflict whenever two stages land in the same hour.
8. **Loose ends live in chat.** What a session leaves open is written once, in its last message.

## The shape

```text
idea, from a phone -> /plan -> the plan's pull request -> the owner settles its questions
  -> one session per stage: /stage -> gate -> /review -> /ship -> Codex -> green -> merge -> /wrap-up
  -> every morning: what merged, what is red, what waits for the owner
```

Five rules hold it together.

1. What must always happen is enforced by a machine: a GitHub rule, a hook, a test, a generated file. An
   instruction is read by two tools and thins out in a long session.
2. The contract is small. Everything else loads with the files it is about.
3. One stage, one session.
4. A loose end is an issue.
5. Production is read through a door that cannot write.

## The kit

### Standing orders

A block in `CLAUDE.md`, in the owner's name, because a session will not open a pull request unasked. Proposed
text, for the owner to accept or cut:

```markdown
## Standing orders

- The gate passed: open the pull request from the template, subscribe to it, and ask Codex for its review
  (`@codex review`) if it has not started. Do not ask first.
- Answer every review thread: fix it, or say why not. Merge `main` in when the branch conflicts.
- The owner merges. Never report a pull request ready while a check runs or a thread is open.
- One stage, one session. Past 300,000 tokens, or once the stage is merged, write the wrap-up and stop.
- Every pull request targets `main`. New work starts on a new branch from `origin/main`.
- Answer the owner in French, after a notification too. An event that needs nothing gets no answer.
- The owner reads on a phone. Steps for the owner are numbered, one block to paste each, with the way
  through the portal.
```

### Instruction files

- `AGENTS.md` becomes an index: what the project is, one line per directory, the eight commands of the gate,
  the architecture rules, the conventions, the definition of done. Under 200 lines and 12 KB, no line over
  300 characters. A test holds the budget.
- Each area gets its own `AGENTS.md`, with a one-line `CLAUDE.md` beside it that imports it, as the root
  does: `table/`, `battle/`, `studio/`, `learning/`, `infra/`, `src/DownfallArena.Cli/`. It holds that area's
  commands, the tests a change there must run, and its traps. Claude Code loads a directory's `CLAUDE.md`
  when it first touches a file there; other tools read the nearest `AGENTS.md`.
- The long Commands block moves to `docs/reference/commands.md`, one heading per command. The routes move to
  `table/README.md`, which already holds half of them.
- `docs/README.md` lists every plan. A test checks that each `docs/**/*plan*.md` is in it.

### Rules

Path-scoped rules stay for what spans directories with no common parent. One home per fact.

| Rule | Loads on | Says |
| --- | --- | --- |
| `host.md` | `src/DownfallArena.Cli/Table/**`, `tests/DownfallArena.Cli.Tests/Table/**` | Who may call a route (seat, account, operator). The journal is written before the answer. More than one process serves a table. A route has a host test and a line in `table/README.md`. |
| `pages.md` | `table/**`, `studio/**`, `viewer/**` | No dependency (ADR 0024). `identity.css` last (ADR 0158). 320 px first. The words of `player-terminology.md`. A new file joins the route table and its test. |
| `infra.md` | `infra/**`, `Dockerfile`, `.github/workflows/deploy.yml` | No secret in a log. What a change costs. A step only the owner can do is written as a runbook step. |
| `learning.md` | `learning/**`, `docs/learning/**`, `models/**`, `benchmarks/**` | Three seeds, the spread, never the best seed (ADR 0049). The paired reading. The shape of a journal entry. |
| `workflows.md` | `.github/**` | Least permission. The required check always reports. |

### Skills

| Skill | Does | Why |
| --- | --- | --- |
| `/plan <idea>` | Writes a plan in the shape of `docs/tabletop/admin-plan.md` and asks its open questions as choices to tap | 11 plans in 300 pull requests, each shaped by hand. |
| `/stage <plan> <id>` | Makes a fresh session productive in one line: reads that stage and the ADRs it names, branches from `origin/main`, tests first | Sessions are reused because a new one needs a long prompt. |
| `/ship` | The standing orders as steps. Reports ready only on green, with no open thread | "Ouvre pr", "abonne toi", "Codex". |
| `/review [pr]` | Picks the reviewers from the paths touched, checks each finding, sorts them: fix now, issue, dismissed | The subagents exist and are not called. |
| `/wrap-up` | Loose ends become issues. The stage is marked built in its plan. Prints the next stage's one-line prompt | Loose ends live in chat. |
| `/browser-check` | Maps the changed paths to their specs and runs those in the background | 21 spec names listed in `AGENTS.md`; a 30-minute suite. |
| `/ops` | Runs the `ops-report` workflow and reads it (W7) | The owner reads the portal aloud. |
| `/proposals` | Product and operations proposals, as issues (W8) | Asked for. |
| `/retro` | A week's numbers and the findings that recur, as a pull request on the rules and skills | ADR 0005: instructions are "fixed when an agent misbehaves". |
| `/verify`, changed | Starts the storage emulator. Runs what the diff calls for | 97 tests skipped; one gate for every diff. |
| `/adr`, changed | Fetches `main` before it takes a number | Four numbers already taken. |

### Agents

| Agent | Reads a change for |
| --- | --- |
| `host-reviewer` | Who may call a route, status and error codes, idempotence, the journal, two processes on one table, the queues. |
| `pages-reviewer` | The static pages and the battle view: 320 px, touch, accessibility, the service worker, the identity. |
| `security-reviewer` | Tokens, sign-in, sessions, operator routes, rate limits, secrets in logs, headers, Web Push. |
| `infra-reviewer` | Bicep, workflows, the image: least privilege, cost, the way back, what becomes a hand step. |
| `stats-reviewer` | A learning claim against ADR 0049 and the paired reading. |
| `copy-editor` | The English of docs and pages against `.claude/rules/docs.md` and `player-terminology.md`; French against its FR column. |
| `archivist` | "What is decided about X?", answered with ADR numbers on a small model, so a session reads three ADRs and not 180. |
| `product-scout`, `ops-analyst` | Proposals (W8). |

`domain-reviewer`, `code-reviewer`, `test-engineer` and the five tabletop agents stay. Three of them begin by
reading `AGENTS.md`, which is already loaded: that line goes. Each agent names its `model` and a `maxTurns`.
Two reviewers try `memory: project`, which keeps what an agent learns under `.claude/agent-memory/`, in the
repository. It is documented; whether it holds in a cloud session is the trial. For a risky change (sign-in,
storage), `/code-review ultra` is a cloud review, a research preview: three free runs, then about $5 to $25 each.

### Hooks and settings

- `SessionStart` also starts Azurite and exports its three endpoints, so the storage tests run.
- A `Stop` hook says so, once, when code changed after the last gate.
- `permissions.allow` gains the commands of the gate that are missing (`npm --prefix battle`, `node --test`,
  `uv run --project learning`, the browser suite). `mcp__github__merge_pull_request` is denied if the owner
  keeps the merge (question 1).

### Cloud environment

The owner approves each of these on a card; a session proposes them.

- One environment for this repository, `maintest`, chosen when a session starts.
- A setup script: the .NET SDK from the distribution, Azurite and the Bicep CLI, then `npm ci` for `battle/`
  and the browser suite and `uv sync` for `learning/`. Each took seconds here (Azurite 17), well inside the
  five minutes after which a setup is no longer kept as a snapshot.
- Hosts: `sonarcloud.io` is there. `play.downfallinstinct.com` is added, to read the deployed app. A read-only
  Sonar token is saved as a network secret, which a session uses and never sees.

### GitHub

Settings only the owner can change.

- A ruleset on `main`: a pull request, the `gate` check, every conversation resolved. Auto-merge allowed, so
  a tap on Merge before the checks finish queues the merge instead of making it.
- Head branches deleted on merge. 334 remain today; the merged ones are pruned once.
- Issues carry the loose ends: `follow-up`, `flaky`, `ops`, `proposal`.
- Secret scanning and push protection.

### CI

- One job, `gate`, always reports and needs the others, so it can be required. Path filters move inside jobs.
- The browser suite also runs on `main`, and `deploy` waits for it. On a pull request it runs the projects and
  specs the diff calls for; the whole matrix runs nightly. One retry, with a report of what flaked.
- A red `main` opens an issue, so the next session starts from it.

### Scheduled work

Cloud scheduled tasks are a research preview. Each run is a whole session on the account's limits, hourly at
most, started by a clock, an API call or a GitHub event. One is added at a time.

| When | What | Ends as |
| --- | --- | --- |
| Every morning | What merged. What is red on `main` and in the deploy. Pull requests waiting for the owner. Sessions waiting on an answer. Open follow-ups | A push, in French, one screen long. |
| Weekly | Dependabot and vulnerable packages. Indexes and budgets. `/retro` | A pull request, or issues. |
| Monthly | `/proposals` | Issues. |
| A pull request with no Claude trailer opens | `/review`, with comments | Review threads. |

### Sessions

- `/stage` starts one; `/wrap-up` ends it. A session whose stage is merged is archived, not reused.
- For a night's work, `/goal <condition>` keeps a session going until the condition holds.
- Claude Code projects, a public beta on personal plans, hold one conversation over parallel cloud threads.
  A plan with stages fits it; to try on one plan.
- Dynamic workflows (`ultracode`) fan one audit out to many agents. They suit a sweep, a rename or a security
  pass over every route, and they spend accordingly.

### Operations

- `ops-report.yml`, on demand and daily, signs in with a role that can only read. It prints the revisions, the
  restarts, the tending job's runs, the queue lengths, the last errors and the month's cost. It prints to the
  log: a session can read a job's log and cannot read a run's summary.
- `deploy.yml` tries the deployed app once it is up (`GET /api/lobby`) and prints its address in the log.
- Alerts in `main.bicep`: availability, restarts, dead letters, a failed tending run, a budget.
- `infra/runbooks/` keeps the steps sessions dictated by hand: the origin certificate, the redirect URI, the
  OAuth origins, DNS, a role assignment. Each is written for a phone.
- At a desk, Claude in Chrome acts in the owner's own signed-in browser and can do a portal step under the
  owner's eyes.

### Proposals

- `product-scout` reads the plans' open gates (the new-player and physical gates, the French layer that
  waits), the loose-end issues, the recorded sessions, and what `ops-report` shows of use. It writes at most
  five proposals: the evidence, a sketch, the size, the risk.
- `ops-analyst` reads `infra/`, the workflows, `ops-report` and the runbooks, and proposes what to automate
  first. Three seeds from this reading: a preview revision per pull request, so a change is seen on a real
  phone before the merge; Cloudflare's settings as code; the static route table of `TableFiles.cs` derived at
  build, which needs an ADR of its own.

### Language

- `copy-editor` reads every pull request that changes `docs/**`, a page's text or the rulebook.
- A test holds player-facing strings to the Player-facing EN column of `player-terminology.md`.
- French: the FR column exists and every page says `lang="en"`. A locale is a feature for `/plan`.

### Design

- A design system read from `table/identity.css` and `docs/art/branding.md` (Claude Design reads a repository
  with `/design-sync`), so a mock-up wears the real identity.
- A new screen is mocked first, as a page the owner opens on the phone, then sent to a session.
- A pull request that changes a page carries screenshots at 320, 390 and 1440 px; the Playwright projects
  exist. Visual baselines for a few screens are an option: the suite has none.

### Cowork

Cowork is now part of the Claude app, runs in the cloud, and works from a phone.

- One project, "Downfall Instinct", for what is not the repository: research (publishing, crowdfunding,
  comparable games), store and rulebook copy as living documents with comments, briefs.
- Its GitHub connector reads files and nothing else. Whatever ends as an issue or a pull request stays a Claude
  Code scheduled task.
- Skills have the same format in both. A `.claude-plugin/marketplace.json` in this repository can serve the
  language and proposal skills to chat as well; not before W8.

## Stages

Each stage is one pull request with its tests, and leaves the gate green.

1. **W0, standing orders and rails.** The standing orders in `CLAUDE.md`. The missing permissions.
   `SessionStart` starts Azurite. `scripts/workflow-metrics.py`: what this plan measured, and CI on `main`,
   which it did not. The owner, in settings: the environment and its setup script, the Sonar token, the ruleset
   with today's two CI jobs required, auto-merge, branch deletion.
2. **W1, the contract.** An ADR: `AGENTS.md` is an index with a budget, and an area's instructions load with
   the area. The index, the area files, `docs/reference/commands.md`, the rules, the test of the budget.
3. **W2, the gate.** The `gate` job. The browser suite on `main` and in front of `deploy`. Specs chosen by the
   diff. The retry and the flake report. The issue a red `main` opens. The ruleset then requires `gate` alone.
4. **W3, the shared files.** The ADR index generated, or dropped for the directory listing. `/adr` fetches
   first, and a test refuses a number used twice. The journal as one file per entry, with a generated index.
5. **W4, the pipeline.** `/plan`, `/stage`, `/ship`, `/wrap-up`, `/browser-check`, the new `/verify`, the
   `Stop` hook.
6. **W5, the reviewers.** The agents, `/review`, the memory trial.
7. **W6, scheduled work.** The morning digest. Then, one at a time, the weekly run and the review trigger.
8. **W7, operations.** `ops-report`, the smoke test, the alerts, the runbooks, `/ops`.
9. **W8, proposals, language, design.** `product-scout`, `ops-analyst`, `/proposals`, `/retro`, the
   terminology test, the design system, the Cowork project.

## Left out, and why

- Plugins and language servers declared in the repository: a cloud session loads neither.
- Managed Code Review and Claude in Slack: Team and Enterprise plans.
- Agent teams: experimental, and for interactive sessions.
- Auto memory: local to a machine. A cloud session shares none, so a lesson goes in a rule or it is lost.
- `/insights`: reads local sessions only. `scripts/workflow-metrics.py` and `/retro` stand in for it.
- A merge queue: whether a personal repository can have one was not checked.

## How to tell it worked

| Measure | Today | After |
| --- | --- | --- |
| `AGENTS.md` | 43 KB; changed in 101 of 300 pull requests | Under 12 KB; changed when a convention changes. |
| Merged before green | In 5 of the 8 latest sessions | GitHub refuses. |
| What the owner types to land a pull request | "Ouvre pr", "abonne toi", "Codex" | "Go". |
| Context when a stage ends | 700,000 to 900,000 tokens | Under 300,000. |
| A wake-up with nothing to do | $0.13 to $0.82 | A tenth of that. |
| Tests the local gate skips | 97 | 4. |
| Sonar's findings | Not readable in a session | Read. |
| An ADR number already taken | Four times | A test refuses it. |
| Loose ends | In chat | In issues. |

## Questions for the owner

1. Who merges? The owner, as today, with the ruleset turning an early tap into a queued merge; or the session,
   for a stage of an accepted plan, once everything is green.
2. Are the standing orders right as written? They let a session open a pull request, subscribe and ask Codex
   without a word from the owner.
3. What does the ruleset require at first: the two CI jobs only, or the browser suite too, before W2 makes it
   fast?
4. ADRs: one per feature decision, as now (180 in ten weeks), or only what is hard to reverse, with a
   feature's decisions kept in its plan?
5. One stage, one session, with a ceiling at 300,000 tokens: agreed? Should Claude Code projects be tried?
6. Which scheduled run first, and at what hour? The plan says the morning digest.
7. A read-only Azure role for `ops-report`: acceptable?
8. The order: W0, W1, W2 as written, or the reviewers (W5) before the contract?
