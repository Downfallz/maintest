# 2026-09-13. The five remaining weights, none of which moves, and a sixth that does nothing

- **What changed**: nothing in the engine. `kill`, `stun`, `heal`, `bleed` and `risk` were swept one at a
  time on content `91da955c` against the `energy` 0.3 baseline, 400 benchmark seeds, all four evaluations —
  41 points in all. **Every one of the five stays where phase L5 hand-set it.** `damage` is not swept: it is
  the unit, so moving it alone is the same experiment as scaling the other eight the other way.
- **The tables** (objective; lower is better; the current value in bold):

  | `kill` | 0 | 2 | 3 | 4 | 4.5 | **5** | 5.5 | 6 | 8 | 10 |
  | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
  | objective | 59.54 | 58.15 | 53.63 | 62.22 | 52.53 | **49.32** | 51.01 | 52.88 | 67.16 | 71.77 |
  | `player1WinShare` | 0.540 | 0.540 | 0.570 | 0.570 | 0.565 | **0.510** | 0.545 | 0.420 | 0.350 | 0.390 |

  | `stun` | 0 | 1 | 2 | **3** | 4 | 5 | 6 |
  | --- | --- | --- | --- | --- | --- | --- | --- |
  | objective | 52.70 | 50.67 | 52.45 | **49.32** | 48.76 | 62.98 | 59.87 |

  | `heal` | 0 | 0.4 | 0.6 | **0.8** | 1.0 | 1.2 | 1.6 |
  | --- | --- | --- | --- | --- | --- | --- | --- |
  | objective | 50.13 | 62.43 | 58.65 | **49.32** | 62.35 | 68.55 | 73.95 |
  | rounds | **5.85** | 6.52 | 6.32 | 7.80 | 5.98 | 8.10 | 9.97 |

  | `bleed` | 0 | 0.4 | 0.6 | **0.8** | 1.0 | 1.2 | 1.6 |
  | --- | --- | --- | --- | --- | --- | --- | --- |
  | objective | 57.52 | 50.48 | 49.38 | **49.32** | 58.48 | 71.22 | 65.53 |

- **`kill` is the one where the coarse grid lied to me.** On 0/2/3/4/5/6/8/10 it read as a step of 5..6 with
  5.0 on its lower edge, and I said so. The refinement says the step is **4.5..6.0** and 5.0 is near its
  middle: 4.0's 62.22 is an isolated spike between 3.0's 53.63 and 4.5's 52.53, not a boundary. A grid coarse
  enough to miss a spike is coarse enough to invent an edge.
- **`stun` is flat where it matters.** 0 to 4 all read between 48.76 and 52.70 and `player1WinShare` barely
  moves; 5 breaks to 62.98. A weight whose value does not matter over most of its range is a result, and 3.0
  sits well inside that range. 4.0 reads 0.6 better and that is not a reason to move a number.
- **`heal` 0.8 is a narrow minimum**, not a plateau: 0.6 reads 58.65 and 1.0 reads 62.35, nine and thirteen
  worse. It is kept because nothing else comes close, and the fragility is recorded rather than smoothed
  over. Read `heal` 0.0 whole before liking its 50.13: matches end in **5.85 rounds**, under the floor of 8,
  with entropy at 2.91. The objective is good there because the game is cut short.
- **`bleed` 0.8 is the top edge of a step** whose middle is 0.6 (49.38 against 49.32). The middle-of-the-step
  test that chose 0.65, 2.1 and 0.3 would prefer 0.6. It is not moved: 0.06 is noise, the other columns split
  (0.6 takes `tierWinSpread`, `spellsNeverCast` and `exploit`; 0.8 takes entropy, `player1WinShare` and
  `spellsBarelyCast`), and the cost is a moved fingerprint and digest. That test picks a **new** value; it is
  not a reason to move one already inside the step.
- **`risk` does nothing, and that is the finding.** Swept at 0, 1, 2, 4, 20 and 100 it plays out identically,
  every column, over 400 seeds — setting it to zero changes not one match. 3, 6 and 9 give one other
  identical reading (49.25). So the two outcomes are selected by **whether the value is a multiple of three**,
  not by how large it is, which is a floating-point tie-break and not an effect: `risk x 1 / 3` is an exact
  integer there and lands on another candidate's score, and ties go to the first spell in ordinal id order.
- **Why it is inert**: `weights.Risk` is read in **three** places. Two are inside `Score` — the `Fizzled`
  branch and the dropped-target share — and a decision goes through `Expected`, which scores a resolution
  simulated against the **current** board, where nothing has fizzled and no target has dropped, while `Best`
  only offers legal targets. So both are the same zero for every candidate and cancel in the argmax. The
  real fizzle rate of 0.163 comes from targets dying between declaration and resolution — exactly what the
  bot cannot see when it chooses. This is ADR 0020's shape again: a weight that is documented, priced and
  unreachable.

  **Correction, the same day**: this entry first said "two places, both inside `Score`", and that was wrong.
  The third is `HeuristicAgent.DecideIntent`, which scores a castable spell with no legal target at
  `-weights.Risk` against the other spells' scores. That one *is* on the decision path and can discriminate:
  a spell whose best target set scores below `-risk` loses to a spell with no target at all at a low weight
  and beats it at a high one. The sweep says it does not happen on these 400 seeds at any value from 0 to
  100, so the measured conclusion stands unchanged — but the reason given for it was incomplete, and the
  claim was published in this entry, in `agents.md` and in the pull request before it was checked against
  a full search of the source.
- **Open, and not decided here**: whether the bot should learn to see that risk (price the expected drop at
  declaration) or whether the term should go. That is a decision with an ADR, not a number to tune, and
  nothing in this entry changes the engine.
- **The engine is deterministic, checked rather than assumed.** The 49.25 reading looked like noise, so
  `risk 3.0` was run twice: identical to three decimals. That mattered beyond this entry — three earlier
  conclusions in this journal lean on exact reproduction, and a single unexplained row would have put them
  in doubt.
- **Verified**: the sweep restores both files and rebuilds the engine at the defaults on the way out; the
  tree is clean and `ScoringWeights.Default` is untouched.
