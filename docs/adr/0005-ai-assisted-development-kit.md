# 0005. AGENTS.md and Claude Code kit as first-class project assets

Date: 2026-09-08
Status: Accepted

## Context

A stated goal of the repository is to practise AI-assisted development with current industry conventions.
Agents that lack context re-invent conventions, build on dead code, and skip verification. The fix is the
same as for humans: written, versioned, discoverable instructions.

## Decision

We will maintain:

- `AGENTS.md` at the root as the tool-agnostic contract (project map, commands, rules, conventions,
  definition of done). Any agent (Claude Code, Copilot, Cursor, Codex) reads it.
- `CLAUDE.md` importing `AGENTS.md` and adding Claude Code specifics only.
- `.claude/rules/` for path-scoped rules, `.claude/agents/` for focused subagents (domain review, tests,
  code review), `.claude/skills/` for repeatable workflows (`/adr`, `/new-aggregate`, `/verify`), and
  `.claude/hooks/` for automation (SDK install on the web, format after edit).
- `.claude/settings.json` shared in git; personal overrides in the git-ignored `settings.local.json`.

Instructions are treated like code: reviewed in PRs and fixed when an agent misbehaves.

## Consequences

- Good: consistent agent behaviour across sessions and tools; onboarding doc for humans as a by-product.
- Bad: instructions drift if not maintained. `.claude/rules/docs.md` and the PR template ask for updates.
- Neutral: Claude Code on the web needs network access to download the .NET SDK; when the environment
  policy blocks it, the hook says so and the session proceeds without a compiler.

## Alternatives considered

- Only `CLAUDE.md`: ties the project to one tool; `AGENTS.md` is the cross-tool convention.
- Instructions in the wiki or in prompts: not versioned, not reviewed, invisible to other agents.

## Follow-up

- Keep `AGENTS.md` in sync with `.editorconfig`, `Directory.Build.props`, and the architecture tests.
