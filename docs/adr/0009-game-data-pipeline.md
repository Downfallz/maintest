# 0009. Keep the data builder: one consolidated, hashed game schema

Date: 2026-09-08
Status: Accepted

## Context

Game content (creature definitions, spells, talent trees, aliases) is authored as many small JSON files under
`Data/`. Tuning a spell is editing one file. The engine, however, wants one coherent, validated catalogue
with a version it can stamp on every match and simulation result. The legacy `DA.Game.DataBuilder` did
exactly that: read `Data/**`, resolve aliases, validate cross-references, write `game.schema.json` and a
SHA-256 content hash.

## Decision

We will keep that pipeline as `tools/DownfallArena.DataBuilder`, a console tool run explicitly (and in CI)
to produce `Data/dst/game.schema.json` plus its hash. It validates every reference (starting spells exist,
talent prerequisites exist, ids are well-formed) and fails with precise errors. Infrastructure loads only
the consolidated file, verifies the hash, and exposes it as `IGameResources.Version`. The authoring format
stays many small files.

## Consequences

- Good: authoring stays granular and diff-friendly; the engine sees one validated catalogue; every simulation
  result carries the content hash it ran with.
- Bad: one extra step after editing content. CI runs it, and the CLI can run it on startup in development.
- Neutral: the JSON schema DTOs live in Infrastructure; the tool references Infrastructure to reuse them.

## Alternatives considered

- Load the folder directly at startup: fewer moving parts, but validation errors surface at runtime and
  there is no stable content version to stamp on results.

## Follow-up

- Phase 2 ports the builder, adds the schema validation the legacy left as a TODO, and fixes the data drift
  (`characterClass`, `level`, missing base stats).
