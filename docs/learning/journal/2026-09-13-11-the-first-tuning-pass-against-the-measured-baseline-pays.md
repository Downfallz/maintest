# 2026-09-13. The first tuning pass against the measured baseline pays the bill the weight left

- **What changed**: seven numbers, found by `tune-content` on the "Tune the catalogue" workflow (run 6, seed
  0, 24 rounds of 6, 1096 evaluations, 291 catalogues played of 292 handed over). Content `9211421b` to
  **`c1b49503`**, digest regenerated and verified 400/400. Objective **12.954 to 4.773**.

  | Spell | Move |
  | --- | --- |
  | `protective_slam` | energy cost 2 to **3** |
  | `healing_screech` | Spell initiative 1 to **0** |
  | `summon_minions` | caster `Damage` 3 to **2** |
  | `poison_slash` | Spell initiative 1 to **0** |
  | `momentum` | `EnergyRegeneration` 1 to **2** a round |
  | `rejuvenate` | critical chance 0.17 to **0.22** |
  | `noxious_cure` | critical chance 0.33 to **0.28** |

- **`spellsBarelyCast` reaches 0.** ADR 0032 moved the initiative weight and left five spells cast by nobody;
  the entry after it paid one by hand and said the other four were the tuner's to settle. They are settled.
  Sixteen of the eighteen enabled spells are cast on the greedy mirror, `pummel` among them again.
- **`tierWinSpread` is all but solved**: 0.386 to **0.166** against a band of 0.15, a penalty of 0.03 where it
  was 5.59. That reading has been one of the three worst all pass. What is left is `tierUsageShare` at 0.615
  and `tierDamageSpread` at 2.727, and between them they are now 4.75 of the 4.77.
- **Three of the seven moves were illegal or impossible twelve hours ago.** `rejuvenate` and `noxious_cure`
  are critical-chance moves, and `knobs.py` refused a critical chance on a spell that does not damage until
  ADR 0033 made the multiplier reach a direct heal; `summon_minions`' move is on a caster effect, a field ADR
  0031 created and a knob added the same night. The pass found its value in exactly the space those two ADRs
  opened, which is the cleanest argument either of them will get.
- **`protective_slam` is the move both searches found.** A local pass with a much smaller budget (8 rounds of
  4, 179 catalogues) proposed the same cost of 3 and nothing else in common. It read 13.73 a round and
  `check-knobs` had it outclassing three tier-2 spells on paper; at 3 it reads **9.15** and the findings drop
  from **seven to five**. A move two independent searches reach from different seeds is not a fit to one run.
- **The baseline got harder to beat as well**: `exploit` 0.340 to **0.297**, and `skill` holds at 0.998.
  Matches run 9.200 rounds with `roundCapShare` at 0.025 — inside the band with room, where the local pass
  reached 10.885 and spent the whole margin at 0.050.
- **One thing to watch, and it is a real tension.** Two spells took a Spell initiative of **0**:
  `healing_screech` and `poison_slash`. That is the stat that, at a weight of 2.1, is a 2.1-point handicap at
  every unlock against every rival at 1 — and it is exactly the mechanism ADR 0032 recorded as what killed
  `pummel`. The measurement says it works here, both spells are still cast, and a catalogue where Spell
  initiative varies is a catalogue where the stat finally discriminates instead of cancelling out. But it is
  the second time that stat has decided something large, and nobody has yet chosen those 1-to-3 numbers for
  the job they now do. `docs/domain/spells.md` still carries that as an open question.
- **Verified rather than taken on trust**: the content hash rebuilds to `c1b49503`, the digest verifies, and
  the four evaluations replayed here read 4.77 with every target matching the proposal's own table. Every
  move was read against its spell's `keep` clauses and none of them breaks one — the cost, the crits and the
  caster damage leave each identity intact, and `momentum` keeps both the free cast and the Spell initiative
  its entry calls "half of what it is for".
