# 0012. A closed taxonomy of spell effects

Date: 2026-09-08
Status: Accepted

## Context

The prototype described spell effects with an `EffectKind` enum plus marker interfaces (`IInstantEffect`,
`IOverTimeEffect`, `IPermanentEffect`) and a base record whose `Kind` was never assigned. The resolution
services switched on the enum, handled two kinds, and silently ignored or threw on the rest. Crit and energy
cost were computed and discarded. Content could declare effects the engine did not implement.

## Decision

Effects are a closed set of sealed records under `Domain/Resources/Effects`, each with validated factories:

- Instant: `Damage`, `Heal`, `EnergyGain` (a positive amount).
- Lasting (become conditions): `Bleed` (amount per round, rounds), `Stun` (rounds), `DefenseBuff` and
  `InitiativeDebuff` (amount, rounds or permanent). Lasting effects carry a `Duration` and a `StackingPolicy`
  (`Stack`, `Refresh`, `Ignore`), defaulting to `Refresh` for bleed and stun and `Stack` for buffs.

There is no kind enum: the type is the kind. Combat rules pattern-match on the concrete records, so a new
effect is a compile-time change everywhere it must be handled. Content declares effects by a `kind` string
that the schema mapper translates; an unknown kind is a build error.

## Consequences

- Good: no dead or half-implemented kinds; the data builder rejects what the engine cannot resolve.
- Bad: adding an effect touches the domain, the mapper, and the rules. That is the point.
- Neutral: `Retaliate` from the prototype is not carried over until a rule defines it.

## Alternatives considered

- Keep the enum with a payload record: one switch per rule anyway, with a runtime default branch to forget.
- Data-driven effect scripts: far more than the game needs now.

## Follow-up

- Phase 6 implements resolution for each record; the glossary lists the kinds.
