# Downfall Arena

A turn-based tactical board game engine in .NET, and a playground for professional engineering practice:
Domain-Driven Design, clean architecture, executable architecture rules, ADRs, and AI-assisted development.

## Status

Clean slate. The solution builds, the layering is enforced by tests, and the domain is a placeholder.
Earlier prototypes live under [`legacy/`](legacy/README.md) as frozen reference material.

## Quick start

```bash
dotnet restore
dotnet build
dotnet test
dotnet run --project src/DownfallArena.Cli
```

Requires the .NET SDK version in `global.json`. A devcontainer is provided in `.devcontainer/`.

## Layout

| Path | Purpose |
| --- | --- |
| `src/DownfallArena.Domain` | Pure domain model. No dependencies. |
| `src/DownfallArena.Application` | Use cases and ports. |
| `src/DownfallArena.Infrastructure` | Adapters implementing the ports. |
| `src/DownfallArena.Cli` | Composition root and console entry point. |
| `tests/` | Domain, application, and architecture tests. |
| `docs/` | ADRs, architecture notes, domain glossary and rules. |
| `legacy/` | Frozen prototypes. Read-only. |

## Working with AI agents

`AGENTS.md` is the contract for any coding agent. `CLAUDE.md` adds Claude Code specifics, and `.claude/` holds
rules, subagents, skills, and hooks. See [docs/README.md](docs/README.md) for the full documentation index.

## Documentation

- [Architecture overview](docs/architecture/overview.md)
- [Architecture decision records](docs/adr/README.md)
- [Domain glossary](docs/domain/glossary.md)
- [Game rules](docs/domain/game-rules.md)
- [Contributing](CONTRIBUTING.md)
