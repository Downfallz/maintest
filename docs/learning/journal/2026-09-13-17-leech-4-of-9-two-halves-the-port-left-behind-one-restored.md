# 2026-09-13. Leech, 4 of 9: two halves the port left behind, one restored and one substituted

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
