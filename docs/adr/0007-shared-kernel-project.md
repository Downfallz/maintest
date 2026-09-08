# 0007. A dependency-free SharedKernel project below Domain

Date: 2026-09-08
Status: Accepted

## Context

Identifiers (`MatchId`, `SpellId`, ...), stats (`Health`, `Energy`, ...), the `Result` and `DomainError`
primitives, and ports such as a random source are needed by every layer: the domain uses them, application
commands carry them, infrastructure serialises them, the data builder validates them. The legacy generation
kept them in `DA.Game.Shared` and the arrangement worked. Putting them inside Domain would force the data
builder and the hosts to reference the whole domain for an id type.

## Decision

We will add `src/DownfallArena.SharedKernel`: a project with no dependencies, referenced by Domain and, through
it, by everything above. It holds the DDD primitives (`Entity`, `AggregateRoot`, `IDomainEvent`, `Result`,
`DomainError`), strongly typed identifiers, stats value objects, and framework-free ports that several layers
share (`IRandomSource`). It contains no game rules: anything that knows what a Match or a Round does belongs
in Domain.

## Consequences

- Good: one place for cross-cutting types; tools and hosts can depend on ids without pulling in the engine.
- Bad: a temptation to grow the kernel into a utility bag. The rule "no game rules, no dependencies" is
  enforced by the architecture tests and reviewed in every PR that touches it.
- Neutral: Domain currently contains nothing else; phase 2 fills it.

## Alternatives considered

- Everything in Domain: simplest, but the data builder and future hosts would reference the engine for ids.
- A `Contracts` project per layer: heavier, and the legacy showed the split was never needed.

## Follow-up

- `tests/DownfallArena.Architecture.Tests`: SharedKernel references only the base class library; Domain
  references only the base class library and SharedKernel.
- `AGENTS.md` repository map and architecture rules.
