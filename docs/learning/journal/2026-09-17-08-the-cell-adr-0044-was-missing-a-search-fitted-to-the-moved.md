# 2026-09-17. The cell ADR 0044 was missing: a search fitted to the moved catalogue takes every match on both of them, so that content move is worth nothing at all and the exploit term is a constant

- **The missing half, run.** The entry below measured four sets searched on `7e199df4` and replayed on the
  catalogue with Crushing Stomp at the weakest of its own bounds, and said what it could not say: what a
  search fitted to the moved catalogue would find there. That search is in. `search-weights` from
  `stun-first`, against Greedy, on the weakened catalogue, 161 candidates over ten rounds, 21 minutes. It
  reached every match at candidate 19 and never gave it back. What it moved is one decision: `stun` 10.210
  to 0.697 and `kill` 9.643 to 18.010, every other weight within a point of where it started. The set does
  not lose the stun route, it stops playing it, and takes a kill route instead. `learning/weights/kill-first.json`.

- **Replayed on seeds nothing has played**, because the search chose the best of 161 candidates on the
  objective's own fixed seeds, and that is selection on the set being reported. 200 seeds, 6660001 to
  6660200, both catalogues, agent A against Greedy, which is the evaluation the objective reads.

  | exploit agent | `7e199df4` | Crushing Stomp weakened |
  | --- | --- | --- |
  | `stun-first`, fitted to `7e199df4` | 1.0000 | 0.7200 |
  | `kill-first`, fitted to the weakened one | **1.0000** | **1.0000** |

- **The move is worth nothing to an exploiter, and both numbers this journal has reported for it are
  artefacts.** The refresh entry read it as 116 points of objective and the entry below, under the panel of
  four, as 55. Against a set fitted to the catalogue it actually faces, it is **0**: the same 1.0000, in
  7.63 rounds against 7.16. Taking Crushing Stomp away does not make the catalogue less exploitable. It
  moves where the exploit is, and blinds the sets built on the old one. The bound the entry below stated
  held in the direction it was used — a fresh exploiter can only read higher than the best of four — and it
  read the highest there is.

- **So `exploit` is saturated, and that is a fact about the objective rather than about this catalogue.**
  On both catalogues measured, a searched set takes every match from Greedy. The term's target is 0.55 with
  a scale of 0.05, so 1.0000 costs 162.00 of the 172.42 points the current content scores: not a gradient a
  tuning pass can descend, a constant it carries. ADR 0044 froze content tuning until this split was
  measured; the split says the term as calibrated cannot be satisfied by content at all, which is a stronger
  reason not to tune against it than the one that stopped it.

- **The instrument is saturated at both ends, and the same 200 seeds show it.** Against Greedy on the
  current catalogue the top of the panel ties at every match, and the two readings the same evaluation
  already carries do not:

  | exploit agent, against Greedy | win rate | rounds | health left |
  | --- | --- | --- | --- |
  | `stun-first` | 1.0000 | 5.79 | 41.00 |
  | `pressure-floor` | 1.0000 | 6.00 | 41.00 |
  | `kill-first` | 1.0000 | 7.16 | 47.83 |
  | `search-4` | 0.9275 | 7.34 | 19.75 |
  | `mixture-mean` | 0.8300 | 9.44 | 20.52 |

  Three sets read the same number and end the match a round and a half apart. Turn the opponent up instead
  and the panel falls off the other end: against `stun-first`, `pressure-floor` takes 0.175, `mixture-mean`
  0.098, `search-4` 0.063, and `kill-first` — which takes every match from Greedy on **both** catalogues —
  takes 0.005, one match in 200. So the term does not read a property of the catalogue at either setting.
  It reads the distance between two fixed agents, and at the distance the objective uses, the scale has no
  room left above it.

- **What does move is the clock.** The best exploiter needs 5.79 rounds on the current catalogue and 7.63 on
  the weakened one, a third longer, while the win rate reads 1.0000 on both. The evaluation has been
  carrying that number the whole time. A metric that is still moving where `winRateA` is pinned is the
  obvious candidate for what this term should read, and choosing it is a decision with an ADR to write, not
  an entry to file.

- **What this does not say.** One spell, one direction, one step, on a catalogue nudged rather than rebuilt:
  a different move might be beyond a fitted set's reach, and nothing here measures that. And 1.0000 against
  Greedy says as much about Greedy as about the content — the term compares a searched set against the
  taste the catalogue is balanced for, and a searched set has now beaten that taste everywhere it has been
  asked. Whether the reading should move by target, by opponent, or both is a decision and belongs in an
  ADR, not here.

- **`kill-first` joins the panel** under [ADR 0052](../../adr/0052-read-the-exploit-term-as-the-best-of-a-panel.md),
  as the one set no measured content move has blinded. It costs one evaluation per candidate and changes no
  reading on this catalogue: three of the five already read 1.0000, ties fall to the earliest, and the
  objective stays 172.421.
