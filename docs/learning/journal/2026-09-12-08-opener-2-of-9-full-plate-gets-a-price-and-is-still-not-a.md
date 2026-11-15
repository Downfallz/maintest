# 2026-09-12. Opener 2 of 9: Full Plate gets a price, and is still not a choice

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
