---
paths:
  - "tests/**"
---

# Test rules

- Stack: xUnit v3 on Microsoft.Testing.Platform, Shouldly assertions, NSubstitute doubles, NetArchTest for
  architecture. Shared config lives in `tests/Directory.Build.props`; do not repeat it in project files.
- Name tests as behaviour sentences with underscores: `Round_cannot_move_from_combat_back_to_planning`.
- Arrange, act, assert, separated by blank lines. One behaviour per test. No logic in tests.
- Domain tests: real objects only, no mocks. Build fixtures with small builders or factory methods in a
  `Support/` folder when construction gets noisy.
- Application tests: substitute ports with NSubstitute, assert on the interaction that matters.
- Read what a host says through a sink it is given (`Action<string> Said`, a `TextWriter`), never by swapping
  `Console.Out`: it is one writer for the whole process, and classes run side by side. A class that must swap
  it carries `[Collection(ConsoleOutput.Name)]` (`ConsoleOutputTests` holds it, #656).
- Never delete or skip a failing test to get green. Fix the code or fix the test, and say which.
- If a layering rule changes, update `tests/DownfallArena.Architecture.Tests` in the same change as the ADR.
