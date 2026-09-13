# Learning journal

One entry per change that moves a number: content, engine, agents, or the benchmark seeds. Each entry names
the run stamps involved so that any two results can be compared on one axis at a time (ADR 0013). Newest
first.

## 2026-09-13. ADR 0035: the taxonomy stops telling the content what it may mean

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

## 2026-09-13. Leech, 4 of 9: two halves the port left behind, one restored and one substituted

- **Checked the legacy source before touching anything, and it held the answer to both spells.**
  `legacy/.../LeechSpells.cs` pairs Hateful Sacrifice's hit of 10 with `SelfDirect Health -4`, and Soul
  Devourer's hit of 3 with `Direct Energy -2` on the target. Only the first halves were ported.
  `docs/domain/spells.md` had already written the instruction: "Psycho Rush and Hateful Sacrifice are still
  halves of themselves, and are **re-authored when their tier is opened**." It is open.
- **What changed**: `hateful_sacrifice` gains `casterEffects: Damage 4` — the sacrifice its name promises;
  `soul_devourer` goes from Damage 3 at a price of 3 to **Damage 5 with a caster heal of 5 at a price of 2**.
  Content `ce613dba` to **`2e85d5af`**.
- **The energy drain is not coming back, and would not help if it did.** There is no negative `EnergyGain`,
  and energy is 0.2 a point — the reading that killed `momentum` and that made ADR 0020's nomination of
  `summon_minions` decline itself. So the theft keeps its meaning and changes its currency, the way
  `infectious_blast` traded a defense shred for tempo: the Leech takes, and what it takes is health.
  **0 casts to 343**, 2.00 a round to 9.00.
- **`hateful_sacrifice` loses its stand-in.** Its second keep read "its price stands in for the missing
  self-damage"; the self-damage is here, so the stand-in is gone. It reads 10.00 a round to **7.33**, just
  under the band, and that is accepted rather than compensated — `cast_value` charges four health in full
  where a bot pays it only in the rounds when four health is what it had left. 621 casts to 420.
- **The alternative put both in the band and cost the class its shape**: damage 11 with the recoil, and 7 with
  a heal of 7 at a price of 3, reads 33.42 against 29.04 and gives `soul_devourer` **626** casts against
  `hateful_sacrifice`'s 269. One spell replacing another, where the chosen pair reads 420 / 343 / 417 across
  the three — a class with three spells in it.
- **And two things `docs/domain/spells.md` claimed that are not true.** It said Psycho Rush's recoil was
  expressible through caster effects: it is **-2 defense**, and `DefenseBuff.Of` refuses anything below 1, so
  there is no negative buff to put anywhere. A caster effect is a new *place* for an effect, never a new
  *kind* — the same reason Parasite Jab's real lifesteal is still out. Psycho Rush is still half of itself,
  and the Berserker entry above buffed it without noticing that its missing half was still missing.

## 2026-09-13. Berserker, 3 of 9: the class whose point is the roll, whose deep spells did not gamble

- **What changed**: `tornado`'s price 2 to **3**; `psycho_rush` damage 9 to **10** and critical chance 0.33 to
  **0.5**; and `crushing_stomp`'s chance back down from 0.8 to **0.75**, one entry after it went up. Content
  `7c8cecf1` to **`ce613dba`**. Objective **41.60 to 26.77**, the largest single drop of this pass.
- **The class's identity was in its opener and nowhere else.** `enraged_charge` carries the highest critical
  chance in the catalogue and its entry keeps that as the thing making it a Berserker spell "rather than an
  expensive hit". Both spells behind it sat at **0.33** — lower than the opener, on a line whose own keep
  reads "the line's gamble". `psycho_rush` now takes 0.5, the top of its bounds, and goes from 7.98 a round to
  **10.00** and from **6 casts to 53**.
- **The entry before this one broke that claim and this one puts it back.** Raising `crushing_stomp` to 0.8
  tied `enraged_charge` exactly. Nothing `crushing_stomp` keeps mentions its chance, so the tie cost the
  Berserker its identity and cost the Warlord nothing: 0.75 reads 9.12 a round against 9.30, and the highest
  chance in the catalogue is one spell's again.
- **`tornado` was the second-largest reading in the catalogue** at 15.96 against a band of 8 to 14, and its own
  intent nominates its price — "the first place to look when matches end too quickly", with matches at 6.4
  rounds under a band of 8. At 3 it reads 10.64.
- **The better number lost on purpose.** Cutting its damage to 3 and keeping the price at 2 reads **7.01
  rounds** and takes `spellsNeverCast` to **0**, against 6.55 and 1 for the price move, and scores 31.93
  against 26.77. It also makes `tornado` a cheaper `meteor` — the same hit on the same three targets, a tier
  deeper. A tier-3 spell that copies a tier-2 one is the defect this pass exists to remove.
- **And another keep that stopped being true when the tier came on**: "the cheapest spell that reaches three
  enemies" — `infectious_blast` costs one. Same shape as `chain_slash`'s two entries ago, and there will be
  more.

## 2026-09-13. Warlord, 2 of 9: the branch nobody walked

- **What changed**: three spells, the whole class. `full_plate` 2 permanent defense to **3**;
  `restorative_gush` Heal 6 to **7** with a critical chance of 0.17 to **0.5**; `crushing_stomp` damage 6 to
  **7**, chance 0.667 to **0.8**, stun one round to **two** — all three at their existing prices. Content
  `31952876` to **`7c8cecf1`**, digest regenerated and verified.
- **The opener had to move, and it is a tier-2 spell in a tier-3 pass.** `full_plate` is the Warlord's gate,
  and at 2 permanent it was declared by **5 sides of 400** on the mirrored run while both of its children were
  cast **zero** times. Nothing could be learned about the two spells this pass was about, because nobody
  arrived to use them: a buff to either would have read zero before and zero after. At 3 the gate is declared
  by 34, and `restorative_gush` and `crushing_stomp` are cast **26** and **55**.
- **Two of the three were capped under their own tier by their own bounds.** `restorative_gush` could reach
  6.55 a round on its heal alone against a band of 8 to 14; `full_plate` tops out at 5.85 against a tier-2
  median of 7.20, which `check-knobs` has been saying for some time. Only `crushing_stomp` had the room, and
  it used it: 6.50 to **9.30** without its price moving, because the price is the spell.
- **A knob an earlier entry claimed to have added was never added.** `restorative_gush`'s note said ADR 0033
  made its critical chance live and "the knob is here for the pass that enables it". It was not there. The
  note also said the spell was disabled and carried no chance; by the time anyone read it, all three sentences
  were false. The knob exists now, and it is what takes the spell into its band — 5.62 to **8.40** — so the
  omission was load-bearing rather than untidy.
- **It costs the objective 1.6**, 39.97 to 41.60, and the alternative measured worse: a one-round stun reads
  45.31 and gets `crushing_stomp` cast 29 times against 55. `spellsBarelyCast` goes 8 to 6. A tuning pass can
  price a branch people walk; it cannot invent one nobody reaches.

## 2026-09-13. Mercenary, 1 of 9: a false claim withdrawn and an armour spell raised to its tier

- **`chain_slash` keeps every number it has.** It reads 10.00 a round and the tier-3 band being aimed at is 8
  to 14, so it is already there. Two candidates that made it bigger — damage 6 at a cost of 4, and damage 6 at
  a critical chance of 0.6 — both measured worse than what is authored. What was wrong was the sentence: its
  entry claimed "the largest cast in the catalogue", true only while the spells that beat it were disabled.
  It puts 10 on the board; `crazed_specter` puts 18 and `tornado` 12 at the same depth. It is now what it
  actually is — the only cast that hits exactly two, the one rung between a spike and a storm.
- **`thundering_seal` goes to the top of its own bounds**: 2 permanent and 2 for a round become **3 and 3 for
  two**, same price of 2. Cast value 5.20 to **9.75** a round. Content `5e9e95c6` to **`31952876`**, digest
  regenerated and verified.
- **Numbers**: 16 casts to **94**, and `spellsNeverCast` **6 to 1** — matches run 5.56 rounds to **6.33**, and
  a longer match buys more evolution picks, so more of the catalogue comes up at all. That second-order effect
  is worth more here than the spell itself.
- **Not the best score on the board, and taken deliberately.** 3 permanent with 2 for two rounds reads 34.33
  against this one's 39.97; the difference is `spellsBarelyCast` going 4 to 8. The tier's problem is its
  floor — ten of eighteen sit under the band — so the shape nearer the tier-3 median of about 10.5 wins over
  the shape that scores better today. A tuning pass can walk it back inside its own bounds; it cannot invent
  the floor.
- **The class has one tension and it is recorded rather than solved**: `protective_slam` says "protection
  through tempo, never armour" and the Mercenary's defensive payoff is pure armour. The taxonomy has no
  initiative *buff*, so protecting an ally through tempo cannot be said at all. And `thundering_seal` is
  `revenant_guards` on one ally instead of three — left for the Necromancer's turn.
- **Note on the baseline**: HEAD reads 36.16 here, not the 41.50 the entry below records. ADR 0034 changed the
  tier reading between them and said scores across it are not comparable. This is that.

## 2026-09-13. A tier is a depth a player climbs, and the tier-3 step is 1.13x

- **What changed**: `_tiers` reads a spell's prerequisites as well as its node (ADR 0034). The catalogue's
  shape goes from 3 / 6 / **27** to 3 / 6 / 9 / **18**. No content moved; scores across this change are not
  comparable, the way ADR 0029 made them incomparable.
- **The entry below called this "not a blocker" and that was wrong.** It was measured the lazy way — the
  values barely move, because each target reports its worst tier — when the test that mattered was whether
  the reading still ranks two candidates the same way. It does not. On two proposals for `chain_slash`,
  `tierDamageSpread` reads 3.360 / 3.662 / 4.164 under node depth and 2.910 / **2.825** / **2.884** under the
  real one: both look worse one way and better the other. The blob holds the openers and the biggest tier-3
  casts together, so any tier-3 buff widens it; split, the same buff is measured against the tier-3 floor and
  narrows it. Eighteen spells were about to be designed against a yardstick that reverses the sign.
- **What the fix makes visible, and it is the number the tier-3 pass needs.** Cast value a round, by tier
  median: 2.00, 4.53, 7.20, **8.12**. The step between tiers is **2.26x, then 1.59x, then 1.13x** — tier 3 is
  barely a tier. And it is the widest: 0.60 to 25.20, a factor of **42**, against 7.6 at tier 2 and 2.2 at
  tier 1.
- **So the pass has two jobs, not one**: raise the median toward roughly 10.5 — what a 1.45x step on 7.20
  would give, holding the decay between the last two steps — and collapse the spread. Against a working band
  of **8 to 14**, tier 3 today is four spells too big (`infectious_blast` 25.2, `tornado` 16.0,
  `crazed_specter` 16.0, `revenant_guards` 15.6), four already inside it (`toxic_waves` 11.2, `ice_spear`
  10.2, `hateful_sacrifice` 10.0, `chain_slash` 10.0) and **ten too small**, ending at `death_squad` 0.6.
- **Which settles the first spell before it was touched.** `chain_slash` reads 10.00 and is already in the
  band; two candidates that made it bigger both measured worse. Its problem was never its size — its knob
  entry claims "the largest cast in the catalogue" and that has been false since `crazed_specter` (18 damage
  on the board) and `tornado` (12) were enabled beside it.

## 2026-09-13. Tier 3 is on, and it costs what enabling a tier costs

- **What changed**: the eighteen spells behind the nine openers are `enabled`. Eighteen files, one flag each,
  no number touched. Content `c1b49503` to **`5e9e95c6`**, 36 spells in the tree, digest regenerated and
  verified 400/400. Objective **4.773 to 41.50**.
- **What it costs, and none of it is a surprise**: `averageRounds` 9.200 to **5.560**, well under its band —
  the new spells are the big ones, and a catalogue that kills faster ends sooner. `spellsNeverCast` 0 to
  **6** and `spellsBarelyCast` 0 to **5**, so twelve of thirty-six are never cast at all. `throwing_star`
  takes **37.6 %** of the mirror's casts, up from 26.1 %. This is the same shape the tier-2 enable had, and
  the pass that follows is what pays it down.
- **Two findings worth having before the spell-by-spell work starts.**
- **The tier reading does not see prerequisites.** `_tiers` walks talent-tree *nodes*, and a class node holds
  its opener and both of its children — they are separated by `prerequisites`, which `_walk` never reads. So
  twenty-seven spells now share "tier 2" where reading the prerequisites gives 3 / 6 / 9 / **18**, the real
  shape. **Smaller than it looks**, and worth writing down because the first reading of it here was wrong:
  each tier metric reports its *worst* tier, so splitting 27 into 9 and 18 moves only `tierDamageSpread`
  (3.360 to 2.910) and leaves `tierUsageShare` and `tierWinSpread` where they were. A real defect, one metric,
  not a blocker.
- **A child is only ever as reachable as its opener**, and that splits the dead into two kinds that need
  different answers. **Dead at the root**: `restorative_gush` and `crushing_stomp` at zero behind `full_plate`
  at 0.06 %, `revenant_guards` behind `summon_minions` at 0.18 %, `toxic_waves` behind `healing_screech` at
  0.41 %. No number on the child moves anything while nobody takes the parent. **Dead on its own merits**:
  `soul_devourer` at zero behind `parasite_jab`, which takes **12.16 %** — the opener thrives and the child
  is refused anyway, at 2.00 a round against its opener's 6.90.
- **And one claim the content makes that the numbers do not support.** `chain_slash`'s knob entry calls it
  "the largest cast in the catalogue". It puts 10 damage on the board (5 over two targets); `crazed_specter`
  puts **18** (6 over three) and `tornado` puts 12, both at the same depth, and `hateful_sacrifice` ties it at
  10. The claim was true when the two spells that beat it were disabled.

## 2026-09-13. The first tuning pass against the measured baseline pays the bill the weight left

- **What changed**: seven numbers, found by `tune-content` on the "Tune the catalogue" workflow (run 6, seed
  0, 24 rounds of 6, 1096 evaluations, 291 catalogues played of 292 handed over). Content `9211421b` to
  **`c1b49503`**, digest regenerated and verified 400/400. Objective **12.954 to 4.773**.

  | Spell | Move |
  | --- | --- |
  | `protective_slam` | energy cost 2 to **3** |
  | `healing_screech` | Spell initiative 1 to **0** |
  | `summon_minions` | caster `Damage` 3 to **2** |
  | `poison_slash` | Spell initiative 1 to **0** |
  | `momentum` | `EnergyRegeneration` 1 to **2** a round |
  | `rejuvenate` | critical chance 0.17 to **0.22** |
  | `noxious_cure` | critical chance 0.33 to **0.28** |

- **`spellsBarelyCast` reaches 0.** ADR 0032 moved the initiative weight and left five spells cast by nobody;
  the entry after it paid one by hand and said the other four were the tuner's to settle. They are settled.
  Sixteen of the eighteen enabled spells are cast on the greedy mirror, `pummel` among them again.
- **`tierWinSpread` is all but solved**: 0.386 to **0.166** against a band of 0.15, a penalty of 0.03 where it
  was 5.59. That reading has been one of the three worst all pass. What is left is `tierUsageShare` at 0.615
  and `tierDamageSpread` at 2.727, and between them they are now 4.75 of the 4.77.
- **Three of the seven moves were illegal or impossible twelve hours ago.** `rejuvenate` and `noxious_cure`
  are critical-chance moves, and `knobs.py` refused a critical chance on a spell that does not damage until
  ADR 0033 made the multiplier reach a direct heal; `summon_minions`' move is on a caster effect, a field ADR
  0031 created and a knob added the same night. The pass found its value in exactly the space those two ADRs
  opened, which is the cleanest argument either of them will get.
