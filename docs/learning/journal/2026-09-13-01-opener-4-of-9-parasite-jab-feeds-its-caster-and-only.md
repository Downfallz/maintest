# 2026-09-13. Opener 4 of 9: Parasite Jab feeds its caster, and only matters when it is hurt

- **What changed**: `parasite_jab` goes from `Damage 2` to `Damage 3` and gains a **caster `Heal` of 3**, the
  first content in the catalogue to use the mechanism of
  [ADR 0031](../../adr/0031-an-effect-that-lands-on-the-caster.md). Its entry said "placeholder until
  caster-side effects exist"; they exist. Content `113f9acd` to **`5bd398ce`**.
- **The design is in a term nobody authored.** `ActionScorer.HealScore` counts only the health a target is
  *missing*, and the caster is a target of its own caster effect, so this spell is worth **4.50 at full
  health** — below `lightning_bolt`'s 6.47, so it is not cast — and **6.90 once its caster has three points
  to get back**, above it, so it is. A Leech reaches for this when it is hurt and for something else when it
  is not. That is lifesteal's whole feel, and it is emergent from a rule written for healing in general
  rather than designed into this spell.
- **Numbers**, greedy mirror on the benchmark seeds: **0 casts to 257**, declared by 140 sides of 400, and a
  **52.1 % win share against a 51.9 % baseline** — the first opener whose takers win at all. `protective_slam`
  read 37.4 % and `enraged_charge` 51.3 %. Spells cast go from 13 of 18 to **14**.
- **Verified end to end rather than inferred**, in a recorded match: the heal lands marked `onCaster: true`,
  the damage lands marked `false` and takes the critical multiplier while the heal stays at 3 whatever the
  roll — ADR 0031's second decision, read off a real trace. One cast in the sample landed *only* the heal, its
  damage entirely absorbed, which is the spell doing exactly what it is for.
- **Why it is not lifesteal, still.** A share of the damage dealt reads the resolution — the crit, the armour
  that absorbed it, the target that was already dead — not the spell. That is a new kind of effect and remains
  the open question `docs/domain/spells.md` now carries. A flat heal is the approximation, and it is
  better-behaved: it is the same number whether the bite landed or not.
- **What it did not fix**: matches stay at **5.8 rounds** against a band of 8..16, and the five remaining
  openers are still the prototype's. `check-knobs` is down to five findings, none of them this spell's, and
  four of the five are about spells this pass has not reached yet.
