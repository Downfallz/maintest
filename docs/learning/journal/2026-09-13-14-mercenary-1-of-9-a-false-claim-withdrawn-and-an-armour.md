# 2026-09-13. Mercenary, 1 of 9: a false claim withdrawn and an armour spell raised to its tier

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