- **`protective_slam` is the move both searches found.** A local pass with a much smaller budget (8 rounds of
  4, 179 catalogues) proposed the same cost of 3 and nothing else in common. It read 13.73 a round and
  `check-knobs` had it outclassing three tier-2 spells on paper; at 3 it reads **9.15** and the findings drop
  from **seven to five**. A move two independent searches reach from different seeds is not a fit to one run.
- **The baseline got harder to beat as well**: `exploit` 0.340 to **0.297**, and `skill` holds at 0.998.
  Matches run 9.200 rounds with `roundCapShare` at 0.025 — inside the band with room, where the local pass
  reached 10.885 and spent the whole margin at 0.050.
- **One thing to watch, and it is a real tension.** Two spells took a Spell initiative of **0**:
  `healing_screech` and `poison_slash`. That is the stat that, at a weight of 2.1, is a 2.1-point handicap at
  every unlock against every rival at 1 — and it is exactly the mechanism ADR 0032 recorded as what killed
  `pummel`. The measurement says it works here, both spells are still cast, and a catalogue where Spell
  initiative varies is a catalogue where the stat finally discriminates instead of cancelling out. But it is
  the second time that stat has decided something large, and nobody has yet chosen those 1-to-3 numbers for
  the job they now do. `docs/domain/spells.md` still carries that as an open question.
- **Verified rather than taken on trust**: the content hash rebuilds to `c1b49503`, the digest verifies, and
  the four evaluations replayed here read 4.77 with every target matching the proposal's own table. Every
  move was read against its spell's `keep` clauses and none of them breaks one — the cost, the crits and the
  caster damage leave each identity intact, and `momentum` keeps both the free cast and the Spell initiative
  its entry calls "half of what it is for".

## 2026-09-13. A critical cast heals harder, and the match-length target falls at last

- **What changed**: the critical multiplier now reaches a direct `Heal` on a target (ADR 0033), one word in
  `ResolutionRules`. With it, `healing_screech`'s critical chance goes back to **0.5** and `noxious_cure` stops
  charging the allies it heals **2** points of initiative and charges **1**. Content `d76a1f84` to
  **`9211421b`**. Three spells gain a critical-chance knob; `check-knobs` goes **9 findings to 7**.
- **`averageRounds` reads 8.565.** Band 8..16, penalty **zero**, from 7.000 — and it was 5.8 nine entries ago.
  This target has been violated for the entire life of this journal and it is the reason the objective exists.
  What closed it was healing that can have a good round.
- **Everything outside the three tier readings is now in band.** `player1WinShare` 0.490, `drawRate` 0,
  `roundCapShare` 0, `fizzleRateA` 0.151, `spellEntropyA` **3.496** (best ever), `spellUsageShare` 0.219,
  `spellsNeverCast` 0. `spellsBarelyCast` 4 to 3. Objective **17.89 to 12.95**, which is where it stood before
  the entry below — with every target better than it was then.
- **What it did to the healers.** `healing_screech` **0.2 % of the mirror's casts to 9.9 %** and 12 declaring
  sides to **165**, the tier's best win share at 0.64. `noxious_cure` 0.0 % to 1.0 %, 12 sides to **100**, win
  share 0.58 — from a spell that read -3.00 a round to one people take and win with. `rejuvenate` goes the
  other way, 0.7 % to 0.0 %: the tier-1 heal displaced by the tier-2 one, which is the tree doing its job.
- **This reverses part of the entry below, and the reversal is the interesting bit.** That entry dropped
  `healing_screech`'s chance to 0 on the measured ground that it could not change a score. The measurement was
  right and the conclusion was the wrong way round: a number that does nothing is either decoration to remove
  or a rule to fix, and nothing in that entry asked which. The rule was the answer. The noise floor it
  measured on the way (12.95 against 12.93, and that spell's own casts 12 to 7, on a change that provably
  could not affect a decision) survives and is still the most useful number in it.
- **`noxious_cure` was broken by arithmetic, not by taste.** At 2.1 a point of initiative a slow of 2 costs
  4.2 an ally against a heal of 4 worth 3.2: the cast was worth less than passing. The smallest price the
  taxonomy can say is 1 point for 1 round, 2.1, and that is what it now charges. Its bounds are rebuilt so a
  search can actually work — the heal reaches 5 instead of 4 so it has room to pay, the slow stops at 2
  because 3 is unaffordable at any heal in the box, and the critical chance is a knob. Ceiling 3.30 a round
  to **11.70**.
- **The one thing that got worse**: `exploit` 0.237 to **0.340**. More healing in the game gives an agent with
  different weights more to exploit, and `search-2` picks some of it up. Still well inside its band, and the
  agent is due a refresh anyway.
- **Left open deliberately**: `EnergyGain` does not take the multiplier. Health is what the roll is about and
  energy is a separate economy, so it is named in ADR 0033 as open rather than settled by omission. And
  `healing_screech` at 9.9 % of casts with the tier's best win share is a spell to watch: it is inside its box
  and the tuner can pull it back, but nobody authored it to be the tier's best.

## 2026-09-13. Opener 9 of 9: Healing Screech was a tier-1 heal wearing a tier-2 badge

- **What changed**: `healing_screech`'s regeneration goes from **2 a round for one round to 3 a round for
  two**, and its critical chance from **0.5 to 0**. Same price of 2. Content `74f02621` to **`d76a1f84`**.
  Cast value 3.20 to **6.40 a round**.
- **The defect, in one line**: it healed 4 for 2 energy at tier 2. `rejuvenate` heals 4 for 2 energy at tier
  1. Not merely tied — *behind*, because `healing_screech` delivered half of it next round and a creature that
  dies this round never collects. A Shaman's first pick bought a worse copy of a spell a tier above it. This
  is the same defect the other eight openers had, and it is the last of them.
- **Where the step went is the identity.** The instant half stays at 2 and the regeneration carries the
  increase: 8 health against `rejuvenate`'s 4, with three quarters of it arriving over the next two rounds.
  So it is worth double a tier-1 heal *only if it is cast before the damage*, and on a creature dying this
  round it is still worth 2. The knob entry called it "the anticipation heal, not the emergency one"; now the
  numbers say it too.
- **Numbers**: 12 casts on the greedy mirror to **38**, 12 declaring sides on the exploring run to **49**, and
  its takers' win share 0.583 to **0.694** — the best in tier 2. Matches 6.855 to **7.000**, the closest this
  pass has come to the 8..16 band. `spellsBarelyCast` **5 to 4**: one of the five spells the weight change
  killed, back, and for a design reason rather than a search.
- **The objective still charged 4.96 for it**, 12.95 to 17.89, and for the third time every penny is
  `tierWinSpread` (2.01 to 8.64) while *every other target improves or holds*: rounds, entropy,
  `spellUsageShare`, `tierUsageShare`, `tierDamageSpread`, `spellsBarelyCast`. **The reason is worth writing
  down as a property of the metric, not of the spell.** `tierWinSpread` is max minus min over a tier, so
  raising a weak spell toward the middle helps and raising it past the middle hurts, and the metric cannot
  tell "one spell is too strong" from "one spell is too weak". Tier 2's floor is `full_plate`, and until that
  moves, every improvement to anything else in the tier is billed to whatever improved.
- **A noise floor, measured instead of asserted.** The critical chance of 0.5 could not change a score — the
  multiplier reaches `Damage` and this spell deals none — but it does change the match, because the crit roll
  draws from the shared random source. Removing it *alone* moves the objective 12.95 to 12.93 and this
  spell's own mirror casts 12 to **7**. So: the aggregate targets are stable to about 0.02, and a single
  low-usage spell's cast count can move 40 % on a change that provably cannot affect any decision. Every
  per-spell reading in this journal taken off twenty or thirty declaring sides should be read against that.
- **Two findings handed to the tuning pass rather than fixed here.** `noxious_cure` now reads **-3.00 a
  round**: it puts an `InitiativeDebuff` of 2 on the three allies it heals, and at 2.1 a point that price
  (4.2 an ally) exceeds the heal (3.2 an ally). I sized that bargain against 0.5 three entries ago and the
  weight moved under it. Worse, `check-knobs` says its ceiling is **3.30** at the best corner of its own
  bounds, so **the tuner cannot fix it** — it needs new bounds or a new shape, and that is a decision, not a
  search. And `noxious_cure` (0.33) and `rejuvenate` (0.17) still carry the same decorative critical chance
  this spell just shed.

## 2026-09-13. The initiative weight was a guess for fourteen ADRs, and it was four times too low

- **What changed**: `weights.initiative` goes from **0.5 to 2.1**, swept alone on fixed content `74f02621`
  (ADR 0032). No content moved. Agent fingerprint `a4e83485` to **`93f3683c`**, digest regenerated.
- **The entry below guessed the wrong direction, and said so out loud.** It measured a line-wide initiative
  debuff that Greedy declared 86 times and won 0.279 with, and concluded the scorer over-buys tempo. The sweep
  says the opposite: at 0 and 0.1 the objective reads 50.98 and 40.94 with `player1WinShare` at 0.705 and
  0.680, the worst readings in the sweep. **The bot was losing to tempo because it would not buy it**, and
  buying one bad tempo spell is what a too-low price looks like from the inside. Worth writing down as a
  method note: "the bot picks X and loses with it" does not tell you which way the weight behind X is wrong.
- **`player1WinShare` lands at 0.500.** Band 0.45..0.55, penalty 0, from 0.645. This target has been out of
  band for the whole project and it is the one that says whether going first decides the match. Objective
  **36.66 to 12.95** on unchanged content — the largest single move of this pass, and the only one that came
  from the agent rather than the catalogue.
- **A second measurement the objective does not contain agrees.** `exploit` plays `search-2` — an agent
  searched against the *old* baseline — against this one. Its win share falls **0.390 to 0.177**. A baseline
  that is harder to beat by an exploiter built for its predecessor is not a metric artifact. The fizzle rate
  falls 0.211 to 0.169 and `skill` holds at 0.998 against random.
- **The step, and why 2.1 rather than 2.0.** The readings come in steps because the agents take an argmax.
  1.5 and 1.75 sit on either side of a flip — `throwing_star` goes 1.5 % of casts to 26.5 % — and 2.0, 2.1 and
  2.2 read 12.22, 12.95 and 12.99 with `player1WinShare` 0.490, 0.500 and 0.505. 1.9 and 2.25 are the edges at
  18.51 and 19.58. 2.1 is the middle of the step; 2.0 is the better single number and 0.1 from an edge. Same
  test 0.65 had to pass in ADR 0028.
- **What it costs, and it is a real bill**: `spellsBarelyCast` 0 to **5**. `pummel` 5.0 % of the mirror's casts
  to 0.0 %, `noxious_cure` 0.6 % to 0.0 %, `full_plate` and `healing_screech` under 0.2 % — and
  **`summon_minions`, committed an hour ago, 0.7 % to 0.0 %**. `throwing_star` goes 0.9 % to **30.8 %**, which
  is one spell's stat carrying a weight decision: fifteen of the eighteen enabled spells have a Spell
  initiative of 1, so the unlock term is nearly a flat bonus and what 2.1 really re-prices is `throwing_star`
  (3), `momentum` (3) and `pummel` (0). `pummel` dying is that fact from the other side: an initiative of 0 is
  now a 2.1-point penalty against every rival.
- **And `check-knobs` goes four findings to eight.** `protective_slam` reads 7.33 a round to 13.73 and
  outclasses three tier-2 spells on paper while taking 7.0 % of the casts in play against `meteor`'s 10.8 %.
  The paper reading assumes a full board at full health; `Expected` reads the board in front of it. Bounds
  work for the next pass.
- **What this unblocks**: the tempo version of `summon_minions` was rejected because Greedy over-bought it at
  an unmeasured price. That objection is spent — but so is the spell's current shape, which this weight no
  longer casts. Both go back on the table together, against a baseline that now prices tempo from a
  measurement.

## 2026-09-13. Opener 8 of 9, second pass: Summon Minions becomes a summoning, and the objective charges for it

- **What changed**: `summon_minions` stops being armour on the team and becomes **`Bleed` 2 a round for three
  rounds on up to three enemies**, with **`Damage` 3 on its own caster** (ADR 0031), at a price of **3**
  instead of 2. Content `7ac86454` to **`74f02621`**. Cast value 3.90 to **7.60 a round**.
- **Why the armour version was thrown away.** It measured fine and it read as nothing: a smaller
  `revenant_guards`, which is the spell this one is supposed to *open* rather than rehearse. The entry below
  called that "the class's own idea, one tier early" and that is exactly the defect — an opener whose only
  idea is a weaker copy of its own reward teaches a player nothing on the way there.
- **What it is instead, and it is two firsts.** Nothing lands when the cast resolves: every point of its
  damage is deferred, which is what makes it a summoning and not an attack. And it is the first spell charged
  to its caster's own health, which is what the taxonomy had to say for "raising the dead costs the living".
  No other spell in the catalogue does either.
- **The objective got worse and the number is not small**: **25.51 to 36.66**. Where it comes from, target by
  target: `tierWinSpread` 0.233 to 0.452, which is 0.69 to 9.13 of penalty on its own and accounts for almost
  all of it. That reading is the gap between the best and worst win share inside tier 2, and the two ends of
  it are `healing_screech` (0.567 to 0.652, on 23 declaring sides) and `full_plate` (0.333 to **0.200**, on
  35) — two spells this change does not touch, read off samples of about thirty. The baseline's 0.233 was not
  health; `full_plate`'s takers were already losing. **This spell's own reading went the other way: 0.356 to
  0.429**, the best any defensive-flavoured tier-2 opener has read. `player1WinShare` is flat, 0.640 to 0.645.
- **Numbers**: 37 casts on the greedy mirror and 22 on the exploring run, **7.53 damage a cast** — second in
  the tier behind `enraged_charge`. All 18 enabled spells are cast on the exploring run, so `spellsNeverCast`
  reaches **0**. Matches 6.72 to 6.32 rounds. The `check-knobs` findings stay at **four**, none of them this
  spell's.
- **The negative result is worth more than the change.** Three other versions were measured and all three are
  worse, and one of them found something. **Tempo**: `InitiativeDebuff` 3 for two rounds on the enemy line,
  same caster price, at 2 — objective **37.72**, but `player1WinShare` **0.640 to 0.575**, a penalty of 9.72
  down to 0.75. Nothing else in this whole pass has moved that target, and it is the objective's second-worst
  after `tierUsageShare`: with both sides played equally well the first side wins 64 % of the time, in a game
  that lasts six rounds. The lever that moves it is a line-wide initiative debuff. **The reason it was not
  kept**: Greedy declared it 86 times on the mirror and won 0.279 with it — a trap, and the same blind spot
  this journal has now flagged twice. `weights.Initiative` is 0.5 because someone reasoned it there and no
  search has ever touched it, so the scorer over-buys tempo and loses with it. Fixing that weight is the
  prerequisite for spending this lever, and it would re-price `protective_slam` and `noxious_cure` too.
- **The other two, for the record**: the same rot at a price of 2 with a caster cost of 2 reads **39.96** and
  pushes `player1WinShare` to 0.675 — cheaper reach makes the first-mover problem worse, which is the same
  finding from the other side. Rot and tempo together read **45.90**, the worst of the five: the synthesis is
  not the best of both, it is the sum of what each one costs.
- **What is still open**: whether 36.66 is a price worth paying for an identity. The two alternatives are one
  revert away — the armour version at 25.51, and the tempo version at 37.72 with the initiative weight fixed
  first.

## 2026-09-13. Opener 8 of 9: Summon Minions stops paying for casts nobody can make

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

## 2026-09-13. Opener 7 of 9: Meteor pays for its reach, and buys back a round instead of the spread

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

## 2026-09-13. Noxious Cure's price moves back onto the cured, and three readings had to learn the sign

- **What changed**: the caster bleed of the entry below is replaced by an **`InitiativeDebuff` of 2 for one
  round on the allies it heals**. The heal stays at 4. Content `1220e301` to **`7f1ec9ee`**.
