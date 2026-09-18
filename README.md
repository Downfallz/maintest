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
dotnet run --project src/DownfallArena.Cli -- evaluate --p1 greedy --p2 random --seeds benchmarks/benchmark-seeds.json   # agents: random, greedy, lookahead[:<weights.json>], minimax[:<weights.json>], heuristic:<weights.json>, policy:<policy.json>, explore:<rate>[:<agent>]
dotnet run --project src/DownfallArena.Cli -- benchmark            # verify the benchmark digest (CI does); --write regenerates it
dotnet run --project src/DownfallArena.Cli -- studio               # browse, edit and try the game content (studio/README.md)
uv run --project learning search-weights -o runs/search             # tune the heuristic weights (Python side, docs/learning/training.md)
uv run --project learning train-clone runs/greedy -o models/clone/v1 # train a policy on a recorded run (or train-value)
scripts/iterate.sh                                                   # one full turn of the learning loop (docs/learning/explained.md)
```

Requires the .NET SDK version in `global.json`. A devcontainer is provided in `.devcontainer/`.

### From an IDE

`DownfallArena.slnx` is the XML solution format, which needs Visual Studio 2022 17.13 or newer. The Test
Explorer needs *Options → Test → Use testing platform server mode*, because the runner is
Microsoft.Testing.Platform (`global.json`).

Both runnable projects carry launch profiles, so a command is chosen from the dropdown rather than typed:
`build the content (run this first)` on the DataBuilder, and one per thing you can do on the Cli — a hotseat
table, a table against Greedy, three against harder opponents, one that plays itself until round 3 and hands
over, one bound for a phone, the studio, a bot match, the benchmark.

The three hard profiles are the weights a search found, not a difficulty dial: against Greedy, `stun-first`
and `kill-first` take **every** match and `search-4` takes 0.93 of them (journal, 2026-09-17). The two that
win every match do it by exploiting the catalogue rather than by playing well — which is worth a playtest of
its own — and the searching agents are *not* the hard ones: `lookahead` only matches Greedy and `minimax`
plays below it (journal, 2026-09-16). They all run from the repository root, because the game schema is found at
`data/dst/game.schema.json` relative to it.

Build the content first. `data/dst/` is generated and git-ignored, and nothing that reads the catalogue —
`play`, `human`, `table`, `studio` — starts without it.

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
| `studio/` | Content studio: one static page to browse, edit, version and try the content. |
| `viewer/` | Static viewer for traces, batches, evaluations and training runs. |
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
