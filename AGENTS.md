# AGENTS.md

Instructions for AI coding agents (Claude Code, Copilot, Cursor, Codex, ...) working in this repository.
Humans: this is also the shortest accurate description of how we work here. Keep it current.

## What this project is

Downfall Arena is a turn-based tactical board game (two players, each commanding a team of creatures, played in
rounds made of planning and combat phases) implemented as a .NET engine. The repository is deliberately also a
playground for professional engineering practice: Domain-Driven Design, clean architecture, executable
architecture rules, ADRs, and AI-assisted development.

Game rules are NOT settled. Treat `docs/domain/` as the source of truth for what is decided, and treat the old
prototypes under `legacy/` as inspiration only (see `legacy/README.md`).

## Repository map

```
DownfallArena.slnx            Solution (XML format, .NET 10 SDK)
src/
  DownfallArena.SharedKernel   Primitives (Entity, AggregateRoot, Result, DomainError), ids, stats, shared ports. No dependencies.
  DownfallArena.Domain         Pure domain model. Depends on SharedKernel only. Aggregates, entities, value objects, events.
  DownfallArena.Application    Use cases, ports (interfaces owned here), projections, agents, simulation, learning encodings. Depends on Domain.
  DownfallArena.Infrastructure Adapters implementing the ports. Depends on Application.
  DownfallArena.Cli            Composition root and hosts: the console commands (play, human, simulate, evaluate,
                               benchmark) and the content studio's HTTP host (ADR 0015).
tools/
  DownfallArena.DataBuilder    Consolidates data/ into data/dst/game.schema.json with a content hash (ADR 0009).
data/                          Authored game content (creatures, spells, talent trees, aliases), and the balance knobs
                               a tuning pass may move (data/balance, ADR 0021). See data/README.md.
benchmarks/                    The fixed benchmark seeds and one outcome digest per content hash, verified in CI. See benchmarks/README.md.
viewer/                        Static HTML viewer for learning artifacts (traces, batches, training runs). See viewer/README.md.
studio/                        Static HTML content studio: browse, edit, version and try the game content. See studio/README.md.
learning/                      The Python training project (uv, ruff, pytest) and the heuristic weights files. See docs/learning/training.md.
models/                        Trained policies (policy.json, small, committed with their evaluation). See models/README.md.
scripts/                       iterate.sh, one turn of the learning loop (docs/learning/training.md), and
                               sweep-weight.py, which measures one agent scoring weight alone (ADR 0037).
tests/
  DownfallArena.SharedKernel.Tests  Unit tests for primitives, identifiers, stats.
  DownfallArena.Domain.Tests        Unit tests for the domain (fast, no mocks needed). Sees Domain internals.
  DownfallArena.Application.Tests   Use case tests with NSubstitute for ports.
  DownfallArena.Infrastructure.Tests Adapter tests (in-memory, file-backed, seeded random).
  DownfallArena.Architecture.Tests  NetArchTest rules that fail the build when layering is violated.
docs/
  adr/          Architecture Decision Records. New decision = new ADR.
  architecture/ How the code is organized and why.
  domain/       Glossary (ubiquitous language), game rules, and the spell catalogue.
legacy/         Frozen prototypes from before the clean slate. Read-only reference.
.claude/        Claude Code configuration: rules, agents, skills, hooks.
.github/        CI, issue and PR templates, Dependabot, CODEOWNERS.
```

## Commands

