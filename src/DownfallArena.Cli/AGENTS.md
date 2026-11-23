# src/DownfallArena.Cli — the composition root and the hosts

The only place concrete adapters are wired together. It holds the console commands (play, human, simulate,
evaluate, benchmark, scenario-cut, scenario-check, scenario-generate, scenario-import, push-keys), the content studio's HTTP host (`Studio/`,
ADR 0015) and the table's (`Table/`, docs/tabletop/playtest-app.md), with `table-tend` and `table-worker`.
Both hosts are one loopback host plus a route table (`Hosting/HttpHost.cs`).

## Commands

Every command and flag is in docs/reference/commands.md; every table route, with who may call it, in
`table/README.md#routes`.

```bash
dotnet run --project src/DownfallArena.Cli -- play --seed 1
dotnet run --project src/DownfallArena.Cli -- table --lobby --accounts dev
dotnet run --project src/DownfallArena.Cli -- studio
```

## A change here runs

- The gate, with the battle bundle built first: `TableFilesTests` reads it.
- Its tests are in `tests/DownfallArena.Cli.Tests` (`Table/` for the table host, the rest for the console
  and the studio). A route has a host test.
- A change the pages read: `node --test studio/*.test.js table/*.test.js showcase/*.test.js` and the browser specs it reaches.
- A change to the benchmark's agents or content: `benchmark` (CI verifies the digest; `--write` regenerates it).

## Traps

- `Table/` follows `.claude/rules/host.md`: who may call a route, the journal written before the answer, more
  than one process serving a table.
- Without Azurite the storage tests are skipped, not passed: say so.
- A test that needs a table under way holds it with a person seat that has arrived (`Arrive()`, then
  `Session.Begin()`): two bots play without waiting and can finish before the read (#598).
- A test that plays two processes over one table gives each its own `HostedTables` over the same runs and index:
  two registries over one host's services rebuild the match into one `InMemoryMatchRepository` (#1187).
- A flag or a route is described in docs/reference/commands.md and table/README.md, not in `AGENTS.md`.
- `--format` and `--declaration` are refused by `benchmark` (and `--declaration` by `studio` and `--practice`).
