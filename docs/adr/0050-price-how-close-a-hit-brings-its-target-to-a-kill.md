# 0050. Price how close a hit brings its target to a kill, and leave the baseline at zero

Date: 2026-09-16
Status: Accepted

## Context

The eight scoring weights have been searched from `search-4` and from `mixture-mean` against a panel of
opponents, with two fitnesses, and both searches ended in the same place: no neighbour of `mixture-mean`
improves one opponent without paying another beyond the noise of 400 matches (journal, 2026-09-16, the
worst-matchup rung and the search under the floor). What the weights weigh has not changed since phase L5.
Between `damage`, which reads every point the same wherever it lands, and `kill`, which pays only once the
last point lands, the scorer has no term for a hit that sets a kill up: three points on a creature at four
and three on a creature at twenty score the same, and the bot spreads damage where concentrating it would
end a creature's turns sooner.

## Decision

We will add a ninth weight, `pressure`, priced on the share of the health a target had that a hit takes: the
effective damage over the health before the hit, one for a kill, nothing on a creature with no health to
take, signed like the damage it rides on so a recoil counts against. It ships in `ScoringWeights.Default` at
**zero**, so the benchmark digest verifies unchanged and only the agent fingerprint moves (`362b0496` to
`5833ff4d`); a weights file may set it, and `search-weights` searches it with the other eight.

The weight was swept alone on content `7e199df4` with `scripts/sweep-weight.py`, the method of ADR 0028, 0032
and 0037, and the points were then played head to head on 200 seeds no sweep or search has used (`995317`
onward), the baseline's own weights plus the one value, against the compiled Greedy at zero and against
`search-4`:

| `pressure` | objective | rounds | entropy | `player1WinShare` | `skill` | `exploit` | vs Greedy, unseen | vs `search-4`, unseen |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| **0.0 (kept)** | 124.36 | 7.78 | 3.117 | 0.465 | 0.978 | 0.927 | 0.500 by construction | 0.086 (`search-4` scores 0.914) |
| 0.5 | 24.90 | 5.83 | 2.708 | 0.370 | 0.993 | 0.618 | 0.445 (0.393 to 0.497) | 0.263 (0.212 to 0.313) |
| 1.0 | 33.49 | 6.14 | 2.819 | 0.335 | 0.993 | 0.427 | 0.479 (0.425 to 0.533) | 0.461 (0.408 to 0.515) |
| 2.0 | 26.23 | 5.63 | 2.739 | 0.410 | 0.990 | 0.310 | **0.751 (0.706 to 0.796)** | **0.480 (0.426 to 0.534)** |
| 3.0 | 276.01 | 5.14 | 2.922 | 0.000 | 0.998 | 0.593 | 0.153 (0.116 to 0.189) | 0.088 (0.059 to 0.116) |
| 5.0 | 272.21 | 5.02 | 2.879 | 0.000 | 0.998 | 0.410 | not played | not played |
| 8.0 | 80.95 | 6.30 | 1.997 | 0.715 | 0.990 | 0.680 | not played | not played |

Two readings, and they pull apart. **As an agent, the term is the largest single gain this project has
measured**: at 2.0 the baseline's own weights beat the baseline 0.751 and hold `search-4`, which took a search
of all eight weights to reach 0.914 against Greedy, at parity, with the whole interval clear of the 0.086
Greedy scores against it. `exploit` says the same from the other side: `search-3`, searched to beat Greedy,
drops from 0.927 against the baseline to 0.310 against it at 2.0. The step is narrow: 1.0 ties the baseline,
2.0 beats it by 25 points, 3.0 collapses to 0.153, and at 2.0 the set still loses to `mixture-mean` 0.306. A
value that good one step from one that bad is not a value to set by hand; it is a dimension to search with
the other eight, which is what a weights file is for. **As a baseline, it is not usable at any value above
zero**: matches end in six rounds instead of eight, under the balance objective's band, and the
mirror's first-mover share leaves 0.45..0.55 at every point, to 0.335 at 1.0 and to 0.000 at 3.0 and 5.0,
where the second mover wins every mirrored match. The objective's drop from 124 to 25 is the `exploit` row
falling, not the content getting better.

So the baseline stays at zero and the weight is for the searched sets. Greedy is the yardstick every number
in the journal is read against and the agent the benchmark digest records; moving it by 25 points against
itself would make every reading before this ADR incomparable with every reading after, for a gain the
searched sets can carry instead. The next search runs from `mixture-mean` with nine weights, the ninth free,
against the same panel and under the same floor.

## Consequences

- Good: a term the eight weights could not express, measured at two points to beat the baseline by 25 and to
  hold `search-4`, from a single number set by hand. The ladder has somewhere to go again.
- Good: nothing that was stamped moves. The digest verifies, every committed weights file loads, and a file
  that does not name `pressure` keeps zero.
- Bad: the fingerprint of the built-in weights changes, so a run stamped before this ADR compares with one
  after it on every axis but the agent string; `compare-stamps` names the difference.
- Bad: the mirror at any non-zero value is lopsided toward the second mover, and the balance objective reads
  that as its heaviest target. A content pass on a baseline that presses would be tuning for that lopsidedness.
  Whether a searched set that presses is exploitable by an opponent that presses back is the next question,
  and the panel answers it only once a pressing set is in it.
- Neutral: `knobs.py`'s `cast_value` reads the weights it knows and ignores the ninth; the studio's weights
  panel grows a box from the engine's own list.

## Alternatives considered

- **Move the baseline to 2.0.** The strongest Greedy this project has had, and the reason not to is the
  table's other half: a mirror the second mover wins, matches under the round band, and forty journal entries
  that stop comparing with the next one. The ladder's sets are where strength goes; the baseline is where
  comparability lives.
- **Price the share of maximum health rather than of the health left.** That reads a hit the same on a
  creature at four of twenty as on one at twenty of twenty, which is the blindness the term is for.
- **A term on the weakest enemy's health after the action, over the whole board.** The same pull with more
  to compute per candidate, and no way to sign it per target; the per-hit share prefers the weaker target
  and the finishing hit already.
- **Let the search find it without a sweep.** ADR 0032 and 0037 say why not: a joint search confounds the
  term with eight others, and this one had to be read alone before it was worth a search's cost.

## Follow-up

- `ScoringWeights`, `ActionScorer.Score` and `Damage`, `JsonScoringWeightsSource`, `WEIGHT_NAMES` and
  `DEFAULT_WEIGHTS`, `learning/weights/greedy.json`, the studio's weights order, the fingerprint pin.
- `docs/learning/agents.md` (the score table, the built-in weights table, the provenance paragraph) and the
  glossary's Scoring weights entry.
- A search from `mixture-mean` with the ninth weight free, against `greedy`, `mixture-worst`, `search-4` and
  `random` under the floor, and its journal entry.
