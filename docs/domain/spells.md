# Spells

The 36 spells of `data/Spells`, carried over from the legacy prototype. Vocabulary is defined in
[glossary.md](glossary.md); the effect taxonomy they are written in is
[ADR 0012](../adr/0012-effect-taxonomy.md), extended by
[ADR 0019](../adr/0019-regeneration-the-healing-counterpart-of-bleed.md),
[ADR 0020](../adr/0020-energy-regeneration-and-the-price-of-energy.md),
[ADR 0035](../adr/0035-lowering-defense-and-taking-energy.md) and
[ADR 0036](../adr/0036-raising-initiative-the-mirror-that-was-left-out.md); the authoring format is `data/README.md`.

Status: **inherited**. The numbers below are the prototype's, not a balance pass. They exist so the engine,
the agents, and the learning loop run on content with some variety instead of 36 copies of the same
one-damage attack. Every one of them is open to change.

**And several have changed.** This table is the record of what the port brought over, so a tuning pass
([ADR 0021](../adr/0021-tune-the-catalogue-with-a-declared-search-space.md)) does not rewrite it — it would
erase the only account of where the content started. `data/Spells` is where the numbers a build reads live,
`data/balance/knobs.json` says which of them a pass may move and what each spell is for, and
`docs/learning/journal.md` records every move with the reason. The rows below that differ from `data/` differ
because a pass moved them; read them as history, never as the catalogue.

## Where they come from

The source is `legacy/DownfallArena/DA.GameResources/Spells/*.cs`, one C# class per class of creature, one
method per spell (`legacy/README.md`). Its spell model is not ours:

| Legacy | Meaning | Here |
| --- | --- | --- |
| `EffectType.Direct` + `Stats.Damage` | damage the targets now | `Damage` |
| `EffectType.Temporary` + `Stats.Damage`, `Length` | damage the targets each round | `Bleed` (`amountPerRound`, `durationRounds`) |
| `EffectType.Direct` + `Stats.Health` | heal the targets now | `Heal` |
| `EffectType.Direct` + `Stats.Energy` | give the targets energy | `EnergyGain` |
| `EffectType.Direct` + `Stats.Energy`, negative | take energy off the targets | `EnergyDrain` (ADR 0035) |
| `EffectType.Direct` + `Stats.Defense` | raise the targets' defense for good | `DefenseBuff` with `permanent: true` |
| `EffectType.Temporary` + `Stats.Defense`, `Length` | raise it for a few rounds | `DefenseBuff` with `durationRounds` |
| `EffectType.Direct` or `Temporary` + `Stats.Defense`, negative | lower it, for good or for a few rounds | `DefenseDebuff`, `permanent: true` or `durationRounds` (ADR 0035) |
| `EffectType.Direct` + `Stats.Stun` | stun the targets | `Stun` |
| `EffectType.Temporary` + `Stats.Initiative` | speed the targets up | `InitiativeBuff` with `durationRounds` (ADR 0036) |
| `EffectType.Temporary` + `Stats.Initiative`, negative | slow the targets down | `InitiativeDebuff` |
| `SpellType`, `CharacterClass`, `EnergyCost`, `CriticalChance` | — | the same fields, `null` read as 0 (a Critical chance bonus of 0 moves nothing) |
| `Initiative` | summed over a character's unlocked spells to *be* its initiative | nothing: a pick buys a package and the package pays one bonus (ADR 0056), so the per-spell number is gone (ADR 0059) |
| `NbTargets` | 1, or 2 and 3 for the sweeps | `targeting.scope` and `maxTargets` |
| `Level` | depth in the talent tree | nothing: the tree in `data/TalentTrees` already says it |

