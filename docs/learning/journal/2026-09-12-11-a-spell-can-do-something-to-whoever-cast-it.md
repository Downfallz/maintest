# 2026-09-12. A spell can do something to whoever cast it

- **What changed**: the mechanism of [ADR 0031](../../adr/0031-an-effect-that-lands-on-the-caster.md), across the
  engine, the knobs tooling, the studio and the docs. **No content uses it yet**: the content hash does not
  move — `113f9acd` before and after — and the digest still verifies 400 of 400, because a spell with no
  caster effect is written without the field and an authored empty list is dropped to nothing.
- **Five lines of engine, and the reason is that the architecture already allowed for it.** `CombatExecution`
  applies every outcome to `outcome.Target` and changed nothing. `ActionScorer` signs every term by
  ownership and changed nothing either — which was the ADR's central claim and is now measured: a recoil of 2
  on a hit of 3 scores `0.95 * (3 - 2) + 0.05 * (6 - 2)`, and a recoil that would kill its caster costs the
  kill weight. One expression in `ResolutionRules` assumed effects belong to targets.
- **Two real holes in `ContentAudit`** the change opened, each with a test that fails without its fix:
  `Grants` read the *targeting origin* to decide whether a creature has an energy ceiling, so an offensive
  spell paying its own caster in energy would have slipped past; and `Signature` compared target effects only,
  so two spells alike on their targets and different on their caster read as indistinguishable.
- **The sign is the whole job in the knobs tooling**, as the ADR predicted. A caster heal counts for the spell
  and a recoil counts against it, once per cast and never multiplied by the critical chance. That sign then
  has to run through every reading, and two of them are not obvious:
  - `dominates` treats the caster half as its own axis where **an absent group is a zero, not a gap**. The
    rule that a missing effect disqualifies reads it backwards: carrying no recoil is being better on that
    axis. A test caught this, not a review — the first version had `ATTACK` failing to dominate the same
    spell with a recoil bolted on.
  - the ceiling of a knob that addresses a **harmful** caster effect is its **minimum**. Sent to the maximum
    it would understate the ceiling, and an understated ceiling is exactly how `outclassed` invents a finding
    instead of missing one.
- **What this is not**: lifesteal. A share of the damage dealt reads the resolution rather than the spell and
  is a new kind of effect, deferred on purpose. `docs/domain/spells.md` moves it from "did not survive" to an
  open question about proportional effects.
- **Numbers**: none. Nothing in `data/` moved and no match played differently. The next entry is the one that
  re-authors `parasite_jab` and the three other halved spells, and that one will have numbers.
