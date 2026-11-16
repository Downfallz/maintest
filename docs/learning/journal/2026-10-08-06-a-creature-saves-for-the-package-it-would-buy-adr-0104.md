# 2026-10-08. A creature saves for the package it would buy (ADR 0104)

All on content `da8d744b`.

- **What changed.** The round before an evolution round, a creature's energy reserve (ADR 0103) is the larger of
  its own and the one it would have with the package the scorer would buy it now.
- **Greedy does not move.** The benchmark digest is unchanged on all 400 matches. Counted over that benchmark,
  the anticipated reserve raises the energy term in 3 670 of 415 650 candidate readings, and none of them flips
  a decision: at energy 0.3 against damage 1, a point or two more held never outscores a hit.
- **An earlier A/B was not a measurement of this.** Greedy against itself with the change switched off by a
  sentinel pressure weight of 5e-7 read 51.0 % / 48.5 % on the benchmark seeds and 49.0 % / 50.0 % on the
  confirmation seeds. Since the change moves no Greedy decision, that gap is the sentinel weight breaking ties,
  and noise. A switch that touches the weights is no control.
- **The lookahead is not measured yet.** lookahead-36's weights predate the reserve and play far below what they
  did (journal, ADR 0103), so an A/B on them would read the change on weights that no longer fit. It will be
  measured on the weights #304 refits, with a switch outside the weights.
