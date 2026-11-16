# 2026-09-24. Why the strongest sets still stall their own mirror: permanent defense, then a stun trade, and the weights that choose both

- **What ran.** `main` at `be89c0e`, with the stun immunity of ADR 0072 and the bleed price of ADR 0073, and
  content `4ab506fa`. Twenty matches of each stalled mirror were played with a trace, on seeds 995317 to
  995336. Then each mirror was replayed on 200 seeds from 995317 with one thing changed: a weight of the set,
  or the content. Nothing is committed but this entry.
- **What a stalled match looks like.** Averages over the living creatures of 20 traced matches:

  | round | `stun-first` mirror: defense, energy, health | `search-19` mirror: defense, energy, health |
  | --- | --- | --- |
  | 5 | 6.7, 5.2, 27.2 | 8.7, 6.8, 30.0 |
  | 10 | 17.3, 5.0, 26.9 | 10.8, 9.8, 26.7 |
  | 20 | 13.3, 12.4, 24.3 | 11.6, 14.5, 20.7 |
  | 30 | 13.6, 18.1, 16.7 | 12.5, 20.4, 14.2 |

  - **Rounds 1 to 10 build defense.** `guard` and `thundering_seal` are 0.63 of `stun-first`'s actions there
    and 0.74 of `search-19`'s. Each cast leaves permanent points behind, one for `guard` and three for the
    seal. By round 10 every creature carries about 11 points of permanent defense, which is about 13 by
    round 30.
  - **After round 10, hits bounce and stuns are traded.** `crushing_stomp` (7 damage, a 2-round stun) is 0.53
    to 0.70 of all actions. Of the damaging casts that land after round 10, 0.79 deal nothing through the
    defense in the `stun-first` mirror and 0.47 in the `search-19` one. A quarter to a third of actions fizzle
    on a stunned caster.
  - **Energy piles up.** A creature holds 18 to 20 energy at round 30. Stunned creatures cannot spend it, and
    nothing the sets price highly costs that much.
  - **Health falls about half a point a round a creature.** Bleeds (`summon_minions`) arrive after round 20 in the
    `stun-first` mirror, too late to finish anyone by round 30.
- **What ends it.** Each line changes one thing. The table gives the average rounds and the share of matches
  at the round cap:

  | change | `stun-first` mirror | `search-19` mirror | Greedy's mirror |
  | --- | --- | --- | --- |
  | none | 30.00, 1.000 | 29.98, 0.970 | 9.96, 0.000 |
  | the set's defense weight to 0.65, Greedy's | 26.82, 0.120 | **10.87, 0.000** | |
  | the set's stun weight to 3, Greedy's | 27.00, **0.000** | 25.89, 0.290 | |
  | both | 27.00, 0.365 | 12.82, 0.015 | |
  | content: every permanent defense point lasts 3 rounds instead | 21.93, 0.065 | 20.52, 0.045 | 9.96, 0.000 |
  | content: `crushing_stomp` stuns for 1 round | 29.72, 0.720 | 28.78, 0.290 | 9.96, 0.000 |

  The content lines were built with the data builder into scratch copies, as hashes `bc99f534` and
  `dd02aa1f`. Greedy's mirror reads the same on both: it casts neither spell enough for either change to reach
  a match.
- **What it says.** The stall is permanent defense stacked early, then a stun trade on top of it. Two
  different levers break it:
  - **The weights.** `search-19` prices a point of defense at 1.24 against a point of damage at 0.27. At
    Greedy's 0.65 it stops stacking and plays `poison_slash`, and its mirror lasts 10.9 rounds. `stun-first`
    prices a stun at 10.2, 22 times a point of damage; at 3 it still stacks defense and its matches last 27
    rounds, but they end.
  - **The content.** Permanent defense is what makes the stacking pay. Made to last three rounds, both
    mirrors end in about 21 rounds, and Greedy does not notice. The knobs file already calls `guard`, which is
    cheap, repeatable and permanent, "the likeliest single cause of a match no one can finish". Shortening
    the stun does much less.
- **What it does not say.** The weight search cannot find the first lever alone. It scores wins, and a set
  that stalls its own mirror loses no match for it: `search-19` still beats every other set. The content lever
  is a rules question for the owner, who kept the rules as they were on 2026-09-23 and asked for the answer to
  come from bleeds. The bleed price of ADR 0073 made the sets cast bleeds (entries below). It did not make them
  stop stacking.
- **Next.** Two choices for the owner. One is a length term in the weight search, so that a candidate whose
  mirror reaches the cap ranks below one that does not. The other is a rule on permanent defense: a ceiling,
  a decay, or a duration. Heuristic rung 21 (#195) runs on the current rules and will show whether a search
  on wins alone moves off defense.
