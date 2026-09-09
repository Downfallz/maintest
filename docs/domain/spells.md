# Spells

The 36 spells of `data/Spells`, carried over from the legacy prototype. Vocabulary is defined in
[glossary.md](glossary.md); the effect taxonomy they are written in is
[ADR 0012](../adr/0012-effect-taxonomy.md), the authoring format is `data/README.md`.

Status: **inherited**. The numbers below are the prototype's, not a balance pass. They exist so the engine,
the agents, and the learning loop run on content with some variety instead of 36 copies of the same
one-damage attack. Every one of them is open to change.

## Where they come from

The source is `legacy/DownfallArena/DA.GameResources/Spells/*.cs`, one C# class per class of creature, one
method per spell (`legacy/README.md`). Its spell model is not ours:

| Legacy | Meaning | Here |
| --- | --- | --- |
| `EffectType.Direct` + `Stats.Damage` | damage the targets now | `Damage` |
| `EffectType.Temporary` + `Stats.Damage`, `Length` | damage the targets each round | `Bleed` (`amountPerRound`, `durationRounds`) |
| `EffectType.Direct` + `Stats.Health` | heal the targets now | `Heal` |
| `EffectType.Direct` + `Stats.Energy` | give the targets energy | `EnergyGain` |
| `EffectType.Direct` + `Stats.Defense` | raise the targets' defense for good | `DefenseBuff` with `permanent: true` |
| `EffectType.Temporary` + `Stats.Defense`, `Length` | raise it for a few rounds | `DefenseBuff` with `durationRounds` |
| `EffectType.Direct` + `Stats.Stun` | stun the targets | `Stun` |
| `EffectType.Temporary` + `Stats.Initiative`, negative | slow the targets down | `InitiativeDebuff` |
| `SpellType`, `CharacterClass`, `Initiative`, `EnergyCost`, `CriticalChance` | — | the same fields, `null` read as 0 (a Critical chance bonus of 0 moves nothing) |
| `NbTargets` | 1, or 2 and 3 for the sweeps | `targeting.scope` and `maxTargets` |
| `Level` | depth in the talent tree | nothing: the tree in `data/TalentTrees` already says it |