- **Why this is the better answer, and it was the maintainer's.** Legacy stripped 2 defense from the allies it
  healed. `infectious_blast` already shows what this repository does with a defense shred it cannot say — it
  becomes the one stat debuff the taxonomy has — so Noxious Cure takes the same substitution and the cure is
  noxious to the cured again, rather than to the curer. The caster bleed was a faithful *cost* in the wrong
  place.
- **And it exposed a blind spot that had been there all along.** `cast_value` and `dominates` read a target
  effect unsigned, so slowing the allies you heal read as an **extra effect for free**: the spell priced at
  12.60 where a plain heal of the same size priced at 9.60, and it read as *strictly dominating* that plain
  heal. A cost mistaken for a gift, which `noNewStrictDominance` would have refused a candidate over.
- **The rule is the one the caster half already uses, read one level out**: a harmful kind is the point of a
  spell aimed at enemies and a price in one aimed at friends, and the targeting origin is the only thing that
  says which. Three readings turn on it — the value, the dominance comparison, and which end of a knob is the
  spell's best corner. Fixing two of the three produced a finding at 0.60 that was pure tooling; fixing the
  third cleared it.
- **A default that was nearly a bug**: the first version read "friendly" as *not `Enemy`*, so a document whose
  targeting could not be read turned every hit in it into a price. Three existing tests caught it — a spell
  fixture with no targeting priced at -9.0 instead of 9.0. It reads the named origins `Ally` and `Self` now.
- **Numbers**, both runs still above where the spell started, and the same five findings as before with no new
  one:

  | | before any change | caster bleed | **debuff on the cured** |
  | --- | --- | --- | --- |
  | greedy mirror | 15 | 39 | **31** |
  | `explore:0.2` (variety) | 56 | 77 | **63** |

- **Also**: `momentum` comes off zero on the exploring run — one cast, which is noise, but it is no longer a
  spell nothing ever touches.

## 2026-09-13. Opener 6 of 9: Noxious Cure gets its bargain back, on the other shoulder

- **What changed**: `noxious_cure` heals **4** instead of 3 to up to three allies, and its caster now **bleeds
  1 a round for two rounds**. Content `633c017a` to **`1220e301`**. Cast value 7.20 to **8.00** — a bigger heal
  and a real cost, not one or the other.
- **The toxin moved from the cured to the curer.** Legacy stripped 2 defense from the allies it healed, and the
  taxonomy cannot say that: `DefenseBuff.Of` refuses anything below 1, so there is no negative buff and no way
  to put a cost on an ally. Its knobs entry said so in as many words — *"Currently missing its downside"*. The
  downside now sits on whoever brewed the cure (ADR 0031), which is a different spell from the prototype's and
  is the point: what cannot be said about an ally can be said about the caster.
- **The first attempt was worse than doing nothing, and the measurement said so.** Keeping the heal at 3 and
  adding the bleed took it from 15 casts to **2** on the greedy mirror: the cost moved the spell from just
  above Greedy's attack line to just below it — 5.60 against `lightning_bolt`'s 6.47 — and an argmax does not
  take second best. The staircase again, from a change worth 1.6.
- **So the bargain was made generous as well as costly**, which is what a bargain is. Heal 4 with the same
  bleed reads 8.00, and both runs go **up from where they started**, not merely back:

  | | before | heal 3 + bleed | **heal 4 + bleed** |
  | --- | --- | --- | --- |
  | greedy mirror | 15 | 2 | **39** |
  | `explore:0.2` (variety) | 56 | 23 | **77** |

- **The bleed is load-bearing twice.** It is the spell's identity, and it is also what stops it dominating
  `rejuvenate`: same origin, same cost, same Spell initiative, four healing against four, and three targets
  against one — without a cost on the caster this would have been a strict domination the moment the heal
  reached 4. Signed caster effects are what let the check see that.
- **Two readings the numbers hide**, both now in the entry's note: `HealScore` counts only the health an ally
  is *missing*, so on a whole team this spell is worth nothing and is never a free cast; and `ConditionScore`
  prices a bleed against the health left, so the cost is real but an agent cannot see its own bleed killing
  it — true of every bleed in the game rather than of this spell.
- **Unchanged**: 5.8 rounds, the same five `check-knobs` findings and no new one.

## 2026-09-13. Opener 5 of 9: Momentum builds energy instead of handing it over, and is still never cast

- **What changed**: `momentum` stops being `EnergyGain 1` and becomes **`EnergyRegeneration` 1 a round for 3
  rounds**, the first spell to use the kind [ADR 0020](../adr/0020-energy-regeneration-and-the-price-of-energy.md)
  added and deliberately left unused — that ADR named Momentum as one of its two candidates, and this is the
  decision it was waiting for. Free and self-targeted as before, Spell initiative still 3. Content `5bd398ce`
  to **`633c017a`**. Cast value 0.20 to **0.60**, ceiling 0.40 to **1.60**.
- **The name finally says what the spell does.** It was `Wait` with a bigger unlock reward and *less* energy:
  Wait is free and gives 2, Momentum was free and gave 1, and both spend the same activation — strictly worse
  than a spell every creature starts with. It is now Wait's **opposite trade** rather than its weaker copy:
  Wait hands energy over now, Momentum builds it, and the Assassin comes out ahead if the match lasts and
  behind if it does not.
- **And it is still never cast.** Measured three ways rather than assumed: `random` casts it **290** times, so
  it is reachable and castable and nothing is wrong with the plumbing; `explore:0.5` casts it **8** times, last
  of eighteen; `explore:0.2` — the run the objective reads variety on — casts it **0**. Greedy never casts it
  either. It is simply the last thing any agent with an opinion will choose.
- **No knob in its box changes that, and the reason is structural.** Energy is priced at 0.2 a point, so the
  top of its bounds is 1.60 an activation against an attack's 6 and up. To clear the bar `check-knobs` holds it
  to it would have to hand out twenty points of energy. The finding against `full_plate` therefore survives
  this change — and it is **right**: the way out it names is the other one, the rival's bounds or the price of
  energy, which ADR 0020 set and nothing has ever tuned.
- **Nor is there a design answer inside the taxonomy.** A `Self` spell may Heal, Regenerate, buff Defense or
  give Energy. The first three would make Momentum a worse Guard; the fourth is the cheapest weight in the
  game. Buffing initiative — the Assassin's actual identity — is on the list of what did not survive the port.
  So a tempo spell cannot be worth casting here, and that is a fact about the scorer and the taxonomy rather
  than about this spell.
- **Where that leaves it**: coherent, named correctly, using a kind that had no user, and still costing the
  objective a `spellsNeverCast`. The same shape as `full_plate` two entries ago — the spell is right and what
  would make it live is outside a content pass. Two weights are now identified as set-by-reasoning and never
  measured: `weights.Initiative` (ADR 0018) and `weights.Energy` (ADR 0020).

## 2026-09-13. Opener 4 of 9: Parasite Jab feeds its caster, and only matters when it is hurt

- **What changed**: `parasite_jab` goes from `Damage 2` to `Damage 3` and gains a **caster `Heal` of 3**, the
  first content in the catalogue to use the mechanism of
  [ADR 0031](../adr/0031-an-effect-that-lands-on-the-caster.md). Its entry said "placeholder until
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

## 2026-09-12. A spell can do something to whoever cast it

- **What changed**: the mechanism of [ADR 0031](../adr/0031-an-effect-that-lands-on-the-caster.md), across the
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
## 2026-09-12. Opener 3 of 9: Enraged Charge becomes the gamble, and Full Plate gets its second point

- **What changed**: `enraged_charge` merges its two damage effects into one of **7** and takes a critical
  chance of **0.8**, the highest in the catalogue, keeping its price of 3. `full_plate` goes to **2** points
  of permanent defense. Content `5095c388` to **`113f9acd`**.
- **The Berserker's opener changes sides.** Its entry called it "the reliable version of the Berserker's
  gamble", the dependable half of a line whose gambling half (`psycho_rush`) is a tier deeper and disabled —
  so the class was offered the safe version of a choice it could not yet make. The opener is the gamble now,
  one heavy swing on the highest roll in the game, and depth can carry the reliable one.
- **Two effects into one**: the scorer sums damage per target anyway, so a flurry of 4 and 3 and a swing of 7
  are the same number with one of them harder to read. Its `keep` said "two damage effects: it reads as a
  flurry"; that is no longer what the spell is for and the entry says so.
- **Read a round, not a cast** — and this is where the reasoning had to be corrected twice. 12.60 a cast
  looks like twice the bolt and is 8.40 a round against `protective_slam`'s 7.33, because three energy at an
  income of two comes up twice in three rounds. The first arithmetic here divided by 2 instead of 1.5 and
  made the spell look unreachable inside its own bounds; energy has no cap and carries, so the amortised rate
  is the right one.
- **Numbers**: `enraged_charge` **32 casts to 322**, ten times, with a 51.3 % win share for the sides that
  declare it against a 51.9 % baseline — the healthiest reading any opener has had.
- **Full Plate: the prediction was wrong.** Yesterday's entry said nothing inside its bounds would make it a
  choice. At two points it is cast **56 times**, from zero. The reason the estimate missed is the same one
  that keeps it out of `outclassed`: the score divides what a buff prevents by the allies it could have gone
  to, and reading that without a board understated what a second point does to the threat behind it.
- **And the cost, stated plainly: this went the wrong way on the things the objective measures.**
  `protective_slam` falls from 354 casts to **83**, `guard` from 471 to 149, spells cast from 14 to 13, and
  matches from 6.5 rounds to **5.8** — further below the 8..16 band, not nearer. An opener at 8.40 a round is
  now the strongest thing in the tier and it took the air from the one designed before it.
- **Which is what a spell-at-a-time pass does**, each spell designed against the state the last one left. The
  tier is re-measured whole at the end and this entry is not a claim that the tier is balanced — it is the
  record of what one spell did to the others.

## 2026-09-12. The knobs check reads a round instead of a cast, and stops skipping the defensive half

- **What changed**: `learning/`, no content. `outclassed` compares a defensive spell with defensive spells
  instead of skipping it, and everything it reads is divided by the rounds a cast takes to pay for itself.
- **The defensive half.** Spells with no `Damage` effect were skipped outright, because a heal and an attack
  share no unit — true, and it left a dead defensive spell invisible: `full_plate` at 0 casts and `guard` at
  471 read the same to every check in the repository. What the reading misses about a defensive spell, the
  kill it denies (ADR 0022) and the threat behind it, it misses on **both** sides of a defensive pair, so it
  cancels there and does not cancel against an attack. Partitioning rather than skipping finds `momentum`
  (0.40 against 3.90) and `summon_minions` (0.80) — two of the nine openers, both never cast.
- **A round, not a cast**, which is the same mistake as the sweep on the other axis. Energy carries between
  rounds, so a spell costing three at an income of two comes up twice in three rounds — 1.5 rounds a cast,
  not 2 — floored at one because a creature acts once a round however cheap the spell is. Read a cast at a
  time, `enraged_charge` at 12.60 reported `protective_slam` as never a choice on content that casts it 354
  times; a round at a time it is 8.40 against the slam's 7.33 and there is nothing to report.
- **Divided by, not filtered on.** Skipping costlier rivals would have been the cheaper fix and it loses the
  case this check exists for: `pummel` at one energy really is outclassed by `lightning_bolt` at two, 5.15 a
  round against 6.47. A cheaper spell can be outclassed through its price as well as despite it.
- **What the invariant is worth.** Both halves of this were found the same way — by making the change, running
  it against the catalogue, and reading a finding the measurement contradicted. `outclassed` may under-report
  and may not invent, and each time it invented one, the cause was a real axis it was not reading.
- **Left standing**: `rejuvenate` reports at 3.20 against `guard`'s 3.25, a 1.5 % hairline on a spell cast 145
  times. True about the bounds and not worth a tolerance constant to silence.
- **Not fixed, and now understood**: `full_plate` is still not reported, because the reading has no board. It
  is dead for a reason no static check can see — it can only armour *itself*, while `guard` puts its defense
  on whichever ally is under threat.

## 2026-09-12. Opener 2 of 9: Full Plate gets a price, and is still not a choice

- **What changed**: `full_plate` costs **1 energy** instead of 0, and its cost knob's lower bound goes from
  0 to 1 so no pass can put it back. The permanent point of defense is untouched. Content `a319d2cc` to
  **`5095c388`**.
- **Why the price and not a cap.** The spell is `SpellType.Passive` in a model where nothing implements a
  passive, so it is castable, repeatable and permanent — bounded by nothing but the round cap. Capping it
  (a few rounds instead of permanent) would have made it a second `guard` and dropped the one idea the spell
  has, armour the Warlord always wears. The price is the brake that keeps the idea: half a round's income per
  point, so a creature armouring itself is a creature not attacking.
- **The check written an hour earlier is what cleared it.** `unbounded` reported `full_plate` before this and
  reports nothing after, which is the whole reason that check reads the price rather than the magnitude.
- **Numbers**: play is **identical**, cast for cast and round for round — 6.5 rounds, the same 14 spells in
  the same counts. `full_plate` was cast 0 times before and is cast 0 times now. The digest is regenerated
  because the hash moved, not because an outcome did.
- **Said plainly: this did not make it a choice, and nothing inside its bounds will.** Greedy prices
  `weights.Defense x prevented / allies`, so a permanent point of defense on one creature of three reads
  **0.65** against the bolt's 6.47; at its knob ceiling of 2 points it reads 1.30. `outclassed` cannot see
  this because it skips spells with no `Damage` effect — the rule that keeps it from reporting `rejuvenate`
  and `guard`, which are cast for a survival it cannot read. But `guard` is cast 471 times and `full_plate`
  zero, and no reading in the tooling tells those two apart.
- **So the deferral is now concrete**: `full_plate` becomes a real choice when a passive is a real always-on
  modifier the creature never spends an activation on, which is a domain rule and an ADR, not a content pass.
  Until then it is a safe dead spell rather than a dangerous one, and that is the whole claim.

## 2026-09-12. The knobs check learns to read a sweep, and to see a free permanent buff

- **What changed**: `learning/`, no content. `cast_value` now prices a cast for every target the spell is
  allowed, `outclassed` only takes a rival that reaches as many targets or more, and a new `unbounded` check
  reports a permanent effect that costs no energy. No number in `data/` moved, so the content hash and the
  digest are untouched.
- **The sweep.** `cast_value` read one target where `ActionScorer` sums a resolution over all of them, so
  `meteor` priced at **6.00** and played at **10.74 damage a landed cast** — the largest single source in the
  catalogue, passing every check. Measured over the benchmark seeds, the single-target spells land 0.88 to
  1.01 of their expected value and `meteor` lands **1.79** of its three targets: reading one understated it
  by 1.8x, reading three overstates it by 1.7x. Three is the reading kept, because the question the number
  answers — can this spell ever be a choice — is a question about a spell at its best.
- **Which broke the module's own rule, and the fix is the second half.** With a sweep priced at 18.00,
  `outclassed` made `meteor` the bar for its whole tier and reported `protective_slam` as never a choice on
  content that casts it **354 times in 400 matches**. That module states its error may only run one way —
  under-report, never invent — so a rival now has to reach as many targets or more. A single-target spell
  cannot match a sweep without ceasing to be single-target, which is `dominates`'s axis and not this one.
  With the rule, the findings are `parasite_jab` twice, `pummel` and `throwing_star`: the four true ones.
- **The permanent buff, where the diagnosis was wrong and worth writing down.** The suspicion was that
  `PERMANENT_CONDITION_ROUNDS = 3` understated a permanent effect. It does not: it matches
  `ActionScorer.PermanentConditionRounds`, and raising it would not find the real problem anyway, because
  every reading here prices **one cast** and one `full_plate` is a point of defense at any horizon. What has
  no brake is re-casting: free, never expiring, stacking, bounded by nothing but the round cap. So the check
  reads the **price**, not the magnitude, and `full_plate` is the one spell in the catalogue that trips it.
  Its own cost knob reaches zero, so this is also what stops a tuning pass from putting it back.
