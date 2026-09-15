# 0047. A lookahead agent needs a hypothetical board

Date: 2026-09-15
Status: Proposed

## Context

Every agent the engine ships decides at **one ply**. `ActionScorer` prices an action with the domain's own
`ResolutionRules` on the current snapshots and stops there (`docs/learning/agents.md`); ADR 0044's context
names the consequence — it "prices a lasting effect where it lands, never sees whether tempo converts".
`search-4` beats `Greedy` 0.930 on this catalogue by moving eight weights, and the best learned agent here
is a clone at parity with it. Nothing in the repository looks further ahead than the move being made, so
"look further ahead" is the largest unexplored axis and the one most likely to produce an agent stronger
than `search-4` without any learning at all.

**The blocker is that a hypothetical board cannot be built.** A lookahead of any shape — two plies, or
rollouts to the end of the match — needs to ask "if I play this, what does the board look like?" and the
engine has no answer:

- `CombatExecution.Apply` takes `IReadOnlyList<Creature>`, the **entities**, and mutates them through
  `TakeDamage`, `SpendEnergy`, `Heal`: mutators that are `internal` on purpose, because "only `Match` and the
  rules it runs change a creature" (`AGENTS.md`, `Creature`). Application cannot call them.
- `Match` has `Create` and no reconstruction: a match starts at round one or not at all.
- `Creature.Spawn` creates a creature at full health. `Condition`'s constructor is `internal` and its
  `RemainingRounds` moves only through `Tick`.
- `CreatureSnapshot` is a complete immutable description of a creature, and `Match.Snapshots()` hands one
  out — but nothing goes the other way.

So the state a lookahead would explore is reachable only by replaying a match from round one, which is not a
lookahead.

## Decision

**Not yet taken.** This ADR exists to record that the estimate behind the work was wrong and to put the three
shapes side by side before one is built. The estimate given when this was chosen was "a few days"; the
investigation above says otherwise, and the cheapest of the three still changes the Domain.

| | What it adds | Rules duplicated? | Cost |
| --- | --- | --- | --- |
| **A. Snapshot applier** | `Apply(CombatResolution, snapshots) -> snapshots` in Domain | **Yes** — capping by health left, healing by health missing, condition stacking policy all live in the entity mutators and would be written a second time | Days, plus a permanent divergence risk |
| **B. Reconstructible creature** | `Creature.Restore(snapshot, definition)` and a `Condition` restore path, so a board can be cloned and the **existing** `CombatExecution.Apply` run on the clones | **No** — one applier, the real one | A week, and it opens a door |
| **C. Replay from the root** | Re-run the match from round one with forced decisions to reach the hypothetical state | No | Correct and far too slow to play 400 matches with |

**B is the recommended shape**, for the reason A fails: two appliers that must agree about capping and
stacking is exactly the kind of duplication that reads fine on the day and drifts in six months, and this
repository has already been bitten four times in two days by cross-references drifting (journal,
2026-09-15). One applier or none.

B's cost is not the code, it is the door: a public reconstruction path lets any caller fabricate a creature
at an arbitrary health, energy and condition state, which is precisely the invariant protection
`AGENTS.md` asks for ("state changes go through aggregate methods that protect invariants"). Keeping
`Restore` `internal` and putting the clone-and-advance behind a Domain-owned service keeps the door inside
the aggregate's own assembly, which is what makes B acceptable rather than a hole.

## Consequences

- Good: whichever shape lands, the engine gains the ability to answer "what would the board be", which is
  the prerequisite for a lookahead agent, for a rollout agent, and for any future search.
- Good: a lookahead agent is **not capped by a teacher**, unlike every clone, and needs no training run, no
  dataset and no feature schema. It is the only route on the table that could beat `search-4` this week
  without a learning result.
- Bad: **B touches the Domain's invariant protection**, which is the thing this repository is most careful
  about. It needs a `domain-reviewer` pass and the architecture tests read again, not just a green build.
- Bad: the cost of one decision rises by the branching factor times the cost of a resolution. A two-ply
  search over intents and targets is not obviously affordable at 400 mirrored matches an evaluation; that
  has to be measured on a small run before the agent is worth finishing.
- Neutral: none of this changes the benchmark digest. A new agent kind plays nothing by default, and
  `Greedy` against `Greedy` stays what the digest is defined by (ADR 0013, decision I).

## Alternatives considered

- **A one-and-a-half ply approximation**: apply only the numeric outcomes (damage, heal, energy) to the
  snapshots by arithmetic, skip conditions entirely, and score the opponent's best reply on that. No
  stacking policy to duplicate, so much cheaper than A — and wrong in exactly the cases ADR 0044 cares
  about, since a lasting effect is the thing whose conversion is not being seen. Rejected as measuring the
  wrong thing.
- **Give up on lookahead and spend the week on the value policy.** Defensible: ADR 0046's lambda moved the
  value policy from 1 win in 400 to 0.10125 and there is an untried optimum between 0.5 and 0.95. But that
  route is capped by nothing and blocked by a weak baseline (`baselineR2` 0.07), and it has had four runs
  without producing an agent worth committing.
- **Do it in the Cli as a one-off experiment** rather than a shipped agent, to measure whether the lookahead
  is worth anything before paying for B. This is the cheapest way to get evidence, and the honest first
  step if the answer to "is two plies worth a week" is not known. It still needs the hypothetical board.

## Follow-up

- Nothing is implemented. The decision to record next is which of A, B or C is built, or whether the week
  goes elsewhere.
- If B: `Creature.Restore`, a `Condition` restore path, a Domain-owned board-clone service, a new
  `AgentKind`, and a test that a cloned board advanced by `CombatExecution.Apply` matches the same board
  advanced inside a real `Match`.
- Measure before finishing: the cost of one decision at two plies, on a twenty-match run, against the
  single-ply agents.