Targeting is a spell-level `origin` here, so it is derived rather than carried: `Enemy` for the offensive
spells, `Ally` for the ones that heal or buff (a creature's own team includes itself), `Self` for the three
that took no target at all.

## What did not survive the translation

The effect taxonomy is closed and every effect applies to the spell's targets. Six legacy ideas have no
counterpart, so they were dropped or approximated. Each is a rule to decide, not an oversight:

- **Effects on the caster** (`SelfDirect`, `SelfTemporary`). A spell hits its targets and nothing else.
  Dropped: Protective Slam's +1 defense on itself, Psycho Rush's -2 defense recoil, Parasite Jab's lifesteal,
  Hateful Sacrifice's 4 self-damage. The last two lose the cost that made the spell a choice.
- **Debuffing a stat other than initiative.** There is no negative `DefenseBuff` and no energy drain.
  Dropped: Noxious Cure's -2 defense on the healed allies, Soul Devourer's -2 energy. Infectious Blast was
  *only* a defense shred, so it is approximated with the one stat debuff the taxonomy has,
  -2 initiative for two rounds.
- **Buffing initiative or critical chance.** Death Squad gave its team +10 initiative and +100% crit for a
  round; both are unrepresentable. It is approximated as the tempo it was meant to buy: 1 energy to each of
  up to three allies.
- **Minions.** The Necromancer banked minions and spent them on Revenant Guards and Crazed Specter.
  Summon Minions is approximated as a resource the other two do use, energy (3 for a cost of 2); the minion
  cost of the other two is dropped.
- **Healing over time.** Healing Screech healed 2 and then 2 more over a round; folded into a single `Heal 4`.
- **Retaliate.** Thundering Seal's damage back on the attacker. Explicitly not carried over until a rule
  defines it (ADR 0012); only its defense half is left.

Two more places where the model forced a hand:

- **Spells with no effect.** Legacy Wait and Momentum did literally nothing. A spell needs at least one
  effect here, so both are `EnergyGain 1` on the caster: pass the round and gather.
- **Passive spells.** `Full Plate` is `SpellType.Passive` in both models, but nothing implements a passive
  yet, so it is a castable, self-targeted, permanent +1 defense. Same for the permanent half of Guard,
  Thundering Seal and Revenant Guards: legacy applied those to the stat for good, and re-casting stacks them,
  exactly as it did there.

One deliberate correction: legacy Poison Slash is `SpellType.Defensive` while dealing damage to an enemy.
That reads as a typo in the prototype; it is `Offensive` here.

## The spells

The crit column is the Critical chance bonus, what the spell adds to its caster's own. Durations are
in rounds.

| Spell | Class | Type | Initiative | Energy | Crit | Targets | Effects |
| --- | --- | --- | --- | --- | --- | --- | --- |
| Wait | Creature | Defensive | 1 | 0 | — | Self | EnergyGain 1 |
| Basic Attack | Creature | Offensive | 1 | 1 | — | Enemy | Damage 1 |
| Heavy Strike | Creature | Offensive | 1 | 2 | — | Enemy | Damage 3 |
| Pummel | Brawler | Offensive | 1 | 1 | 0.667 | Enemy | Damage 2 |
| Guard | Brawler | Defensive | 1 | 1 | — | Ally | DefenseBuff 1 (permanent), DefenseBuff 1 (2r) |
| Protective Slam | Mercenary | Offensive | 1 | 2 | 0.333 | Enemy | Damage 3 |
| Chain Slash | Mercenary | Offensive | 2 | 3 | 0.5 | up to 2 enemies | Damage 5 |
| Thundering Seal | Mercenary | Defensive | 2 | 2 | — | Ally | DefenseBuff 2 (permanent), DefenseBuff 2 (1r) |
| Full Plate | Warlord | Passive | 1 | 0 | — | Self | DefenseBuff 1 (permanent) |
| Restorative Gush | Warlord | Defensive | 2 | 2 | 0.17 | Ally | Heal 6 |
| Crushing Stomp | Warlord | Offensive | 1 | 4 | 0.667 | Enemy | Damage 6, Stun 1r |
| Enraged Charge | Berserker | Offensive | 1 | 3 | — | Enemy | Damage 4, Damage 3 |
| Tornado | Berserker | Offensive | 1 | 2 | 0.33 | up to 3 enemies | Damage 4 |
| Psycho Rush | Berserker | Offensive | 1 | 3 | 0.33 | Enemy | Damage 9 |
| Poison Slash | Scoundrel | Offensive | 1 | 2 | — | Enemy | Damage 2, Bleed 1/r for 1r |
| Throwing Star | Scoundrel | Offensive | 2 | 1 | — | Enemy | Damage 2 |
| Parasite Jab | Leech | Offensive | 1 | 2 | 0.5 | Enemy | Damage 2 |
| Hateful Sacrifice | Leech | Offensive | 3 | 3 | 0.5 | Enemy | Damage 10 |
| Soul Devourer | Leech | Offensive | 2 | 3 | — | Enemy | Damage 3 |
| Momentum | Assassin | Defensive | 3 | 0 | — | Self | EnergyGain 1 |
| Death Squad | Assassin | Defensive | 3 | 2 | — | up to 3 allies | EnergyGain 1 |
| Mortal Wound | Assassin | Offensive | 2 | 3 | 0.5 | Enemy | Damage 4, Bleed 4/r for 2r |
| Noxious Cure | Trickster | Defensive | 1 | 2 | 0.33 | up to 3 allies | Heal 3 |
| Tranquilizer Dart | Trickster | Offensive | 2 | 3 | — | Enemy | Damage 3, Stun 1r |
| Infectious Blast | Trickster | Offensive | 2 | 1 | — | up to 3 enemies | InitiativeDebuff 2 (2r) |
| Lightning Bolt | Sorcerer | Offensive | 1 | 2 | 0.667 | Enemy | Damage 3 |
| Rejuvenate | Sorcerer | Defensive | 1 | 1 | 0.17 | Ally | Heal 3 |
| Meteor | Wizard | Offensive | 1 | 3 | 0.5 | up to 3 enemies | Damage 4 |
| Engulfing Flames | Wizard | Offensive | 1 | 3 | 0.33 | Enemy | Damage 9 |
| Ice Spear | Wizard | Offensive | 2 | 2 | 0.5 | Enemy | Damage 4, InitiativeDebuff 2 (1r) |
| Summon Minions | Necromancer | Defensive | 1 | 2 | — | Self | EnergyGain 3 |
| Revenant Guards | Necromancer | Defensive | 1 | 2 | 0.33 | up to 3 allies | DefenseBuff 2 (permanent), DefenseBuff 2 (1r) |
| Crazed Specter | Necromancer | Offensive | 1 | 3 | 0.33 | up to 3 enemies | Damage 6 |
| Healing Screech | Shaman | Defensive | 1 | 2 | 0.5 | Ally | Heal 4 |
| Toxic Waves | Shaman | Offensive | 2 | 3 | 0.33 | up to 3 enemies | Damage 3, Bleed 2/r for 1r |
| Restorative Burst | Shaman | Defensive | 2 | 2 | — | Ally | Heal 3, EnergyGain 2 |

Mortal Wound is the one number that is a reading rather than a copy: legacy wrote its second half as a
`Direct` damage of 4 carrying a `Length` of 2, which that engine ignored, resolving it as a second instant
hit. The name and the length say a lasting wound, so it is a bleed here.

## Open questions

- Effects on the caster: lifesteal, recoil, and self-buffs are a third of what the prototype's spells did.
  Either the taxonomy grows a caster-side effect, or those spells stay half of themselves.
- Defense and energy debuffs, and initiative and critical buffs: the taxonomy only debuffs initiative and
  only buffs defense, which is why three spells are approximations.
- Passive spells: `SpellType.Passive` exists and does nothing. A passive is an always-on modifier the
  creature never spends an activation on.
- Minions as a second resource, or the Necromancer keeps paying in energy.
- The permanent stat buffs stack every time they are cast, unbounded, as they did in the prototype. That is
  probably not what anyone wants at a round cap of 30.
