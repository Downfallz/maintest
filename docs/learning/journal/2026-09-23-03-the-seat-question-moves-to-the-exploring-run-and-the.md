# 2026-09-23. The seat question moves to the exploring run, and the objective falls from 285.77 to 42.77 without the content moving

- **What changed.** `player1WinShare` is read on `variety` instead of `mirror` (ADR 0062). Same band, scale and
  weight; nothing in the content moved and no match was replayed for the table below — the objective is a pure
  function of the metrics, so the runs already recorded this week were re-scored under both.

- **Why the mirror stopped answering.** Under packages two identical greedy agents buy the same package in the
  same round, every initiative ties, and the timeline gives every tie to Player 1. The mirror read **1.000 on
  four catalogues in five** and was 243 of the objective's 285.77 points. Before packages it had signal: 0.500,
  0.510, 0.490 in ADRs 0032, 0037 and 0042.

  | content | mirror P1 | variety P1 | old objective | new objective |
  | --- | --- | --- | --- | --- |
  | shipped (`6df8dc30`) | 1.000 | 0.480 | 285.77 | **42.77** |
  | `tier:prowler` +3 → +5 | 0.735 | 0.440 | 101.76 | **60.81** |
  | Throwing Star 2e ×2 dmg 3 (#172) | 1.000 | 0.495 | 288.31 | 45.31 |
  | Throwing Star 2e ×2 dmg 2 | 1.000 | 0.480 | 314.61 | 71.61 |
  | Throwing Star 3e ×2 dmg 3 | 1.000 | 0.480 | 307.44 | 64.44 |

  The old objective recomputed from those metrics reproduces every recorded score to the hundredth, which is
  the check that the re-scoring is the objective and not an approximation of it.

- **The ranking that flips.** The prowler move was the old objective's best candidate by 184 points and is
  the new one's worse by 18: all its gain was the seat term, and every variety term it moved got worse. That
  is the trap ADR 0061 warned against, closed from the objective's side.

- **Not answered.** Whether a tie *should* go to the seat is a rule for the game and the table. On divergent
  play it barely matters — `variety` 0.44 to 0.495, `skill` 0.49 to 0.50 — which is why the measurement could
  move without the rule doing so.
