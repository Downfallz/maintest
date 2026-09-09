# Downfall Arena

[![CI](https://github.com/Downfallz/maintest/actions/workflows/ci.yml/badge.svg)](https://github.com/Downfallz/maintest/actions/workflows/ci.yml)
[![Quality Gate](https://sonarcloud.io/api/project_badges/measure?project=Downfallz_maintest&metric=alert_status)](https://sonarcloud.io/summary/new_code?id=Downfallz_maintest)
[![Coverage](https://sonarcloud.io/api/project_badges/measure?project=Downfallz_maintest&metric=coverage)](https://sonarcloud.io/summary/new_code?id=Downfallz_maintest)

A turn-based tactical board game engine in .NET, and a playground for professional engineering practice:
Domain-Driven Design, clean architecture, executable architecture rules, ADRs, and AI-assisted development.

## Status

The engine plays complete matches: domain, application layer, in-memory adapters, a console host, and a batch
simulator. Game rules are still evolving (`docs/domain/game-rules.md`). Earlier prototypes live under
[`legacy/`](legacy/README.md) as frozen reference material.

## Quick start

```bash
dotnet restore
dotnet build
dotnet test
dotnet run --project tools/DownfallArena.DataBuilder -- data data/dst   # consolidate the game content
dotnet run --project src/DownfallArena.Cli -- play --seed 1             # bot vs bot, with a log
dotnet run --project src/DownfallArena.Cli -- human                     # you against a bot
dotnet run --project src/DownfallArena.Cli -- simulate --matches 200 --seed 1 --out simulation.csv
dotnet run --project src/DownfallArena.Cli -- simulate --matches 200 --seed 1 --record runs/random   # plus a dataset and traces
dotnet run --project src/DownfallArena.Cli -- play --seed 1 --trace match.trace.json                  # plus the match trace
dotnet run --project src/DownfallArena.Cli -- evaluate --p1 greedy --p2 random --seeds benchmarks/benchmark-seeds.json   # agents: random, greedy, heuristic:<weights.json>, policy:<policy.json>
dotnet run --project src/DownfallArena.Cli -- benchmark            # verify the benchmark digest (CI does); --write regenerates it
uv run --project learning search-weights -o runs/search             # tune the heuristic weights (Python side, docs/learning/training.md)
uv run --project learning train-clone runs/greedy -o models/clone/v1 # train a policy on a recorded run (or train-value)
scripts/iterate.sh                                                   # one full turn of the learning loop (docs/learning/explained.md)
```

Requires the .NET SDK version in `global.json`. A devcontainer is provided in `.devcontainer/`.

## Layout

| Path | Purpose |
| --- | --- |
| `src/DownfallArena.SharedKernel` | Primitives, identifiers, stats, shared ports. No dependencies. |
| `src/DownfallArena.Domain` | Pure domain model. Depends on SharedKernel only. |
| `src/DownfallArena.Application` | Use cases and ports. |
| `src/DownfallArena.Infrastructure` | Adapters implementing the ports. |
| `src/DownfallArena.Cli` | Composition root and console entry point. |
| `tools/DownfallArena.DataBuilder` | Validates and consolidates the game content. |
| `data/` | Authored game content: creatures, spells, talent trees. |
| `tests/` | Shared kernel, domain, application, infrastructure, and architecture tests. |
| `docs/` | Roadmap, ADRs, architecture notes, domain glossary and rules. |
| `legacy/` | Frozen prototypes. Read-only. |

## Working with AI agents

`AGENTS.md` is the contract for any coding agent. `CLAUDE.md` adds Claude Code specifics, and `.claude/` holds
rules, subagents, skills, and hooks. See [docs/README.md](docs/README.md) for the full documentation index.

## Documentation

- [Roadmap](docs/roadmap.md)
- [Learning roadmap](docs/learning-roadmap.md)
- [Architecture overview](docs/architecture/overview.md)
- [Architecture decision records](docs/adr/README.md)
- [Domain glossary](docs/domain/glossary.md)
- [Game rules](docs/domain/game-rules.md)
- [Contributing](CONTRIBUTING.md)
