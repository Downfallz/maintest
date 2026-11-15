# 2026-11-01. The lookahead guessing with search-51 takes the first seat, and the only one in the lobby

Content `f3e8a42d`. On 2026-11-01 search-51 beat the built-in lookahead 0.63. A lookahead is only as good as
the side it guesses, so the next step was to give it search-51 to guess with. Two readings:
- **A**, `lookahead:learning/weights/search-51.json`: search-51 is both its evaluation and the heuristic it
  guesses with and rolls out on. A bare weights path does both (`LookaheadAgent`'s default inner agent is a
  heuristic on the same weights).
- **B**, `lookahead:heuristic:learning/weights/search-51.json`: its built-in evaluation, guessing and rolling out
  as search-51 (ADR 0055).

Measured on 100 seeds no candidate saw (6891751 to 6891850), both seats:

| agent | against search-51 | against the built-in lookahead | rounds |
| --- | --- | --- | --- |
| A | 0.615 (0.55 to 0.68) | 0.570 (0.50 to 0.64) | 9.8, 9.3 |
| **B** | **0.655** (0.59 to 0.72) | 0.560 (0.49 to 0.63) | 9.5, 9.1 |
| built-in lookahead, for reference | 0.370 | 0.5 | |

- **Both beat search-51.** The plain lookahead lost to it 0.37: guessing the other side well is what it was
  missing.
- **A and B cannot be told apart.** Read with `paired`, B minus A is +0.04 (-0.06 to +0.14).
- **Against the plain lookahead** both lean ahead, but neither interval clears 0.5 on 200 matches.

**B is seated first**: it keeps the evaluation that earned the lookahead its seat and changes only the side it
guesses. At the owner's request, the public lobby now offers that one bot and no other (ADR 0121): Greedy and
Random leave it, and it seats whatever `learning/seatable.json` puts first.
