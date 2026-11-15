# 2026-10-06. Basic Attack is removed

- **Why.** On content `ad3e4d00` it was almost never cast, as a share of all casts:

  | agent | Basic Attack | Wait |
  |---|---|---|
  | lookahead-34 | 0.2 to 1.5 % | 3.5 to 6.5 % |
  | search-23 | 0 to 0.1 % | 5.6 to 11.4 % |
  | pressure-floor | 0 to 0.2 % | 15.2 to 23.2 % |
  | Greedy | 0 to 0.9 % | 0.7 to 28.5 % |

  The lookahead figures come from 60 matches against each of Greedy, search-23 and pressure-floor. The other
  three come from 400 matches on the benchmark seeds, from the evaluations of 2026-10-06. An agent saving energy
  for a bigger spell casts Wait, not a 1-energy jab. Heavy Strike, also in the starting kit, does the jab's
  work for one more energy, the domination ci-9 first named.
- **What changed.**
  - The spell file, its alias and its knobs entry are gone.
  - Its place in the base node and in the tier-1 prerequisites of both talent trees is gone.
  - It is out of the creature's starting spells. Every creature now starts with Heavy Strike and Wait.
  - `startingKitOffersAChoice` reads those two.
  - The practice scenarios' scripted opponent casts Heavy Strike where it cast Basic Attack.
  - The studio's browser tests count 44 spells and 9 at one energy.
- Content `3c9eb083`, 44 spells, benchmark digest regenerated.
