# 0003. .NET 10 solution with enforced clean-architecture layering

Date: 2026-09-08
Status: Accepted

## Context

The engine must be a pure, deterministic core (the rules of the game) that can be driven by a console, a
simulator running millions of games, an API, or a UI, without those hosts leaking into it. We also want the
repository to demonstrate current .NET practice rather than habits from older projects.

## Decision

We will build on .NET 10 (LTS) with C# latest, using:

- the XML solution format (`DownfallArena.slnx`),
- `Directory.Build.props` for shared settings (nullable, implicit usings, warnings as errors,
  `AnalysisLevel=latest-recommended`, code style enforced in build, deterministic builds, artifacts output),
- Central Package Management (`Directory.Packages.props`) with transitive pinning,
- four projects in `src/`: Domain (no dependencies), Application (ports and use cases), Infrastructure
  (adapters), Cli (composition root),
- the dependency rule Cli -> Infrastructure -> Application -> Domain, enforced by
  `tests/DownfallArena.Architecture.Tests`.

No mediator, ORM, or messaging framework is adopted yet. Each will get its own ADR when a real need appears.

## Consequences

- Good: the domain stays testable without mocks; hosts are replaceable; violations fail CI.
- Bad: warnings-as-errors and analyzers slow down quick hacks. That is intended for this repository.
- Neutral: `latest-recommended` rather than `latest-all` keeps the noise proportionate; tighten later if wanted.

## Alternatives considered

- Single project with folders: simplest, but layering then depends on discipline alone.
- Vertical slices per feature: attractive later; premature while the domain is empty.
- Keep the `.sln` format: works, but `.slnx` is readable and mergeable.

## Follow-up

- `AGENTS.md` architecture rules section.
- `tests/DownfallArena.Architecture.Tests/LayerDependencyTests.cs`.
