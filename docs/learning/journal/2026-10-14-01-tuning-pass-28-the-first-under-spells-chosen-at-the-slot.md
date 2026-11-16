# 2026-10-14. Tuning pass 28, the first under spells chosen at the slot: Bonewall's ward at 3, rejected

The content stays `3ae51e9f`. Pass 28 proposed `dbfb9439` (run 38340294993, seed 0, 1 round of 2 after the
opening sweep, 180 catalogues), the first pass whose every evaluation chooses its spells at the slot (ADR 0112).
The objective is the one of 2026-10-12, which scores this content 10.05 under mode B, all of it the forced
openings. The owner rejected the proposal; what follows is why, and what stays.

- **What it moved.** One knob: Bonewall's ward (Necrotic, Warped's level 2) for 3 rounds instead of 2, the top
  of its box. It came from the opening sweep; the round that followed found nothing better.
- **What the run read.** 10.05 to 4.24 on the search seeds, 6.72 to 1.85 on the confirmation seeds, 6.02 to
  4.14 on the workflow's own hold-out (995317 to 995516). 5.72 of the 5.81 it gained on the search seeds is one
  term: Warped + Warped, 0.242 to 0.297. Brute + Brute went from 0.383 to 0.410, into its band.
- **Replayed on 200 other seeds no candidate saw** (3000000 to 3000199, both seats), each opening forced on
  one side against Greedy, with the difference read seed by seed (bootstrap over seeds, 95 %):

  | opening | `3ae51e9f` | `dbfb9439` | difference |
  | --- | --- | --- | --- |
  | Warped + Warped | 0.273 | 0.291 | +0.019 (-0.020 to +0.056) |
  | Brute + Brute | 0.334 | 0.348 | +0.014 (-0.018 to +0.046) |
  | Predator + Predator | 0.465 | 0.458 | -0.008 (-0.022 to +0.002) |
  | Brute + Predator | 0.470 | 0.456 | -0.014 (-0.035 to +0.005) |
  | Warped + Predator | 0.566 | 0.554 | -0.013 (-0.025 to -0.002) |
  | Brute + Warped | 0.500 | 0.500 | 0 |

  The gain shrinks from +0.055 to +0.019 and is no longer settled: the best of 180 catalogues on one block of
  seeds is partly that block's luck. Both openings stay under 0.4, so Brute + Brute's band was the search
  seeds'. The objective is quadratic, so even these small moves are worth about 2.3 of score, which is what the
  hold-out read. The full objective was not replayed on this window: the variety evaluation alone, lookahead
  against lookahead, outlasted what it was worth here.
- **What the move does to the game.** Bonewall is 3 defense on one ally for 1 energy, and recast on the same
  ally it stacks up to the defense ceiling (ADR 0076); for 3 rounds a creature can carry three wards at once.
  Every team that owns Warped bought it more, not only the opening it was searched for: in the Greedy mirror
  on these seeds 205 of the 400 sides cast it instead of 114 (4.2 % of the casts instead of 2.4 %), Greedy
  cast it 146 times against Warped + Warped instead of 67, and the forced Brute + Brute, which buys Warped
  later, 400 instead of 300. The matches got longer (10.25 to 10.61 rounds in the mirror) and drew more (1.0 %
  to 3.5 %, still under the 5 % band). So the weak openings came closer to Greedy because both sides stalled
  more, not because they play better, and the openings that were in their band moved away from it slightly.
  That is pass 27's pattern again (2026-10-11): a move that changes what Greedy does reads as a better opening.
- **The decision.** Rejected: stacked wards are a stall, not an answer to an opening. Bonewall stays at 2
  rounds, its `durationRounds` knob now stops at 2 so the next pass does not propose the same move, and its
  note says why. The digest of `dbfb9439` is not committed.
- **What is left is a design question, not another pass.** Tier 1 and the starting kit have no knobs since the
  owner settled them (2026-10-09) and the packages never had any, so the search reaches an opening only through
  the level-2 spells behind it, and the one it found is the one rejected here. Warped + Warped (0.273) and
  Brute + Brute (0.334) under mode B are the owner's: a tier-1 spell, a package's initiative bonus, or a level-2
  spell whose box is widened on purpose.
- **What stays from the pass.** `mirror.fizzleRateA` read 0.000 on every candidate, as ADR 0112 said it would:
  nothing fizzles when a spell is chosen at its slot. Its weight is now 0, as the exploit win rate's is, and it
  stays listed because a nonzero reading would be a bug. It changes no score, but it changes the objective's
  target fingerprint.
- **Not re-measured.** The exploit panel (0.917 on the search seeds, weight 0) was assembled under hidden
  intents; nothing here says its sets are still the best exploiters at the slot.
