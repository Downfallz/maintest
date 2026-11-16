# 2026-09-13. Opener 7 of 9: Meteor pays for its reach, and buys back a round instead of the spread

- **What changed**: `meteor` hits for **3** a target instead of 4, and its damage bounds go from 3..5 to
  **2..4** — a floor low enough to reach a real per-target discount, a cap that can never match
  `lightning_bolt` on one target. Content `7f1ec9ee` to **`e28be68d`**. Read per round, 12.00 to **9.00**.
- **First, a correction to this journal.** Three entries have called this spell the tier's monopoly. That was
  true when the tier was enabled — 946 casts, the largest damage source, nothing else in the tier — and it has
  not been true for a while. Its share of landed casts is about 8 %, against `heavy_strike`'s 28 %. What it
  was is the **ceiling**, not a monopoly, and the number that says so is `tierDamageSpread`.
- **What was actually wrong**: a sweep that hits each target as hard as a single-target spell of the same
  depth is not a sweep, it is that spell three times. `meteor` dealt 4 a target where `lightning_bolt` deals
  4 to one, so reach cost nothing. Its entry deferred the question — *"its relation to Tornado is the
  decision, not its absolute numbers"* — to a spell a tier deeper and disabled, so it was deferring its
  numbers to one nobody can cast while it became its own tier's ceiling.
- **The change was aimed at the spread and it moved the match length instead**, which is the honest headline:

  | | before | after |
  | --- | --- | --- |
  | `meteor` damage a landed cast | 11.37 | **8.69** |
  | **matches** | 5.8 rounds | **6.7** |
  | `parasite_jab` casts, greedy mirror | 263 | **465** |
  | `protective_slam` | 81 | **141** |
  | `noxious_cure` | 31 | **74** |
  | `tierDamageSpread`, tier 2 | 3.06 | **3.01** |

  Taking a quarter off the biggest damage source lengthened matches by 16 % and spread the casts across the
  tier. `averageRounds` is the objective's most-violated target — band 8..16 — and this is the first change all
  pass to move it the right way.
- **The spread did not move because the ceiling changed hands.** `enraged_charge` now leads at 10.94 damage a
  landed cast, and the floor is `parasite_jab` at 3.64. Both are deliberate: the first is the gamble the
  maintainer chose at opener 3, the second is the weak attack that pays in healing from opener 4. To bring the
  ratio under 2 the ceiling has to fall under 7.3 or the floor rise over 5.5.
- **Which is worth saying plainly: `tierDamageSpread` reads "a deliberately weak attack that pays in another
  currency" as a balance failure.** `parasite_jab` is in the tier's damage comparison because it deals damage,
  and its damage is low on purpose. The same family as the finding that `spellUsageShare` under 0.25 is
  unreachable for an argmax: a target the content cannot satisfy without abandoning a design decision.
