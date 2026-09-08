---
paths:
  - "src/DownfallArena.Domain/**"
---

# Domain layer rules

- This project has zero dependencies. If you feel the need for a package here, stop and reconsider the design.
- Model with the glossary (`docs/domain/glossary.md`). Names in code must match names in the glossary exactly.
- Aggregates: one aggregate root per consistency boundary. Only the root is reachable from outside; child
  entities are modified through root methods. Keep aggregates small.
- Invariants are protected in the aggregate, not in a service. A domain service exists only for logic that
  spans aggregates or needs no aggregate at all.
- Return `Result` / `Result<T>` for rule violations. Throw only for programming errors.
- Value objects are immutable `record`s with validation in a static factory (`Speed.Create(int)`), not in the
  constructor of a positional record when it can fail.
- Domain events are raised inside aggregate methods after the state change, never before.
- No `DateTime.Now`, no `Random`, no I/O, no logging. Pure, deterministic, unit-testable.
- Every public aggregate method that changes state gets a test in `tests/DownfallArena.Domain.Tests`.
