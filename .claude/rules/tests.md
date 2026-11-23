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
- Read what a host says through a sink it is given (a `TextWriter`, or `Action<string> Said` as `QueuedAgentTurns`
  has; new code takes one), never by swapping `Console.Out`: it is one writer for the whole process, and test
  classes run in parallel. No test swaps it: `ConsoleOutputTests` refuses `Console.SetOut` and `Console.SetError`
  under `tests/`, because a helper in one file can swap it for the classes of another (#656, #801).
- A bug fix's test is run once with the fix taken out, and seen red, before it is called the test of the fix.
  Ten times a test stayed green without its fix (code-reviewer `lessons.md`).
- A scanner or a validator is tested on every spelling of what it refuses (the case, the separators, the other
  language, the field nested deeper), not on the one spelling that prompted it (#1022, UX12 P4).
- A fixture or a mock is shaped as the host sends it: copy its shape from the route's own test, never by hand
  (#1073). A test that lists exact items (paths, files, scenarios) and a rename are grepped in every suite,
  `studio/browser/` and the pages' tests included (#1016, #1087, #1092).
- Never delete or skip a failing test to get green. Fix the code or fix the test, and say which.
- If a layering rule changes, update `tests/DownfallArena.Architecture.Tests` in the same change as the ADR.
