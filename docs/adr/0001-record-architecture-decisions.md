# 0001. Record architecture decisions

Date: 2026-09-08
Status: Accepted

## Context

The previous iterations of this project accumulated two or three parallel designs with no record of why one
replaced another. Coming back after a pause meant re-deriving decisions from code. With AI agents doing a
share of the work, undocumented decisions get silently re-litigated on every task.

## Decision

We will record every hard-to-reverse decision as an Architecture Decision Record in `docs/adr/`, using the
template in `0000-template.md`, numbered sequentially, and indexed in `docs/adr/README.md`. Accepted ADRs
are immutable; a new ADR supersedes an old one.

## Consequences

- Good: decisions are discoverable by humans and agents. `AGENTS.md` tells agents to read them first.
- Bad: a small writing cost per decision. Kept low by the one-page limit.
- Neutral: game-design decisions that constrain the engine also qualify as ADRs.

## Alternatives considered

- Wiki or issue comments: not versioned with the code, invisible to agents working from the repository.
- Comments in code: scattered, no index, no record of alternatives.

## Follow-up

None.
