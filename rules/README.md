# rules/ — the official rulebook

The rulebook a player reads, the app's edition (ADR 0188, [rulebook-plan.md](../docs/tabletop/rulebook-plan.md)).
[game-rules.md](../docs/domain/game-rules.md) stays the specification: this book is its reading for a player, in
the player's words ([player-terminology.md](../docs/domain/player-terminology.md)). The tabletop draft,
[rulebook.md](../docs/tabletop/rulebook.md), is the base it is written from.

`scripts/rules/build.mjs` builds it into `table/rules.json`, which is committed. After a change here, to a value it
draws, or to an id the pages link to:

```bash
node scripts/rules/build.mjs            # rebuild table/rules.json, or list what it refuses
node scripts/rules/build.mjs --check    # is table/rules.json the build of rules/?
```

`table/rules.test.js`, in the gate, fails when the file is out of date. The file records the whole hosted rule set
it was drawn from, so any change to `docs/tabletop/playtest.rules.json` calls for a rebuild too.

## The files

- `NN-<name>.md`: one file a Part, in teaching order. The number is the file's place, never written in it.
- `moved.json`: `{ "old-id": "new-id" }`, for a section removed or renamed, so an old link still lands.
- `changes.md`: what changed for a player, one line each: `- YYYY-MM-DD: what changed (ADR NNNN).`

## The format

A small subset of Markdown. The build refuses anything outside it: code, HTML, quotes, images, headings of a third
level, a link out of the book that does not start with `https://`.

````markdown
# What you are trying to do {#the-goal}

A paragraph of the Part, before its first section.

## Winning {#winning}

Paragraphs, lists (`-` or `1.`), tables, **bold**, *italics*, [a link to a section](#a-round).
A creature starts at {creature.health} Health.

sources: ADR 0011, game-rules#match-lifecycle

```card
id: health
title: Health
One to three lines.
> The example, with its values drawn: {max(0, creature.health - 7)}.
```
````

- **Ids.** Every heading ends with `{#id}`: lowercase words and hyphens, unique across the book. The id is the
  link (`/rules#winning`); numbers (1.1) are rendered and may change, ids never do. An id gone from the book
  needs its line in `moved.json`, or the build refuses it. The earlier ids are read from `table/rules.json` on
  `origin/main` and in the working tree, so fetch `main` first. A conflict in `table/rules.json` is settled by
  taking `main`'s file and rebuilding.
- **Values.** `{...}` is drawn when the book is built: `rules.<field>` from the hosted rule set
  (`docs/tabletop/playtest.rules.json`), `creature.<stat>` from the reference creature
  (`data/Creatures/main.v1.json`, `health` reads `baseHealth`), `spell:<name>:vN.<field>` from the spell's file
  (`energyCost`, `name`, `criticalChance`, or an effect by its kind: `damage`, `bleed`, `bleed.durationRounds`),
  `tier:<name>:vN.<field>` from the mutation's file under `data/Tiers/` (`name`, `level`, `initiativeBonus`, or
  its passive's `sunder`, `damageBonus`, `upkeepEnergy`). A spell's or a mutation's name may be typed. Numbers,
  `+ - * /`, parentheses, `max`, `min` and `floor` combine them. Never type a number the content holds. An
  example draws every value its arithmetic reads, the creature's base stats included (its base Defense, its own
  critical chance), and states no fact about the content that the build does not check.
- **Sources.** Every section ends with its `sources:` line: the ADRs and the `game-rules.md` headings (as
  `game-rules#<anchor>`) it gives a reading of. Only its card may follow it.
- **Cards.** A section a concept card summarises carries it, once: its id from `ConceptCards.Ids`, its title,
  one to three lines and its example.
- **Words.** The player's words only. An engine or legacy word that `player-terminology.md` replaced is refused.
