# 0059. Retire Spell initiative: the package pays it now

Date: 2026-09-22

Status: Accepted

## Context

[ADR 0017](0017-spell-initiative-on-unlock.md) gave a long-inert `Spell` stat a rule: unlocking a Spell raised
its learner's Base initiative by that Spell's own number, so a pick could buy tempo. [ADR
0056](0056-a-pick-buys-a-package-every-other-round.md) then made the package the unit of evolution, and moved
that bonus to the package, paid once per purchase. Nothing removed the per-spell stat, so the catalogue has
carried 36 authored numbers that no rule reads since. This is not a dormant field — it is a field that reads
as content: `check-knobs` compared it in `noNewStrictDominance` and `noIndistinguishableSpells`, 33 spells
declared a knob on it, the content audit reported on it with a sentence about unlocking that stopped being
true, and two knob entries described spells whose stated purpose was the tempo they no longer buy. ADR 0017's
own list of rejected alternatives has the answer written down: *"Delete the Spell stat outright: the smallest
change, and it throws away a lever the game wants."* The lever is not thrown away any more. It moved.

## Decision

We will remove `SpellInitiative` from `SpellStats`, `initiative` from the authored spell schema and from all
36 spell files, and every reading built on it: the content audit's flat-stat finding and its indistinguishable
signature, `SpellReach`'s column, and the Python scorer's dominance and twin rules. The strict unknown-field
reader is what enforces it — a spell document that still carries `initiative` is refused at the save and at
the build, rather than quietly ignored, because a silently dropped field is a number an author believes they
are tuning. The 33 knobs that addressed it go with it. `InitiativeBuff` and `InitiativeDebuff` are untouched:
those are combat effects, and the agents' `initiative` scoring weight (ADR 0018, ADR 0032) is a different
thing again and keeps its name and its measured value.

The consolidated schema is **versioned for the new shape**: 3 for a catalogue with no packages, 4 for one that
has them, replacing 1 and 2. Dropping a member from that document is the same compatibility break as adding
one and in the same place — the content hash is taken over the document, so an engine that still knows
`initiative` deserializes it back as 0, writes it into the canonical form, and reports a hash that does not
match. That reads as corruption for a catalogue that is sound, and the mirror reads as an unknown member
rather than as an old file. A version neither engine shares is the sentence worth reading instead.

## Consequences

- Good: the catalogue stops carrying a stat that decides nothing. Measured before the removal by moving every
  spell's number and replaying the benchmark seeds: 400 matches, identical winners, rounds and remaining
  health. After the removal the benchmark digest is identical entry for entry to the one before it; only the
  content hash moves.
- Good: the tuner stops spending search budget on an inert knob — 155 knobs to 122 — and stops accepting or
  refusing candidates over a number no match reads. The constraints were the worse half: a candidate could be
  rejected for `noNewStrictDominance` on an initiative it gave up, which is a rejection over nothing.
- Good: two knob entries that described a spell by what it no longer does are now honest. `momentum` is the
  energy trade alone; `throwing_star` is a cheap jab whose written purpose was the tempo its package now pays.
- Bad: the content hash moves (`d367db1c` to `6df8dc30`), so the benchmark digest is regenerated and the
  journal records why. Every digest keyed to an older hash stays valid for the content it names, as always.
- Bad: a catalogue with no packages no longer hashes to the document it hashed before packages existed. That
  property held from ADR 0009 to here and is what kept version 1 meaningful; the spell shape is what ends it,
  and `GameSchemaVersionTests` pins the new canonical bytes as strictly as it pinned the old ones.
- Bad: `throwing_star` is left without an identity. Its whole entry rested on buying two points of Base
  initiative, and under ADR 0056 `tier:prowler:v1` pays that bonus once for both of its spells — which is why
  that package splits its casts 1813 to 140 toward `poison_slash` (#169). This ADR records the hole rather
  than filling it: giving the spell a purpose is a content decision, and this one removes a stat.
- Neutral: `features:v6` is unaffected. The observation never carried the per-spell number; a creature's Base
  initiative is a creature feature, and the package still raises it.
- Neutral: historical traces, digests and journal entries keep their meanings. Nothing is rewritten to pretend
  the stat was never there, and ADR 0017 stays readable as the decision it was.

## Alternatives considered

- Leave it at zero on every spell: the schema keeps a field, authors keep meeting it, and `check-knobs` keeps
  comparing a column of zeroes. A stat nothing reads still has to be authored, tuned and explained, and the
  zeroes would say "not used yet" rather than "gone".
- Keep it and make the package's bonus the sum of its spells': that is [ADR
  0057](0057-a-package-is-authored-not-derived.md) resolved the other way. A derived number is one an author
  cannot set, and it would tie a package's tempo to how many spells happen to be in it.
- Move the 33 knobs onto the packages' `initiativeBonus` in the same change: the right next step, and a
  different one. It adds a knob kind to `knobs.json`, the Python loader, the tuner and the studio's Balance
  view, and it deserves its own measurement — whether tuning package initiative moves the objective at all.
  Removing an inert knob needs no such evidence; adding a live one does.

## Follow-up

- `src/DownfallArena.Domain/Resources/SpellStats.cs`: the record is `(Cost, CriticalChance)`.
- `src/DownfallArena.Infrastructure/Resources/Schema/SpellDto.cs` and `GameSchemaMapper.cs`: the field is
  gone, so the strict reader refuses a file that still carries it. `ContentStoreTests` pins that refusal.
- `src/DownfallArena.Infrastructure/Resources/Schema/GameSchema.cs` and `GameSchemaBuilder.cs`: versions 3 and
  4, and a refusal that says which side of the change a document it cannot read comes from.
- `src/DownfallArena.Application/Content/ContentAudit.cs` and `SpellReach.cs`: the flat-stat entry, the
  signature term and the exported column.
- `learning/src/downfall_learning/knobs.py`: `_signature`, `dominates` and the docs that named the stat.
- `data/Spells/**`: the field, in all 36. `data/balance/knobs.json`: the 33 knobs, the two constraint
  statements, the three starting-spell notes that said the stat was not a knob there, and the `momentum` and
  `throwing_star` entries.
- `studio/studio.js`: the spell editor's comment promised the field survived a save untouched. It does not —
  the save is refused.
- `benchmarks/`: a digest for the new content hash, with a journal entry.
- `docs/domain/glossary.md`, `game-rules.md`, `spells.md`, `data/README.md`, `data/balance/README.md` and
  `docs/tabletop/translation.md`: the rule they describe.
