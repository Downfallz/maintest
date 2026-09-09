# Architecture Decision Records

An ADR captures one decision that is hard to reverse: an architecture choice, a dependency, a convention,
or a settled game-design question. Copy `0000-template.md`, take the next number, keep it under a page.
Accepted ADRs are immutable. To change a decision, write a new ADR that supersedes the old one.

| # | Title | Status |
| --- | --- | --- |
| [0001](0001-record-architecture-decisions.md) | Record architecture decisions | Accepted |
| [0002](0002-clean-slate-restart.md) | Restart from a clean slate, freeze the prototypes | Accepted |
| [0003](0003-dotnet-10-layered-solution.md) | .NET 10 solution with enforced clean-architecture layering | Accepted |
| [0004](0004-testing-stack.md) | Testing stack: xUnit v3, Microsoft.Testing.Platform, Shouldly, NSubstitute, NetArchTest | Accepted |
| [0005](0005-ai-assisted-development-kit.md) | AGENTS.md and Claude Code kit as first-class project assets | Accepted |
| [0006](0006-sonarcloud-analysis-from-ci.md) | Run SonarCloud analysis from CI with the scanner for .NET | Accepted |
| [0007](0007-shared-kernel-project.md) | A dependency-free SharedKernel project below Domain | Accepted |
| [0008](0008-messaging-without-a-mediator.md) | Use cases and domain events without a mediator library | Accepted |
| [0009](0009-game-data-pipeline.md) | Keep the data builder: one consolidated, hashed game schema | Accepted |
| [0010](0010-round-phases-and-sub-phases.md) | Rounds are driven by a phase and sub-phase state machine | Accepted |
| [0011](0011-win-condition-and-round-cap.md) | Win condition: last team standing, with a round cap | Accepted |
| [0012](0012-effect-taxonomy.md) | A closed taxonomy of spell effects | Accepted |
| [0013](0013-learning-stack.md) | Learning stack: Python trains, the engine records and hosts policies, a static viewer | Accepted |
| [0014](0014-exploration-in-recorded-datasets.md) | Record datasets with an exploring agent instead of reaching for reinforcement learning | Accepted |
| [0015](0015-content-studio.md) | A local content studio: browse, edit, version and try the game content | Accepted |
