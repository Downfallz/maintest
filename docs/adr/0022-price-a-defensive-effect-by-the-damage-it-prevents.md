# 0022. Price a defensive effect by the damage it prevents

Date: 2026-09-10
Status: Proposed

## Context

`ActionScorer` prices a `DefenseBuff` as `buff × amount × rounds` and a `Heal` as `heal × missing health`,
neither of which is a quantity the game contains: defense subtracts from **every incoming hit**
(`ResolutionRules.Outcome`), so what a buff is worth depends on how many hits land on that creature, and a
heal on a creature about to die is worth what the death is worth, not what the health points are worth. On the
nine-spell core content the consequence is measurable: `guard` scores 2.5 and `rejuvenate` at most 2.4 against
`lightning_bolt`'s 5.15, so neither is ever cast — 11,910 actions of a benchmark run land **zero** heals and
zero defense buffs. A converged `search-weights` run rules the weights out as the cause: an agent optimised
purely to win leaves `heal` where it was and moves `buff` *down*, so no amount of tuning the eight numbers
reaches this. What is missing is not a price but a quantity to put a price on.

## Decision

We will price a defensive outcome by the damage it prevents, and price a prevented death at what causing one
already costs. `ActionScorer` gains a threat reading: the damage the living, unstunned enemies of a creature
could deal it in one round, each contributing the best of the damaging spells it knows and can afford,
crit-weighted and after the target's `TotalDefense`, computed from spell stats rather than from a nested
resolution. A `DefenseBuff` is then worth `buff × amount × expected hits over its duration`, where the
expected hits per round are the attackers that can hurt the target divided by its living allies, so the value
rises as a team is focused down rather than staying flat. On top of that, any defensive outcome that takes its
target from dying to this round's threat to surviving it adds `kill` — the same weight the scorer already pays
for taking a life, with the same sign rule, so denying a kill and scoring one are priced as one thing. The
threat reading is computed only for an outcome that needs it, so an attack's score costs exactly what it costs
today.

## Consequences

- Good: the agents get a reason to defend that exists in the game. A `guard` that drops an incoming 3 to 1 on
  a creature at 2 health now outscores the attack it competes with, and only then.
- Good: the price of a life is one number. `weights.Kill` values taking one and denying one, so a search
  cannot drift the two apart the way `buff` and `initiative` drifted before [0018](0018-price-initiative-in-the-agent-weights.md).
- Good: `search-weights` gains a dial that does something. `buff` and `heal` multiply a quantity that varies
  with the board, so moving them changes behaviour rather than scaling a constant.
- Bad: the benchmark digest changes, and `Greedy` is the baseline every learned agent is measured against, so
  no run stamped before this compares term by term with a run after it. The journal entries that read
  "0 healing, 0 defense buffs" describe an agent that no longer exists.
- Bad: the threat reading is an estimate, and a wrong one whenever the enemy does something other than its
  best single attack. It assumes every enemy attacks, none of them heals, and the damage spreads evenly over
  the living allies.
- Bad: a defensive spell now costs more to score than an attack does. The cost is bounded by
  enemies × known spells and is paid only by outcomes that need it, but a catalogue of mostly defensive
  spells would feel it.
- Neutral: `Regeneration` deliberately does **not** get the survival term. It ticks at the end of a round, so
  it cannot save a creature from that round's threat; it keeps its healing term and nothing else.
- Neutral: no weight is added or removed, so the weights fingerprint changes only through behaviour, and
  `learning/weights/greedy.json`, the studio panel and the search's name list are untouched.

## Alternatives considered

- Raise `heal` and `buff` and leave the formulas alone: measured and refuted. A converged search that plays
  only to win moved `buff` down, because the quantity those weights multiply is not what defence is worth.
- Compute the threat with a full nested resolution of every enemy action: more accurate, and it turns a
  one-step lookahead into a two-step one on every defensive outcome. The value learner is the right place for
  a real horizon ([0016](0016-value-learning-on-an-advantage-baseline.md)), not the heuristic.
- Give a denied kill its own weight: a third name for a quantity `kill` already prices, and the first search
  that moved one without the other would let the agent value a life differently depending on which side of it
  it was on.
- Change the content instead — make `guard` and `rejuvenate` numerically stronger until a blind agent picks
  them: it tunes the game to fit a defect in the agent, and [0021](0021-tune-the-catalogue-with-a-declared-search-space.md)'s
  search would then be optimising against a scorer that cannot see what it is buying.

## Follow-up

- `src/DownfallArena.Application/Agents/ActionScorer.cs`: the threat reading, and the `HealScore` and
  `ConditionScore` paths that use it.
- `tests/DownfallArena.Application.Tests`: the survival term, the focus scaling, the regeneration exception,
  and that an attack's score is unchanged.
- `docs/learning/agents.md`: the resolution table, which currently states both old formulas.
- `benchmarks/`: the digest, which this changes through `Greedy`.
- `docs/learning/journal.md`: an entry with the before and after, because this moves the baseline.
