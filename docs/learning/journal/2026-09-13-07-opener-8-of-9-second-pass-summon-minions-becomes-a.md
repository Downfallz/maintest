# 2026-09-13. Opener 8 of 9, second pass: Summon Minions becomes a summoning, and the objective charges for it

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
