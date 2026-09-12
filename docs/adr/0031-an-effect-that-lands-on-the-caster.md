# 0031. An effect that lands on the caster

Date: 2026-09-12

Status: Proposed

## Context

[ADR 0012](0012-effect-taxonomy.md) closed the effect taxonomy and gave a spell one target origin, so every
effect of a cast lands on the same creatures. A third of what the prototype's spells did has no counterpart:
Parasite Jab's lifesteal, Psycho Rush's recoil, Hateful Sacrifice's self-damage, Protective Slam's point of
self-defense. Each was the cost or the reward that made the spell a choice, and `docs/domain/spells.md` has
carried them as an open question since the port. The tier-2 openers make it urgent rather than tidy:
`parasite_jab` is a 2-damage hit for 2 energy that `lightning_bolt` strictly dominates, it was cast 0 times
in 400 matches, and no move inside its declared bounds makes it a choice — because the half of the spell
that justified its price does not exist.

## Decision

We will let a spell carry a second, optional list of effects, `casterEffects`, drawn from the same closed
taxonomy and resolved **once per cast against the actor** rather than against the targets. They resolve only
when the cast resolves, so a fizzle applies none of them; the critical multiplier does **not** reach them,
because a recoil that doubles when the blow lands well is a different idea from the one being added; and a
spell must still carry at least one ordinary effect, so `casterEffects` is a half of a spell and never a
whole one. A caster effect may kill its own caster: the outcome goes through the same rules as any other, and
a spell that can end its caster is a real design and not an error to guard against.

This works because `EffectOutcome` already names the creature it lands on. `CombatExecution` applies every
outcome to `outcome.Target`, and `ActionScorer` signs every term by ownership — so self-damage already scores
against the caster, a heal on the caster already scores for it, and a caster's death already costs
`weights.Kill`. Neither needs a line changed. The one place that assumes effects belong to targets is a single
expression in `ResolutionRules`.

## Consequences

- Good: four spells get back the half that made them a choice, and `parasite_jab` becomes designable.
  A cost paid by the caster is the lever this catalogue most obviously lacks: every price today is energy.
- Good: no new effect kind, no new outcome type, no change to execution or to scoring. The taxonomy stays
  closed and a caster effect is read by everything that already reads an outcome.
- Bad: the knobs tooling reads `document["effects"]` everywhere. A caster heal would be invisible to
  `cast_value` and a self-damage would count as zero when it is worth *negative* — the same class of blind
  spot as the sweep and the per-round reading, and it has to be fixed in the same change or the checks will
  lie about every spell that uses this.
- Bad: one more place for content to be authored wrong, and one more shape the studio's spell editor has to
  offer.
- Neutral: **this is not lifesteal.** A fixed amount on the caster is not a share of the damage dealt, which
  depends on the resolution — the crit, the armour that absorbed it, the targets that had already died — and
  would be a new kind of effect rather than a new place to put one. Parasite Jab gets a flat heal, which is a
  better-behaved approximation than the prototype's and is priced by everything without further work.
- Neutral: once per cast and not once per target hit. A sweep that healed its caster for each of three
  targets would make the reward scale with the board, which is the thing `maxTargets` already does to damage
  and the one number `cast_value` had to be taught to read.

## Alternatives considered

- **A `Self` target origin on an effect rather than a list.** Same resolution, but it puts a targeting
  concept inside the effect records, which are deliberately about magnitude only, and every rule that
  pattern-matches an effect would have to ask where it goes.
- **Proportional effects (true lifesteal) now.** The identity Parasite Jab actually had. It reads the
  resolution rather than the spell, so it needs a new effect kind, a rule for what it is a share *of*, and a
  scorer that can price something it cannot compute until after the roll. Deferred deliberately, and this
  decision does not block it: a proportional effect would be a new kind carried in the same list.
- **Leave the four spells as halves and tune around them.** What has been done since the port. It is why
  `parasite_jab` is dominated with no legal move out, and why `protective_slam` had to be given an identity
  it never had.
- **Effects on an arbitrary third party.** More general and nothing asks for it: every legacy case is the
  caster.

## Follow-up

- `Spell` and its factory, `SpellDto`, `GameSchemaMapper`, the DataBuilder's validation, and the one
  expression in `ResolutionRules` that turns effects into outcomes.
- `learning/src/downfall_learning/knobs.py`: `cast_value` must price a caster effect with its sign, and
  `dominates` must read it as its own axis. A knob path may address `/casterEffects/...`.
- `studio/studio.js` and its Node tests (ADR 0024): the spell editor authors this list.
- `docs/domain/spells.md` ("What did not survive the translation") and `docs/domain/glossary.md`.
- `data/balance/README.md`: what the checks read.
