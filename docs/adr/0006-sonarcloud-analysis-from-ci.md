# 0006. Run SonarCloud analysis from CI with the scanner for .NET

Date: 2026-09-08
Status: Accepted

## Context

SonarCloud was analysing pull requests with its automatic analysis. It caught real issues (shell hygiene,
an unsafe `curl -L`, C# pitfalls), but it cannot see test coverage: the coverage report is produced by
`dotnet test` in CI and automatic analysis never receives it, so "coverage on new code" always reads 0%.
Coverage is a useful signal for a domain that is meant to be exhaustively unit-tested.

## Decision

We will run SonarCloud analysis from the CI workflow using the scanner for .NET, pinned as a local tool in
`.config/dotnet-tools.json`. The workflow wraps build and test between `dotnet sonarscanner begin` and
`end`, tests write a Visual Studio coverage XML report that the scanner uploads, and `legacy/` stays
excluded. A final step (`.github/scripts/sonar-report.sh`) waits for the analysis, prints the quality gate
status and every open issue and hotspot into the CI log, and fails the job when the gate fails, so the
verdict is readable from GitHub alone. The Sonar steps are skipped when the `SONAR_TOKEN` secret is
absent, so forks and unconfigured clones still build and test normally.

## Consequences

- Good: coverage and analysis come from the same build CI validates; the scanner version is pinned and
  reviewable; one tool configuration instead of a Sonar-side setting.
- Bad: a token to manage (rotate it like any credential); CI runs a little longer.
- Neutral: automatic analysis must be turned off in the SonarCloud project, otherwise both compete.

## Alternatives considered

- Keep automatic analysis: no coverage, no control over scanner version or exclusions in the repo.
- Upload coverage only, keep automatic analysis: not supported by SonarCloud.

## Follow-up

One-time setup by the project owner:

1. SonarCloud, project `Downfallz_maintest`: Administration, Analysis Method, turn off Automatic Analysis.
2. SonarCloud, My Account, Security: generate a token; add it as the `SONAR_TOKEN` repository secret
   on GitHub (Settings, Secrets and variables, Actions).
3. Check that the organization key in `.github/workflows/ci.yml` (`SONAR_ORGANIZATION`) matches the
   SonarCloud organization.
