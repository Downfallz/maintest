# 2026-09-13. Warlord, 2 of 9: the branch nobody walked

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
