# 2026-09-13. Opener 9 of 9: Healing Screech was a tier-1 heal wearing a tier-2 badge

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
