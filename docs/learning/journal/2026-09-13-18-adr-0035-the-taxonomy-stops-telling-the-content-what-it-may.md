# 2026-09-13. ADR 0035: the taxonomy stops telling the content what it may mean

- **What changed**: two effect kinds, `DefenseDebuff` and `EnergyDrain`, the mirrors of `DefenseBuff` and
  `EnergyGain` (ADR 0035), and the four spells that were waiting on them. `soul_devourer` tears **2 energy**
  out of what it hits, with its caster heal down 5 to 4; `infectious_blast` is the **permanent -2 defense on
  all three enemies** it always was; `noxious_cure` takes **2 defense for a round** off the allies it heals
  instead of slowing them; `psycho_rush` carries its **-2 defense recoil on its own caster** and stops being
  half a spell. Content `2e85d5af` to **`4b50a377`**. Feature schema **`features:v3` to `features:v4`** — a new
  condition kind is a new layout, so no run recorded before this is comparable with one after it.
- **The substitution had been used three times and it was the same one every time**: whatever a spell meant to
  take, it took tempo instead. That was neutral while a point of initiative was priced at 0.5. ADR 0032
  measured it at **2.1**, and from then on every spell pushed onto that stand-in became a tempo spell whether
  or not tempo was its idea. `infectious_blast` read **25.20 a round**, the largest number in the catalogue,
  purely from the substitution; as the defense debuff it always was it reads **11.70**.
- **Two `check-knobs` findings went away without a single bound moving.** `revenant_guards` was reported as
  having no bounds that could make it a choice beside `infectious_blast` at 25.20, and `psycho_rush` as
  strictly better than `engulfing_flames` -- which it was only because its recoil was missing. 14 findings to
  12. `death_squad`'s is still reported and now names `revenant_guards` at 15.60 instead, so that one is its
  own bounds and was never about the substitution. A finding can be a missing half rather than a wrong number.
- **The spell nobody could cast woke up.** `tranquilizer_dart` landed **1 cast in 400 matches before and 23
  after**: `infectious_blast` at one energy was strictly better tempo, and it was taking the Trickster's whole
  budget. `noxious_cure` goes **38 casts to 141** and 0.412 to 0.543 — its bargain is payable now that the
  price is 1.30 an ally in defense instead of 4.20 in tempo. `infectious_blast` itself goes the other way,
  290 casts to 89, and its win share **0.262 to 0.427**: cast less and winning more is what over-casting a
  spell looks like from the other side.
- **The objective got worse and it is not being chased: 29.04 to 58.92.** Almost all of it is
  `tierDamageSpread` hitting its cap (14.61 to 36.00, the ceiling). The cause is the paragraph above:
  `tranquilizer_dart` now has enough casts to be read at all, at **1.17 damage a cast**, in a tier-3 that also
  holds `crazed_specter` at 15.85. That imbalance was there the whole time; the substitution was hiding it by
  keeping the spell out of the sample. This is 1 of the 18 spells of the rework and the larger tuning pass
  comes after it, so the number is recorded rather than answered.
- **`soul_devourer`'s drain is priced at 0.40 and that reading is wrong.** Energy is 0.2 a point, so tearing
  two out scores like handing two over. Taking two energy off a creature does not cost it two points of
  anything — it costs it the cast it was saving for, and nothing in the scorers reads a cast denied. Its knob
  note says so. It won anyway: 0.628 to **0.707** win share.
- **And the drain finds less than it asks for**, which the new `Drain` column is what says: 205 landed casts
  took **97 energy**, under half of the 2 each one aims at. `Creature.LoseEnergy` takes what is there and a
  greedy bot spends down to nothing, so most casts land on an empty pool. That is the mechanic working, and it
  is the second reason this spell's drain is worth less in play than on paper.
- **The bot cannot see what the debuff does, only what it costs.** `DefensiveScore` is the only term that
  reads the threat a creature faces, and only a `DefenseBuff` reaches it. So nothing in `ActionScorer` knows
  that lowering a defense raises what the next hit takes -- not on the enemy, which is the point of
  `infectious_blast`, and not on `psycho_rush`'s own caster, which is the point of its recoil. ADR 0035
  recorded the pricing as a stand-in; this is the part that is a decision and not a rounding error, and it is
  written where it still governs one, in `ConditionScore`.
- **A review caught what the whole suite missed.** `FeatureSchema.ConditionKinds` and `ObservationBuilder`'s
  amount switch are two lists that must move together, and only one of them moved: every `simulate --record`
  and every policy decision threw the moment a defense debuff landed, with 282 of 282 tests green, because no test had
  ever put one on a board. `ObservationBuilderTests` now asks every published kind for its amount, and the
  message for a kind the schema knows and the builder does not says that rather than "publish a new version",
  which is the sentence that sends a reader to the wrong file.
