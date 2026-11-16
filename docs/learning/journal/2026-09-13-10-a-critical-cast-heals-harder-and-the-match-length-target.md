# 2026-09-13. A critical cast heals harder, and the match-length target falls at last

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
