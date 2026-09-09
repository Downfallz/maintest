# 0015. Learn action values against a state baseline

Date: 2026-09-09
Status: Proposed

## Context

Value regression has now lost 400 mirrored matches of 400 to `Greedy` seven times, across every axis the data
offers: with and without exploration (ADR 0014), on 200 and on 1000 matches, at regularizations from 1 to 10,
at sample thresholds from 5 to 50. The held-out fit moved a great deal over those runs and the win rate never
moved at all — `ci-4` reached r² 0.190, and `ci-5`, which fitted 293 of 455 action keys instead of 191,
returned r² 0.188. Filling in the rows that had no model changed nothing, so the shortage of data is not what
stands between this learner and a single win. What remains is the shape: one ridge regression per action key,
each fitted alone on the raw match return of the steps where that action was taken, then compared with the
others at a single state. The return of one step is the outcome of a match of roughly a hundred and fifty
decisions, so it measures the situation the player was in far more than the action they chose; each row
learns that situation and their intercepts are calibrated on different worlds, which is exactly what makes
them incomparable at the moment of choosing.

## Decision

We will fit one state-value model `V(s)` on every step of the dataset, with no split by action, and regress
each action key on the residual `return - V(s)` rather than on the return itself. The baseline is estimated on
the whole dataset, so it is the best-determined part of the model, and subtracting it removes from every
action's target the part of the return that the situation explains. What each action row then learns is what
that action is worth *relative to the position*, which is the quantity the policy needs when it ranks two
legal moves at one state. The policy file gains the baseline as one more row, and `PolicyAgent` keeps
choosing the highest-scoring legal key, so nothing changes in the engine or in the artifact contract beyond
one named row.

## Consequences

- Good: the quantity being estimated is the one the policy uses. A row no longer has to explain the whole
  return, only the part its action is responsible for, so the same data buys a far better-determined estimate.
- Good: the baseline is one regression over all 250,000 training steps, not 455 regressions over a few
  hundred each, so it is well determined even where individual action rows are thin.
- Good: it is cheap and it is inside the existing machinery — one more `Ridge` fit, one more row in
  `policy.json`, no new dependency and no reinforcement learning runtime (ADR 0013 stands).
- Bad: two stages mean two places to get wrong, and a badly fitted baseline pushes its own error into every
  action row rather than cancelling out.
- Bad: the residual is smaller than the return, so its signal-to-noise ratio is worse in absolute terms; a
  weak improvement may be harder to see than the current, obviously broken, result.
- Neutral: `policy.json` grows one row and its schema gains a name. Existing policies stay readable, since a
  file without a baseline is a baseline of zero.
- Neutral: this does not make the learner on-policy. It is still one step of policy improvement over the
  behaviour that produced the data, and it inherits that ceiling.

## Alternatives considered

- **More data, again**: `ci-4` and `ci-5` already answered this. Five times the matches tripled the fit and
  left the win rate at zero, and the count of action keys grows with the data, so the ratio does not converge.
- **A single joint model over the state and the action** (one row of weights over features crossed with the
  action key): mathematically close to what we have, since per-action ridge already is a fully interacted
  model; without pooling it would inherit the same problem, and with pooling it becomes a bigger version of
  this ADR that needs a new file format.
- **Temporal-difference bootstrapping**: the principled fix to credit assignment, and a much larger change —
  a training loop over transitions, a target network or an equivalent, and the reinforcement-learning runtime
  ADR 0013 deliberately kept out of this project. Worth reopening only if a baseline is not enough.
- **Give up on value regression and keep behaviour cloning**: cloning already reaches `Greedy`'s strength and
  cannot exceed it, so the loop would have no way to produce a bot better than the one it records.

## Follow-up

- `learning/src/downfall_learning/train_value.py`: fit the baseline, regress the residual, report its own fit.
- `learning/src/downfall_learning/policy.py` and the `policy.json` contract in `docs/learning/artifacts.md`:
  the baseline row, and reading a file that has none as a zero baseline.
- `src/DownfallArena.Application/Agents/PolicyAgent.cs` and `PolicyFile`: score the baseline row.
- `docs/learning/training.md` and `docs/learning/explained.md`: what the baseline is, in both registers.
- `docs/domain/glossary.md`: baseline, advantage.
