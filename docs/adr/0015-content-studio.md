# 0015. A local content studio: browse, edit, version and try the game content

Date: 2026-09-09
Status: Accepted

## Context

Game content is authored as many small JSON files under `data/` and consolidated by the data builder
(ADR 0009). That is diff-friendly and precise, but a designer tuning a casting cost has to find the right
file, remember the effect vocabulary, hand-edit the alias map when a spell gets a new version, re-run the
builder, then run the CLI to see what the change does. Nothing shows the talent tree as a tree, and nothing
tells you which spells a change reaches. The viewer (ADR 0013, decision J) already proved that a static page
with no build step is the right weight for looking at this project's data; what it cannot do is write files
or run the engine.

## Decision

We will ship a **content studio**: `dotnet run --project src/DownfallArena.Cli -- studio` starts a loopback-only
HTTP host that serves the static page in `studio/` and a small JSON API over the authored content. The page
browses creatures, spells and talent trees with cross-links, edits them in forms that know the schema, saves a
document back to its own file, cuts a new version of an item (`:v2`, new file, alias repointed), toggles an
item's `enabled` flag, rebuilds the consolidated schema, and launches a match or an evaluation between two
agents, opening the result in the existing viewer. Reading, validating and writing authored files is
Infrastructure (`Resources/Authoring`), next to the DTOs it shares with the builder; the HTTP host and the
page are the Cli's, which is already the composition root and already knows how to run matches.

Authored items gain one optional field, `"enabled": false`, which excludes an item from the consolidated
schema. References to a disabled spell are pruned (from starting spells, talent nodes and prerequisites)
rather than reported, because a disabled spell is content that does not exist for the engine; a creature
whose talent tree is disabled is an error, because it cannot be played. The flag is authoring-only: the
builder clears it on the way into `game.schema.json`, so content where nothing is disabled hashes exactly as
it did before.

## Consequences

- Good: tuning content is a form and a button, the talent tree is visible as a tree, and the feedback loop
  (edit, build, run, look) is one page. Versioning a spell no longer means editing the alias map by hand.
- Good: the studio reuses the builder for validation and the viewer for results, so there is one definition of
  "valid content" and one definition of "what a run looks like".
- Bad: one more host in the Cli, and a static page whose fields must follow the DTOs when they change.
- Bad: `enabled` adds a second way for an item to be absent from a build; the builder prints what it pruned.
- Neutral: the studio is a local authoring tool, bound to `127.0.0.1` and never part of a deployed game.

## Alternatives considered

- A static page using the File System Access API: no host to write, but no engine, so no run button, and
  the browser permission dance on every session.
- A separate tool under `tools/`: cleaner isolation, but running a match needs the whole composition root,
  which is the Cli's job (architecture rule 4).
- Deleting a spell's file instead of disabling it: loses the content and its history for what is usually a
  temporary "not this iteration".

## Follow-up

- `data/README.md` documents `enabled`.
- `AGENTS.md` and `README.md` list the `studio` command.
- `GameSchemaBuilder` filters and prunes; `GameSchemaBuilderTests` covers it.
- `studio/README.md` describes the page.
