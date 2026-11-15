# 2026-09-10. The core three classes only: matches lengthen, and one spell takes 86% of the casts

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
