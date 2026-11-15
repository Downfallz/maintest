# 2026-10-08. Search 36: the lookahead's weights refitted against the Greedy of ADR 0102

All on content `cedcfb61`.

- **Why.** Since ADR 0102 Greedy buys what adds over the spells of its kind, and lookahead-34, fitted against
  the Greedy before it, beat it only 50.7 % of 400 matches where it took 94.8 %.
- **The first try ran out of time.** `kind: lookahead` from lookahead-34, against Greedy on 60 benchmark seeds
  and `search-23` on 10, 4 rounds of 5 (#301): after five hours it had played 15 of its 21 evaluations, about
  20 minutes each where 8 were expected, and would have run past the six-hour timeout. It wrote nothing until
  its end, so nothing was kept. The search now writes its best set after every round, marked incomplete.
- **The run.** Greedy alone on the same 60 seeds, 3 rounds of 5, seed 0: 1h56 of search and 13 minutes of
  hold-out, so `search-23` was most of the first try's cost. The best of 16 scored 0.8208 where lookahead-34
  scored 0.4792 on those seeds.
- **What it moved.** Damage 1.0 to 0.425, the one large move: only ratios count, so kills, stuns, heals and
  initiative are worth about twice as much against damage. Pressure 0.001 to 0.116; heal 1.006 to 1.172, stun
  3.166 to 3.293, kill 4.857 to 4.966; bleed, energy, defense and initiative barely move.
- **Replayed on 60 seeds nothing had played** (995377 to 995436, both seats), read with `paired`:

  | against Greedy | wins | score |
  |---|---|---|
  | lookahead-36 | 104 of 120 | 0.867 |
  | lookahead-34 | 55 of 120 | 0.458 |

  A paired difference of +0.408 (+0.286 to +0.531), clear of zero. On the first 30 of those seeds the
  lookahead on its built-in weights takes 54 of 60 from Greedy where lookahead-36 takes 58.
- **Not measured.** Against `search-23`: the replay ran over an hour for half its seeds while the content was
  scored beside it, and was stopped.
- **Kept as `learning/weights/lookahead/lookahead-36.json`**, the values as the run printed them. The table
  seats it first, the lookahead on its built-in weights second; lookahead-34 leaves the featured list.
- **What strong play looks like.** 60 matches of lookahead-36 against itself from seed 300 last 15.2 rounds
  (median 14), and the first creature dies at round 11.0. Both sides cast more defense than offense (2.5 to 2.3
  a round): Guard 13 casts a match, Toxic Mend 10, Protective Slam 8. Heals come to 8.1 a round against 10.3
  damage. The exploring run, which the length target is read on, plays 20 % of its decisions at random and
  reads 11.0 rounds: the randomness breaks a stand-off the strong mirror does not.
