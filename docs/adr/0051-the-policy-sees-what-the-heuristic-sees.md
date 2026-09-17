# 0051. The policy sees what the heuristic sees

Date: 2026-09-17
Status: Accepted

## Context

Every learned policy this project has trained is a linear scorer over the observation: one row per action
key, dotted with the board, plus a bias (`policy.json`, ADR 0016). The observation says what the board is.
It does not say what an action would do to it: whether this Strike kills, how much of the target's health it
takes, what the bleed it applies is worth over the rounds it holds. The heuristic agents read exactly that,
nine terms per candidate action (`ActionScorer`, ADR 0050 for the ninth), and weigh them; a policy has to
infer it from the board, one key at a time, with a row that cannot depend on which candidate is being scored.

The journal has measured the ceiling that puts on a clone three times. A clone of `search-4` plays at parity
with `search-4` (2026-09-15); a clone of `mixture-mean`, a teacher that beats `search-4` by 25 points, plays
at parity with `search-4` as well and 27 points below the clone of `search-4` against Greedy (2026-09-16).
The eight decisions in a hundred the clone misses are the ones the stronger teacher makes and `search-4` does
not, and they are the decisions that hang on the terms: a kill on a creature at three health reads, on the
board, as a health fraction among a hundred and nine features, and the key `intent:1:spell:strike:v1` has
one row for every board it is ever cast on. A clone is capped by what it can copy, and what it can copy
stops where the observation stops. The value fit is under the same ceiling from the other side: the return it
regresses per key over the board is mostly the position, and the part an action adds is the part the board
does not carry.

The terms are cheap: the scorer computes the nine of them on the way to the one number it returns, and
reading them for every candidate of a decision is the one-step lookahead the heuristic pays for that decision.

## Decision

We will record, beside the observation of every step, the scorer's terms of every candidate action the
decision offered, and let a policy carry one weight per term, shared by every action key, added to a
candidate's score on its own terms.

**The channel.** `ScoreTerms` is the nine signed quantities `ScoringWeights` multiplies, in weight order;
`ActionScorer` computes them once and its score is `ScoringWeights.Apply(terms)`, so the heuristic agents
decide exactly as before. `CandidateTerms` reads them per candidate the way `HeuristicAgent` reads: an
intent carries the terms of the target set the built-in weights would bind for it, the allies' declared kills
written off; a target set its own, the revealed actions' kills written off; an unlock its combat estimate,
the initiative it buys and the cost it cannot cover; a speed choice and a pass all zeros. The invariant the
channel rests on, tested over a played match: every intent and every target set the heuristic chose scores
best, under the built-in weights, among the terms recorded beside it. A policy whose candidate weights are
the built-in weights and whose rows are zero therefore plays the heuristic's combat decisions exactly.

**The artifacts.** A step gains `candidateTerms`, one number list per candidate in candidate order; the
manifest gains `candidateTermNames`; `policy.json` gains `candidateTermNames` and `candidateWeights`. The
feature schema stays `features:v5`: the terms belong to the candidate, not to the board, and travel as a
second channel next to the observation, so every dataset and every model recorded before this ADR loads
unchanged and plays as before, `train-value` on a run without terms fits exactly what it fitted, and a policy
without weights scores the keys alone. `train-clone` is a different learner with or without terms (below), so
its `loss` and `accuracy` on a run without them are not comparable with the journal's before this ADR.
A reader refuses a step missing the terms its run names, terms that do not match the step's candidates, runs
naming different terms loaded together, and a policy naming the terms in another order than the engine's.

**The learners.** `train-clone` becomes a conditional logit: the policy's own score of every candidate the
step offered, a softmax over those candidates alone, the negative log probability of the action taken as
the loss, minimized by Adam in mini-batches. The multinomial classifier it was could not read a term at all,
since a term belongs to the candidate and not to the key, and spent its capacity telling apart actions never
offered together. `train-value` fits the terms first, the way it fits the baseline first: one ridge over the
terms of the action taken, on every training step, to the advantages, and the rows on what that leaves;
`termsR2` reports what the terms alone explain on the held-out steps. The engine's `PolicyAgent` hands a
policy that carries candidate weights each candidate's terms, read by the same `CandidateTerms` that
recorded them, so a policy plays identically on both sides; a policy without them never pays for the reading.

## Consequences

- Good: a policy can express what the heuristic reads, and a clone's ceiling is no longer the observation.
  With nine numbers set to the built-in weights it is the heuristic; what its rows then learn is what the
  board says beyond what an action does, which is the question the loop was asked and could not answer.
- Good: nothing stamped moves. The benchmark digest verifies unchanged, `features:v5` is untouched, `ci-69`
  and every dataset since it load as before, and every committed policy plays as before.
- Good: the terms are shared by every key, so they are fitted on every step rather than on the sixty a key
  has (ADR 0045), which is where the value fit was starving.
- Bad: a step is wider. Nine floats per candidate: under a tenth of the viewer's sample run on disk, at under
  two candidates a step, and more where a decision offers more. The recorder reads the terms beside the
  agent it wraps, so a heuristic teacher pays its one-step reading twice and a random one pays it for the
  first time.
- Bad: the reading is the built-in weights', whatever agent is recorded, so that a dataset and the policy
  trained on it read the same numbers without carrying weights of their own. A target set's terms are its
  own under any weights; an intent's are one fixed target set's, the one Greedy would bind, and a heuristic
  teacher playing other weights may bind another. The invariant above is exact for Greedy and a reading of
  the board, not of the teacher, for `search-4` or `mixture-mean`; if a clone of such a teacher stays capped
  with the terms, reading them under the teacher's weights, recorded in the manifest and carried by the
  policy, is the next change.
- Bad: the clone no longer uses `SGDClassifier`; the optimizer, the batching and the fold of the scaling
  into the file are this project's, and their correctness rests on the tests that recover a planted rule from
  the terms and refuse to without them.
- Neutral: a policy's score is no longer a function of the observation and the key alone. Anything that
  replays a policy from its file needs the engine's reading of the terms, which is why `PolicyAgent` and
  `Policy.choose` are the only two readers, each tested to add a candidate's terms under the same weights.

## Alternatives considered

- **A new feature schema with the terms of every candidate in the observation.** The observation is one
  vector per decision and the candidates are a ragged list, so it would hold the terms of the first N
  candidates in a fixed order, which the rows still could not tell apart per candidate; and it would bump the
  schema, cutting every dataset before it. A second channel keeps the row per key and the vector per board
  as they are and adds what is per candidate where it belongs.
- **Feed the heuristic's one score, not its nine terms.** One number per candidate under one weight is the
  heuristic as a prior the policy can scale but not reshape. Nine terms under nine weights let the learner
  move the kill weight against the damage weight the way `search-weights` does, from data rather than from
  four hundred matches per candidate.
- **Keep the multinomial clone and add the terms of the action taken as features.** A classifier over keys
  scores a key, not a candidate; the taken candidate's terms as features would tell it what was done, not
  what each option would have done, and the argmax at play time has no such feature to read.
- **A non-linear policy.** It would read the board better and the file would need a runtime the engine does
  not have (AGENTS.md: no ML runtime in .NET). The terms are the cheapest reading of the rules a linear
  policy can be given, and the ceiling they lift is measured before a wider model is worth the runtime.

## Follow-up

- A turn of the loop on a dataset that records the terms, three seeds, against the same bar: whether the
  clone of `mixture-mean` now keeps its teacher's edge, and what `termsR2` says of the value fit.
- `docs/learning/artifacts.md`, `docs/learning/training.md`, `docs/learning/features.md`, the glossary's
  Candidate terms entry.
