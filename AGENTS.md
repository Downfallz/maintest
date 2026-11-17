# AGENTS.md

Instructions for AI coding agents (Claude Code, Copilot, Cursor, Codex, ...) working in this repository.
Humans: this is also the shortest accurate description of how we work here. Keep it current.

This file is an index with a budget (ADR 0181): under 200 lines, under 12 KB, no line over 300 characters, held by
`InstructionFileTests`. Detail lives with what it is about: an area's `AGENTS.md`, `.claude/rules/`, the docs.

## What this project is

Downfall Instinct is a turn-based tactical board game (two players, each commanding a team of creatures, played in
rounds made of planning and combat phases) implemented as a .NET engine, and hosted as a table people play online.
The repository is deliberately also a playground for professional engineering practice: Domain-Driven Design,
clean architecture, executable architecture rules, ADRs, and AI-assisted development.

Game rules are NOT settled. Treat `docs/domain/` as the source of truth for what is decided, and treat the old
prototypes under `legacy/` as inspiration only (see `legacy/README.md`).

## Repository map

An area marked * has its own `AGENTS.md` (and a `CLAUDE.md` that imports it): read it before changing the area.

| Path | What it is |
| --- | --- |
| `DownfallArena.slnx` | The solution (XML format, .NET 10 SDK). |
| `src/DownfallArena.SharedKernel` | Primitives (Entity, AggregateRoot, Result, DomainError), ids, stats, shared ports. No dependencies. |
| `src/DownfallArena.Domain` | The pure domain model: aggregates, entities, value objects, events. Depends on SharedKernel only. |
| `src/DownfallArena.Application` | Use cases, ports (interfaces owned here), projections, agents, simulation, learning encodings. |
| `src/DownfallArena.Infrastructure` | Adapters implementing the ports. Depends on Application. |
| `src/DownfallArena.Cli` * | Composition root and hosts: the console commands, the studio's HTTP host (ADR 0015) and the table's. |
| `tools/DownfallArena.DataBuilder` | Consolidates `data/` into `data/dst/game.schema.json` with a content hash (ADR 0009). |
| `data/` | Authored content (creatures, spells, talent trees, aliases) and the balance knobs (`data/balance`, ADR 0021). See `data/README.md`. |
| `benchmarks/` | The fixed benchmark seeds and one outcome digest per content hash, verified in CI. See `benchmarks/README.md`. |
| `viewer/` | Static HTML viewer for learning artifacts (traces, batches, training runs). See `viewer/README.md`. |
| `studio/` * | Static HTML content studio: browse, edit, version and try the content; and the browser suite (`studio/browser/`). |
| `showcase/` | English/French public game site. Build with `node showcase/build.mjs`; Cloudflare Pages serves `showcase/dist`. |
| `table/` * | Static HTML table: the pages people play on, served by `table` (docs/tabletop/playtest-app.md). |
| `battle/` * | The Phaser/TypeScript battle view; builds into the ignored `table/battle-build/`. |
| `learning/` * | The Python training project (uv, ruff, pytest) and the heuristic weights files. See docs/learning/. |
| `scenarios/` | Match scenarios the admin panel lists and plays, one file each, and the tutorials under `tutorials/` (`scenarios/README.md`, ADR 0179, 0187). |
| `rules/` | The official rulebook, the app's edition, in English and French, built into `table/rules*.json` (ADR 0188, 0193). See `rules/README.md`. |
| `models/` | Trained policies (`policy.json`, small, committed with their evaluation). See `models/README.md`. |
| `infra/` * | The hosted table on Azure behind Cloudflare: `main.bicep`, `bootstrap.sh`, `README.md` (ADR 0080-0082, 0162, 0175). |
| `Dockerfile` | The table's image, built and tried by `.github/workflows/image.yml` before it is pushed and deployed. |
| `scripts/` | The gate's `verify.sh`, the learning loop's `iterate.sh` and readers, the reports and the reviewers' picker. See `scripts/README.md`. |
| `tests/` | One test project per layer, plus `Cli.Tests` (the hosts) and `Architecture.Tests` (layering and these files). |
| `docs/` | Indexed in `docs/README.md`: every plan, the ADRs, the domain, the tabletop, art, learning, engineering. |
| `docs/reference/commands.md` | Every command, flag and route, one heading per command. |
| `legacy/` | Frozen prototypes from before the clean slate. Read-only reference. |
| `.claude/` | Claude Code configuration: rules, agents, skills, hooks. |
| `.github/` | CI, issue and PR templates, Dependabot, CODEOWNERS. |

