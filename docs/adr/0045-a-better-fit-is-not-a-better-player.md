# 0045. A better fit is not a better player

Date: 2026-09-15
Status: Accepted

## Context

The value policy fits one ridge regression per action key over the whole observation (ADR 0016). On the
1000-match exploring dataset of `ci-10` that is **431 features fitted from a median of 60 examples**: only
**63 of 612** keys have as many examples as there are features, so 549 of the regressions are
underdetermined. It shows: `r2` reads **−0.2876** against a `baselineR2` of **0.1355**, meaning the position
alone predicts the held-out return better than the position and the action together. More data barely helps —
doubling the dataset moves the median to 120, still a quarter of 431 — and raising `min_samples` starves rows
instead, which `ci-5` already measured.

The obvious fix is to stop fitting each action on its own: fit one regression per **decision kind** over every
step of that kind, and leave each action key a scalar. The board response is then determined by tens of
thousands of steps rather than sixty, and what a key keeps for itself is a mean, which sixty samples settle.
The cost is expressiveness: every Intent answers the board identically, and only a scalar tells two spells
apart.

## Decision

We will keep **one regression per action key** as the default, because the sharing was measured and it plays
worse. `train-value --share kind` stays as a measured alternative and `--share action` is the default; both
write the same `policy.json`, so nothing downstream knows the difference. Measured on `ci-10`'s exploring
dataset, held out by match, against `Greedy` on the benchmark seeds:

| | `action` | `kind` |
| --- | --- | --- |
| regressions | 600 | **5** |
| `r2` | −0.2876 | **+0.1292** |
| loss | 1.33 | **0.90** |
| imitation accuracy | 0.3407 | **0.3647** |
| **win rate against `Greedy`** | **0.4350** | 0.3250 |

Every number that describes the fit improves, one of them by 0.42 of `r2`, and the agent loses **11 points of
win rate**. The underdetermined rows are noisy, and the noise is apparently worth more than the ranking the
shared model throws away: a policy that answers every position with one spell preference order cannot say
"cast this here and that there", which is most of what there is to learn.

## Consequences

- Good: **the strongest evidence yet that the fit is not the objective**, which is the lesson `ci-9` got wrong
  in the other direction — it stopped this work on `baselineR2 > r2` and the agent later went 0 of 400 to
  43.5% with that gap wider still. A policy has to *rank* the actions of one position; `r2` scores absolute
  prediction, and the two can move in opposite directions by this much.
- Good: the diagnosis in the Context is still correct and now quantified, so the next attempt starts from a
  measured place rather than the same guess.
- Bad: **the defect this ADR set out to fix is still there.** 549 of 612 regressions remain underdetermined;
  the only thing established is that collapsing them to five is worse.
- Bad: a second way to train is a second thing to keep working, for an option that currently loses. It earns
  its place by being the evidence, and the test suite pins both.
- Neutral: `policy.json` is unchanged in shape, so the engine's `PolicyAgent`, the file's readers and ADR
  0013's exchange format are untouched. `--share kind` is not smaller either — it is 7.2 MB against 6.0, the
  shared rows being dense where unfitted per-action rows were exactly zero.
- Neutral: `regressions` joins the metrics, so a training log says how many models a policy actually holds.

## Alternatives considered

- **Make `kind` the default anyway, on the fit.** What this ADR exists to refuse. It would have been an easy
  mistake: every number on the training side says it is better.
- **Share by (kind, spell) instead of by kind**, so a spell keeps its own board response while slots and
  target sets pool. A real middle, untried, and the obvious next measurement: it multiplies the examples per
  regression by roughly four without flattening the spell choice, which is the part `kind` destroys.
- **More data.** Measured as insufficient above: the ratio improves with the square root of nothing useful.
- **Featurise the action into the observation** — one model over (state, action) features rather than a row
  per key. The principled version, and much the largest change: it needs the same featurisation on the C#
  side of `PolicyAgent`, so it needs ADR 0013's exchange format to move with it. Worth doing if the
  (kind, spell) middle also fails.
- **Drop the value policy and keep the clone.** The clone is the weaker agent here (32.0% against 43.5%) and
  is capped by what it imitates, so this would remove the better half.

## Follow-up

- `learning/src/downfall_learning/train_value.py`: `SHARES`, `ValueOptions.share`, `_per_kind`, `_per_action`.
- `learning/src/downfall_learning/cli.py` (`--share`) and `scripts/iterate.sh` (`--value-share`).
- `learning/tests/test_train_value.py` pins both splits and the refusal of an unknown one.
- The journal entry of 2026-09-15 carries the table and what it cost to learn.
- Open: the (kind, spell) middle above, and whether the round-cap liability the journal records for the value
  policy survives it.
