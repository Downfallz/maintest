# CLAUDE.md

@AGENTS.md

## Claude Code specifics

- Path-scoped rules live in `.claude/rules/`. They load automatically when you touch matching files.
- Subagents in `.claude/agents/`: `domain-reviewer` (DDD purity review), `test-engineer` (writes tests first),
  `code-reviewer` (adversarial review of a diff). Use them for their purpose instead of doing it all inline.
- Skills in `.claude/skills/`: `/adr` (create a decision record), `/new-aggregate` (scaffold an aggregate with
  tests), `/verify` (build, tests, format check, the CI gate).
- `.claude/hooks/session-start.sh` installs the .NET SDK on Claude Code on the web when it is missing.
  If the SDK cannot be installed (network policy), say so and do not pretend the build was verified.
- Personal overrides go in `.claude/settings.local.json` (git-ignored), never in `.claude/settings.json`.
