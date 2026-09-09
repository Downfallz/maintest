# 0017. Unlocking a spell raises the creature's initiative

Date: 2026-09-09

Status: Accepted

## Context

A `Spell` has carried an `Initiative` since the game resources were modelled, and nothing has ever read it.
The Combat timeline is built in Planning, from the Speed choices and the Creature's own Initiative, before any
Intent exists (ADR 0010) — so a Spell's initiative cannot order a round it is declared into, and the field
has been inert data the content audit reports on and no rule uses. Instantiating the 36 prototype spells put
real numbers in it, 1 to 3, which made the gap visible rather than theoretical: the catalogue looks as if it
varies turn order and does not. Either the field earns a meaning or it goes.

## Decision

We will pay a Spell's initiative once, when a Creature unlocks the Spell, as a permanent raise to that
Creature's Initiative for the rest of the Match. The stat is named **Spell initiative**.
`Creature.UnlockSpell` takes the whole `Spell` rather than its id and accumulates the raise, and
`CurrentInitiative` becomes the base Initiative plus everything unlocked minus the active initiative debuffs.
A refused unlock — a Spell already known, or a dead Creature — raises nothing. Starting Spells do not pay: a
Creature definition's `baseInitiative` is authored knowing its starting kit, so counting the kit again would
be double payment.

## Consequences

- Good: the field has a rule, and it is one the round order can actually see, since the raise is in place
  before the next timeline is built.
- Good: Evolution gains a second axis. Unlocking is no longer only "what can I cast" but "how soon do I act",
  and a Player can buy tempo with a pick.
- Good: the numbers already in `data/` start doing something, with no content change.
- Bad: it moves every benchmark outcome. The digest must be regenerated with a journal entry, and the
  balance work on the spell catalogue restarts from the new numbers.
- Bad: two Creatures that know the same Spells can differ in Initiative, because one started with a Spell the
  other unlocked. That is a real inconsistency, accepted here to avoid the double payment.
- Bad: the heuristic agents do not see the new lever. `ActionScorer.Estimate` prices an evolution pick by
  what the spell does in combat, so a pick bought for tempo scores as if it bought nothing, and `Greedy`
  will under-rate initiative until the scoring weights grow a term for it.
- Neutral: nothing in the content changes shape. The learning features read `CurrentInitiative` already.
- Neutral: the raise is unbounded, like the permanent defense buffs. A Creature that unlocks a whole class
  line gains 6 or 7 on a base of 5, which may be too much; that is a numbers question, not a rule question.

## Alternatives considered

- Order the Combat timeline by the declared Spell's initiative: the honest reading of the prototype's field,
  but the timeline is built before Intents are declared and Intents are hidden until revealed, so it would
  mean rebuilding both the round's phase order (ADR 0010) and the hidden-intent rule.
- Count every known Spell, starting kit included: consistent between two Creatures that know the same Spells,
  but it double-pays the base stat block and shifts every creature identically at spawn, which changes no
  relative order and only inflates the number.
- Delete `SpellStats.Initiative`: the smallest change, and it throws away a lever the game wants.

## Follow-up

- `docs/domain/glossary.md`: `Spell initiative` added, `Evolution`, `Creature` and `Spell stats` updated.
- `docs/domain/game-rules.md`: the Evolution sub-phase states the raise.
- `docs/domain/spells.md`: the mapping table and the open questions.
- `src/DownfallArena.Application/Content/ContentAudit.cs`: the flat-stat finding for `initiative` describes
  the unlock, not a cast.
- `benchmarks/`: a new digest for the new outcomes, with an entry in `docs/learning/journal.md`.