- **Why it matters now**: the nine openers are being designed against these numbers. A tool that reads a
  sweep at a third of its worth and calls an unbounded spell fine is not a tool to design nine spells with.

## 2026-09-12. Opener 1 of 9: Protective Slam protects by staggering

- **What changed**: `protective_slam` gains an `InitiativeDebuff` of 2 over two rounds and its damage goes
  from 3 to 4. Content `ef078082` to **`a319d2cc`**. Cast value 4.00 to **7.33**, against 6.47 for
  `lightning_bolt` at the same two energy.
- **The identity decision**: legacy gave the slam a point of defense on its caster, which the taxonomy cannot
  express — a spell has one target origin, so hitting an enemy and protecting an ally are two spells. Rather
  than leave it a plain hit that nothing can save, protection is expressed as tempo: a slammed enemy acts
  later. It is the one protective thing that can be said to an enemy, and it gives the spell teeth a bigger
  hit would not have, because defense absorbs damage and does nothing to a stagger.
- **Above the tier-1 baseline on purpose.** A deeper spell outclassing a shallower one is what a talent tree
  is for — `dominance()` already exempts it — so an opener should beat `lightning_bolt`, not sit under it.
  How far above is bounded by the match, not by taste: 3 creatures of 20 health give a side 60, three casts a
  round at 6.47 wipe that in 3.1 rounds in theory and 6.2 in play, and `averageRounds` wants 8 to 16. The
  tier 0 to tier 1 step was 2.15x; repeating it here would put an opener at 13.9 and end matches in half the
  rounds we already cannot afford. The premium is therefore about a fifth, and the reward for the pick is the
  capability rather than the magnitude.
- **Where the value sits, and why it is mostly damage.** Damage 4 with a debuff of 2 puts 27 % of the cast
  value on `weights.Initiative`; 3 damage with a debuff of 3 reads the same 7.00 but puts 43 % there. That
  weight is 0.5 on reasoning alone (ADR 0018) and `search-weights` has never tuned it, so the smaller
  exposure wins. The bounds reach the other shape.
- **Numbers**, greedy mirror on the benchmark seeds: **0 casts to 354**, declared by 155 sides of 400,
  88.1 % of declarations land. Matches lengthen from 6.2 rounds to **6.5** — the stagger slows the damage
  race — draws fall to zero, and the spells cast go from 12 to 14: `wait` and `poison_slash` come back.
  `enraged_charge` falls from 122 casts to 32, which is the Berserker opener losing to the Mercenary one and
  is opener 3 of 9's problem to answer.
- **The caveat, with a number on it now**: sides that declare it win **37.4 %** of the time against a
  51.9 % baseline. It is cast often and taking it currently correlates with losing. That is either the
  opportunity cost of the picks it takes, or the unmeasured initiative weight paying less on the board than
  in the score — the risk named above, arriving. Left standing rather than patched: it is one spell of nine,
  and the tier is re-measured whole at the end.

## 2026-09-12. The nine openers of tier 2 are on, with the prototype's numbers and no balance claim

- **What changed**: the one opener of each of the nine specialisations is enabled — `protective_slam`,
  `full_plate`, `enraged_charge`, `parasite_jab`, `momentum`, `noxious_cure`, `meteor`, `summon_minions`,
  `healing_screech` — together with `talent-tree:base_creature:v1`, which carries them, and the one creature
  now points at that tree. The eighteen deeper spells of those specialisations stay off and are pruned from
  their nodes. The catalogue goes from 9 spells to **18**. Content `37ec4b49` to **`ef078082`**.
- **`talent-tree:core_classes:v1` is disabled in the same change.** `base_creature` has the same root, the
  same three branch nodes and the same six tier-1 spells, so it is a strict superset: leaving both on would
  ship a tree no creature can reach. This is what the one failing test caught —
  `The_repository_content_builds_and_loads` asserts the repository ships a single tree, and that assertion is
  right.
- **No number was tuned.** Every enabled spell carries the value the legacy port gave it. This entry is the
  measurement of that state, not a balance pass, and the state is deliberately unbalanced.
- **Numbers**, greedy mirror on the benchmark seeds: matches fall from 8.5 rounds to **6.2**, 0.5 % reach the
  round cap, 12 of the 18 spells are cast. Of the nine openers, four are cast — `meteor` **946**,
  `noxious_cure` 151, `enraged_charge` 122, `healing_screech` 55 — and **five are never cast at all**:
  `protective_slam`, `full_plate`, `parasite_jab`, `momentum`, `summon_minions`.
- **`meteor` is the new monopoly**: 946 landed casts for **8928 damage**, the largest single source in the
  game. Four damage on up to three enemies for three energy, on teams of three.
- **Why the five are dead, and it is not close.** Greedy takes the highest raw score it can afford, and
  `weights.Energy` is 0.2, so a cheaper spell gains almost nothing in the score: cost bites through the two
  energy a round pays, not through the price. `lightning_bolt` carries 6.47 at two energy and any creature can
  unlock it beside its own specialisation, so that is the bar. `protective_slam` carries 4.00 at the same
  price, `parasite_jab` 3.00, `full_plate` 1.95, `summon_minions` 0.60, `momentum` 0.20 — and `momentum` and
  `summon_minions` both hand over less energy per activation than the free `wait` every creature starts with.
  `check-knobs` reaches the same finding from the content alone: `lightning_bolt` strictly dominates
  `protective_slam` and `parasite_jab`, and neither becomes a choice anywhere inside its declared bounds.
- **A gap in the tooling this exposed**: `cast_value` — what `check-knobs` compares spells with — ignores
  `maxTargets`, while `ActionScorer` sums a cast over every target it hits. `meteor` reads 6.00 to the knobs
  and plays at roughly 18. That is why an AoE could be the strongest spell in the game and no check said so.
  Recorded here; fixing it is its own change.
- **What follows**: the nine openers are designed one at a time, identity first, and this digest is the
  before. Nothing in this entry is a claim that the tier is balanced.

## 2026-09-12. The catalogue tuned against a yardstick that measures it, and Greedy narrows anyway

- **What this is**: `tune-content --seed 0`, 24 rounds of 6, `--pair-depth 2`, against the objective of
  [ADR 0029](../adr/0029-read-variety-on-an-exploring-run.md) and the baseline of
  [ADR 0028](../adr/0028-name-the-defense-weight-and-move-its-price-one-step-up.md). **Applied.** Content
  `d4a21a55` to **`37ec4b49`**, digest regenerated and verified. 188 candidates, of which the engine played
  **147**: the other 42 were catalogues it had already played, 168 evaluations it did not have to run
  (ADR 0030).
- **Score 51.999 to 5.611**, and **nine of the fourteen targets are inside their bands**. The best result this
  objective has recorded, and not comparable with anything before ADR 0029 — those scores were read off a
  different player.
- **Eight moves**:

  | Spell | Knob | From | To |
  | --- | --- | --- | --- |
  | `poison_slash` | bleed per round | 1 | **3** |
  | `poison_slash` | damage | 2 | 3 |
  | `throwing_star` | damage | 2 | 3 |
  | `throwing_star` | Spell initiative | 2 | 3 |
  | `rejuvenate` | heal | 3 | 4 |
  | `lightning_bolt` | critical chance | 0.667 | 0.617 |
  | `pummel` | Spell initiative | 1 | 0 |
  | `wait` | energy gain | 1 | **2** |

- **Where the 46.39 came from**, and it is the answer to the entry above:

  | Target | Before | After | Band |
  | --- | --- | --- | --- |
  | `tierDamageSpread` | **5.000** (capped, 36.00) | **2.758** (2.30) | ..2 |
  | `roundCapShare` | **0.175** (12.50) | **0.040** (0.00) | ..0.05 |
  | `tierUsageShare` | 0.568 | 0.531 | ..0.5 |
  | `averageRounds` | 10.735 | 8.475 | 8..16 |
  | `exploit.winRateA` | 0.550 | **0.458** | ..0.55 |
  | `tierWinSpread` | 0.148 | **0.226** | ..0.15 |
  | `fizzleRateA` | 0.108 | **0.156** | ..0.15 |
  | `spellUsageShare` | 0.410 | 0.409 | ..0.25 |

  `tierDamageSpread` alone is **33.70 of the gain** and is no longer capped. It was the one of the seven
  variety targets that did not move when the yardstick changed player, which is what said it was the content
  and not the agent; the first search that could see it without eleven points of agent noise on top went
  straight at it. And the round-cap bill ADR 0028 left is paid.
- **The `exploit` guard was live and it held.** It sat exactly on its limit at 0.550 before this, so it was
  one step from costing something; the proposal moved away from it to 0.458. The searched agent loses to the
  taste the catalogue is balanced for, on content searched without that being asked for.
- **And Greedy plays narrower, which is the finding.** Read on both runs, not off the score:

  | | mirror before | mirror after | variety after |
  | --- | --- | --- | --- |
  | `lightning_bolt` | 48.3 % | **62.2 %** | 41.5 % |
  | `rejuvenate` | 25.4 % | **10.3 %** | 15.8 % |
  | `guard` | 13.2 % | **7.5 %** | 13.1 % |
  | entropy | 1.992 | **1.846** | **2.573** |
  | spells cast | 7 of 9 | 8 of 9 | **9 of 9** |

  On the exploring run every spell is cast and the entropy is the highest it has been. On the greedy mirror
  the game is *narrower* than before, and the defensive half that ADR 0028 had finally made the baseline buy
  gives most of it back. The tuner paid `roundCapShare` by making matches faster and more lethal — 10.7 to
  8.5 rounds — and defence is what that cost.
- **That is ADR 0029's tension arriving, not a defect.** The variety targets were moved onto a player that
  can see a choice, and the player whose prices define the catalogue still does not take it. Both readings
  are true: the content offers more, and Greedy uses less of it. Which one a balance pass should serve is a
  design question this journal cannot settle, and the next one on it should be about the argmax rather than
  about a knob.
- **On `wait` at 2 energy**: inside the bounds the entry declared (`min 1, max 2`), still free, and its
  invariant holds — at 0.2 a point of energy it scores 0.4 against `heavy_strike`'s ~3, and Greedy casts it
  **3 times in 6398**. Worth knowing anyway: `energyPerRound` is 2, so a skipped activation now doubles a
  round's income.
- **What is still wrong**: `spellUsageShare` at 0.409 against a band of 0.25 is now the largest term (2.53),
  and ADR 0029 already measured that no content reachable inside these bounds brings it under about a half
  for an argmax. `tierDamageSpread` at 2.758 is close. `pummel` remains outclassed by `lightning_bolt` at
  tier 1 — 5.40 against 6.47 — which `check-knobs` has reported through every pass and which no move inside
  the current bounds can fix.

## 2026-09-12. The baseline finally buys defence, and the play moves in steps rather than smoothly

- **What changed**: [ADR 0028](../adr/0028-name-the-defense-weight-and-move-its-price-one-step-up.md). The
  weight called `buff` is now called `defense`, the only thing it has priced since ADR 0018, and its default
  goes **0.5 to 0.65**.
- **Digest**: regenerated and verified, **174 of 400 entries moved**, content `d4a21a55`. `Greedy` is a
  different player now, fingerprint `@7aff3a10` to **`@a4e83485`**, so nothing stamped before this compares
  term by term with anything after it.
- **Why the number needed deciding at all.** ADR 0022 changed what this term multiplies — from
  `buff x amount x rounds` to the damage a buff actually prevents — and deliberately left the value at 0.5. So
  the number had been reasoned about one quantity and was scaling another. Asked what 0.5 meant, nobody could
  say. Keeping it needed an argument as much as moving it did.
- **Sweeping this weight alone**, mirror evaluations on the benchmark seeds:

  | `defense` | rounds | round cap | entropy | `guard` | `rejuvenate` | `lightning_bolt` | never cast |
  | --- | --- | --- | --- | --- | --- | --- | --- |
  | 0.5 (before) | 7.78 | 4.5 % | 1.800 | 6.9 % | 11.9 % | 62.2 % | 2 |
  | 0.6 | 7.88 | 4.5 % | 1.791 | 8.1 % | 12.0 % | 62.1 % | 3 |
  | 0.62 | 10.64 | 17.0 % | 1.984 | 13.1 % | 25.3 % | 48.5 % | 2 |
  | **0.65** | **10.73** | 17.5 % | **1.992** | 13.2 % | **25.4 %** | 48.3 % | 2 |
  | 0.68 | 12.36 | 23.0 % | 2.222 | 18.6 % | 23.4 % | 41.8 % | 1 |
  | 0.7 | 12.36 | 23.0 % | 2.222 | 18.6 % | 23.4 % | 41.8 % | 1 |
  | 1.0 | 22.81 | 68.5 % | 2.365 | 26.0 % | 24.2 % | 21.9 % | 1 |
  | 1.5 | 30.00 | **100 %** | 1.612 | 28.9 % | 44.5 % | **0 %** | 5 |

- **It is a staircase, not a slope**, and that is the finding. 0.62 and 0.65 read identically; so do 0.68 and
  0.7. The agents take an argmax, so a decision flips only when an ordering flips, and nothing moves in
  between. Picking a price is picking a step, not a point, and 0.65 sits in the middle of its step rather than
  on an edge — a small error in it changes nothing, which a boundary value could not promise.
- **What 0.65 buys**: matches go 7.78 to **10.73 rounds**, inside the 8..16 band for the first time on this
  content, and the two defensive spells roughly double — `guard` 6.9 % to 13.2 %, `rejuvenate` 11.9 % to
  25.4 %. The defensive half of the catalogue was not dead because it was badly designed. It was dead because
  the baseline would not buy it.
- **What it costs**: `roundCapShare` 4.5 % to **17.5 %**, well past its 5 % target. That bill goes to the next
  tuning pass, which can answer it by cheapening attacks or making healing cost more. And the catalogue on
  `main` was tuned against a baseline that would not defend, so it is now tuned for a player who no longer
  exists.
- **The baseline plays better, checked rather than assumed.** `search-2`, the searched agent of the entry
  below, beat the old `Greedy` **0.5875** on hold-out seeds and beats the new one **0.5650** — 226 of 400. It
  kept 2.25 points of its edge and the new baseline closed the rest.
- **Why 1.5 was not taken** (it was the value asked for): every match runs out of rounds and
  `lightning_bolt` is never cast at all. Both sides turtle and nobody dies. The cause is compounding, and it
  is what the shared unit hides — `damage` prices a hit paid once, `DefensiveScore` multiplies what a buff
  prevents by the rounds it holds. A `guard` taking 2 off an incoming hit for 3 rounds prevents 6, so at 1.5
  it scores 9, more than any attack in this catalogue can. And the intuition behind it is already paid for
  separately: `DefensiveScore` adds `weights.Kill` when a buff turns a lethal round survivable, so this weight
  prices attrition only.
- **Also fixed**: the glossary's Scoring weights entry listed eight terms and missed `initiative`, which ADR
  0018 added three days ago. The ubiquitous language had drifted off the code it names.
- **What is next**: a `tune-content` pass against this baseline. It inherits a 17.5 % round cap and a
  catalogue tuned for the previous player, and for the first time it is searching for content a defender will
  actually pick up.

## 2026-09-12. The searched agent wins on seeds it never saw, and wins by playing fewer spells

- **What this is**: the weights of workflow run 2 of `Search the agent weights`, committed as
  `learning/weights/search-2.json` — the first searched set in the repository. Seed 0, 10 rounds of 16, 161
  evaluations against `Greedy` on the benchmark seeds, content `d4a21a55`, engine `0848ab6bdc0f`. Stamped
  `Heuristic:…@4e93f46b`. The run reproduces locally to every printed digit, which is what a deterministic
  search is supposed to do and had never been checked across two machines before.
