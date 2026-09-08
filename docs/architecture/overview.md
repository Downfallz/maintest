# Architecture overview

## Shape

```
+---------------------------------------------------------------+
|  Hosts        DownfallArena.Cli   (later: Simulation, Api, Ui)|
+---------------------------------------------------------------+
|  Adapters     DownfallArena.Infrastructure                    |
|               repositories, game data loaders, clocks, rng     |
+---------------------------------------------------------------+
|  Use cases    DownfallArena.Application                       |
|               handlers, ports (interfaces), DTOs               |
+---------------------------------------------------------------+
|  Domain       DownfallArena.Domain                            |
|               aggregates, entities, value objects, events      |
+---------------------------------------------------------------+
|  Kernel       DownfallArena.SharedKernel                      |
|               primitives, identifiers, stats, shared ports     |
+---------------------------------------------------------------+

Tools (outside the stack): tools/DownfallArena.DataBuilder consolidates game content (ADR 0009).
```

Dependencies point downward only. The domain knows nothing about the layers above it, and the shared kernel
knows nothing at all. `tests/DownfallArena.Architecture.Tests` turns this diagram into failing tests.

## Layers

**SharedKernel** (ADR 0007) holds what every layer shares and no game rule: the building blocks (`Entity`,
`AggregateRoot`, `IDomainEvent`, `Result`, `DomainError`), strongly typed identifiers (including the versioned
content ids `SpellId`, `CreatureDefinitionId`, `TalentTreeId`), the stats (`Health`, `Energy`, `Defense`,
`Initiative`, `CriticalChance`), and the `IRandomSource` port.

**Domain** is the game. It is pure C#: no packages, no I/O, no time, no randomness. It exposes aggregates
whose methods return `Result` for rule violations and raise domain events for things that happened.
Bounded contexts are folders (`Resources/` and `Matches/` per the roadmap; `Players/`, `Rosters/`, ... as
they appear).

**Application** turns intents into domain calls. A use case handler loads an aggregate through a port,
invokes one method, persists, and dispatches the events. Ports are interfaces owned by this layer.
`AddApplication` registers everything, including `TimeProvider.System` as the default clock.

**Infrastructure** implements the ports: in-memory or file-backed repositories, JSON game data, a seeded
random source for reproducible simulations. `AddInfrastructure` registers the adapters.

**Cli** is the composition root. It is the only project that references Infrastructure, and it contains no
logic beyond wiring and presentation.

## Flow of a command (target state)

1. A host receives an intent (a player submits a combat action).
2. It calls the matching Application handler with a command DTO.
3. The handler loads the `Match` aggregate, calls `match.SubmitCombatAction(...)`, gets a `Result`.
4. On success the handler persists the aggregate and dispatches its domain events; on failure it returns
   the `DomainError` to the host, which presents it.
5. Event handlers (Application) react: advance the phase when all players have acted, record history for
   analysis, notify observers.

## Determinism

Simulations and AI training need identical inputs to produce identical games. Time comes from
`TimeProvider`, randomness from an injected, seeded source, and the domain never touches either directly.
This is why they are ports, not statics.

## Build and quality settings

`Directory.Build.props` applies to every project: .NET 10, nullable, warnings as errors,
`AnalysisLevel=latest-recommended`, code style enforced during build, deterministic output under
`artifacts/`. `Directory.Packages.props` pins every NuGet version once. `.editorconfig` defines formatting
and naming; `dotnet format --verify-no-changes` guards it in CI.
