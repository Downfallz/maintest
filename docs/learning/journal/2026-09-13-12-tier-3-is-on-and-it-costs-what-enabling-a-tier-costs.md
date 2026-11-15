# 2026-09-13. Tier 3 is on, and it costs what enabling a tier costs

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