```bash
dotnet restore
dotnet build --no-restore                 # warnings are errors
dotnet test --no-build                    # Microsoft.Testing.Platform runner (see global.json)
dotnet test --no-build -- --coverage      # with code coverage
dotnet format --verify-no-changes         # what CI runs; use `dotnet format` to fix
node --test studio/*.test.js              # the studio page's own tests (ADR 0024); needs no install
dotnet run --project tools/DownfallArena.DataBuilder -- data data/dst   # validate and consolidate content
dotnet run --project src/DownfallArena.Cli -- play --seed 1             # bot vs bot with a log (needs data/dst)
dotnet run --project src/DownfallArena.Cli -- human                     # you against a random bot
dotnet run --project src/DownfallArena.Cli -- simulate --matches 200 --seed 1 --out simulation.csv
dotnet run --project src/DownfallArena.Cli -- simulate --matches 200 --seed 1 --record runs/random   # plus a dataset and traces (--traces N caps the traces; 0 keeps none)
dotnet run --project src/DownfallArena.Cli -- play --seed 1 --trace match.trace.json                  # plus the match trace
dotnet run --project src/DownfallArena.Cli -- evaluate --p1 greedy --p2 random --seeds benchmarks/benchmark-seeds.json   # agents: random, greedy, lookahead[:<weights.json>], minimax[:<weights.json>], heuristic:<weights.json>, policy:<policy.json>, explore:<rate>[:<agent>] (explore deviates from greedy, or from the agent named: heuristic:<weights.json>, policy:<policy.json>, or a bare path as the weights shorthand)
dotnet run --project src/DownfallArena.Cli -- benchmark            # verify the benchmark digest (CI does); --write regenerates it
dotnet run --project src/DownfallArena.Cli -- studio               # the content studio on http://127.0.0.1:5099 (studio/README.md)
dotnet run --project src/DownfallArena.Cli -- studio --export site/data  # what the published studio reads, as files (ADR 0023)
uv sync --project learning && uv run --project learning ruff check learning && (cd learning && uv run pytest)   # the Python side
uv run --project learning search-weights -o runs/search             # tune the heuristic weights with the built CLI (docs/learning/training.md); --kind lookahead|minimax tunes them for that reading instead; --opponent greedy,heuristic:<w.json>,random scores each candidate as its worst matchup in the list, so it cannot learn one opponent
uv run --project learning check-knobs                                # the balance knobs against the content they describe (data/balance/README.md)
uv run --project learning tune-content -o runs/tune-1                # search those knobs for a better catalogue (ADR 0021); --apply writes it
uv run --project learning score-content -o runs/score --seeds unseen.json   # play the content as it stands on a seed file, no search: how a proposal is checked on seeds it was not searched on
uv run --project learning python scripts/sweep-weight.py energy 0.2 0.3 0.4   # one scoring weight alone, on fixed content (ADR 0037); one sweep at a time
# the same two searches run on GitHub Actions, each able to push a proposal branch: "Tune the catalogue" changes the content, "Search the agent weights" adds what it found next to greedy.json (docs/learning/training.md)
# "Learning loop" runs scripts/iterate.sh there on learning/experiments/next.json, and with commit=true proposes a policy that clears its bar under models/ (models/README.md)
uv run --project learning train-clone runs/greedy -o models/clone/v1 # or train-value; export-csv; compare-stamps
uv run --project learning evaluate-policy models/clone/v1 --opponent greedy   # play a policy with the engine, win rate into its log
uv run --project learning spread runs/<id>                           # every win rate of a turn ranged across its dataset seeds (ADR 0049)
scripts/iterate.sh --against <previous-run-id>                       # one full turn of the loop into runs/<id>/; --help lists every tuning flag
scripts/iterate.sh --seeds "1 5001 10001"                            # the default at 5000 matches: three dataset seeds spaced by the match count, because closer seeds record the same matches shifted, and the turn reports the spread, because one seed is a sample (ADR 0049)
scripts/iterate.sh --explore 0.2                                     # plus an exploring dataset for the value policy (ADR 0014)
scripts/iterate.sh --teacher heuristic:learning/weights/search-4.json # record a stronger player than greedy; a clone is capped by what it imitates (journal, 2026-09-15)
```

Run build, tests, and format check before declaring any task done; when `learning/` changes, also run its
ruff check, ruff format check, and pytest; when `studio/` changes, also run `node --test studio/*.test.js`. CI runs exactly these, then sends the build and the coverage
reports (C# and Python) to SonarCloud with the scanner for .NET (`.config/dotnet-tools.json`). The Sonar
quality gate covers C#, Python, shell scripts, and workflows, and must pass on every pull request.

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

1. Read `docs/roadmap.md` (which phase the task belongs to), the relevant ADRs in `docs/adr/`, and the
   glossary before touching the domain.
2. For non-trivial work, state a short plan first: which layer, which aggregate, which tests.
3. Write or update tests alongside the code. Red, green, then refactor.
4. Run `dotnet build`, `dotnet test`, `dotnet format --verify-no-changes`.
5. Update docs that the change makes stale (glossary, ADRs, this file).
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