The tests: `SharedKernel.Tests` (primitives, identifiers, stats), `Domain.Tests` (fast, no mocks, sees Domain
internals), `Application.Tests` (use cases, NSubstitute for ports), `Infrastructure.Tests` (adapters in memory,
file-backed and seeded, plus the match invariants in `Invariants/` checked on every match of a seeded batch on
the real content), `Cli.Tests` (the console commands and the HTTP hosts), `Architecture.Tests` (NetArchTest rules
that fail the build when layering is violated, and the budget of these instruction files).

## The gate

```bash
npm --prefix battle ci --ignore-scripts && npm --prefix battle run build   # battle assets the host route tests read
dotnet restore
dotnet build --no-restore                       # warnings are errors
dotnet format --verify-no-changes --no-restore  # `dotnet format` fixes
dotnet test --no-build                          # Microsoft.Testing.Platform (global.json); `-- --coverage` for coverage
node --test studio/*.test.js table/*.test.js showcase/*.test.js  # the static pages' own tests (ADR 0024); needs no install
uv sync --project learning && uv run --project learning ruff check learning && (cd learning && uv run pytest)
dotnet run --project tools/DownfallArena.DataBuilder -- data data/dst   # validate and consolidate content
```

Every other command (play, simulate, evaluate, benchmark, studio, table, table-tend, table-worker, the scenarios,
the learning CLI, iterate.sh) is in `docs/reference/commands.md`. `/verify` runs the gate (`scripts/verify.sh`): the
battle bundle, the Python side and DataBuilder when the diff calls for them, the rest always.

## Definition of done

