# 0017. Unlocking a spell raises the creature's initiative

Date: 2026-09-09

Status: Accepted

## Context

A `Spell` has carried an `Initiative` since the game resources were modelled, and no rule in this engine has
ever read it. The prototype did, and read it as the rule this ADR proposes: `CharacterTalentStatsHandler`
computed `stats.Initiative = unlockedSpells.Sum(x => x.Initiative)` and `Character.Initiative` was that sum
plus a `BonusInitiative` the conditions moved — a character had no initiative of its own at all, only what its
unlocked spells gave it. The clean slate kept the field on `Spell`, gave Creature definitions a
`baseInitiative`, and wired the Combat timeline to the Creature's own initiative, built in Planning before any
Intent exists (ADR 0010). The Spell's half was never reconnected, so the field has been inert data the content
audit reports on and no rule uses. Instantiating the 36 prototype spells put real numbers in it, 1 to 3, which
made the gap visible rather than theoretical: the catalogue looks as if it varies turn order and does not.
Either the field earns a meaning or it goes.

## Decision

We will pay a Spell's initiative once, when a Creature unlocks the Spell, as a permanent raise to that
Creature's **Base initiative** for the rest of the Match. The Spell's stat is named **Spell initiative**. A
Creature's base starts at its definition's and only grows; `CurrentInitiative`, which the timeline orders on,
is that base less the active initiative debuffs, so a debuffed Creature still reads a base that says what it
unlocked. `Creature.UnlockSpell` takes the whole `Spell` rather than its id, and both values sit on the
snapshot, so projections, traces and the viewer can show the gap. A refused unlock — a Spell already known,
or a dead Creature — raises nothing. Starting Spells do not pay: a Creature definition's `baseInitiative` is
authored knowing its starting kit, so counting the kit again would be double payment. The heuristic agents
gain an `initiative` weight and score an unlock as its combat value plus that weight times the Spell
initiative, so a pick taken for tempo is priced rather than ignored.

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
- Good: one price for one point of initiative. The `InitiativeDebuff` a spell puts on an enemy moved from
  `w.buff` to `w.initiative` with it, so the agents cannot value taking a point off an enemy differently from
  buying one, which they would have the first time `search-weights` ran.
- Bad: the `initiative` weight is a guess. It is set at 0.5, priced like a defense buff, on no evidence
  beyond the reasoning that initiative is indirect, and `search-weights` has never seen it. It also moves the
  weights fingerprint, so every heuristic agent spec is a new version.
- Neutral: nothing in the content changes shape.
- Neutral: the observation is unchanged, so the feature schema stays `features:v1`. The base is the current
  initiative plus the `InitiativeDebuff_amount` feature, which sums the active debuffs — except where those
  debuffs floor the current one at zero, and the base is no longer recoverable. Publishing a
  `base_initiative` feature for that corner is a `features:v2`, and it was not worth invalidating the run
  history for; `docs/learning/features.md` records the decision.
- Neutral: the raise is unbounded, like the permanent defense buffs. A Creature that unlocks a whole class
  line gains 6 or 7 on a base of 5, which may be too much; that is a numbers question, not a rule question.

## Alternatives considered

- Order the Combat timeline by the declared Spell's initiative: the honest reading of the prototype's field,
  but the timeline is built before Intents are declared and Intents are hidden until revealed, so it would
  mean rebuilding both the round's phase order (ADR 0010) and the hidden-intent rule.
- Count every known Spell, starting kit included: this is what the prototype did, and it was coherent there
  because a character had no base of its own — the sum *was* its initiative. Here a Creature definition
  carries a `baseInitiative` authored knowing the kit, so summing the kit again double-pays it, and it shifts
  every creature identically at spawn, which changes no relative order and only inflates the number. The
  alternative worth revisiting is the prototype's whole shape: drop `baseInitiative` and let the unlocked
  spells be the initiative. That is a content redesign, not a rule change, and it is out of scope here.
- Delete `SpellStats.Initiative`: the smallest change, and it throws away a lever the game wants.

## Follow-up

- `docs/domain/glossary.md`: `Spell initiative` added, `Evolution`, `Creature` and `Spell stats` updated.
- `docs/domain/game-rules.md`: the Evolution sub-phase states the raise.
- `docs/domain/spells.md`: the mapping table and the open questions.
- `src/DownfallArena.Application/Content/ContentAudit.cs`: the flat-stat finding for `initiative` describes
  the unlock, not a cast.
- `docs/learning/agents.md`, `learning/weights/greedy.json` and `learning/src/downfall_learning/export.py`:
  the `initiative` weight, its default and its name list.
- `viewer/index.html` and `studio/studio.js`: the viewer shows "I 4 of 8" when a debuff pulls a Creature off
  its base, and the studio's spell field is labelled Spell initiative.
- Not renamed: the glossary calls the Spell's stat *Spell initiative*, and the code leaves it
  `SpellStats.Initiative`. The type already carries the qualifier — the same class holds `CriticalChance` for
  the glossary's *Critical chance bonus* — so `SpellStats.SpellInitiative` would stutter and would have to be
  matched by a `SpellCriticalChanceBonus` to stay consistent. The Creature's side is where the ambiguity was,
  and there the names are explicit: `BaseInitiative` and `CurrentInitiative`, with the definition's block now
  private.
- `benchmarks/`: a new digest for the new outcomes, with an entry in `docs/learning/journal.md`. That entry
  must supersede the bullet of 2026-09-09 that reads "`spell.Stats.Initiative` is dead data": true of the
  engine when it was written, false from this change on.
