# 0018. Price initiative in the heuristic agents' weights

Date: 2026-09-09

Status: Accepted

## Context

[ADR 0017](0017-spell-initiative-on-unlock.md) makes an Evolution unlock raise a Creature's Base initiative,
so a pick can now be taken for tempo rather than for damage. The heuristic agents cannot see that: they price
a pick with `ActionScorer.Estimate`, which is what the Spell would do in combat on the current board, and the
initiative it buys is not part of any resolution. `Greedy` is the benchmark baseline and the deterministic
opponent every learned agent is measured against, so a baseline that always takes the damage and leaves the
tempo would not merely play badly — it would make the digest measure a rule nobody uses, and `search-weights`
would have no dial to turn. The same quantity was also priced twice already: `ConditionScore` valued an
`InitiativeDebuff` with `w.buff`, the defense-buff weight, so taking a point of initiative off an enemy and
buying one for yourself had different names before either had a rule.

## Decision

We will give the scoring weights an `initiative` term, one price for one point of initiative wherever it
moves. `ActionScorer.UnlockValue` is an unlock's combat value plus `w.initiative` times the Spell initiative,
and `HeuristicAgent.DecideEvolution` picks on that instead of on `Estimate`; `ConditionScore` prices an
`InitiativeDebuff` with `w.initiative` instead of `w.buff`. The default is **0.5**, the same as `buff`, on the
reasoning that initiative is indirect in the way defense is: it may win a race that was never close. Casting
is untouched — initiative is not part of a resolution, so `Estimate` keeps meaning exactly what it says, and
the new term reaches only the Evolution decision and the debuff outcome.

## Consequences

- Good: the lever ADR 0017 adds is one the baseline actually pulls, so the benchmark measures the rule in
  play rather than a rule the agent ignores.
- Good: one price for one point of initiative. The agents cannot value giving and taking differently, which
  they would have the first time `search-weights` moved `buff` without moving the other.
- Good: it is a dial `search-weights` can turn. A weight that does not exist cannot be searched, and this is
  the cheapest way to find out what tempo is worth in this content.
- Bad: 0.5 is a guess, and the weight `search-weights` has the least evidence about. It is reasoned from
  defense being indirect, not measured; the first search that includes it may move it a long way.
- Bad: the weights fingerprint changes, so every heuristic agent spec is a new version and no run stamped
  with the old one is comparable term by term.
- Bad: this and ADR 0017 land in the same benchmark digest, so the digest cannot separate what the rule did
  from what the pricing did. Accepted deliberately: a digest of the rule without its price would measure a
  baseline that ignores the rule, which is a worse number, not a cleaner one.
- Neutral: the studio's weights panel and `/api/weights` are driven off `ScoringWeights.Named`, so the ninth
  box appears with no page change.

## Alternatives considered

- Leave the agents alone and let `search-weights` find it: it cannot. The search moves the weights that
  exist, and with no initiative term every candidate prices an unlock the same way.
- Fold the initiative into `Estimate`: it would make every caster of the spell pay attention to a number only
  an unlock moves, and `Estimate` is also what the intent decision reads.
- Reuse `w.buff` for the unlock too: cheaper by one weight, and it welds defense and initiative into one
  number that a search can then only move together. The opposite of what a search is for.
- Give the debuff its own weight and leave `w.buff` alone: three names for two things, and it leaves the
  giving-and-taking asymmetry in place.

## Follow-up

- `src/DownfallArena.Application/Agents/ScoringWeights.cs`, `ActionScorer.cs`, `HeuristicAgent.cs` and
  `src/DownfallArena.Infrastructure/Agents/JsonScoringWeightsSource.cs`.
- `learning/weights/greedy.json` and `learning/src/downfall_learning/export.py`: the default and the name
  list the search and the file reader share.
- `docs/learning/agents.md`: the resolution table now files an `InitiativeDebuff` under `w.initiative`, the
  Evolution decision states the second term, and the built-in table carries the ninth weight.
- `docs/learning-roadmap.md`: the weight list and the count the search walks.
- `benchmarks/`: the digest ADR 0017 already requires; this ADR is the other half of the same regeneration.
