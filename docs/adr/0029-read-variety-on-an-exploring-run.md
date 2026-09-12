# 0029. Read variety on an exploring run, not on the greedy mirror

Date: 2026-09-12

Status: Accepted

## Context

Seven of the objective's targets ask whether the catalogue offers a choice: how spread the casts are
(`spellEntropyA`, `spellUsageShare`, `tierUsageShare`), how many spells go uncast (`spellsNeverCast`,
`spellsBarelyCast`), and how evenly the spells of one tier hit and win (`tierDamageSpread`, `tierWinSpread`).
All seven were read on the mirrored run, where both sides are `Greedy`.

`Greedy` picks the single best-scoring option, and every creature unlocks every spell, so two spells of near
equal value do not split the casts — the marginally better one takes nearly all of them. A sweep of
`lightning_bolt`'s critical chance across its whole range shows it: at 0.317 `pummel` holds 55.6 % of the
casts and `lightning_bolt` 10.3 %, at 0.417 they swap to 7.3 % and 62.4 %, and the largest share any one
spell takes never falls below 0.504 anywhere in between. Across the 149 candidates of a full tuning pass the
lowest it reached was 0.284, on content that scored 338.70 on everything else. A band of 0.25 is not a band
this player can land in, whatever the content is.

So the objective was paying every tuning pass to chase a number an argmax cannot produce — the same shape of
defect as the attribution hole [0027](0027-a-condition-remembers-the-spell-that-applied-it.md) closed, where
the objective priced something that was not being measured.

## Decision

We will read those seven targets on a new `variety` evaluation, `explore:0.2` against itself: both sides play
greedily four decisions in five and draw uniformly over the legal candidates the fifth ([ADR
0014](0014-exploration-in-recorded-datasets.md)). The rule is stated once and applied to all seven:
**a target that needs a spell to be cast in order to mean anything is read on the exploring run.** The
mirrored greedy run keeps what it reads well — who wins, how long a match lasts, how often it runs out of
rounds, how often an action fizzles.

**No band and no weight changes.** That is the point: the same questions, asked of a player who can see the
choice. On the core content, with nothing in the content moved:

| Target | On the greedy mirror | On the exploring run | Band |
| --- | --- | --- | --- |
| `spellEntropyA` | 1.992 (penalty 1.03) | **2.530** (0.00) | 2.5.. |
| `tierWinSpread` | 0.322 (2.97) | **0.148** (0.00) | ..0.15 |
| `tierUsageShare` | 0.742 (11.68) | **0.568** (0.93) | ..0.5 |
| `spellUsageShare` | 0.478 (5.21) | **0.410** (2.57) | ..0.25 |
| `spellsNeverCast` | 2 | **0** | ..2 |
| `spellsBarelyCast` | 1 | **0** | ..2 |
| `tierDamageSpread` | 5.000 (36.00) | 5.000 (36.00) | ..2 |
| **total objective** | **69.39** | **52.00** | |

Two spells read as never cast and are cast. Four readings land in or near their bands. `tierDamageSpread`
does not move at all, and that is the useful half of the result: it is the one of the seven that was never
about the agent, and it is now 36.00 of the remaining 52.00.

The rate is 0.2 and not higher on purpose. Exploration is a dial between measuring the content and measuring
the dice: at 0.35 `tierUsageShare` reads 0.482, inside its band, but it reads better *because* the play is
more random, and at 1.0 every reading would be perfect and say nothing. 0.2 leaves four decisions in five to
the content.

## Consequences

- Good: the variety targets became answerable. A tuning pass can now improve them instead of grinding against
  a floor the agent sets.
- Good: `tierDamageSpread` is exposed as the real content problem, 36.00 of the remaining 52.00. It was
  hidden behind eleven points of `tierUsageShare` that the agent, not the catalogue, was responsible for.
- Good: two spells that every journal entry called "never cast" are cast the moment a player looks at them.
  That claim was about `Greedy`, not about the content, and the entries that made it were wrong about which.
- Bad: **every score before this is incomparable with every score after it.** The core content went 69.39 to
  52.00 with no number in it moving. The objective's own `score` note says so.
- Bad: the objective now depends on a drawing agent. It stays reproducible — `ExploringAgent` is
  deterministic for a seeded source and the seed file is fixed — but a small content change can shift the
  whole draw sequence, so a candidate's reading carries more noise than a mirrored greedy one does. The
  benchmark digest is untouched and stays defined by agents that draw nothing (ADR 0013, decision I).
- Bad: a fourth evaluation takes a candidate from about 27 seconds to about 34, and the tuning workflow's
  default budget from about 130 minutes to about 160. Its timeout goes to 300 minutes.
- Neutral: no content, no rule and no weight moved, so the benchmark digest does not change.

## Alternatives considered

- **Raise `spellUsageShare`'s weight so the tuner cares more.** Measured, not argued: at weight 2 the winning
  candidate of the last pass scores 45.17 and the best spread-out alternative 103.33 — the same choice. It
  takes a weight of about 6 to flip, and what it flips to scores 103 on everything else. That is not
  tightening a target, it is letting one term eat the objective.
- **Widen the knob bounds so tier 1 can become a choice.** `check-knobs` has been reporting that
  `lightning_bolt` carries 6.67 where `pummel` tops out at 5.40, and it reads like the binding constraint. It
  is not: `lightning_bolt`'s critical chance already reaches 0.17, and moving it anywhere only relocates the
  monopoly rather than splitting it.
- **Reband the targets to what the argmax can reach**, around 0.40. Honest, and it concedes the wrong thing:
  it would write the agent's limitation into the definition of a balanced catalogue.
- **Drop the spread targets and keep `tierWinSpread` alone**, on the argument that equal worth is the real
  question and equal usage is a proxy. Tempting, and it loses the reading that catches a tier where one spell
  is worth the same as another and still takes every cast.

## Follow-up

- `data/balance/knobs.json`: the `variety` evaluation, the seven `on` fields, the `score` note.
- `.github/workflows/tune.yml`: the timeout.
- `data/balance/README.md`: the evaluation list and the per-candidate cost.
- The next tuning pass is against a different objective, so the proposal in `runs/tune-local-1` is stale.
