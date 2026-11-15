# 2026-10-11. Tuning pass 27: Body Slam's ward at 2, and the Predator openings come back

Content `95a78999` to `3ae51e9f`, the first pass with the Greedy of ADR 0108, the six opening targets of ADR
0109 and `search-40` in the exploit panel (run 38092515038, seed 0, 1 round of 2 after the opening sweep, 180 catalogues in 4h39).

- **What it moved.** Two knobs: Body Slam's caster defense 3 to 2 (Ironhide, Brute's level 2) and Ice Grip's
  critical chance 0.40 to 0.45 (Stormborn, Warped's level 2). Tier 1 is untouched, as it has to be.
- **The score.** 19.53 to 5.52 on the search seeds and 27.66 to 9.35 on the confirmation seeds. Body Slam came
  from the opening sweep (6.25, 10.15 on confirmation), Ice Grip from the climb that followed.
- **Replayed here on 200 seeds no candidate saw** (995317 to 995516, both seats), each opening forced on one side
  against Greedy, which opens Brute + Warped:

  | opening | main | tune 27 |
  | --- | --- | --- |
  | Predator + Predator | 0.383 (0.32 to 0.45) | **0.585** (0.52 to 0.65) |
  | Brute + Predator | 0.278 (0.23 to 0.33) | **0.445** (0.39 to 0.50) |
  | Warped + Predator | 0.359 (0.31 to 0.40) | **0.506** (0.46 to 0.56) |
  | Brute + Brute | 0.325 (0.28 to 0.37) | 0.383 (0.33 to 0.43) |
  | Warped + Warped | 0.285 (0.24 to 0.33) | 0.279 (0.24 to 0.32) |
  | Brute + Warped | 0.500 | 0.500 |

  The Greedy mirror lasts 10.3 rounds on both catalogues on these seeds, none at the cap; on the benchmark seeds
  its digests read 9.83 rounds for `95a78999` and 10.31 for `3ae51e9f`, half a round longer, still none at the
  cap. Every other target stays inside its band.
- **How.** Nothing about Predator changed: Greedy's own line got weaker. It buys Ironhide behind its Brute, and a
  Body Slam that wards 2 instead of 3 is a little less of a wall, so every opening it is measured against comes
  closer to even. Greedy still never opens Predator, since it reads Predator and Warped alike at round 1 and the
  tie goes to Warped by the options' order ; what moved is what
  a player who does open it can expect.
- **Still outside.** Warped + Warped at 0.28, which no knob outside tier 1 moved.