- **The weights, as ratios to `damage`**:

  | Weight | Greedy | Found | Change |
  | --- | --- | --- | --- |
  | `damage` | 1.000 | 1.000 | — |
  | `kill` | 5.000 | 5.327 | +7% |
  | `heal` | 0.800 | 0.634 | -21% |
  | `stun` | 3.000 | 1.768 | **-41%** |
  | `bleed` | 0.800 | 0.433 | **-46%** |
  | `buff` | 0.500 | 0.506 | +1% |
  | `energy` | 0.200 | **-0.188** | sign flip |
  | `risk` | 2.000 | 1.563 | -22% |
  | `initiative` | 0.500 | 0.309 | -38% |

- **It holds out of sample**, which is the part the search itself cannot establish and which no entry before
  this one measured. Two blocks of 200 seeds no candidate played, consecutive integers past the largest
  benchmark seed, mirrored:

  | Seeds | Found | Baseline |
  | --- | --- | --- |
  | 995317.. | **0.5875** (235/400, [0.553, 0.622]) | 0.5000 by construction |
  | 2000000.. | **0.5775** (231/400, [0.546, 0.609]) | — |

  Both clear of one half, and the second lands on the searched score exactly. The 7.75 points are not an
  artifact of the seed file.
- **And it wins by playing *less*, which is the finding.** On the hold-out, against Greedy's own mix:

  | Spell | Found | Greedy |
  | --- | --- | --- |
  | `lightning_bolt` | **79.4%** | 64.6% |
  | `heavy_strike` | 8.4% | 10.2% |
  | `rejuvenate` | 6.5% | 4.9% |
  | `guard` | 5.4% | 8.8% |
  | `pummel` | **0.3%** | 7.5% |
  | `basic_attack` | never | 3.5% |
  | `poison_slash` | never | 0.6% |

  Entropy **1.07 against 1.76**, four spells never cast against two. A `bleed` at -46% and an `energy` that
  goes negative are the same decision seen from two angles: stop paying for anything that is not damage now,
  and stop hoarding the energy that buys it. The tuned catalogue gave `pummel` damage 3 and a 0.717 critical
  chance, and an agent that only wants to win casts it 19 times where Greedy casts it 432.
- **Why this is not the baseline**, and the reason is sharper than 2026-09-10's. That entry refused a searched
  agent because it played the same spells as Greedy with better aim — nothing gained. This one refuses the
  opposite: `tune-content` is paid to spread casts (`spellEntropyA`, `tierUsageShare`, `spellsNeverCast`), and
  this agent is paid to concentrate them. Make it the baseline and every tuning pass is measured by a player
  that actively refuses variety, so the objective spends its budget fighting its own yardstick.
  `ScoringWeights.Default` and `greedy.json` are untouched; every benchmark and every entry below stays
  comparable.
- **What it is good for**: a second opinion that disagrees with the baseline on purpose. `evaluate --p1
  heuristic:learning/weights/search-2.json` says what a catalogue looks like to a player who only wants to
  win, which is the reading a spread objective cannot give itself.
- **What is still open**: run 1 (seed 1, content `c0ec6984`) moved `bleed` **+67%** where this one moves it
  -46%, and both converged to exactly 0.5775 on their own seeds. Different content and different seed, so it
  is not a contradiction — but two opposite readings of the same knob at the same score is a reason to run a
  third seed before anyone reads a weight's direction as a fact about the game.

## 2026-09-12. A condition names its cast, and the objective stops paying to keep bleeds unplayable

- **What changed**: [ADR 0027](../adr/0027-a-condition-remembers-the-spell-that-applied-it.md). A condition
  remembers the cast that applied it, so what it does at upkeep is counted against that spell and that side.
  No rule moved: the total is computed and applied exactly as before, and only the split of what the board
  took is new.
- **Digest**: unchanged, 400 of 400 entries, on content `be58a32d`. That is the point — this is a reading,
  not a rule, so every benchmark and every entry below stays comparable on the engine axis.
- **What it was hiding**, measured on the core content by raising `poison_slash`'s bleed:

  | bleed | objective | `tierDamageSpread` | what it means |
  | --- | --- | --- | --- |
  | 1 per round over 1 (today) | 40.45 | 2.78 | |
  | 2 over 2, before this change | **95.15** | **5.00** (capped) | a catastrophe that was not there |
  | 2 over 2, after | 79.08 | 5.00 (capped) | the number is honest; the gap is real |
  | **2 over 3, after** | 51.85 | **1.61** | inside its band |

  `poison_slash` reads `damage 0, conditionDamage 325` on a mirrored run: its direct damage is absorbed
  entirely by the stacked defense and only the bleed gets through, because a bleed ignores defense. So the
  spell was being compared at **zero damage per cast**, floored to 0.5, against `lightning_bolt`'s 5.76 — and
  raising its bleed made that worse, by making the spell worth casting and entering it into the comparison.
  The objective was paying the search to keep bleed spells unplayable.
- **What 2 over 3 now says**, which is the interesting part: `tierDamageSpread` 2.43 to 0, `tierWinSpread`
  1.45 to 0, `spellUsageShare` 18.53 to 11.61, `spellEntropyA` 4.31 to 1.58 — and `tierUsageShare` 13.71 to
  **38.41**, because `poison_slash` becomes its tier's monopoly instead of `lightning_bolt`. Four terms
  improve and one gets much worse. That is a real trade-off the objective can now report, where before it
  reported a measurement hole.
- **Not comparable**: `tierDamageSpread` reads something else now, so scores from before this are not on the
  same scale. The last `tune-content` proposal was searched against the old reading.
- **What is next**: re-run `tune-content`. `poison_slash`'s bounds reach a bleed of 3 per round over 3
  rounds, and for the first time the search can see what that buys.

## 2026-09-10. The catalogue tuned for an agent that defends, and the first-mover share finally lands in its band

- **What this is**: `tune-content --seed 0` against the scorer of the entry below, once its two review
  findings were fixed. 72 candidates, 146 evaluations, **score 148.82 to 40.45**. Applied. Content
  `c0ec6984` to **`be58a32d`**, digest regenerated and verified.
- **Three moves**, and that is all it took:

  | Spell | Knob | From | To |
  | --- | --- | --- | --- |
  | `lightning_bolt` | damage | 3 | 4 |
  | `rejuvenate` | energy cost | 1 | 2 |
  | `basic_attack` | damage | 1 | 2 |

- **Six of the twelve targets are now on target**, which has never happened before:

  | Target | Before | After | Band |
  | --- | --- | --- | --- |
  | `player1WinShare` | 0.685 | **0.455** | 0.45..0.55 |
  | `averageRounds` | 13.39 | **8.29** | 8..16 |
  | `roundCapShare` | 0.175 | **0.045** | ..0.05 |
  | `drawRate` | 0.015 | 0.005 | ..0.05 |
  | `spellsNeverCast` | 1 | 2 | ..2 |
  | `skill.winRateA` | 1.000 | 1.000 | 0.65.. |

  **`player1WinShare` is the headline.** Going first was worth 0.64 to 0.69 in every entry of this journal,
  no content move had ever touched it, and it is now 0.455 — inside the band. What moved it was not a rule
  about turn order: it was making the defensive half of the catalogue playable, so the side that moves second
  has something to do with the tempo it loses. A heal at 2 energy instead of 1 is what tipped it.
- **What the board looks like now**: seven of nine spells cast, `guard` 573 casts and 1146 buffs applied,
  `rejuvenate` 625 casts and 1717 health restored. `poison_slash` (3) and `pummel` (2) are the two that fell
  away; `wait` and `basic_attack`, dead in every entry before this one, are cast.
- **What it cost**: entropy 1.75 to 1.46 and `spellUsageShare` 0.603 to 0.680, both worse. `lightning_bolt`
  keeps 3848 of 5655 landed casts. The two largest penalties left are `spellUsageShare` (18.53 of the 40.45)
  and `tierUsageShare` (13.71): the game is balanced between the sides and no longer stalls, but one spell
  still owns the catalogue.
- **Read the score against 148.82, not against the 119.40 of two entries ago.** The scorer changed between
  them, so the baseline it starts from is a different number for the same content. This is the same warning
  the tier entry carries, for the same reason.
- **What is next**: `spellUsageShare`. Three of the twelve targets carry 34.7 of the remaining 40.5, all three
  about one spell taking most of the casts, and no move in this pass touched it. Whether a knob search can
  reach it at all, or whether it needs a spell that does not exist yet, is the open question.

## 2026-09-10. Defence gets a price: the catalogue plays seven spells instead of four, and stalls

- **What changed**: ADR 0022 prices a defensive effect by the damage it prevents. No content moved in this
  entry; the digest for content `c0ec6984` is regenerated because the engine changed under it. Engine at the
  commit this entry lands with.
- **What it does**, greedy against greedy on the benchmark seeds:

  | | Before | After |
  | --- | --- | --- |
  | Spells cast | 4 of 9 | **7 of 9** |
  | `rejuvenate` | 0 | 2895 casts, 6509 health restored |
  | `guard` | 0 | 1424 casts, 2656 buffs applied |
  | `poison_slash` | 0 | 963 |
  | Spell entropy | 0.74 | **2.02** |
  | Average rounds | 8.1 | **18.1** |
  | Round-cap share | 0.000 | **0.385** |

  The defensive half of the catalogue came alive on the first try, and it broke the game: two bots that both
  value survival heal faster than they hurt, and nearly two matches in five ran out of rounds. That is the
  honest result of pricing defence correctly in content authored for agents that ignored it. The measurements
  above are the first cut of the scorer; two review findings then changed its arithmetic, so treat them as the
  shape of the effect rather than as numbers to compare against later runs.
- **Two bugs caught in review, both real, both in the direction of overvaluing defence**:
  - Prevention was priced at a point per point of buff. Defense comes off a hit before the floor at zero, so
    a point past a hit's plain damage only reaches its critical branch: against a 3-damage spell at 3
    defense, a fourth point is worth 0.05, not 1. It is now read as the difference between the unbuffed and
    the buffed threat.
  - The denied kill was priced per outcome, and `guard` carries two defense buffs. When one point alone
    crossed the survival threshold the cast was paid `kill` twice; when survival needed both points it was
    paid none. The whole defensive reading now happens once per target, with a target's healing and all of
    its buffs read together, and the buffs priced on top of each other rather than each from the bare board.

  Both ship with a test that fails without the fix. Both were found by review, not by the suite, which is
  worth saying plainly: the six tests written with the feature all passed on the buggy code, because the test
  board has zero defense and one buff per cast — the two cases where the bugs are invisible.
- **What is next**: re-tune the catalogue against this scorer. The proposal that first accompanied this change
  was searched against the arithmetic the two findings corrected and has been discarded rather than kept.

## 2026-09-10. `search-weights` on the nine-spell content: the weights are not what keeps defence off the board

- **What this is**: the experiment the entry below named as the next run. A weight search plays to win and
  nothing else, so it settles whether Greedy ignores `guard` and `rejuvenate` because its eight weights
  undervalue defence, or because defence is genuinely not worth buying in this catalogue.
  `search-weights -o runs/search-9spells --seed 1`, 10 iterations of 16, 161 evaluations, against `greedy`
  on the benchmark seeds (400 matches, mirrored). Content hash `c0ec6984`, engine `f9488f36136f`.
- **What it found**: a candidate at **0.5775** mean score against Greedy, interval [0.536, 0.619], found at
  iteration 2. The population had collapsed onto it by iteration 7 and the remaining four iterations found
  nothing better, so this is a converged search, not a truncated one.
- **The weights, read as ratios to `damage`** (only ratios matter — scaling every weight scales every score):

  | Weight | Greedy | Found | Change |
  | --- | --- | --- | --- |
  | `damage` | 1.000 | 1.000 | — |
  | `kill` | 5.000 | 5.569 | +11% |
  | `heal` | 0.800 | 0.811 | **+1%** |
  | `stun` | 3.000 | 3.141 | +5% |
  | `bleed` | 0.800 | 1.340 | +67% |
  | `buff` | 0.500 | 0.440 | **-12%** |
  | `energy` | 0.200 | 0.055 | -73% |
  | `risk` | 2.000 | 2.172 | +9% |
  | `initiative` | 0.500 | 0.369 | -26% |

- **The answer**: no. A search that cares only about winning left `heal` where it was (+1% is inside the
  noise of a 400-match evaluation) and moved `buff` **down**. Nothing in the objective told it to avoid
  defence; it declined to buy it. The two real moves are elsewhere: `bleed` +67% — damage over time is
  underpriced when a match lasts eight rounds — and `energy` -73%, hoarding energy buys almost nothing.
- **What the winning agent actually cast** (6009 actions):

  | Spell | Casts |
  | --- | --- |
  | `lightning_bolt` | 5205 |
  | `heavy_strike` | 411 |
  | `pummel` | 351 |
  | `throwing_star` | 42 |

  Five of nine spells never cast: `wait`, `basic_attack`, `guard`, `poison_slash`, `rejuvenate`. Across both
  agents, 11,910 actions produced **0 healing, 0 defense buffs, 0 regenerations**. The winner plays the same
  four spells as Greedy in nearly the same proportions; its 7.75 points come from targeting and timing, not
  from a different spell mix.
- **What this rules out and what it leaves**: tuning the eight weights is not the lever. What remains is the
  scorer's pricing and the content itself — `HealScore` counts missing health with no notion of the damage
  actually incoming, and `DefenseBuff` is priced `buff × amount × rounds` on a flat
  `PermanentConditionRounds = 3` rather than by the damage it prevents. Both are ADR territory, in the line
  of ADR 0018 on the initiative weight. The horizon is the third suspect and the one no weight can fix: a
  one-step lookahead cannot see "I survive the round I would otherwise lose".
- **Decision**: apply nothing. `learning/weights/greedy.json` stays as authored — a 57.75% agent that plays
  the same four spells is not a better baseline, it is the same baseline with sharper aim, and changing the
  benchmark opponent would invalidate every comparison in this journal for no insight. The next entry should
  be about pricing a defensive effect by the damage it prevents, not about weights.

## 2026-09-10. First tuning pass on the tier objective: tier 1 becomes a choice, the defensive half stays dead

- **What this is**: the first run of `tune-content` against the objective that reads per tier. 58 candidates,
  118 evaluations, **score 119.40 to 34.73**. Content unchanged: this is a proposal, measured and written
  down, not applied. Scores here do not compare with the 104.05 and 15.56 of the entries below — those were
  a different objective (the entry below says why).
- **The five moves**:

  | Spell | Knob | From | To |
  | --- | --- | --- | --- |
  | `lightning_bolt` | energy cost | 2 | 3 |
  | `basic_attack` | damage | 1 | 2 |
  | `pummel` | critical chance | 0.667 | 0.717 |
  | `guard` | Spell initiative | 1 | 0 |
  | `rejuvenate` | heal | 3 | 2 |

- **What it does to the play**, built and played to check rather than read off the score:

  | Tier | Before | After |
  | --- | --- | --- |
  | 0 | `heavy_strike` **100%** | `heavy_strike` 81%, `basic_attack` 19% |
  | 1 | `lightning_bolt` 93%, `pummel` 6% | `lightning_bolt` 57%, `pummel` **43%** |

  Tier 1 becomes a real choice, two spells sharing the casts almost evenly where one took everything. Tier 0
  opens as well. Matches run **9.4 rounds**, inside the band. Entropy 0.74 to **1.93**. And `tierWinSpread`
  falls from 0.262 to **0.003**: spells offered together are now worth about the same in results, which is
  the reading that says a tier is a choice rather than a formality.
- **What it does not fix, and this is the finding**: `tierUsageShare` stays at **0.814**, still 19.8 of the
  remaining 34.7, and it is tier 0 — `heavy_strike` keeps 81% and `wait` is never cast. Five of the nine
  Spells are still never cast: `wait`, `guard`, `poison_slash`, `rejuvenate`, and now `throwing_star`. The
  whole defensive half of the catalogue is dead, and the first-mover share does not move on this path either
  (0.640).
