# 0046. Credit a move along its trajectory, not from the end of the match

Date: 2026-09-15
Status: Accepted

## Context

`Returns.Of` awards one scalar per (match, slot) — win, loss, draw, plus a tenth of the health margin (ADR
0013, decision C) — and the dataset joins **that same scalar onto every step of the episode**
(`artifacts.py`, `_select`). A 30-round match therefore labels hundreds of decisions with one ±1. The
pivotal cast and the forced one carry the same number, and nothing in the target says which was which.

ADR 0016 subtracts the fitted state value, which removes what the position was worth but not the variance:
the residual of every step of a match still moves together with that match's outcome. ADR 0045 then measured
two ways of grouping the action rows and found the better fit played 11 points worse. Both groupings were
fitted on this same target, so **the grouping and the target were never separated**, and the diagnosis ADR
0045 left open — 549 of 612 regressions underdetermined — is one hypothesis rather than the established
cause.

## Decision

We will estimate what an action added **along its own trajectory** (generalized advantage estimation), with
`--gae-lambda` and `--discount` on `train-value` and `--value-lambda` / `--value-discount` on
`scripts/iterate.sh`.

A match pays nothing before it ends, so the temporal difference of a step is `discount * V(next) - V(here)`:
how much the position improved, which is the part the move is answerable for. The last step of an episode
takes the real return instead. `lambda` sets how far each step still looks ahead, and it spans the two
extremes rather than replacing one with the other:

| `lambda` | what a row is fitted on | |
| --- | --- | --- |
| **1.0** (default) | the episode return over the state value | what every run before this computed |
| 0.0 | the one-step change in state value | least variance, and only as good as the baseline |

A trajectory is one **(match, slot)**, in recorded order: both players of a match are interleaved in the
arrays, so `Dataset` now carries `slots` alongside `match_ids`.

**The default does not move.** At `lambda` 1.0 the backward sum telescopes back to `returns - values`, which
makes this a strict generalization: measured at 1.6e-15 of maximum difference over 12,400 steps of 30-step
episodes, four orders below the six decimals a policy is written with (ADR 0013). So no existing number is
invalidated by this landing, and the comparison is one run against another rather than against history.

## Consequences

- Good: the target the action rows are fitted on is a knob for the first time, and the metric `advantageStd`
  says what it costs. The defect ADR 0045 named — thin, noisy regressions — is attacked from the other side:
  the same 60 examples per key determine far more when what they carry is one move's effect rather than one
  match's outcome.
- Good: nothing downstream moves. `policy.json`, its readers, `PolicyAgent` and ADR 0013's exchange format
  are untouched, exactly as with ADR 0045's `--share`.
- Bad: **below `lambda` 1.0 the rows no longer target the episode return, so `loss` and `r2` are expected to
  read worse while the agent plays better.** They are kept because they say whether the fit did what it was
  asked, and ADR 0045 is the standing warning against reading them as the objective.
- Bad: at low `lambda` the signal is whatever the baseline says, and `baselineR2` has read between 0.07 and
  0.14. A bad baseline makes a bad advantage, where the episode return at least could not be wrong about who
  won. This is the trade, and it is why both ends are reachable and neither is assumed.
- Neutral: the backward recursion is a Python loop over each episode, about 12,000 steps a second. Against a
  turn of the loop measured in minutes it does not register.

## Alternatives considered

- **Fit the baseline on bootstrapped targets too**, rather than on returns. The next step if this works, and
  a larger one: the baseline is currently the best-determined part of the model and making it recursive
  risks that. Fitting V on returns and the advantage on V is the standard split and the smaller change.
- **A per-step reward shaped from the health margin**, so a move is paid when it lands. That changes what the
  engine records (`Returns.Of`) and therefore every dataset ever recorded, where this changes only how a
  recorded dataset is read. Worth doing if the discount turns out to matter more than the lambda.
- **Leave it and try the `(kind, spell)` middle first** (ADR 0045's own follow-up). Rejected as an ordering:
  that middle groups rows fitted on this target, so measuring it first measures the two together again,
  which is the mistake this ADR exists to stop repeating.
- **Change nothing, since `r2` will look worse.** What this ADR refuses, for the reason ADR 0045 established
  the hard way.

## Follow-up

- `learning/src/downfall_learning/train_value.py`: `_episodes`, `_advantages`, `ValueOptions.discount` and
  `ValueOptions.gae_lambda`, and the `gaeLambda` / `discount` / `advantageStd` metrics.
- `learning/src/downfall_learning/artifacts.py`: `Dataset.slots`, so a trajectory can be named.
- `learning/tests/test_train_value.py` pins the telescoping at 1.0, the one-step form at 0.0, that a
  trajectory is one player of one match, and the refusal of a lambda or a discount outside its range.
- Open: which `lambda` wins, measured on one dataset against the win rate and never against `r2`; whether
  the `(kind, spell)` middle behaves differently once the target is not the whole match; and whether a
  discount below 1.0 adds anything the lambda has not.
