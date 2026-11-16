# 2026-09-10. Balance read per tier, and the starting kit turns out not to be a choice at all

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
