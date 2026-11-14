# CLAUDE.md

@AGENTS.md

## Claude Code specifics

- Path-scoped rules live in `.claude/rules/`. They load automatically when you touch matching files.
- Subagents in `.claude/agents/`: `domain-reviewer` (DDD purity review), `test-engineer` (writes tests first),
  `code-reviewer` (adversarial review of a diff). For the board game translation (`docs/tabletop/plan.md`):
  `boardgame-director` (what survives the table), `tabletop-mathematician` (measures a candidate rule set),
  `component-designer` (what is in the box), `rulebook-writer` (the rulebook and the player aid),
  `playtest-app-architect` (the app ADR). Use them for their purpose instead of doing it all inline.
- Skills in `.claude/skills/`: `/adr` (create a decision record), `/new-aggregate` (scaffold an aggregate with
  tests), `/verify` (build, tests, format check, the CI gate), `/spells-report` (the spells the bots cast and
  leave, package by package, rebuilt and republished to the owner's report page).
- `.claude/hooks/session-start.sh` installs the .NET SDK on Claude Code on the web when it is missing. It
  tries Microsoft's `dotnet-install.sh` first, then the distribution's own `dotnet-sdk-<channel>` package,
  because a session's network policy decides which of the two is reachable. If neither works it says so and
  exits 0: when that happens, say the build was not verified rather than pretending it was.
- The SessionStart hook also starts the Azurite storage emulator when it is installed, and exports
  `AZURITE_BLOB_ENDPOINT`, `AZURITE_QUEUE_ENDPOINT` and `AZURITE_TABLE_ENDPOINT`, so the storage tests run
  instead of being skipped. When it is not installed the hook says so: report those tests as skipped.
- Personal overrides go in `.claude/settings.local.json` (git-ignored), never in `.claude/settings.json`.

## Standing orders

Settled by the owner on 2026-11-14 (`docs/engineering/ai-workflow-plan.md`, questions 1, 2 and 5).

- The gate passed: open the pull request from the template, subscribe to it, and ask Codex for its review
  (`@codex review`) if it has not started. Do not ask first.
- Answer every review thread: fix it, or say why not. Merge `main` in when the branch conflicts.
- The owner merges; a session never does. Never report a pull request ready while a check runs or a thread
  is open.
- One stage, one session. Past 300,000 tokens, or once the stage is merged, write the wrap-up (what was done,
  what was verified, what is left, the next stage's one-line prompt) and stop.
- Every pull request targets `main`. New work starts on a new branch from `origin/main`.
- Answer the owner in French, after a notification too. An event that needs nothing gets no answer.
- The owner reads on a phone. Steps for the owner are numbered, one block to paste each, with the way
  through the portal.
