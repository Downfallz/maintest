# 0055. A searching agent may be built on a policy, so the loop has an operator that improves one

Date: 2026-09-17
Status: Proposed

## Context

The learning loop records a teacher's play, fits a policy to it, and the policy becomes the next teacher. Two
turns have now measured what that loop does, and the answer is that it does not climb.

| | copy accuracy | the clone against its own teacher |
| --- | --- | --- |
| `ci-118`, teacher `search-4`, multinomial | 0.9600 to 0.9647 | 0.5513 to 0.6175 |
| `ci-126`, teacher `search-4`, terms | 0.9928 to 0.9943 | 0.4950 to 0.5000 |
| `ci-138`, teacher `stun-first`, terms | 0.9846 to 0.9855 | 0.4875 to 0.4925 |

The better the copy, the closer to one half it lands: a tight copy makes the match a mirror, a mirror is one
half by the symmetry of the seeds, and the copy approaches that half from below because a copy is never quite
the thing it copies (journal, 2026-09-17). **Imitation is a fixed point.** Whatever the clone arm improves —
the encoding, the loss, the candidate terms — it improves how fast the loop reaches its teacher, never what
its teacher is.

The other arm does not carry the loop either. The value fit reads 0.0537 to 0.7550 against Greedy across
three seeds of one configuration, and the jackknife of their mean is 0.2913 plus or minus 0.3454: an interval
from zero to almost one. It is not a weak signal, it is noise.

So what has actually climbed is the weight ladder — `greedy`, `search-2`, `search-4`, `mixture-mean`,
`pressure-floor`, `stun-first` — and that is a cross-entropy search over nine numbers of a hand-written
scorer, run from outside the loop. It is also saturating: `stun-first` takes every match from Greedy and from
Random, and the rest of the panel takes 0.005 to 0.175 from it.

A loop that climbs needs an **improvement operator**: something that plays better than the policy, whose play
is then the policy's next lesson. This repository already has one and cannot point it at a policy. The
searching agents of [ADR 0047](0047-a-lookahead-agent-needs-a-hypothetical-board.md) play the round out on a
hypothetical board and choose the move whose round ends best, which beats the one-step reading the policy
makes. But `lookahead` and `minimax` read their play from a **weights file**, and a policy is only ever a
whole agent: `lookahead:policy:<file>` does not parse, and the two cannot be composed.

## Decision

A searching agent may name an **inner agent** after its kind, the way an exploring agent already does:
`lookahead:policy:models/clone/ci-138/policy.json`, `minimax:heuristic:learning/weights/stun-first.json`. A
bare path stays what it has always been, the shorthand for a weights file, so every spec written so far reads
the same.

What the inner agent replaces is **every seat the search has to guess**, and nothing else:

- the intent of an ally that has not declared yet, as the round is played out;
- the evolution and the speed, which are not combat moves and have no round to play out.

What it does **not** replace is the **evaluation**. The sum over the round stays `ActionScorer`'s, on the
weights the spec carries or the built-in ones. A clone's scores are logits of what its teacher would do and a
value policy's are predicted returns under its own baseline; neither is a quantity that can be summed over the
actions of a round and compared across moves. Search here amplifies the policy's *play*, and is judged by a
yardstick that does not move.

The stamp fingerprints the inner agent, as it already does for `explore`, so two runs on different policies at
one path stamp apart.

The operator is worth having, measured before this record was written. The `ci-69` clone against Greedy on the
200 benchmark seeds, both sides, 400 matches:

| agent A | score | 95 % interval | rounds | at the round cap | draws |
| --- | --- | --- | --- | --- | --- |
| `policy:models/clone/ci-69/policy.json` | 0.7250 | 0.6775 to 0.7725 | 11.52 | 20.0 % | 12 |
| `lookahead:policy:models/clone/ci-69/policy.json` | 0.8350 | 0.7969 to 0.8731 | 7.61 | 4.5 % | 0 |

The intervals do not overlap, and the searched version closes in two thirds of the rounds without ever leaving
a match to the cap. Search over that policy is a real improvement to it, which is the whole premise: it is the
play a clone of it would be learning.

## Consequences

- Good: the loop gets a teacher it can improve. `lookahead:policy:<clone>` plays the round out where the clone
  plays one step, so cloning its play is the first turn in this repository whose teacher is not a fixed
  agent but the loop's own last output, made better. If that clone beats the clone it was searched with, the
  loop climbs for the first time; if it does not, the operator is too weak and the record says so.
- Good: it costs no new concept. An agent that wraps an agent is what `explore:<rate>:<agent>` already is, and
  `IPlayerAgent` is the seam; `LookaheadAgent` stops building its own inner agent and is handed one.
- Bad: a searched turn is far more expensive to record than a one-step one — the round is played out for every
  castable spell, at every slot, and a policy is a dot product per candidate rather than nine weights. The
  dataset sizes of `docs/learning/training.md` are for one-step play, and the first searched turn measures
  what it really costs before anyone plans a five-seed one.
- Bad: the improvement is bounded by the evaluation. With the scorer fixed, search finds the move that the
  *weights* like best among rounds the *policy* plays, so a ceiling remains and it is the weights'. Lifting it
  needs a learned evaluation of a position, which this repository does not have and which the value arm's
  noise says is not close.
- Neutral: nothing about existing specs, stamps or the benchmark digest changes. `lookahead` and
  `lookahead:<weights>` build exactly what they built before.

## Alternatives considered

- **Let the policy be the evaluation too, summing its scores over the round.** That is the version that would
  make search amplify *learning* rather than the hand-written weights, and it is what an eventual value
  network would do. It is refused here because the policies this loop produces do not carry a value: a
  clone's score is a logit, comparable within one decision and meaningless summed across a round. Doing it
  anyway would produce a number that looks like a value and is not, which is how the exploit term spent a
  week measuring an agent instead of a catalogue.
- **Make the enemy guess the policy as well.** The enemy slots are played by `ActionScorer.Best` directly
  rather than through an agent, because an enemy's board is not the actor's `PlayerBoardState`. Routing them
  through an agent is a larger change with its own design, and the deliberate limit of this record is that
  the inner agent plays the seats the search already asked an agent about.
- **Keep cloning and make the teacher better by hand.** That is the weight ladder, and it is what this
  repository has done for a month. It works, it is saturating, and it is not a loop: every rung is a person
  asking for a search.
- **Fix the value arm instead.** It is the other half of a real loop and it is not abandoned, but its readings
  span the whole range at three seeds; the first thing it needs is a signal that is not dominated by who moved
  first, which is a separate change (the mirror of `stun-first` is decided by move order 92.5 % of the time).

## Follow-up

- `AgentKind`, `AgentSpec`, `AgentFactory`, `LookaheadAgent`, their tests, `docs/learning/agents.md` and the
  usage line in `AGENTS.md`.
- The first turn: `scripts/iterate.sh --teacher lookahead:policy:<the ci-138 clone>`, read against `ci-138` on
  the same seeds, which asks the only question that matters here — does a clone of searched play beat the
  clone it searched with.
- What this record does not decide, and what the answer to that turn will inform: whether the loop should
  learn a position's value, which is what would let search amplify the learning rather than the weights.