- **Why the defensive half is dead, from the scorer rather than from a guess**: `ActionScorer` is a one-step
  lookahead, and on the same board `lightning_bolt` scores about 5.2 (3 damage, doubled by a critical 72% of
  the time, at `damage` 1.0) while `rejuvenate` scores at most 2.4 (`heal` 0.8 × 3 restored) and `guard`
  2.5 (`buff` 0.5 × 1 × 3 permanent rounds, plus 1 × 2 rounds). An attack is worth twice a defence to the
  agent that measures the content, every single time.
- **Which of the two is wrong is a measurable question, not an opinion**: either the weights undervalue
  defence, or defence genuinely is not worth it in a nine-round game with 20 health. `search-weights` tunes
  those eight numbers *for winning* and has never been run on this content. If the weights that win keep
  `heal` low, the content is the problem and no amount of tuning `rejuvenate`'s number will make a bot want
  it.
- **Decision**: apply nothing yet. The next run is `search-weights` on this content, and the entry after
  this one should say whether a bot that plays to win ever buys a heal.

## 2026-09-10. Balance read per tier, and the starting kit turns out not to be a choice at all

- **What changed**: the objective, so **no score from before this entry compares with a score after it**.
  Three targets are added and one is reweighted. No content moved.
- **Why**: the catalogue-wide `spellUsageShare` cannot see a monopolised tier. A Tier is the set of Spells
  offered at one depth of the Talent tree, which is what a player actually chooses between, so that is where
  the question "is this a choice" belongs.
- **The three readings**, each on its worst tier: `tierUsageShare`, the largest share of landed casts one
  Spell takes inside its tier; `tierDamageSpread`, how many times harder the best damaging Spell of a tier
  hits per landed cast than the worst; `tierWinSpread`, the gap between the best and worst win share. Each
  skips what it cannot read — a tier nobody cast, a Spell that deals no damage, a Spell too few sides
  declared — rather than guessing, using the engine's own threshold of eight sides.
- **The finding, and it is not small**: on the core content `spellUsageShare` reads 0.855 while
  **`tierUsageShare` reads 1.000**. `heavy_strike` takes *every* landed cast of tier 0; `basic_attack` and
  `wait` take none. Tier 1 is barely better, `lightning_bolt` at 93% against `pummel` 6% and
  `throwing_star` 0%. The starting kit every match is dealt is not a choice, and no rule caught it:
  `startingKitOffersAChoice` only refuses strict dominance, and `basic_attack` is cheaper than
  `heavy_strike`, so nothing is strictly better than anything. It is simply never worth casting.
- **What it costs at 3 energy**: with `lightning_bolt` at 3, `tierUsageShare` is still 0.874 — tier 0, the
  same problem — while `tierWinSpread` falls from 0.262 to 0.071. Fixing the Sorcerer's price does nothing
  for the starting kit, which the catalogue-wide reading could not have told us.
- **`spellUsageShare` drops to weight 1.** The tier reading is the better instrument and catches everything
  the wide one does; the wide one stays as a coarse guard rather than double-counting at full weight.
- **Decision**: keep, apply no content change. The next question is no longer only what `lightning_bolt`
  costs, it is why a creature never casts two of the three Spells it starts with.

## 2026-09-10. The search sweeps first: 104.05 to 15.56, and two good moves that do not add up

- **What changed**: the tuner, not the content. Three things, after the entry below left one spell holding
  85% of the casts and the search failing to touch it (ADR 0021).
- **Dominance reads the Talent tree now.** A Spell is compared against another at its own depth or deeper,
  because reaching a deeper node costs picks and prerequisites: being better there is the reward. On this
  content the report goes from three pairs to none, and all three were a tier-1 Spell beating a tier-0 one.
  Depth comes from the Creature's starting Spells and from walking the trees, shallowest wins.
- **The search sweeps every playable knob once before it climbs.** The reason is a measured miss: a uniform
  draw over 29 knobs with 40 candidates leaves a one-in-four chance a given knob is never tried, and the
  previous run lost that flip on `lightning_bolt`'s energy cost — the single best move in the catalogue.
  The random phase that follows leans four to one on the knobs the sweep showed can move a metric, and
  draws only from Spells the build carries: 110 of the 139 knobs sit on turned-off Spells.
- **A bug the sweep found immediately**: the first sweep returned zero playable candidates. `violations()`
  rebuilt the candidate catalogue with only its Spells and files, so the tiers went missing, so a tiered
  "before" was compared against an untiered "after" and every candidate read as adding the catalogue's three
  progression pairs. `Content.with_spells` is the one way to say "the same catalogue with other numbers in
  it" now. 40 of 58 sweep moves are legal; 6 are refused for genuinely creating a same-tier dominance.
- **The run**: 52 candidates, 106 evaluations, **score 104.05 to 15.56** against 88.76 for the run before.
  Five moves, and the first is the one that was never tried:

  | Move | From | To |
  | --- | --- | --- |
  | `lightning_bolt` energy cost | 2 | 3 |
  | `guard` Spell initiative | 1 | 0 |
  | `basic_attack` damage | 1 | 2 |
  | `pummel` critical chance | 0.667 | 0.717 |
  | `rejuvenate` heal | 3 | 2 |

  Five of the nine targets are inside their band: average rounds **9.44**, draws, round cap, fizzle rate
  **0.156**, and the skill gap. `spellUsageShare` falls from 0.855 to **0.357** and entropy rises from 0.74
  to **1.93**. Only 3 of 52 candidates changed no metric, against 11 of 32 before, which is the draw no
  longer landing on Spells nobody casts.
- **What is left is the first-mover edge**: `player1WinShare` 0.640, 9.7 of the remaining 15.6 points.
- **And a negative result worth more than the run**: the entry below said the two findings should compose.
  They do not. `lightning_bolt` at 3 *and* `pummel` at Spell initiative 0, measured together, score
  **25.27** — worse than the sweep's five moves, and `player1WinShare` goes to **0.680**, worse than either
  change alone. Taking the cheap unlock's tempo away helped while `lightning_bolt` cost 2 and hurts once it
  costs 3. A proposal is only valid for the catalogue it was measured on, which is an argument for
  searching again after every accepted change rather than stacking proposals.
- **Decision**: still apply nothing. The tool is now worth pointing at the question, and the question is
  what `lightning_bolt` should cost, with the first-mover edge measured again afterwards.

## 2026-09-10. `tune-content` on the nine Spells: the first-mover edge finally moves, and it costs length

- **What this is**: the entry the one below promised. No content changed — this is what the search proposes,
  measured, and nothing from it is applied. 40 candidates at `--seed 1 --iterations 10 --neighbours 4`, 82
  evaluations, about a quarter of an hour on content `c0ec6984`.
- **Score 104.05 to 88.76** over five moves (ADR 0021 scores the distance outside every band, zero being on
  target):

  | Move | From | To |
  | --- | --- | --- |
  | `pummel` Spell initiative | 1 | 0 |
  | `pummel` damage | 2 | 1 |
  | `lightning_bolt` damage | 3 | 4 |
  | `lightning_bolt` critical chance | 0.667 | 0.617 |
  | `poison_slash` energy cost | 2 | 3 |

- **The number that moved is the one nothing had moved**: `player1WinShare` **0.665 to 0.535**, inside the
  0.45 to 0.55 band for the first time since the first digest. Making matches half again as long did not
  touch it (the entry below); taking a point of Spell initiative off the cheapest unlock did. That is ADR
  0017 read backwards: unlocking is how a Creature gets faster, so the cheapest unlock decides who acts
  first for the rest of the match, and `pummel` at one energy is the cheapest there is.
- **It paid for that in length**: `averageRounds` **8.135 to 5.865**, back outside the 8 to 16 band we had
  just entered, and `fizzleRateA` 0.180 to 0.209. The objective weighs the first-mover share at 3 with a
  scale of 0.05 and length at 2 with a scale of 3, so a tenth of the share is worth more to it than two
  rounds. That is a choice written in `data/balance/knobs.json`, not a fact about the game, and this run is
  the first evidence about whether it is the right one.
- **The dominant Spell is untouched**: `spellUsageShare` 0.855 to 0.846, still 71 of the remaining 88.8
  points. A hill climb moving one number one step cannot close a gap that wide, and it raised
  `lightning_bolt`'s damage rather than lowering it, because damage barely moves its share while it does
  move the length the objective is also chasing. `spellsNeverCast` stayed at 5 of 9.
- **Decision**: apply nothing. The proposal names the right lever and the wrong price for it. What
  `lightning_bolt` should cost is a design question, and answering it first is what would let a search
  spend its budget on the rest.

## 2026-09-10. The core three classes only: matches lengthen, and one spell takes 86% of the casts

- **What changed**: the content, not the engine. The Creature moved from the full talent tree onto a new
  `talent-tree:core_classes:v1` — the same root and the same three class nodes, without the nine
  specialisations — and the old tree and the 27 specialisation Spells were turned off with
  `"enabled": false` (ADR 0015). The build carries **9 Spells** instead of 36: `wait`, `basic_attack`,
  `heavy_strike`, then `pummel` and `guard`, `poison_slash` and `throwing_star`, `lightning_bolt` and
  `rejuvenate`. Nothing on disk was deleted; turning the flags back restores the catalogue.
- **Why**: the balance signals on 36 Spells were dominated by content no match reaches. 26 of them were
  never cast, so two thirds of the tuner's score was dead content and no single number could move it
  (ADR 0021). Nine reachable Spells is a catalogue a balance pass can actually close.
- **Digest**: `benchmarks/c0ec6984e2c1df0805941aab44649f2d54ebcb5aadb4fc8c1de56a2ee4a7a960.json`, `Greedy`
  against `Greedy` on the 200 benchmark seeds, mirrored, engine `5475d117d0eb`, against
  `50a291d5...` before. Content is the only axis that moved.
- **Matches got longer, which is what we wanted**: **8.1 rounds on average** against 5.8, spread 6 to 10
  against 5 to 9, still every one by elimination and none by the round cap. The winner ends on **10.1
  health of 60** against 17.0, so the matches are longer *and* closer. Taking the specialisations away took
  away the big single casts — Psycho Rush and Hateful Sacrifice hit for 9 and 10 — and what is left trades
  in twos and threes.
- **The first-mover edge did not move**: 133 of 200, **66.5%**, against 128 and 64.0%. Well outside the
  band the objective asks for and unchanged by making matches half again as long, which says the edge is
  not about how long the race is.
- **Entropy collapsed, and the reason is one Spell**: **0.74 bits** against 2.21. `Greedy` declares four
  Spells of the nine, and `lightning_bolt` takes **4172 of 4881 landed casts, 85.5%**, for 20094 of the
  22000 damage dealt. `heavy_strike` lands 400, `pummel` 288, `throwing_star` 21. `wait`, `guard`,
  `poison_slash`, `rejuvenate` and `basic_attack` are never declared at all.
- **Which the audit already predicted**: `check-knobs` reports three strict dominances in this catalogue,
  and one of them is `lightning_bolt` over `heavy_strike` — same targeting, same cost of 2, same Spell
  initiative, 3 damage each, and a critical chance of 0.667 against 0. There is no reason to ever declare
  the second, and `Greedy` does not. The other two are `pummel` and `throwing_star` over `basic_attack`.
- **Fizzles fell to 18.0%** from 21.4%, and criticals rose to 53.7% from 25.4%: with `lightning_bolt`
  taking most casts, the run's critical rate is close to its own.
- **Objective**: `spellsNeverCast` moved from a band of 12 to a band of **2**. Twelve was written for a
  catalogue of 36 and cannot be exceeded by one of 9, so it had stopped being a target at all.
- **Decision**: keep. This is the content the balance work continues on, and it poses exactly one obvious
  question — what `lightning_bolt` should cost — plus the two dominances under it. The next entry is what
  `tune-content` does with that.

## 2026-09-10. Energy gets a price and a lasting kind: nothing moves, and that is the finding

- **What changed**: (a) `ActionScorer` scores `EnergyOutcome`, which it never did — the switch matched
  `HealOutcome` and `ConditionOutcome` and let energy fall through to zero, while the `weights.Energy` term
  beside it priced only the energy the actor *keeps* after paying. (b) `EnergyRegeneration` joins the effect
  taxonomy, `Regeneration` with energy in place of health, given before the healing and the bleeds and never
  wasted because energy has no cap (ADR 0020). (c) `SpellOutcome.Buffs` splits into `DefenseBuffs` and
  `InitiativeDebuffs`: one number for two stats could not say which one a spell moved. (d) An outcome on a
  creature the same action kills is no longer scored on any of the three paths, since `Heal` and `GainEnergy`
  both return zero on a corpse.
- **Digest**: unchanged. `benchmarks/50a291d5…7a87.json` verifies with **400 of 400 matches identical**,
  engine `094515bf7822`, schema `features:v3+18b1bd690dff`. This entry exists anyway, because the absence of
  movement is the result: the journal's rule is one entry per change that moves a number, and the number that
  refused to move is the whole point.
- **What started it**: 200 recorded `Greedy`-vs-`Greedy` matches, read off the traces. Of **5096
  declarations, none was one of the five `EnergyGain` spells** — not Wait, which every creature knows from
  round one, not Summon Minions, which gives 3 energy for a cost of 2. The agent was structurally unable to
  value any of them, so the count is not a preference, it is a blind spot.
- **Why fixing the blind spot changed nothing**: a point of energy is worth 0.2, and the wall is arithmetic.
  Best-case one-step score of what `Greedy` actually picks against what it never picks:

  | Spell | Score | | Spell | Score |
  | --- | --- | --- | --- | --- |
  | `meteor` | 18.0 | | `restorative_gush` | 4.8 |
  | `engulfing_flames` | 12.0 | | `healing_screech` | 3.2 |
  | `ice_spear` | 7.0 | | `restorative_burst` | 2.8 |
  | `lightning_bolt` | 5.0 | | `rejuvenate` | 2.4 |
  | `heavy_strike` | 3.0 | | `summon_minions` | 0.6 |
  | `basic_attack` | 1.0 | | `wait` | 0.2 |

  The best heal in the game, cast at the one moment it caps out, loses to `lightning_bolt`. The best energy
  spell loses to `basic_attack`. No decision flips, so the 400 mirrored seeds replay identically.
- **The same measurement, for healing**: **0 of 5096** declarations were a heal, and the reason is not the
  declaration step. In **1798** declarations the actor was missing health — often 8 to 14 of 20 — and in
  exactly **2** of those did it know a heal at all. The break is at the unlock: **2 heal unlocks out of
  4660**. And **47% of evolution choices are made at full health**, where a heal is worth exactly zero by
  construction, round 1 being 800 choices at 100% full — the one round `rejuvenate` is offered beside
  `lightning_bolt`. The spell is on the table precisely when it cannot score.
- **What the fix does buy, then**: the knob exists. Before, no value of `weights.energy` could make an energy
  spell visible, because the gain was multiplied by nothing; `search-weights` could not have found a setting
  that worked, whatever it tried. Now it can, and whether one exists is an empirical question about the
  content rather than a property of the code.
- **Two things I predicted and measured to be false**, both corrected in ADR 0020. I expected the digest to
  move — it does not. And I wrote that energy touching no health made its place in the tick order irrelevant
  — it is not: every upkeep loop skips the dead, so a creature its own bleed kills that round keeps the
  energy it was just given and still reports a tick. No health number and no match result depends on the
  position, which is the narrower claim that survives.
- **`features:v3`**: publishing a condition kind changes the observation layout, so nothing trained under v1
  or v2 is comparable. `EnergyRegeneration` sits beside `Regeneration`, so the three over-time effects stay
  together and every index from +10 on shifts. The Python side reads all three versions; the engine plays
  only the one it reads.
- **No spell uses the new kind yet**, deliberately. Re-pricing Momentum and Summon Minions in the change that
  adds the kind would leave nothing able to say which half moved the numbers. That is also why this change is
  measurably behaviour-preserving, which is what let the two review fixes ride along without muddying it.
