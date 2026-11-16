# 2026-09-30. Every tier-2 package teaches two spells, five tier-3 spells are raised, Lightning Bolt crits less, and a match is 8 to 14 rounds

- **What changed.**
  - #245 (content `8b842137`) gave each of the nine tier-2 packages a second spell: Shield Bash, Shadowstep,
    Extort, Reckless Swing, Frostbite, Soothing Chant, Bone Ward, Latch and Adrenaline Tonic. It also gave
    Meteor a 1-round bleed of 1 on each target and turned Ice Spear's slow into a one-round stun.
  - #245 cut Tranquilizer Dart's stun to one round and gave it a bleed of 1 for 2 rounds, so that Ice Spear does
    not strictly dominate it.
  - #246 raises Soothing Chant's regeneration from 1 to 2 a round.
  - #246 raises five tier-3 spells: Restorative Burst heals 6 (was 4), Hateful Sacrifice deals 11 and costs its
    caster 3 (was 10 and 4), Mortal Wound's bleed of 4 lasts 2 rounds (was 1), Tornado deals 5 (was 4), and
    Infectious Blast's permanent shred is 3 (was 2). Restorative Burst's heal bound widens to 6 for this.
  - #246 lowers Lightning Bolt's critical chance from 0.617 to 0.5.
  - #246 records ADR 0086: the exploring run's length band is 8 to 14 rounds (was 10 to 15), and the exploiter's
    clock floor is 8 (was 10).
  - The spells were sized with the `check-knobs` reading the studio now shows, and no tuning ran.
- **The measurement.** `score-content` on engine `ceb54380`, on both seed files. The first three rows use the
  objective as it was (`adr-0065`, targets `baac0add74b0`). The last two use the objective with ADR 0086's
  bands, so they compare with each other and not with the rows above.

  | content | objective | benchmark seeds | confirmation seeds |
  | --- | --- | --- | --- |
  | `813bb91b`, before #245 | before ADR 0086 | 12.84 | 8.91 |
  | `8b842137`, #245 | before ADR 0086 | 11.62 | 17.53 |
  | `ab944cfb`, Soothing Chant at 2 | before ADR 0086 | 7.80 | 12.48 |
  | `ab944cfb`, Soothing Chant at 2 | ADR 0086 | 7.80 | 12.48 |
  | `c8d39320`, plus the tier-3 changes | ADR 0086 | 7.53 | 10.91 |
  | `7de4593b`, plus Lightning Bolt at 0.5 critical | ADR 0086 | **5.26** | **9.02** |

- **What #245 did.** It did not win on the confirmation seeds, so by ADR 0074's rule it was not an improvement
  on its own.
  - Its benchmark-seed gain was mostly the seat: `variety.player1WinShare` went from 0.615 to 0.575.
  - What it bought is choice inside tier 2. On the confirmation seeds' exploring run, Berserker splits 62/83,
    Parasite 43/35 and Necromancer 645/383, where each package had one spell.
  - What it cost is `variety.tierUsageShare`, led by the Shaman: Soothing Chant 25 casts against Healing
    Screech's 399.
- **What #246 did.**
  - Soothing Chant at 2 a round is cast 124 times against 307.
  - The tier-3 changes move Mortal Wound from 29 casts to 133 on the confirmation seeds, Restorative Burst from
    7 to 14, Tornado from 5 to 12, and Infectious Blast from 0 to 8. Hateful Sacrifice stays at 5.
  - The exploring run shortens from 9.88 to 9.75 rounds.
  - ADR 0086's bands change nothing measurable today: the exploring run was already inside both old and new
    bands. They matter for the next pass that shortens matches.
  - The confirmation score is still above the 8.91 it started from, all of it within-package spread:
    `variety.tierDamageSpread` 3.09 and `variety.tierUsageShare` 0.873.
  - The owner keeps the one-round stuns as they are, having played them.
- **Lightning Bolt.** It was cast 6,597 times on the confirmation seeds' exploring run, more than every other
  spell together bar Heavy Strike. Its critical chance was measured at four settings on `c8d39320`:

  | Lightning Bolt | benchmark | confirmation | its casts | Greedy against Random | exploiter's rounds |
  | --- | --- | --- | --- | --- | --- |
  | 0.617 critical | 7.53 | 10.91 | 6,597 | 1.000 | 14.5 |
  | **0.5 critical** | **5.26** | **9.02** | 6,026 | 1.000 | 12.2 |
  | 0.4 critical | 7.50 | 7.87 | 1,272 | 0.917 | 8.4 |
  | 0.3 critical | 4.63 | 5.43 | 934 | 0.877 | 8.4 |
  | 3 damage, 0.617 critical | 7.36 | 4.53 | 787 | 0.860 | 8.4 |

  - At 0.4 and below the spell collapses behind Heavy Strike, and Greedy's edge over Random falls.
  - The best exploiter then closes in 8.4 rounds. That is inside ADR 0086's new floor of 8, and would have
    cost about 1.3 under the old floor of 10.
  - The owner took 0.5. It is the step that improves both seed files without opening that faster line.
- **Worth watching.** Infectious Blast now reads 17.55 a round in the `check-knobs` reading. That reading
  overstates a permanent shred on three targets, and the exploring run still casts it only 8 times.
- **Stamps.** The digests for `8b842137` (from #245) and `c8d39320` are in `benchmarks/`.
