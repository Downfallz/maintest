# 0014. Record datasets with an exploring agent instead of reaching for reinforcement learning

Date: 2026-09-09
Status: Accepted

## Context

Value regression (ADR 0013, phase L6) has now lost every one of 400 mirrored matches against `Greedy`, with
no draw, in three different settings: `Greedy` self-play on 200 matches, the same fivefold larger with ten
times the ridge penalty and ten times the per-action sample floor, and the union of a `Greedy` self-play and
a `Random` self-play dataset. The held-out fit improved clearly along the way (r² 0.216 to 0.372) and the win
rate never moved by a thousandth. Behaviour cloning, trained on the same data and played through the same
agent, reaches 98.5% accuracy and 38.0% against `Greedy`, which is `Greedy`'s own level: the encoding, the
policy format, and the agent are sound, and the fault is in what value regression is asked to learn.

The reason is that each action key gets its own regression, fitted only on the steps where that action was
taken, and a deterministic policy makes those subsets disjoint state distributions. `heavy_strike` is fitted
on the states where `Greedy` wanted it, `basic_attack` on the leftovers where it was unavailable. The
observed return then measures how good those situations were, not how good the action is, and comparing two
such rows at one state compares two models calibrated on different worlds. Adding `Random` self-play does not
repair it: it covers many actions, but in states no strong policy visits and with returns that a single
action barely moves, so it adds noise rather than counterfactuals. What is missing is not data volume or
regularization but the same state distribution behind every row. The learning roadmap defers reinforcement
learning until the simple learners plateau, and two cheap attempts have now established that they have.

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
- Neutral: this defers PPO self-play once more rather than settling it. Exploration equalizes the state
  distribution behind every action row, which is the defect named above, but it leaves the rows independent
  of each other; if it is not enough, the next step is a single model over state and action together, and
  after that reinforcement learning gets its own ADR with the dependency it needs.

## Alternatives considered

- Train on the union of datasets from different agent pairs (`--allow-mixed`, no code at all): measured, and
  it changed nothing, still 0 of 400. It buys coverage in states no strong policy reaches, with returns that
  a single action barely moves. This is the second cheap attempt the decision above rests on.
- Go straight to PPO self-play: a training loop with the engine inside it and a heavyweight dependency, for a
  problem whose cheap explanation has not yet been tested.
- Label every candidate with the greedy scorer instead of exploring: the model would learn `Greedy`'s own
  scoring and could never exceed it, which is the whole point of the exercise.
- Keep behaviour cloning as the only learner: capped at the teacher's strength by construction, so nothing in
  the loop could ever beat `Greedy`.

## Follow-up

Done when this was accepted:

- `AgentKind.Explore`, `ExploringAgent` and the factory's `explore:<rate>` case, with their tests.
- `docs/domain/glossary.md`, `docs/learning/agents.md`, `docs/learning/training.md`,
  `docs/learning/explained.md`, `AGENTS.md` commands.
- `scripts/iterate.sh --explore <rate>`: it records a second, explored dataset and trains the value policy on
  it, while the clone keeps the pure one, so the clone's numbers stay comparable from run to run.
- The benchmark baseline stays `Greedy` against `Greedy` (`benchmarks/README.md`, ADR 0013 decision I): an
  agent that draws from a random source never defines the digest.
