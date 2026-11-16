# 2026-09-13. Opener 8 of 9: Summon Minions stops paying for casts nobody can make

- **What changed**: `summon_minions` stops being `EnergyGain 3` on its caster and becomes **`DefenseBuff` 1
  for two rounds on up to three allies**, at the same price of 2. Content `e28be68d` to **`7ac86454`**.
  Cast value 0.60 to **3.90 a round**.
- **It was paying for a line nobody can cast.** Its intent said it "decides how long the Necromancer's
  expensive line takes to come online" — and that line, `revenant_guards` and `crazed_specter`, is a tier
  deeper and disabled. The same defect `meteor` had one entry ago, where the numbers deferred to `tornado`.
  A spell whose job is to enable other spells has no job when they are off.
- **ADR 0020's nomination is declined, with a measurement.** That ADR named this spell and `momentum` as the
  two natural candidates for `EnergyRegeneration`. `momentum` took it one entry ago and the entry records what
  happened: 0 casts at `explore:0.2`, 8 at `explore:0.5`, because energy is 0.2 a point and an energy spell
  tops out near 1.60 an activation against an attack's 6 and up. Giving this one the same treatment would have
  bought a second dead opener. Declining a written nomination needs a reason, and the reason is that one.
- **What it is instead is the class's own idea, one tier early.** The dead stand in front of the living:
  `revenant_guards` is that permanently and twice the size, so the Necromancer now reads as one thought from
  its first pick to its last, and the opener is deliberately the smaller half.
- **Numbers**: **0 casts to 44** on the greedy mirror and **5 to 75** on the exploring run. Spells cast go to
  15 of 18 on the mirror and **17 of 18** on variety, which leaves `momentum` as the only one nothing ever
  casts — `spellsNeverCast` at 1, inside its band of 2 for the first time this pass. The `check-knobs` findings
  drop from five to **four**. Matches hold at 6.7 rounds.
- **The cost, and it is real**: `guard` falls from 156 casts to **50**. A team-wide two rounds of armour is
  simply a better use of an activation than one ally's, and the Brawler's tier-1 answer is what pays for it.
  That is the tree working — deeper beats shallower — but a three-fold fall is worth a look before this tier
  is called done.
