# 0019. Regeneration, the healing counterpart of Bleed

Date: 2026-09-09

Status: Accepted

## Context

[ADR 0012](0012-effect-taxonomy.md) closed the effect taxonomy at seven kinds, and healing over time is not
one of them: `Heal` is instant, `Bleed` is damage over time, and nothing is healing over time. The gap is
already recorded as a loss — `docs/domain/spells.md` lists "healing over time" among the six prototype
mechanics that had no counterpart, and Healing Screech, which healed 2 and then 2 more over a round, was
folded into a single `Heal 4` to fit. The benchmark says the other half of the problem: `Greedy` never casts
a heal at all, never applies a stun, and uses nine of thirty-six spells, all of them damage. A defensive
half of the catalogue that holds one instant heal and one defense buff is thin, and the shape a healer wants
— commit now, pay off over rounds, and lose it if the target dies first — has no way to be authored.

## Decision

We will add **`Regeneration`** to the closed taxonomy: healing applied to the target at the start of each of
its rounds while the condition lasts, with an amount per round, a `Duration` and a `StackingPolicy` defaulting
to `Refresh` — `Bleed` with the sign flipped. In the OngoingEffects sub-phase **regenerations heal first, then
bleeds deal their damage**, so a regeneration can carry a creature through a bleed that would otherwise have
killed it; under the other order the two would never meet, since the creature would already be dead. The name
is ours, not the prototype's: it had no such effect, only a `Temporary` modifier on the Health stat.

Publishing a condition kind changes the observation layout, so the feature schema becomes **`features:v2`**.
Healing Screech goes back to the prototype's `Heal 2` plus `Regeneration 2` for one round, which is the first
content to use it and the reason the approximation existed.

## Consequences

- Good: the effect taxonomy is symmetric where it was not, and a healer can be authored as something other
  than a bigger instant number.
- Good: one of the six mechanics lost in translation comes back, and Healing Screech is the prototype's spell
  again rather than a fold of it.
- Bad: `features:v2` invalidates the comparability of every run in `docs/learning/journal.md`. Datasets and
  runs stay readable — they carry their own schema id — but nothing trained on v1 can be measured against
  anything trained on v2 without re-recording.
- Bad: heal-before-bleed is a design choice with no evidence behind it. It makes regeneration a real counter
  to bleed rather than a race bleed always wins, which is the more interesting rule, not a measured one.
- Bad: a third change lands in the same benchmark digest as ADR 0017 and ADR 0018.
- Neutral: nothing forces content to use it. The rest of the catalogue is untouched.
- Neutral: the agents price it with the existing `heal` weight, times the rounds it lasts, capped by what the
  target is missing — the mirror of how `bleed` is priced. No new weight.

## Alternatives considered

- Heal at the end of the round instead of the start: it would make a regeneration applied this round pay
  before the enemy has acted, which is the opposite of the commitment a heal over time is supposed to be.
- Bleed first, then heal: simpler to state, and it makes a regeneration unable to save anyone, which is most
  of what a heal over time is for.
- Net the two into a single tick per creature: fewer numbers, and it hides which effect did what from the
  trace, the viewer and the stats, all of which report bleeds today.
- Give `Heal` a duration instead of a new kind: it would make an instant heal a special case of a lasting one
  and break the instant/lasting split the taxonomy is built on (ADR 0012).

## Follow-up

- `src/DownfallArena.Domain/Resources/Effects/Regeneration.cs`, `Matches/Rules/Rounds/` (the tick, the
  `OngoingEffectTicks` pair, the upkeep order) and the `OngoingEffectsApplied` event, which now carries both.
- `src/DownfallArena.Infrastructure/Resources/GameSchemaMapper.cs` and `data/README.md`: the authored kind.
- `docs/learning/features.md`: the `features:v2` section.
- `src/DownfallArena.Application`: the observation, the scorer, the evaluation's `Regens` counter and the
  audit's `RegenerationHealing`.
- `viewer/index.html` and its samples, `studio/studio.js`: the column, the chip and the effect editor.
- `docs/domain/glossary.md`, `game-rules.md` and `spells.md`.
- `benchmarks/`: the digest ADR 0017 already requires; this is the third change inside it.
