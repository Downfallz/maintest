# 0014. Record datasets with an exploring agent instead of reaching for reinforcement learning

Date: 2026-09-09
Status: Proposed

## Context

Value regression (ADR 0013, phase L6) trained on `Greedy` self-play has now lost every one of 400 mirrored
matches against `Greedy`, twice, while beating `Random`. Between the two runs the dataset grew fivefold, the
ridge penalty went from 1 to 10 and the per-action sample floor from 5 to 50: the held-out fit improved
clearly (r² 0.216 to 0.372) and the win rate did not move by a thousandth. That combination rules out
variance. `Greedy` is deterministic, so in its self-play the action is nearly a function of the state and each
action key's row is fitted only on the states where `Greedy` chose it; the model never sees a counterfactual.
At play time every row is asked to score the same state, far outside its own support, and the ranking is
decided by extrapolation. Behaviour cloning reaches 98.5% accuracy on that same data, which places the fault
in the training target rather than in the features, the encoding, or the pipeline. The learning roadmap
defers reinforcement learning until the simple learners plateau, and they now have.

## Decision

We will give the recorded datasets action coverage before we consider reinforcement learning. A new player
agent kind plays `Greedy`'s choice with probability `1 - epsilon` and a uniformly random legal option
otherwise, seeded like `Random` so a run stays reproducible, addressed as `explore:<epsilon>` (the spec's path
slot carries the number). `simulate --record` with it on both sides produces states where an action was taken
that `Greedy` would not have chosen, which is exactly the counterfactual the per-action regressions lack.
Nothing else changes: the same value regression, the same behaviour cloning, the same `policy.json`, no ML
runtime in .NET and no new dependency on either side.

## Consequences

- Good: the cheapest change that can settle the question, one agent class and one enum value, reusing the
  seeded randomness and the agent registry that already exist. The exploring agent's spec lands in the run
  stamp, so an explored dataset can never be confused with pure self-play.
- Bad: one more knob (`--explore`) in a loop we are deliberately keeping small, and the recorded bot is no
  longer purely `Greedy`, so a clone trained on it imitates a policy that is wrong on purpose part of the
  time. Its argmax should still recover `Greedy`'s choice in most states, but its accuracy figure stops
  meaning what it meant.
- Neutral: this defers PPO self-play once more rather than settling it. If exploration does not lift the value
  agent above `Random`-level play against `Greedy`, the simple path really is exhausted and reinforcement
  learning gets its own ADR with the dependency it needs.

## Alternatives considered

- Train on the union of datasets from different agent pairs (`--allow-mixed`, no code at all): being measured
  now. Broad coverage, but `Random`'s states are far from those a strong policy visits, so it may buy
  coverage in the wrong places. A useful check, not a fix.
- Go straight to PPO self-play: a training loop with the engine inside it and a heavyweight dependency, for a
  problem whose cheap explanation has not yet been tested.
- Label every candidate with the greedy scorer instead of exploring: the model would learn `Greedy`'s own
  scoring and could never exceed it, which is the whole point of the exercise.
- Keep behaviour cloning as the only learner: capped at the teacher's strength by construction, so nothing in
  the loop could ever beat `Greedy`.

## Follow-up

- `AgentKind`, `AgentFactory` and the new agent in `Application/Agents`, with its tests.
- `docs/domain/glossary.md` (a term for the exploring agent), `docs/learning/agents.md`,
  `docs/learning/training.md`, `docs/learning/explained.md`, `AGENTS.md` commands.
- `scripts/iterate.sh`: an `--explore` flag and its `--help` line; whether the loop records one explored
  dataset or keeps a pure one for the clone is a question for the change itself.
- The benchmark baseline stays `Greedy` against `Greedy` (`benchmarks/README.md`, ADR 0013 decision I): an
  agent that draws from a random source must never define the digest.
