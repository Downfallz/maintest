# 0094. The lookahead prices a purchase by playing rounds out

Date: 2026-10-02
Status: Proposed

## Context

Every agent prices a package the way `ActionScorer.PurchaseValue` does: its best spell's one-step value on
the board of the moment, plus the turn order its initiative buys (ADR 0056, ADR 0088). The lookahead
(ADR 0047) delegates to that reading, since evolution "has no timeline yet to play out". At round 1 the
reading cannot see a heal (nobody is hurt), a bleed (it lands next round), a second spell, the team the pick
completes, or the packages it opens, and it shows: Greedy opens Occultist on both picks in 88 % of its
exploring matches. Forcing the opening measured what that costs (journal, 2026-10-02): on the 200 benchmark
seeds, every other opening beats Greedy's own, by +0.09 to +0.43, and the 400 confirmation seeds reproduce
each number. Played opening against opening, Occultist twice is the worst of the six openings for Greedy
(0.30 mean) and for search-19 (0.27), and the best for search-31, which wins every pairing with it while
opening Brute twice itself. So what an opening is worth depends on the opponent's opening and on how the
rest of the match is played, which only playing the match out can read.

## Decision

We will have the lookahead price each purchase it could make by **playing the next rounds out** on a
hypothetical board, and buy the one whose rounds end best. For a candidate, the board takes the candidate,
the side's picks already made this round, its remaining picks as the inner agent (ADR 0055) would make them,
and the enemy's picks as the inner agent would make them in the enemy's seat, since those are face down
(ADR 0089). Every round after that is played by `RoundRollout` with the inner agent in both seats, through
the rules `Match` runs: `Advance` for the start of a round, the actions and the cleanup (ADR 0047),
`TimelineBuilder` and `TieOrderRules` for the turn order, `ActionRules.CanTakeItsSlot` for a slot that
fizzles, and a new `Advance.Buy` for the later evolutions, which restores the creatures and buys through
`Creature.BuyTier` so there is still one applier. A rollout is read like a round of the lookahead: the match
it ends first, then the scorer's sum over its actions, allies for and enemies against. The dice are rolled,
not forced plain: a package is mostly its crits (Pummel 0.77, Lightning Bolt 0.5), and a plain rollout
would price both at their floor. They are derived from one seed the agent draws when it is made and from the
board the purchase is made on, the same for every candidate, so a seeded match replays and a table rebuilt
from its decisions (ADR 0091) buys what its first host would have. Each candidate plays a fixed number of
rollouts of a fixed number of rounds (`PurchaseReading`); creatures whose snapshots differ only by id are
one candidate, and a lone candidate is bought unread. The minimax agent reads a purchase the same way: a
worst reply to a purchase is a worst whole match, which no budget here can search.

## Consequences

- Good: the reading sees what the one-step reading cannot (the team a pick completes, the package it opens,
  a heal once someone is hurt, a bleed) through the real rules, and it is the operator a learned inner agent
  can climb on, as ADR 0055 intended for combat.
- Bad: it is the first reading of the engine that plays whole rounds outside `Match`. The round it walks is
  `Match.Step`'s, written a second time in Application from the same public rules; a sub-phase added to the
  round has to be added there too. `RoundRolloutTests` replays matches from their first Speed sub-phase on
  the match's own dice, from either seat, with bleeds that end a match at upkeep, a round cap, and an agent
  that reorders its ties, and requires the same ending and the same sum, so the two cannot drift silently.
- Bad: a guess of the opponent's pick is a best response to that guess, and the opening matrix says a best
  response is exploitable. It reads the inner agent's opening, not an equilibrium.
- Bad: a rollout shorter than the match can mislead. On a test catalogue where a package opens a stronger one,
  four rounds read the opening as even and the whole match reads it as losing 29 to 49. The defaults of
  `PurchaseReading` are what the journal measured, and a horizon is a cost traded against that error.
- Neutral: it costs rollouts at every evolution with more than one candidate, about ten times the lookahead's
  time on the benchmark seeds. An inner agent that decides at random draws from its own source inside the
  rollouts, as it does in the match; the candidates still share the rules' dice.

## Alternatives considered

- Retune `PurchaseValue` (team-aware marginal value, averaged over plausible boards): cheaper, but still
  a guess of what a package is worth, and the guess is what is wrong.
- An opening book from self-play win rates: invalidated by every content change and by every opponent.
- Mix openings at the matrix's equilibrium: answers round 1 only, on one agent's matrix.

## Follow-up

- `docs/learning/agents.md` and the lookahead's doc comment: evolution is the lookahead's own now.
- The journal entry with the measurement that sets the horizon and the rollout count.
