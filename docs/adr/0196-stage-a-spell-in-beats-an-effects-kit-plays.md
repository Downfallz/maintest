# 0196. Stage a spell in beats an effects kit plays

Date: 2026-11-18
Status: Accepted
Rulebook: none

## Context

[ADR 0131](0131-give-confirmed-arena-spells-a-visual-vocabulary.md) gave the 44 spells a shared vocabulary: 16
vector motifs, each one `Graphics` drawn once. Its own consequence says what that costs: several spells draw one
motif, with no choreography. [The battle's craft plan](../art/battle-craft-plan.md) stages every spell, and the
stagings need particles, light and a flash. The page still may not name a spell. The live bundle has 57 KiB left
under the limit of [ADR 0136](0136-gate-the-arena-release-behind-a-reversible-preference.md), and the owner chose
not to move that limit (question 2 of the plan).

## Decision

A spell's entry in `data/presentation/spell-art.json` may carry a `staging`: a list of at most 12 beats, each a
kind, the millisecond it starts at 1×, and its parameters by name (where, how long, how many, which shape, tint,
blend and direction). The kinds are `motif`, the spell's own motif placed in time, and the primitives of an
effects kit: `burst`, `flow`, `trail`, `gather`, `glow`, `flash`, `shake`, `pulse`.

`SpellArt` refuses a beat it
does not know, one with no start, one that starts outside the action or outlives its window (the windup to 840 ms, the impact to
2540 ms), a second `motif`, `flash` or `shake`, and a misspelt parameter; it publishes the staging on the card's
`visual`. The page reads it again (`table/battle-staging.js`): with no staging, no kit, or one beat it does not
understand, the spell is drawn as its motif, whole.

The kit is `battle/src/fx/`, one file a primitive, built as a
second bundle, `fx.js`. It holds no engine: the live bundle hands it the Phaser it carries. The scene fetches it
after its first frame, by a name stamped with the hash of what was built. `battle/budget.mjs` gives it limits of
its own, 48 KiB raw and 16 KiB gzip; the live bundle's limits do not move. Every object, tween and change to a
figure the kit makes is ended by `SpellEffects.clear`, the path of ADR 0131, and under reduced motion a primitive
returns before it makes anything. A tint is a name of the palette, never the target marker's gold.

## Consequences

- Good: a spell's look stays content, checked by the builder, and BC6 to BC8 stage 44 spells without a page
  change. The first frame costs about 5 KiB more. The kit, at 6.7 KiB, has room to grow apart from the engine.
- Good: a page and a catalogue from two deploys still agree: the kit says which beats it plays, and the page
  draws the motif for a staging that holds another.
- Bad: the vocabulary is held twice, in `SpellArt` and in the page's module; a test reads one against the other.
- Bad: an action that plays before the kit arrives is drawn as its motif. So is every action if `fx.js` fails.
- Bad: each beat makes its own emitter, where the craft book asked for one per texture and blend: two beats
  that overlap cannot share one, since an emitter's settings move its living particles. The cap is in particles:
  24 a beat, and 150 on the field counted as what the living emitters may hold.
- Bad: the `pulse` is a filter. Under a renderer with no graphics card one frame with it takes about ten times a
  frame without. BC10 measures it on a throttled phone profile before a staging uses it on more than one figure.
- Neutral: the frame, never a beat, changes health, energy or conditions. No beat plays a sound (ADR 0134).
  Frame time on a phone is not measured; BC10 measures it.

## Alternatives considered

- Raise the live bundle's limit: the owner refused it, and the first frame would pay for every later staging.
- Bundle the engine in the kit: 1.3 MB, sent twice.
- Split the engine out with esbuild's code splitting: one file a page becomes several, and their names change at
  each build. The host's file table lists each file it serves by name.
- A staging as code, one module a spell: the page would name spells, which ADR 0131 forbids.
- Free colours and free numbers in a beat: a typo would play as a default, and a gold effect would pass.

## Follow-up

- `SpellBeat` and `SpellVisual` (Application), `SpellArt` (Infrastructure), `table/battle-staging.js`,
  `battle/src/fx/`, `battle/src/fx-loader.ts`, `battle/build.mjs`, `battle/budget.mjs`, the host's file table.
- Tests: `SpellArtTests`, `table/battle-staging.test.js`, `table/battle-fx.test.js`,
  `studio/browser/battle-fx.spec.js`.
- `data/README.md`, `battle/README.md`, `docs/art/spell-vfx.md` and the craft book say what a staging is.
