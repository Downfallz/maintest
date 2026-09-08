# 0004. Testing stack

Date: 2026-09-08
Status: Accepted

## Context

Tests are the main safety net for AI-assisted changes and for the eventual balance simulations. The previous
prototypes used xUnit 2, FluentAssertions, and Moq. FluentAssertions moved to a commercial licence in v8,
and xUnit 2 is superseded.

## Decision

We will use:

- xUnit v3 running on Microsoft.Testing.Platform (`global.json` sets the `dotnet test` runner), with
  `Microsoft.Testing.Extensions.CodeCoverage` for coverage,
- Shouldly for assertions,
- NSubstitute for test doubles, restricted to Application-layer ports,
- NetArchTest.Rules for executable architecture rules.

Shared test configuration lives in `tests/Directory.Build.props`. Test names are behaviour sentences with
underscores; `CA1707` is disabled for `tests/**` in `.editorconfig`.

## Consequences

- Good: modern runner, permissive licences, fast domain tests without mocks, layering violations fail CI.
- Bad: Microsoft.Testing.Platform changes some `dotnet test` flags (`-- --coverage` instead of collectors).
  Documented in `AGENTS.md`.
- Neutral: property-based testing (FsCheck) and snapshot testing (Verify) are candidates for later ADRs,
  especially for balance and resolution-order tests.

## Alternatives considered

- TUnit: promising and native to the new platform, but xUnit v3 has the broader tooling support today.
- FluentAssertions 7 pinned: works, but a frozen version is a liability.
- Moq: fine technically; NSubstitute has the leaner syntax and no past supply-chain controversy.

## Follow-up

- `tests/Directory.Build.props`, `.editorconfig` test section, CI workflow.
