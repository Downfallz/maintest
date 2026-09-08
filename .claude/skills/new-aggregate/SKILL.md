---
name: new-aggregate
description: Scaffold a new aggregate in the domain layer following project conventions (strongly typed id, AggregateRoot base, Result-returning methods, domain events, error catalogue) together with its test class. Use when starting a new aggregate such as Match, Round, or Team.
---

# Scaffold an aggregate

Ask for, or infer from the task: aggregate name, bounded context folder (e.g. `Matches`), the first two or
three behaviours it must support, and their invariants. Check `docs/domain/glossary.md` for the names.

Create, under `src/DownfallArena.Domain/<Context>/`:

- `<Name>Id.cs`: `public readonly record struct <Name>Id(Guid Value)` with `New()` using `Guid.CreateVersion7()`.
- `<Name>.cs`: `public sealed class <Name> : AggregateRoot<<Name>Id>` with a private constructor, a static
  `Create(...)` factory returning `Result<<Name>>`, and one method per behaviour returning `Result`.
- `<Name>Errors.cs`: `public static class <Name>Errors` with one `DomainError` per rule violation, code
  `<Name>.<Rule>`.
- `Events/<Something>Happened.cs`: one `sealed record` per event, implementing `IDomainEvent`.

Create, under `tests/DownfallArena.Domain.Tests/<Context>/`:

- `<Name>Tests.cs` with one test per behaviour and one per invariant, named as behaviour sentences.

Then:

1. Add any new glossary term to `docs/domain/glossary.md`.
2. Run `dotnet build` and `dotnet test tests/DownfallArena.Domain.Tests`. Report results verbatim.
3. Do not touch Application or Infrastructure unless the task asks for a use case too.