- Build, tests, and format check pass before any task is declared done.
- `learning/` changed: also its ruff check, `ruff format --check`, and pytest.
- `studio/`, `table/` or `showcase/` changed: also `node --test studio/*.test.js table/*.test.js showcase/*.test.js`.
- `battle/` or `table/battle-*` changed: rebuild the bundle and run the battle browser specs (`battle/AGENTS.md`).
- CI runs these, then sends the build and the coverage reports (C# and Python) to SonarCloud with the scanner for
  .NET (`.config/dotnet-tools.json`). The Sonar quality gate covers C#, Python, shell scripts, and workflows, and
  must pass on every pull request. A Markdown-only pull request runs the light build, without Sonar (ADR 0192).
- A storage test skipped for want of Azurite is reported as skipped, never as passed.
- Docs the change makes stale are updated in the same change: the glossary, ADRs, the area's `AGENTS.md`,
  `docs/reference/commands.md` for a command, a flag or a route, `table/README.md` for a route.

## Architecture rules (enforced by tests/DownfallArena.Architecture.Tests)

1. Dependencies point inward: Cli -> Infrastructure -> Application -> Domain -> SharedKernel. Never the other way.
2. SharedKernel references nothing. Domain references no NuGet package and no project other than SharedKernel.
   SharedKernel holds no game rules (ADR 0007).
3. Application owns its ports (interfaces). Infrastructure implements them. Domain never sees them.
4. Cli is the only place where concrete adapters are wired together. Tools under `tools/` may reference
   Infrastructure; nothing references a tool.

If a task genuinely needs a rule to change, write an ADR first and update the architecture tests in the same PR.

## Domain conventions

- Aggregates extend `AggregateRoot<TId>`, entities extend `Entity<TId>`, value objects are `record`s or
  `readonly record struct`s. Identifiers are strongly typed (`MatchId`, not `Guid`) and live in
  `SharedKernel/Identifiers`; content ids are versioned (`SpellId.Parse("spell:pummel:v1")`).
- Expected failures return `Result` / `Result<T>` with a `DomainError(Code, Message)`. Error codes are stable and
  namespaced by aggregate (`Match.AlreadyStarted`). Exceptions mean a bug or a broken invariant.
- State changes go through aggregate methods that protect invariants. No public setters on domain types.
  Entity mutators (`Creature`, `Round`) are `internal`: only `Match` and the rules it runs change them, and
  `Advance`, on creatures it restores from snapshots and never hands out (ADR 0047).
  `DownfallArena.Domain.Tests` reaches them through `InternalsVisibleTo`.
- Domain events are immutable records named in the past tense (`RoundEnded`), raised via `RaiseDomainEvent`.
- Use the words in `docs/domain/glossary.md`. If you need a word that is not there, add it in the same change.
- Time comes from `TimeProvider`, randomness from `IRandomSource` (SharedKernel). Never `DateTime.Now` or
  `new Random()` inside the domain: simulations must be reproducible.

## Coding conventions

- .NET 10, C# latest, nullable enabled, warnings are errors, `AnalysisLevel=latest-recommended`.
- Python 3.11+ under `learning/`, dependencies pinned by `uv.lock`, `ruff` for lint and format (line length
  110), type hints everywhere, pytest tests named as behaviour like the C# ones. No ML runtime in .NET.
- Formatting is defined by `.editorconfig` and enforced by `dotnet format`. Do not argue with it.
- File-scoped namespaces, one public type per file, folder structure mirrors namespaces.
- NuGet versions live only in `Directory.Packages.props` (Central Package Management).
- Prefer small, focused commits with imperative subject lines (`Add Round aggregate`, not `added stuff`).
- Comments explain why, not what. No commented-out code. No TODOs without an issue number.
- Code, identifiers, commit messages, and docs are in English. Conversation with the maintainer may be in French.

## Testing conventions

- xUnit v3 on Microsoft.Testing.Platform, Shouldly for assertions, NSubstitute for test doubles.
- Test names read as behaviour: `Failed_result_refuses_to_expose_a_value`. One behaviour per test.
- Domain tests use real domain objects, never mocks. Mocks are for ports in Application tests.
- Every bug fix ships with a test that failed before the fix.
- Prefer testing through aggregate public methods over testing private helpers.

## How to work on a task

1. Read `docs/roadmap.md` (which phase the task belongs to), the relevant ADRs in `docs/adr/`, the glossary before
   touching the domain, and the `AGENTS.md` of the area you change.
2. For non-trivial work, state a short plan first: which layer, which aggregate, which tests.
3. Write or update tests alongside the code. Red, green, then refactor.
4. Run the gate (above): at least `dotnet build`, `dotnet test`, `dotnet format --verify-no-changes`.
5. Update docs that the change makes stale (glossary, ADRs, this file, the area's file, the command reference).
6. Summarize what changed, what was verified, and anything left open.

## Things not to do

- Do not modify anything under `legacy/`. It is frozen.
- Do not add a NuGet package to Domain. Do not add a mediator, ORM, or framework without an ADR.
- Do not suppress an analyzer warning to make a build pass. Fix the cause, or justify the suppression in
  `.editorconfig` with a comment.
- Do not skip, disable, or delete tests to get green.
- Do not report a learning-loop win rate from one dataset seed as a result, and never pick the seed that
  scored best: the benchmark seeds are fixed, so that is selection on the test set. One seed is a sample
  (ADR 0049) — a turn runs three, the journal reports the spread, and a gate reads the minimum.
- Do not commit secrets, local settings (`.claude/settings.local.json`), or build output.
- Do not grow this file past its budget: put the detail where it belongs and link it (ADR 0181).
