# 0038. A wasted action is a fizzle, whatever wasted it

Date: 2026-09-13
Status: Accepted

## Context

[ADR 0037](0037-measure-the-energy-weight-and-move-it-from-0-2-to-0-3.md)'s sweep found that the `risk`
weight does nothing: every value from 0 to 100 plays the 400 benchmark seeds identically, column for column,
and setting it to zero changes not one match. Its two outcomes are selected by whether the value is a
multiple of three, which is a floating-point tie-break and not an effect.

The name is the second half of the problem, and the half that can be fixed on its own. "Risk" promises a
reading of probability — how likely is this to go wrong — that the term has never had and does not compute.
What it counts is actions that came to nothing, and there are three ways that happens: a castable spell with
no legal target, a resolution that fizzled, and targets gone before the spell resolved. A fourth is coming:
the actor stunned between declaring and acting. They are one thing under different names, and the weights
table already reaches for the right word when it explains the number — "a wasted action".

Renaming is not free. The weight's name is a JSON key in `learning/weights/*.json`, it is in `Named` and so
in every error message and studio box, and `WEIGHT_NAMES` gates what a Python-written weights file may
contain.

## Decision

We will rename the weight `risk` to **`fizzle`**, everywhere and at once, and we will **change no behaviour**.
The three readers stay exactly where they are, because none of them is wrong: `Score` prices a resolution,
and a real resolution can fizzle and can lose targets. What is missing is a reading at *declaration* time,
and adding one is a separate decision ([ADR 0039](0039-the-bot-cannot-see-the-waste-it-is-about-to-cause.md)),
taken separately so that the digest moves once and it is known which change moved it.

The same rename fixes a claim this project published and got wrong. ADR 0037's journal entry said the weight
is read in two places, both inside `ActionScorer.Score`. It is read in three: `HeuristicAgent.DecideIntent`
scores a castable spell with no legal target at `-weights.Fizzle` against the other spells' scores, and that
one *is* on the decision path. The measured conclusion is unchanged — no value from 0 to 100 moves an
outcome — but the reason given for it was incomplete, and the correction is recorded rather than quietly
folded in.

`ScoringWeights` carries which readers reach a decision, in the doc comment, so the next person to read the
number does not have to rediscover it.

## Consequences

- Good: the name says what the term counts, and the four ways an action comes to nothing stop needing four
  explanations.
- Good: **the fingerprint does not move.** It hashes values, not names, so every stamp written against
  `1933f3ae` still matches. The benchmark digest does not move either: no behaviour changes.
- Bad: **a weights file that still says `"risk"` now fails to load.** `JsonScoringWeightsSource` sets
  `UnmappedMemberHandling.Disallow` and `export.py` refuses a name outside `WEIGHT_NAMES`, so the failure is
  loud on both sides rather than a silently defaulted weight — which is the behaviour we want, and is why
  `greedy.json` and `search-2.json` are converted in this change. Any weights file outside the repository
  needs the same one-line edit.
- Bad: the sweep tables in ADR 0037 and its journal entry name a weight that no longer exists. They are
  accepted and stay as written; this ADR is the link forward.
- Neutral: `models/` policies do not carry weight names, so nothing there moves.
- Neutral: the value stays at 2.0 and stays unmeasured. Measuring it before the term can reach a decision
  would measure nothing, which is exactly what ADR 0037 did.

## Alternatives considered

- **Delete the two readers inside `Score`.** This was the first plan, and reading the code killed it: they
  are unreachable from a decision, not wrong. `Score` documents itself as the score of one resolution, and a
  resolution handed to it really can have fizzled or lost targets. Deleting them would hide the defect
  instead of fixing it, and would leave `Score` wrong for its own stated job.
- **Rename and teach it to see in one change.** Then the benchmark digest moves, and the rename and the new
  behaviour are inside the same move with no way to tell which did what. Two ADRs cost a round trip and buy a
  measurement that means something.
- **`waste` or `forfeit`.** Both read well. `fizzle` wins because the engine already uses it: `Fizzled` is a
  property on `CombatResolution`, `fizzleRateA` is an objective metric, and the evaluation report counts
  `fizzles`. A weight named after the thing the engine already counts needs no glossary entry of its own.
- **Keep `risk`.** Every existing weights file keeps working. But the name is the reason the term reads as
  something it is not, and the next person to tune it would make the same wrong assumption.

## Follow-up

- `ScoringWeights` (the record parameter, `Named`, the doc comment), `ActionScorer`, `HeuristicAgent`,
  `JsonScoringWeightsSource` and its `WeightsFile`.
- `learning/weights/greedy.json`, `learning/weights/search-2.json`,
  `learning/src/downfall_learning/export.py`'s `WEIGHT_NAMES` and `DEFAULT_WEIGHTS`.
- `docs/learning/agents.md`: the term table, the weights table and the provenance section.
- ADR 0039: the declaration-time reading, which is what actually fixes the blind spot.