- **Still not a balance pass**: nothing here was tuned. Both findings now point at the same place — the
  content's numbers, not the agent — and neither the heal amounts nor the energy amounts have ever been
  chosen by measurement.

## 2026-09-09. Initiative on unlock, priced, and a healing over time: the first-mover edge falls to 64%

- **What changed**: three things, and the digest cannot separate them, because `main` took ADR 0017 and
  ADR 0018 without regenerating. (a) Unlocking a spell raises the creature's Base initiative by the Spell
  initiative (ADR 0017), so evolving is also how a creature gets faster. (b) The heuristic agents price that:
  a new `initiative` weight at 0.5, an unlock scored as its combat value plus what it buys, and an
  `InitiativeDebuff` moved from `w.buff` to `w.initiative` so one point has one price (ADR 0018).
  (c) `Regeneration` joins the effect taxonomy, the healing counterpart of `Bleed`, healing before bleeds
  tick; Healing Screech goes back to the prototype's `Heal 2` plus `Regeneration 2` for a round (ADR 0019).
- **Digest**: `benchmarks/50a291d52dbafd5ba25ee92843b04ed92831d1064f436ea667ddc11f5a5c7a87.json`, `Greedy`
  against `Greedy` on the 200 benchmark seeds, mirrored, default rule set, engine `93b090446b0d`. Schema
  `features:v2+0129dfba4876`: publishing a condition kind changes the observation layout, so this is the first
  run under `features:v2` and nothing trained on v1 is comparable to it.
- **The number that moved**: player 1 wins **128 of 200** distinct matches, **64.0%** (95% interval 57.3% to
  70.7%), against **163 of 200, 81.5%** on the previous content. Read on 200, not 400: both agents are
  `Greedy` and an agent is seeded from the match seed and the slot, so the mirrored pass replays the same
  match, confirmed here on 200 of 200 seeds. The first-mover edge is still far outside noise, but a third of
  it is gone, and initiative is the only thing that could have moved it: the creature that unlocks first is no
  longer the creature that acts first for the rest of the match.
- **The rest barely moved**: 368 of 400 entries differ but 286 keep the same winner. Matches run 5 to 9
  rounds, **5.8 on average** against 5.7, still every one by elimination and none by the round cap. The
  winner ends on **17.0 health of 60** against 24.2, so the matches are closer as well as less decided by the
  slot. Fizzles 21.4% against 24.5%, crits 25.4% against 24.7%, spell entropy **2.21 bits** against 2.33.
- **Regeneration is in the engine and absent from the play**: `Greedy` declares ten spells and Healing Screech
  is not among them — it is one of the two declared by fewer than eight sides, and the `Heal` column of the
  spell table is zero on every listed row. The effect resolves, the content uses it, and the baseline still
  never heals. That is the same finding as the entry below, unchanged by giving the defensive half a better
  tool: a one-step lookahead that scores damage does not buy a heal, and a match that ends in under six rounds
  does not get to want one.
- **What the spell table does say**: `pummel` 84.2% on 19 sides and `engulfing_flames` 68.7% on 249 lead;
  `throwing_star` 36.5% and `basic_attack` 39.0% trail. The Berserker line has all but vanished —
  `tornado` 7 declarations, `psycho_rush` 2, against 90 and 131 before — which is what pricing initiative
  did to a line whose spells cost a lot and buy no tempo.
- **Supersedes**: the bullet of the entry below reading "`spell.Stats.Initiative` is dead data ... nothing in
  the domain reads a spell's initiative". True of the engine when it was written, false from ADR 0017 on. The
  stat is `SpellStats.SpellInitiative` now, and `Creature.UnlockSpell` reads it.
- **Still not a balance pass**: none of these numbers was chosen. The `initiative` weight at 0.5 is reasoning,
  not measurement, and `search-weights` has never seen it.

## 2026-09-09. The spells stop being placeholders: matches get four times shorter, player 1 takes 81.5%

- **What changed**: content only. The 36 spells were placeholders — every one of them `Damage 1`, cost 0,
  initiative 1, one enemy — and now carry the prototype's numbers, read from
  `legacy/DownfallArena/DA.GameResources` and documented spell by spell in `docs/domain/spells.md`. Costs 0 to
  4, initiatives 1 to 3 (inert — see the last bullet), Critical chance bonuses 0 to 0.667, 27 single-target
  against 9 multi, 23 aimed at enemies against 9 at allies and 4 at the caster, all seven effect kinds in use
  instead of one, and the real Creature class on each. Nothing in the engine or the agents moved.
- **Digest**: `benchmarks/a63d952bbb54d31f74b66244018cd9aa015b1cc4c3ef5e74cc8da66df3b93153.json`, played by
  `Greedy` against `Greedy` on the 200 benchmark seeds, mirrored, under the default rule set. Engine
  `dc6e40ffb2a2`. It does not supersede `34c616d3...` so much as leave it behind: different content, so the
  two are comparable as a whole and not term by term.
- **Numbers**: 400 matches, player 1 wins **326**, player 2 wins **74**, **no draw at all**, every match by
  elimination. The placeholder content gave 218 / 96 / **86 draws**. Matches now last **5 to 8 rounds, 5.7 on
  average** (170 at five, 172 at six, 52 at seven, 6 at eight) against 19 to 23 and 22.0 before; the round cap
  of 30 is as far out of reach as it ever was. The winner ends with **24.2 health of 60 on average**, spread 1
  to 40, against 3.4 and a spread of 1 to 12. All 400 entries differ from the old digest, and 204 of them keep
  the same winner.
- **What drives the shortening**: the starting kit is `wait`, `basic_attack` and `heavy_strike`, and
  `heavy_strike` went from 1 damage to 3. Three times the damage per activation against unchanged health is
  the whole of the four-fold drop in length, and the rest follows from it: a match decided in five rounds
  leaves no room to trade back, so the loser is eliminated wholesale rather than ground down to a draw, and
  the winner keeps two thirds of a creature's health that the twenty-round grind used to consume.
- **The player 1 edge got worse, not better**: 54.5% of the wins became **81.5%**. Read it on 200 matches,
  not 400: both agents are `Greedy` and an agent is seeded from the match seed and the slot, so the mirrored
  pass replays the same match and the digest holds each one twice. 163 of 200, 95% interval 76.1% to 86.9%,
  is far outside the noise all the same. Reading it with the length: the damage race is now short enough that
  the slot that wins the initiative tie is close to deciding it. This is the number a rule change should move
  (initiative, pick order, the energy curve), and it now has room to move in.
- **Entropy 0.23 to 2.33 bits, which is the answer `ci-9` asked for**: that entry closed the value-learning
  investigation on the finding that the content posed no decision, and named the sign to watch. `Greedy` now
  declares **nine** distinct spells where it declared two, and 2.33 bits is 74% of the 3.17 available over
  nine. Of 5182 declarations: `heavy_strike` 44.9%, `ice_spear` 18.7%, `lightning_bolt` 15.7%, `meteor` 8.1%,
  `engulfing_flames` 4.7%, `pummel` 2.6%, `psycho_rush` 2.5%, `tornado` 1.7%, `basic_attack` 1.0%. The
  content poses a decision now. Whether the decision is *good* is the rest of this entry.
- **Fizzles 5.1% to 24.5%, and that is the cost of the short match**: a quarter of all declarations no longer
  land. Resolve rates split the field — `pummel` 94.1%, `lightning_bolt` 87.3%, `engulfing_flames` 84.6%,
  `heavy_strike` 80.0% against `ice_spear` 55.5%, `basic_attack` 49.0%, `psycho_rush` 48.9%, `tornado` 43.3%.
  Reading, not measurement: the timeline is fixed before intents are declared, so in a five-round match the
  actor or its target is often dead by the time the slot comes up. The fizzle breakdown by reason is not in
  the output; it is what would confirm this.
- **Crits 4.9% to 24.7%**: the placeholder spells all had a Critical chance bonus of 0 on a creature at 0.05,
  so a crit was the creature's own rate. The bonuses now run to 0.667 and the roll moves.
- **Twenty-seven of the thirty-six spells are never declared**, and the shape of the nine that are is one
  shape: `Heal`, `Stun` and `Bleed` are **zero** across the whole table. `Greedy` never heals, never stuns,
  never bleeds; the only non-damage effect it ever applies is `ice_spear`'s initiative debuff, 378 times. It
  evolves down Sorcerer to Wizard and Brawler to Berserker and never touches the defensive half of the
  catalogue. Two readings, and they are not exclusive: a one-step lookahead that scores damage is the wrong
  instrument for a heal, and a match that ends in five rounds never gets to want one.
- **The class lines are not close**: win share of the sides that declared each spell, against 50% —
  `engulfing_flames` **72.0%** (218 sides), `meteor` 54.9% (293), `lightning_bolt` and `heavy_strike` 50.0%
  (400 each), `ice_spear` 49.4% (395), `tornado` 33.3% (60), `pummel` **27.9%** (136), `psycho_rush`
  **16.8%** (131). Correlation, not cause: a side that declared `pummel` is a side that went Brawler, and it
  is the Brawler line that loses. The Wizard line wins, the Berserker line is a trap, and that gap is the
  first balance question this content actually poses.
- **`spell.Stats.Initiative` is dead data**: the initiatives 1 to 3 the prototype gave its spells change
  nothing. `TimelineBuilder` orders the round from the speed choices and `creature.CurrentInitiative`, and the
  timeline is built in Planning, before any intent exists; nothing in the domain reads a spell's initiative.
  Only `ContentAudit` does, and its `FlatSpellStat` line for it — "the spell a creature declares never changes
  when it acts" — describes an effect the engine does not implement. `InitiativeDebuff` is the only thing that
  moves turn order today, which is most of why `ice_spear` earns its place.
- **Not settled**: none of these numbers is a balance pass. They are the prototype's, and
  `docs/domain/spells.md` lists the six legacy mechanics that have no counterpart in the effect taxonomy
  (ADR 0012) and were dropped or approximated — among them the caster-side costs that made
  `hateful_sacrifice` and `parasite_jab` a choice rather than a nuke.

## 2026-09-09. `ci-9`: the baseline works, and it says the content has no decision in it

- **What changed**: ADR 0016 implemented. Same run as `ci-5` otherwise — the thousand-match explored dataset,
  alpha 10, min samples 10 — so the two-part fit is the only difference. Engine `1cc41a7797e3`.
- **The fit improved**: loss 0.8050 to **0.7705**, r² 0.1876 to **0.2225**. 293 fitted actions of 455, the same
  as `ci-5`, as expected. Against `Greedy`: 0 of 400, the eighth time. Against `Random`: 83.2%.
- **The number that ends the investigation**: **`baselineR2` 0.2815 against `r2` 0.2225.** The position alone
  explains more of the held-out return than the position and the action together. The action rows do not
  merely add nothing; they add variance, and the prediction is better without them.
- **Why, and it is not the learner**: a creature starts with three spells, and they are
  `basic_attack` (1 damage), `wait` (1 damage, effects identical to `basic_attack` to the character) and
  `heavy_strike` (1 damage plus Bleed 1 for one round). All three cost 0 energy, all three have initiative 1,
  all three target one enemy. `heavy_strike` therefore strictly dominates: same cost, same speed, same
  targeting, strictly more damage. `wait` and `basic_attack` are one spell under two names. There is no
  trade-off on any axis, and `baseEnergy` is 0, so the resource axis is inert too.
- **Which explains every earlier number**: `Greedy`'s spell entropy of 0.23 is not a defect, it is correct
  play; exploration's deviations are uniformly worse by an amount the position already carries; and an action
  that carries no information cannot be fitted, however the data is recorded or the model is shaped. The
  metric added to diagnose the model diagnosed the content instead.
- **Decision**: stop here on value regression. It is not broken, it is asking a question this content does not
  pose, and the temporal-difference target considered next in ADR 0016 is dropped with it: better credit
  assignment for a decision that does not exist would refine an instrument aimed at nothing. Behaviour cloning
  stays the loop's working learner. The loop's own balance signals — spell entropy, player 1 share, draw rate,
  round cap share — are the instrument for the content work that comes next, and `Greedy`'s entropy rising
  above 0.23 is the sign that the content finally offers a choice.

## 2026-09-09. `ci-5`: filling the empty rows changes nothing, so the shape is the answer

- **What changed**: `--value-min-samples` 50 to 10 on the same thousand-match explored dataset, alpha still
  10, so the regularization does the work the threshold was doing. Engine `a621693e9d91`.
- **The rows filled up**: **293 fitted actions of 455**, against 191. A hundred and two keys that were
  constants now have a regression of their own.
- **Nothing else moved**: r² 0.1895 to **0.1876**, loss 0.8031 to 0.8050, accuracy 0.2941 to 0.2947. The
  held-out fit is flat to three digits, and against `Random` the policy got worse, 88.8% to 82.0%. Against
  `Greedy`: 0 of 400, the seventh time.
- **What that closes**: the data axis. Exploration was necessary and not sufficient (`ci-1` to `ci-3`); five
  times the matches tripled the fit and moved nothing (`ci-4`); giving two thirds of the actions a model
  instead of two fifths moved nothing either. The rows that had no model were not the bottleneck, so no
  amount of recording is going to be.
- **What is left**: the shape. Each action key is a regression of its own, fitted on the raw match return of
  the steps where it was taken, and then compared with the others at one state. A step's return is the
  outcome of a match of about a hundred and fifty decisions: it measures the position far more than the move,
  and each row's intercept is calibrated on its own slice of positions. That is the same sentence as the very
  first diagnosis in this journal, and the data has now ruled out every explanation except it.
- **Decision**: ADR 0016, since accepted: fit one state-value model on every step and regress each action on the
  residual instead of the return. The baseline is the best-determined part of the model and subtracting it
  leaves each row only the part of the outcome its own action is responsible for.

## 2026-09-09. `ci-4`, a thousand matches: the fit triples, the win rate does not move

- **What changed**: `--matches` 200 to 1000, everything else as in `ci-3` (explored at 0.2 from seed 1,
  alpha 10, min samples 50). Engine `007057d2103b`, content `34c616d3…80d7`. Fifteen minutes on a runner.
- **The data**: 313,297 steps over 2,000 episodes, and **455 action keys** where 200 matches found 382.
- **The fit**: **191 fitted actions of 455** (42%, against 24% before), loss 0.9607 to 0.8031, r² 0.047 to
  **0.190**, accuracy 0.304 to 0.294.
- **The win rate**: 0.0% against `Greedy`, 0 of 400, the sixth time. 88.8% against `Random`, down from 94.8%,
  and a spell entropy of 2.58 against 2.70.
- **The part that matters for what to do next**: the number of action keys grows with the data. Five times
  the matches found seventy-three new keys, so the share of rows that clear the threshold climbs slowly
  instead of converging. More matches is not a trajectory that ends anywhere.
- **Also**: the clone, trained on the pure 1000-match dataset, came out at 35.5% against `Greedy` (interval
  32.3% to 38.7%) where the 200-match clone reached 38.0%, with its best epoch at 20 instead of 3. Recorded
  as a fact, not read as a regression: it is one run and the intervals nearly touch.
- **Decision**: one more cheap run before blaming the shape of the model. On this dataset, drop
  `--value-min-samples` to 10 and keep alpha 10, so the regularization does the work the threshold was
  doing. If that fits most of the 455 rows and the win rate is still zero, the threshold is no longer an
  excuse and the suspect is the shape itself: one independent regression per action key, compared with each
  other at a single state, with nothing tying them together. That would be an ADR.

## 2026-09-09. `ci-3`: three quarters of the actions have no model at all

- **What changed**: nothing but the diagnostic. `ci-3` repeats `ci-2` exactly — same explored dataset, same
  alpha 10 and min samples 50 — and returns the same numbers to the digit: loss 0.9607, r² 0.04694, accuracy
  0.3044, 0 of 400 against `Greedy`, 94.75% against `Random`. The loop is deterministic and `fittedActions`
  costs nothing.
