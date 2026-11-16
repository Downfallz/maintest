# 2026-09-28. An action resolves when its targets are confirmed: the benchmark digest is regenerated

- **What changed.** ADR 0083. Combat walks the timeline once. At each slot the spell is revealed, its owner
  aims on the board as it stands, and the action resolves on confirmation. A creature that cannot act when
  its slot comes up is revealed with no target and fizzles, and its owner is not asked. The match ends on
  the slot, or the upkeep, that wipes a team. The observation moves to `features:v8`: same layout, but a
  Target decision now reads a board that already carries the earlier slots. Every policy fitted on v7 is
  refused. The lookahead no longer replays the revealed actions, and it stops a rollout at a wipe.
- **The digest.** Content `813bb91b`, Greedy against Greedy, 200 seeds. 180 of the 200 AB outcomes moved and
  54 changed winner. Every match still ended by elimination but one:

  | | before | after |
  | --- | --- | --- |
  | Player1 wins | 108 | 104 |
  | Draws | 0 | 0 |
  | At the round cap | 0 | 1 |
  | Average rounds (median) | 10.7 (10) | 10.9 (10) |
  | Rounds 10 to 15 | 119 | 109 |
  | Health the winner kept | 26.2 | 29.3 |

- **What it says.** A strike is no longer wasted on a creature an earlier slot killed, and the winner ends
  with more health. Match length barely moves. The agents' weights (`learning/weights/`) were fitted on the
  two-pass combat. Nothing here re-measures them. The next weight search, and the next learning turn, start
  from this digest and from `features:v8`.
