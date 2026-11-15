# 2026-10-08. A capstone per family, bought for a passive (ADR 0101)

Measured on content `9419f935` before and `49c96577` after: the same catalogue plus Titan, Archmage and
Apex. Merged onto the critical chances in twentieths (`b41ba55e`), the content is `cedcfb61`.

- **Why.** The owner finds the picks from round 7 dull: most of them add a spell the creature never casts.
  Buying deeper needed no nudge (lookahead-34 made to buy level 3 first scored 0.527, 0.478 to 0.576, against
  itself), so the answer is a late pick worth taking that is not another spell.
- **What changed.** A Tier may carry a passive and an any-of prerequisite. Each family gets a level-4 capstone,
  opened by any of its level-3 packages and teaching nothing: Titan is immune to stun, Archmage gains 1 energy
  at every upkeep, Apex adds 2 to every direct hit. The damage bonus is also a reusable condition,
  `DamageBuff`, that no spell gives yet. `features:v9`, schema version 5.
- **Lookahead-34 against itself**, 40 matches on seeds 300 to 600, the same seeds on both contents:
  - Capstones were within reach of a living creature 218 times and bought 43 times (Apex 24, Titan 15,
    Archmage 4), first at round 7.
  - Purchases at round 7 or later: 503 before, 505 after, 43 of them capstones. Spell packages bought then and
    never cast afterwards: 241 of 503 (48 %) before, 212 of 462 (46 %) after. The capstones take picks that
    were mostly never-cast level-2 and level-3 packages, and the share of dead spell picks barely moves.
  - Match length: 12.95 rounds before, 12.80 after (sd 4.4, 40 matches: no difference this can read).
- **Greedy prices a passive by the casts it changes.** At first Greedy priced a passive as three rounds of
  its raw amount: Archmage at 0.9, Apex at 6, Titan at 1.5 on average. A level-1 package of another family was
  worth about 13 on the same boards. So in 100 Greedy mirrors a capstone was within reach 551 times and bought
  4 times. Now each passive is priced over the same three rounds by what it does to casts:
  - Archmage: the energy, plus the dearer spell it lets the creature cast more often, read as the share of
    rounds a creature saving for a spell can pay for it.
  - Apex: the best cast once every hit of it is raised by the bonus, against the best cast without it, a
    critical hit counted at the multiplier. A spell that reaches three enemies gains the bonus three times, and
    may become the cast worth making. Against the first reading, the bonus on the hits of the best spell as it
    stands, this scored 51.0 % of 400 and 50.7 % of 800 matches.
  - Titan: the creature's cast, kept once for each living enemy that knows a stun, and half a cast for each
    one that could buy a package teaching a stun at its next pick. Counting those enemies moved Greedy's own
    play by nothing measurable: 50.5 % of 400 and 51.0 % of 800 matches against the rule without them. It is
    kept because an immunity bought before the enemy's stun is worth something.

  The same 100 mirrors then bought 145 capstones: Archmage 142, Apex 2, Titan 1. Matches went from 11.2 rounds
  to 10.05.
- **The new pricing is not weaker.** Greedy with it beat Greedy with the old pricing 52.7 % of 400 matches on
  the benchmark seeds (49.7 to 55.8) and 52.2 % of 800 on the confirmation seeds (50.2 to 54.3).
- **But Greedy's matches get shorter.** The digest, Greedy against itself, changed 324 of 400 matches and
  averaged 9.64 rounds against 11.27 on `49c96577`. Regenerated on `cedcfb61`, it changes 326 of 400 against
  `b41ba55e` and averages 9.53 rounds against 11.16, below the 10 to 15 band (ADR 0068). Every Warped creature that closes a
  line buys Archmage at round 7, and an energy more a round kills sooner. Apex and Titan stay rare under
  Greedy: two damage on a single-target spell is about 6 over three rounds, and few enemies know a stun.
  Lookahead-34 buys them the other way round (Apex 24, Titan 15, Archmage 4). The two bots disagree on what
  the capstones are worth, and the Archmage knob (1 to 2) cannot go lower.
- **Pricing Apex higher does not help Greedy.** Greedy with Apex's bonus priced at double lost to plain Greedy:
  49.5 % of 400 and 49.2 % of 800 matches. Its pricing stays as it is.
- **Three rounds is about what a passive gets.** When a capstone is first in reach in a Greedy mirror, 2 to 4
  rounds are left at the median (3.5 to 5 on average), the purchase's own round included. A reading of the
  rounds left off the board, each side's health over the other's damage a round, read 2.3 at the median: it
  ignores a side's damage falling as its creatures die, its heals and its defense. It priced the capstones
  lower, and Greedy bought 83 instead of 145, so the fixed three rounds stay.
- **Why Apex stays rare under Greedy.** The Predator family's damage spells reach one enemy, bar Throwing Star,
  Night Raid and Infectious Blast, so Apex is about 2 damage a round, 6.3 over three rounds. A level-1 package
  of another family is read at about 13 on the same boards: Greedy prices a package at its best spell's whole
  cast, not at what it adds over the spells the creature already casts. That is the lever left, and it moves
  every purchase Greedy makes, not only the capstones.
- **Open.** A reading of whether capstones decide matches (family win share, comebacks) needs more than 40
  lookahead matches. The passive amounts are knobs (Archmage 1 to 2, Apex 1 to 3) for the next tuning pass.