- **The number**: **93 fitted actions out of 382**. Two hundred and eighty-nine keys kept the mean of their
  few examples instead of a regression, and a row that is a constant scores the same in every state. So for
  three quarters of the legal moves the policy cannot tell one position from another; it simply prefers
  whichever constant is largest. That is what the spell entropy of 2.70 is made of.
- **What it means, read with `ci-1`**: this is a squeeze, not a mystery. At a threshold of 5 nearly every row
  gets a regression, on far too few examples, and the fit comes out worse than the mean. At 50 the fit turns
  positive and three quarters of the rows lose their model. 62,358 steps over 382 keys is about 163 per key
  on average, skewed enough that only 93 clear fifty in the training split. Exploration multiplied the action
  keys by five and the dataset did not follow.
- **Decision**: raise `--matches` to 1000 before concluding anything about the shape of the model. The
  question "is one independent regression per action the wrong shape" cannot be answered on a dataset where
  most of those regressions were never fitted.

## 2026-09-09. The tuned exploring run (`ci-2`): the fit moved, the win rate did not

- **What changed**: the same explored dataset as `ci-1` (`explore:0.2`, 200 matches from seed 1, content
  `34c616d3…80d7`), trained with `--value-alpha 10 --value-min-samples 50` instead of the defaults. It was
  asked for by committing `learning/experiments/next.json`, and the loop ran on the pull request that
  carried it. Engine `454dc1a937e6`, clean: the stamp defect of `ci-1` is gone.
- **The fit moved**: r² -0.146 to **0.047**, loss 1.155 to 0.961, accuracy 0.293 to 0.304. Against `Random`
  the policy went from 82.2% to **94.8%** and its spell entropy fell from 3.40 to 2.70. The regularization
  did produce a measurably better policy.
- **The number that matters did not**: 0.0% against `Greedy`, 0 of 400, for the fifth time.
- **What that settles**: the knobs were the confound in `ci-1`, and they are not the obstacle. Two runs now
  bracket them — worse than the mean at alpha 1 and min samples 5, positive at alpha 10 and min samples 50 —
  and the win rate against `Greedy` is exactly zero in both. Exploration at this rate, on a dataset this
  size, does not make value regression competitive with the bot that produced the data. ADR 0014 was
  necessary, since the counterfactuals now exist, and it is not sufficient.
- **The one thing still unmeasured**: with 382 action keys and a threshold of 50, an unknown share of the
  rows kept a mean instead of a model, and a row without a model cannot tell two states apart. `train-value`
  now reports `fittedActions` beside `actions`, so the next run says it outright.
- **Decision**: read that number first. If most rows are starved, the answer is more matches. If most are
  fitted, the remaining suspect is the shape itself — one independent regression per action key, ranked
  against each other at a single state — and the next thing to try is a single model over the state and the
  action together, which needs its own ADR.

## 2026-09-09. First exploring run (`ci-1`), still 0 of 400, and two things moved at once

- **What changed**: the value policy trained on a dataset recorded with `explore:0.2` instead of pure
  `Greedy` self-play (ADR 0014), on the same content `34c616d3…80d7`. This is also the first run of the
  `Learning loop` workflow, on the merge commit of pull request #24, so the whole turn played on the CI
  runners in three minutes with nobody at a keyboard. Its stamp reads `2988cd742f9c-dirty` because the
  workflow wrote its log inside the checkout before the build stamped the version; the tree was otherwise
  that commit exactly. Fixed in the same change as this entry, so the next stamp is clean.
- **The two datasets**, 200 matches each from seed 1: pure `Greedy`, 63,706 steps over 75 distinct action
  keys; explored, 62,358 steps over **382** action keys.
- **Value policy**, at the defaults (alpha 1.0, min samples 5): loss 1.155, **r² -0.146**, accuracy 0.293 on
  12,320 held-out steps. 0.0% against `Greedy`, 0 of 400 for the fourth time, and 82.2% against `Random`.
  Its spell entropy against `Greedy` is 3.40, next to `Random`'s 4.25 and nowhere near `Greedy`'s 0.20: the
  policy scatters instead of choosing.
- **Clone policy**, still trained on the pure dataset and therefore still the control: 98.6% accuracy, 38.0%
  against `Greedy` (score 0.475), 100% against `Random`. Unchanged, as it should be.
- **What this settles and what it does not**: exploration did deliver what ADR 0014 asked of it. Seventy-five
  action keys became 382, so the actions `Greedy` never plays now carry samples of their own. But the same
  62,000 steps are spread over five times as many independent regressions, and this run used the defaults
  rather than the `--value-alpha 10 --value-min-samples 50` that had taken r² from 0.216 to 0.372 on the
  greedy dataset. The dataset and the knobs moved together, so a fit that is now worse than predicting the
  mean does not by itself refute the ADR.
- **Decision**: repeat the run with those two knobs on the explored dataset, which is one dispatch of the
  workflow. If the win rate is still zero once the fit is no longer worse than the mean, the remaining
  suspect is the shape of the model itself, one independent row per action, and the next thing to try is a
  single model over the state and the action together.

## 2026-09-09. Two tuning attempts and a mixed dataset, all still 0 of 400

- **What changed**: nothing in the engine or the content; three trainings of the value policy on the same
  content `34c616d3…80d7`, evaluated against `Greedy` on the 200 benchmark seeds, mirrored.
- **The three attempts**: 200 matches of `Greedy` self-play at the defaults (run "premier"); the same
  fivefold larger with `--alpha 10 --min-samples 50` (run "second"); and the union of a 200-match `Greedy`
  self-play with a 200-match `Random` self-play, `--allow-mixed`, at those same settings. Win rate against
  `Greedy`: 0.0000 each time, no draw, 400 matches each time. The held-out fit did improve between the first
  two (r² 0.216 to 0.372, loss 0.729 to 0.508), so the models are genuinely different and the metric that
  matters ignored it.
- **The control that matters**: behaviour cloning on the same data, through the same `PolicyAgent`, reaches
  98.5% accuracy and 38.0% against `Greedy`, statistically `Greedy`'s own 39.25%. The encoding, the policy
  file, the agent and the evaluation are therefore sound, and the fault is in what value regression is asked
  to learn, not in the plumbing.
- **Why**: each action key gets its own regression, fitted only on the steps where that action was taken.
  Under a deterministic policy those subsets are disjoint state distributions, so `heavy_strike` is fitted on
  the states where `Greedy` wanted it and `basic_attack` on the leftovers. The return then measures how good
  those situations were, not how good the action is, and ranking two such rows at one state compares models
  calibrated on different worlds. `Random` self-play does not repair it: it covers actions in states no
  strong policy visits, with returns a single action barely moves. This supersedes the "overfitting on a rare
  action" reading of the entry below, which explained the extreme weights but not why more data and more
  regularization changed nothing.
- **Decision**: stop tuning this learner. ADR 0014 proposes recording with an exploring agent, which is the
  one change that puts the same state distribution behind every row. Both cheap attempts it names as
  prerequisites are now done and both failed.

## 2026-09-09. First full turn of the loop (`scripts/iterate.sh`), run "premier"

- **What changed**: nothing in the engine or the content on purpose; this is the first end-to-end run of the
  L7 loop, on content `34c616d3…80d7` (the digest already committed), engine `d3f1fe3284bc-dirty` (the L7
  pull request's head at the time, `#23`, with local uncommitted state, so read this as a smoke test of the
  loop rather than a citable baseline; a clean re-run on the merged engine is the number to keep). 200
  matches of `Greedy` self-play recorded (`simulate --record`, base seed 1), a `train-value` and a
  `train-clone` policy trained on them, both evaluated against `Greedy` and `Random` on the 200 benchmark
  seeds, mirrored (seed set `733404048`).
- **Baselines** (match the committed digest and the L5/L6 journal entries, as expected since content and
  agents did not change): `Greedy` vs `Greedy` 39.25% each, 54.5% player 1 share, 21.5% draws, 22.0 rounds;
  `Greedy` vs `Random` and `Random` vs `Random` unchanged from before.
- **Clone policy**: 38.0% against `Greedy` (interval 34.7% to 41.3%, score 0.475) — statistically the same as
  `Greedy` playing itself (39.25%, interval 36.4% to 42.1%). This is the ceiling behaviour cloning is supposed
  to reach (`docs/learning/explained.md`: "the clone can never be better than what it copies") and it reached
  it: on 200 matches of `Greedy` self-play, the clone reproduced `Greedy`'s own strength almost exactly. 100%
  against `Random`.
- **Value policy — root cause found, with `policy.json` and `training.jsonl` in hand**: 0.0% against
  `Greedy` (0 wins, 0 draws, 400 matches), 99.5% against `Random`. `training.jsonl` shows why the fit itself
  is weak before the match numbers even come in: r² 0.216 and accuracy 25.7% on the 12,683 held-out steps
  (validated on one match in five, never trained on) against 51,023 training steps — a linear model over 383
  features explains barely a fifth of the return's variance. The evaluation's `spellUsage` shows what that
  training failure does at the table: against `Greedy` the policy casts `heavy_strike` 16 times against 9499
  `basic_attack`s (11996 actions total) — almost the mirror image of `Greedy`'s own play, which casts
  `heavy_strike` roughly 96% of the time (L5 journal entry). Against `Random` it is far more reasonable
  (10819 `heavy_strike` against 6193 `basic_attack`), so the policy is not broken in general, only around this
  one choice.
  The weights explain the "why": `intent:X:spell:basic_attack:v1` and `speed:X:Quick` carry the exact same
  bias for creature slots 0 and 1 (`-0.467` and `-0.228` respectively, to the last digit) and an extreme
  outlier for slot 2 (`-6.958` for `Quick`, `-6.355` for `basic_attack`, against a fallback of `0.012` and
  every other bias in roughly `[-0.6, 1.7]`). The identical biases are not a coincidence: `docs/learning/artifacts.md`
  already says the speed and intent sub-phases hand the agent the same board observation for a creature, and
  `Greedy` picks `basic_attack` (and, separately, `Standard`) far less often than `heavy_strike`/`Quick` — the
  same benchmark showed a 578-versus-15039 split. A linear ridge regression over 383 features, fit per action
  key at the default `alpha=1.0`, overfits that thin, low-variance slice of `basic_attack` rows into a large
  negative coefficient; slot 2 happening to draw the worst luck of the three explains the outlier. The value
  agent then reads that coefficient at the table and avoids `heavy_strike` almost entirely.
  Concretely worth trying next: raise `--min-samples` well past the default 5 so a rare key like
  `basic_attack` falls back to the dataset-wide mean instead of fitting its own noisy row; raise the ridge
  `--alpha` (383 features per key is a lot to regularize at the default strength); and record more than 200
  matches so the rare choice gets enough support to fit honestly. None of these are engine bugs — the loop,
  the encoding, and the training code did exactly what they were asked to; the data given to them was small
  enough for the regression to memorize noise on one specific, rare action.
- **Why it matters**: the loop runs end to end, produces a report, and the two learners diverge exactly as
  the theory predicts — cloning is a safe, bounded reproduction of `Greedy` (and reached that bound), value
  regression is more ambitious and, on 200 matches, currently overfits one rare-but-important choice into a
  losing bot. This value policy is not committed under `models/`: on this evidence it is a "tune more" case,
  not a "keep" case, and a citable number needs a clean (non-dirty) engine commit besides. Next: retrain with
  a higher `--min-samples` and `--alpha` on a bigger recorded dataset, re-evaluate against `Greedy`, and only
  then decide whether to commit it with its evaluation.

## 2026-09-08. Greedy against Random, the first measured gap

- **What changed**: nothing; this is the first measurement across two agents on the same engine and content,
  run by the `Evaluate` workflow (engine `d25c67db04d1`, the merge commit of the L5 pull request, content
  `34c616d3…80d7`, schema `features:v1+31987e1de3a9`, the 200 benchmark seeds, stamped as seed set
  `733404048`), `Greedy` against `Random`, mirrored. The `Seed` the command prints at start is the session
  seed of the interactive commands, drawn at random when `--seed` is absent; an evaluation seeds every match
  from the seed file and never uses it.
- **Numbers**: 400 matches, Greedy wins 400 (100.0%, interval 100.0% to 100.0%), score 1.000, 24.5 health
  left on average, no draw, every match by elimination in 15.5 rounds on average, none by the round cap.
  Intent entropy 0.20 bits for Greedy against 4.07 for Random; fizzles 4.0% against 1.6%; crits 4.9% against
  5.2%. Greedy casts `spell:heavy_strike:v1` 17914 times and `spell:basic_attack:v1` 582 times; Random
  spreads its 12000 or so casts over all 36 spells, `spell:heavy_strike:v1`, `spell:wait:v1` and
  `spell:basic_attack:v1` leading at about 1850 each.
- **Why it matters**: the one-step lookahead beats a uniform policy without losing a match, so the baseline
  is a real bar for the learned agents (L6, L7) rather than a coin flip. The gap is so wide that it says
  little about the content: a stronger opponent than Random is needed to grade the heuristic itself, which
  is what the seed pairs against a tuned heuristic will provide. Greedy's higher fizzle rate most likely
  comes from its focus: several creatures aim at the same enemy, the first kill leaves the later actions
  without a target, and they fizzle. Random spreads its targets and rarely loses one.

## 2026-09-08. Greedy replaces Random as the benchmark baseline

- **What changed**: nothing in the engine or the content; the greedy and heuristic agents arrived (learning
  phase L5) and the benchmark now plays `Greedy` against `Greedy`, so the digest is regenerated once here.
  The previous Random digest for the same content hash is superseded, not comparable.
- **Digest**: `benchmarks/34c616d340ed0d4ac67b0874ce3ac1895c126509b94ebe2a9963dfff45af80d7.json`, the same
  content hash as before, played by `Greedy` against `Greedy` on the 200 benchmark seeds, mirrored, under the
  default rule set. Generated by CI on the L5 pull request (engine `24e35df135f7`).
- **Numbers**: 400 matches, player 1 wins 218, player 2 wins 96, 86 draws by mutual elimination, every match
  by elimination between rounds 19 and 23 (22.0 on average, none by the round cap). Per agent: 157 wins,
  39.3% win rate (36.4% to 42.1%), score 0.500, intent entropy 0.23 bits, 5.1% fizzles, 4.9% crits.
  `spell:heavy_strike:v1` is cast 15039 times against 578 `spell:basic_attack:v1`; no other spell is used.
- **Why it matters**: two identical greedy agents give player 1 a 54.5% win share against 24.0%, far outside
  the noise of 400 matches, where Random against Random showed no edge. The first mover wins the damage race
  when both sides play the same deterministic plan; a rule change that tempers it (initiative, pick order)
  now has a number to move. The low entropy and the two-spell repertoire say the current content offers the
  greedy scorer little to choose from. Greedy against Random is measured by the `Evaluate` workflow, not by
  this digest.

## 2026-09-08. First benchmark digest

- **What changed**: nothing in the engine or the content; the evaluation harness and the benchmark digest
  arrived (learning phase L4).
- **Digest**: `benchmarks/34c616d340ed0d4ac67b0874ce3ac1895c126509b94ebe2a9963dfff45af80d7.json`, the
  content of `data/` at that hash, played by `Random` against `Random` on the 200 benchmark seeds, mirrored,
  under the default rule set (team of three, two energy and two picks per round, thirty rounds, double damage
  on a crit). Generated by CI on the merge commit of the L4 pull request (engine `0554a2aa8fe3`).
- **Numbers**: 400 matches, player 1 wins 206, player 2 wins 194, no draw, every match by elimination.
  Read as a first-mover check: 51.5% for player 1 with two identical random agents, inside the noise of 400
  matches, and the mirrored pairs cancel it in every evaluation anyway.
- **Why it matters**: from this commit, any engine or content change that alters an outcome on these seeds
  fails CI until the digest is regenerated here on purpose. The greedy agent (L5) will replace the random
  baseline and regenerate it once.
