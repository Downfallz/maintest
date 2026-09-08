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
  DownfallArena.Domain         Pure domain model. No dependencies. Aggregates, entities, value objects, events.
  DownfallArena.Application    Use cases, ports (interfaces owned here), orchestration. Depends on Domain.
  DownfallArena.Infrastructure Adapters implementing the ports. Depends on Application.
  DownfallArena.Cli            Composition root and console entry point.
tests/
  DownfallArena.Domain.Tests        Unit tests for the domain (fast, no mocks needed).
  DownfallArena.Application.Tests   Use case tests with NSubstitute for ports.
  DownfallArena.Architecture.Tests  NetArchTest rules that fail the build when layering is violated.
docs/
  adr/          Architecture Decision Records. New decision = new ADR.
  architecture/ How the code is organized and why.
  domain/       Glossary (ubiquitous language) and game rules.
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
dotnet run --project src/DownfallArena.Cli
```

Run build, tests, and format check before declaring any task done. CI runs exactly these. SonarCloud also
analyses every pull request (C#, shell scripts, workflows) and its quality gate must pass.

## Architecture rules (enforced by tests/DownfallArena.Architecture.Tests)

1. Dependencies point inward: Cli -> Infrastructure -> Application -> Domain. Never the other way.
2. Domain references no NuGet package and no other project.
3. Application owns its ports (interfaces). Infrastructure implements them. Domain never sees them.
4. Cli is the only place where concrete adapters are wired together.

If a task genuinely needs a rule to change, write an ADR first and update the architecture tests in the same PR.

## Domain conventions

- Aggregates extend `AggregateRoot<TId>`, entities extend `Entity<TId>`, value objects are `record`s or
  `readonly record struct`s. Identifiers are strongly typed (`MatchId`, not `Guid`).
- Expected failures return `Result` / `Result<T>` with a `DomainError(Code, Message)`. Error codes are stable and
  namespaced by aggregate (`Match.AlreadyStarted`). Exceptions mean a bug or a broken invariant.
- State changes go through aggregate methods that protect invariants. No public setters on domain types.
- Domain events are immutable records named in the past tense (`RoundEnded`), raised via `RaiseDomainEvent`.
- Use the words in `docs/domain/glossary.md`. If you need a word that is not there, add it in the same change.
- Time comes from `TimeProvider`, randomness from an injected abstraction. Never `DateTime.Now` or `new Random()`
  inside the domain: simulations must be reproducible.

## Coding conventions

- .NET 10, C# latest, nullable enabled, warnings are errors, `AnalysisLevel=latest-recommended`.
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

1. Read the relevant ADRs in `docs/adr/` and the glossary before touching the domain.
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
- Do not commit secrets, local settings (`.claude/settings.local.json`), or build output.