Targeting is a spell-level `origin` here, so it is derived rather than carried: `Enemy` for the offensive
spells, `Ally` for the ones that heal or buff (a creature's own team includes itself), `Self` for the three
that took no target at all.

## What did not survive the translation

The effect taxonomy is closed and every effect applies to the spell's targets. Six legacy ideas had no
counterpart, so they were dropped or approximated; two have since been recovered and two half-recovered.
Each is a rule to decide, not an oversight:

- ~~**Effects on the caster**~~ (`SelfDirect`, `SelfTemporary`). Recovered: a spell may carry `casterEffects`,
  resolved once per cast against whoever cast it (ADR 0031), so Protective Slam's +1 defense on itself and
  Hateful Sacrifice's 4 self-damage are expressible — and Hateful Sacrifice now carries it, re-authored when
  its tier opened. **One is still not**, and it is not the place that is missing: a caster effect is a new
  *place* to put an effect, never a new *kind*. Parasite Jab's lifesteal is a share of the damage dealt,
  which depends on the resolution rather than on the spell; it carries a **flat heal of 3 on its caster**
  instead, the approximation and the first content to use the mechanism. Psycho Rush's recoil needed the place
  *and* a kind that could lower a stat; it has both now, and carries `DefenseDebuff 2 (1r)` on its own caster.
- ~~**Debuffing a stat other than initiative.**~~ Recovered: ADR 0035 added `DefenseDebuff` and `EnergyDrain`,
  the mirrors of the buff and the gain, and all four spells that were waiting on them say what they meant.
  Soul Devourer tears **2 energy** out of what it hits again, beside the hit and the lifesteal. Infectious
  Blast is the **permanent -2 defense on the whole enemy line** it always was. Noxious Cure shreds **2 defense
  for a round** off the allies it heals, so the cure is noxious to the cured in the stat legacy charged.
  Psycho Rush is the fourth, above. The substitution they shared — whatever a spell meant to take, it took
  tempo instead — had been used three times, and once ADR 0032 priced a point of initiative at 2.1 it was not
  a neutral translation: Infectious Blast read 25.20 a round on that stand-in and 11.70 on its own.
- **Buffing initiative**, ~~or critical chance~~. Half recovered: ADR 0036 added `InitiativeBuff`, the mirror
  `InitiativeDebuff` never had, so Death Squad's team haste is back — **+2 initiative for a round** on up to
  three allies rather than legacy's +10, because our creatures start at 5 and a point of initiative is priced
  at 2.1 (ADR 0032). It had been approximated as the tempo it was meant to *buy*, 1 energy an ally, and energy
  was 0.2 a point at the time: the substitution read 0.60 a round and the spell was cast 0 times in 400
  matches. Energy is 0.3 a point since ADR 0037, which would have read 0.90 — the same dead spell. **The
  critical half is still out**, and is the one thing here that is not a mirror: `CriticalChance` belongs to a
  creature and a spell, is read once at resolution, and is a probability rather than a quantity. A condition
  that changes it is a new shape, and so a decision of its own.
- **Minions**, ~~and what spending one costs~~. Half recovered. The Necromancer banked minions and spent them
  on Revenant Guards and Crazed Specter; legacy carries `MinionsCost = 1` on both, and the port dropped it.
  There is still no minion **bank** — nothing counts them, and a cast never runs out — but what spending one
  costs is now said in the currency this class already pays in: **3 health off the caster** on each of the two
  (ADR 0031). Both were above the tier-3 band with nothing standing between them and their rivals; they are
  inside it now, and neither energy price moved.
  Summon Minions itself was first approximated as the resource the children do use, energy, which made it a
  spell that paid for casts nobody could make. Armour spread over the team was tried next and read as a smaller
  `revenant_guards`, which is the spell it is meant to open rather than rehearse. It is now **what a summoning
  costs and when it pays**: nothing lands on the cast, the minions gnaw at the whole enemy line over the three
  rounds that follow, and raising them takes **2** of the summoner's own health. It is the only spell whose
  damage is entirely deferred; it is no longer the only one charged to its caster's health, and that is the
  point — the whole class pays in blood, and Hateful Sacrifice does too.
- ~~**Healing over time.**~~ Recovered: `Regeneration` was added to the taxonomy (ADR 0019) and Healing
  Screech is the prototype's `Heal 2` plus a regeneration again. The regeneration is now `3` a round for two
  rounds rather than `2` for one: at the prototype's numbers the spell healed 4 for 2 energy, exactly what
  `rejuvenate` heals for the same price one tier earlier, and delivered later — a tier-2 pick that bought
  nothing. Three quarters of it is now the part you have to buy before the damage lands.
- **Retaliate.** Thundering Seal's damage back on the attacker. Explicitly not carried over until a rule
  defines it (ADR 0012); only its defense half is left.

Two more places where the model forced a hand:

- **Spells with no effect.** Legacy Wait and Momentum did literally nothing. A spell needs at least one
  effect here, so Wait is `EnergyGain` on the caster: pass the round and gather. Momentum was first
  `EnergyRegeneration`, the kind ADR 0020 added and named it for: Wait's opposite trade, energy built over the
  next few rounds. At 0.3 a point that tops out around 2.40 an activation against an attack's 6 and up, and no
  agent cast it. **Since ADR 0078 Momentum is a free strike that gathers energy**: no cost, Damage 2 on one
  enemy and `EnergyGain` 2 on its caster, Wait with a blade in it. No spell carries `EnergyRegeneration`
  now; the kind stays in the engine.
- **Passive spells.** `Full Plate` is `SpellType.Passive` in both models, but nothing implements a passive
  yet, so it is a castable, self-targeted, permanent defense buff. Legacy made it free; here it is priced,
  because free plus permanent plus re-castable is bounded by nothing but the round cap (`check-knobs` reports
  that pair, and `data/balance/knobs.json` says why its cost may not fall back to zero). Same for the
  permanent half of Guard, Thundering Seal and Revenant Guards: legacy applied those to the stat for good,
  and re-casting stacks them, exactly as it did there.

One deliberate correction: legacy Poison Slash is `SpellType.Defensive` while dealing damage to an enemy.
That reads as a typo in the prototype; it is `Offensive` here.

## The spells

The crit column is the Critical chance bonus, what the spell adds to its caster's own. There is no initiative
column: a spell buys none of its own, and the package that teaches it pays one bonus for the whole purchase
(ADR 0056, ADR 0059). Durations are in rounds.

A **†** marks a row that is no longer the prototype's spell because a decision changed what it is, rather than
a tuning pass changing what it is worth. Its ADR says why.

| Spell | Class | Type | Energy | Crit | Targets | Effects |
| --- | --- | --- | --- | --- | --- | --- |
| Wait | Creature | Defensive | 0 | — | Self | EnergyGain 1 |
| Basic Attack | Creature | Offensive | 1 | — | Enemy | Damage 1 |
| Heavy Strike | Creature | Offensive | 2 | — | Enemy | Damage 3 |
| Pummel | Brawler | Offensive | 1 | 0.667 | Enemy | Damage 2 |
| Guard | Brawler | Defensive | 1 | — | Ally | DefenseBuff 1 (permanent), DefenseBuff 1 (2r) |
| Protective Slam | Mercenary | Offensive | 2 | 0.333 | Enemy | Damage 3 |
| Chain Slash | Mercenary | Offensive | 3 | 0.5 | up to 2 enemies | Damage 5 |
| Thundering Seal | Mercenary | Defensive | 2 | — | Ally | DefenseBuff 2 (permanent), DefenseBuff 2 (1r) |
| Full Plate | Warlord | Passive | 0 | — | Self | DefenseBuff 1 (permanent) |
| Restorative Gush | Warlord | Defensive | 2 | 0.17 | Ally | Heal 6 |
| Crushing Stomp | Warlord | Offensive | 4 | 0.667 | Enemy | Damage 6, Stun 1r |
| Enraged Charge | Berserker | Offensive | 3 | — | Enemy | Damage 4, Damage 3 |
| Tornado | Berserker | Offensive | 2 | 0.33 | up to 3 enemies | Damage 4 |
| Psycho Rush | Berserker | Offensive | 3 | 0.33 | Enemy | Damage 9 |
| Poison Slash | Scoundrel | Offensive | 2 | — | Enemy | Damage 2, Bleed 1/r for 1r |
| Throwing Star † | Scoundrel | Offensive | 2 | — | up to 2 enemies | Damage 3 |
| Parasite Jab | Leech | Offensive | 2 | 0.5 | Enemy | Damage 2 |
| Hateful Sacrifice | Leech | Offensive | 3 | 0.5 | Enemy | Damage 10 |
| Soul Devourer | Leech | Offensive | 3 | — | Enemy | Damage 3 |
| Momentum † | Assassin | Offensive | 0 | — | Enemy | Damage 2; caster: EnergyGain 2 |
| Death Squad | Assassin | Defensive | 2 | — | up to 3 allies | EnergyGain 1 |
| Mortal Wound | Assassin | Offensive | 3 | 0.5 | Enemy | Damage 4, Bleed 4/r for 2r |
| Noxious Cure | Trickster | Defensive | 2 | 0.33 | up to 3 allies | Heal 3 |
| Tranquilizer Dart | Trickster | Offensive | 3 | — | Enemy | Damage 3, Stun 1r |
| Infectious Blast | Trickster | Offensive | 1 | — | up to 3 enemies | InitiativeDebuff 2 (2r) |
| Lightning Bolt | Sorcerer | Offensive | 2 | 0.667 | Enemy | Damage 3 |
| Rejuvenate | Sorcerer | Defensive | 1 | 0.17 | Ally | Heal 3 |
| Meteor | Wizard | Offensive | 3 | 0.5 | up to 3 enemies | Damage 4 |
| Engulfing Flames | Wizard | Offensive | 3 | 0.33 | Enemy | Damage 9 |
| Ice Spear | Wizard | Offensive | 2 | 0.5 | Enemy | Damage 4, InitiativeDebuff 2 (1r) |
| Summon Minions | Necromancer | Defensive | 2 | — | Self | EnergyGain 3 |
| Revenant Guards | Necromancer | Defensive | 2 | 0.33 | up to 3 allies | DefenseBuff 2 (permanent), DefenseBuff 2 (1r) |
| Crazed Specter | Necromancer | Offensive | 3 | 0.33 | up to 3 enemies | Damage 6 |
| Healing Screech | Shaman | Defensive | 2 | 0.5 | Ally | Heal 2, Regeneration 2/r for 1r |
| Toxic Waves | Shaman | Offensive | 3 | 0.33 | up to 3 enemies | Damage 3, Bleed 2/r for 1r |
| Restorative Burst | Shaman | Defensive | 2 | — | Ally | Heal 3, EnergyGain 2 |

Mortal Wound is the one number that is a reading rather than a copy: legacy wrote its second half as a
`Direct` damage of 4 carrying a `Length` of 2, which that engine ignored, resolving it as a second instant
hit. The name and the length say a lasting wound, so it is a bleed here.

## Open questions

- Proportional effects: lifesteal is a share of the damage a cast actually dealt, so it reads the resolution
  rather than the spell — the crit, the armour that absorbed it, the targets already dead. A fixed effect on
  the caster exists now (ADR 0031) and a proportional one would be a new kind carried in the same list.
  Whether Parasite Jab keeps a flat heal or gets the real thing is open.
- Defense and energy debuffs, and initiative and critical buffs: the taxonomy only debuffs initiative and
  only buffs defense, which is why three spells are approximations.
- Passive spells: `SpellType.Passive` exists and does nothing. A passive is an always-on modifier the
  creature never spends an activation on.
- Minions as a second resource, or the Necromancer keeps paying in energy.
- ~~Whether a starting Spell should also give its Spell initiative.~~ Moot: no Spell gives one (ADR 0059).
  Two Creatures that know the same Spells can still differ in Initiative, but now by the packages they bought
  rather than by which Spells they happened to start with.
- ~~What a point of Initiative is worth.~~ Settled: the heuristic agents price a purchase as its combat value
  plus `w.initiative` times the package's bonus, and that weight was swept alone and moved from 0.5 to 2.1
  (ADR 0032). It is worth about four times what ADR 0018 guessed. What is *not* settled is whether 2.1 still
  holds now that the bonus arrives as one lump of 1 to 5 rather than spell by spell: it was measured against
  the old shape, and re-measuring it is what #170 asks for.
- Whether the 21 package bonuses are right. `scripts/build-tiers.py` seeded each one by summing the per-spell
  numbers it replaced, which is a migration baseline and not a balance argument (ADR 0057). Nothing has
  measured them since, and no knob addresses them yet (ADR 0059).
- ~~What `throwing_star` is for.~~ Settled: it buys **reach** (ADR 0060). A pick takes both halves of
  `tier:prowler:v1` at once, so the two must differ in what a player does with them and not only in how much
  they do — `poison_slash` concentrates, `throwing_star` spreads, and they are priced level so the decision is
  the shape of the damage rather than its cost. What made the question urgent was the measurement: the package
  split its casts 1813 to 140, because both spells carried the same damage per energy and the smaller one's
  only edge, costing 1, is worth nothing in a game where 1.0 % of its owner's recorded states hold less than 2
  energy.
- The permanent stat buffs stack every time they are cast, unbounded, as they did in the prototype. That is
  probably not what anyone wants at a round cap of 30.
