# Learning plan: a loop that climbs without being pushed

Status: **Draft** (2026-09-18). Nothing here is decided. It is one reading of what the measurements allow,
written so the next person — or the next session — starts from what is settled rather than from what sounded
right. Each hard-to-reverse choice it raises becomes an ADR before code depends on it.

## The objective, stated so it can fail

A loop that (1) runs without a person deciding each turn, (2) produces an agent measurably stronger than the
one it started from, and (3) cannot accept a regression. Three parts, and the third is the one that makes the
first two worth anything: a loop without a ratchet is a random walk that reports its best step.

"Measurably" has a specific meaning here, and it is new: the paired difference over the benchmark seeds, from
`paired` (`docs/learning/training.md`). Not a marginal interval. That distinction cost this project a night
and is the reason the rest of this plan is phrased the way it is.

## What is settled

Every number below is the paired per-seed difference over the 200 benchmark seeds, both sides, with the 95 %
interval clear of zero. Content `7e199df4` (journal, 2026-09-18).

| | difference | interval |
| --- | --- | --- |
| A search's **inner agent**: `policy:ci-69` over the built-in heuristic | **+0.3588** | +0.2920 to +0.4255 |
| Search over `heuristic:stun-first` | **−0.1700** | −0.2052 to −0.1348 |
| The **evaluation**: `stun-first`'s own weights over the built-in ones, guesser fixed | **+0.1300** | +0.0892 to +0.1708 |
| Search over `policy:ci-69` | **+0.1100** | +0.0537 to +0.1663 |
| Inner agent: `heuristic:search-4` over `policy:ci-69` | **+0.0775** | +0.0346 to +0.1204 |

Three readings did **not** settle, and are not evidence of sameness either: search over `search-4`
(−0.0175), the built-in guesser against a random one (−0.0125), and `search-4`'s own evaluation against the
built-in one (+0.0075).

Two things follow that shape everything below.

**Search is not an improver on its own.** It gives a weak inner agent 0.1100 and takes 0.1700 from the
strongest agent this repository has, which scores 1.0000 against Greedy unsearched. Whatever it is doing, it
is not "make the agent better"; it substitutes its evaluation's judgement for the agent's in the positions it
plays out, and that is an improvement only while the evaluation is the better of the two.

**Greedy is exhausted as a yardstick.** `stun-first` takes every match. A fixed opponent cannot drive a loop
past the point where you beat it always, and every gate in this project currently reads a score against
Greedy.

## What is refuted, so it is not tried again

- **The carrier loop, `P(n+1) = clone(lookahead:policy:P(n))`.** The order inverts under search: `P1` beats
  `P0` bare, and searching over `P1` reads 0.6875 against Greedy where searching over `P0` reads 0.8350.
  The second iteration is below the first. Do not build this expecting it to climb.
- **Imitation as the climb.** Three turns: the better the clone copies, the closer to one half it lands
  against its own teacher (0.5513 to 0.6175 at 96 % copy, 0.4875 to 0.4925 at 98.5 %). A clone reaches its
  teacher; it does not pass it. Levers on the clone arm change how *fast* it arrives.
- **An ADR on the champion bar.** `ci-149` refuted the hypothesis behind it: the champion bar passed on all
  three seeds and the Greedy bar refused, for a real reason.
- **Copy accuracy as the arm's headline number.** It does not predict how well a policy guesses inside a
  search, which is the job that turned out to matter.

## The three parts, and where each one stands

| | what it is | status |
| --- | --- | --- |
| **Operator** | something that makes the current agent better | search, but only while the agent is weaker than the evaluation — and `stun-first` is already above it |
| **Distillation** | capturing that improvement back into the agent | cloning works as copying (99 %) and transfers a one-step policy, not a search |
| **Ratchet** | a gate that cannot accept a regression | `paired` exists now; the gates still read marginal numbers against a fixed Greedy |

The binding constraint, measured three ways tonight, is the **evaluation**: nine hand-written numbers that
everything is scored by. That is where the plan goes.

---

## Line A: close the weight ladder into a league

**The claim.** The loop that works already exists and nobody called it one. `greedy → search-2 → search-3 →
search-4 → mixture-mean → pressure-floor → stun-first` is a chain of rungs, each searched from the one before
it, ending at an agent that takes every match from Greedy. `learning/weights/` holds ten sets in all — the
rest are branches that did not become rungs, which is what a pool is made of. The chain is missing exactly
two things: a person triggers each rung, and the opponent panel is written by hand.

**Why this line first.** It is the only arm that has demonstrably climbed, every component exists
(`search-weights`, the workflow, the proposal branch, the pool directory, and now `paired`), and it produces
the thing Line B needs in order to be judged at all.

### A1 — the panel becomes the pool

`search.json` names three opponents by hand. A candidate should be scored against `learning/weights/*.json`:
the ten, then eleven, then twelve. This is what replaces the exhausted Greedy yardstick with a moving one.

- **Produces**: a rung's score is a mean over the pool rather than over three hand-picked agents.
- **Falsified by**: a pool score that ranks candidates the same way the three-opponent panel did, over two
  rungs. Then the panel was not the constraint and A1 bought nothing but runtime.
- **Cost**: linear in pool size. At the measured **21.9 s per candidate** for a three-opponent panel, a
  ten-agent pool is roughly 73 s, and a 161-candidate search goes from 59 minutes to about 3¼ hours —
  inside a 6-hour job, but not by much.

**Plan B1, when the pool outgrows the job.** Score against a sample rather than the whole pool: the top *k*
by current rating plus a random draw from the rest, so a candidate cannot win by beating only the weak half
and cannot avoid the champions. Trigger: the first rung that exceeds four hours.

### A2 — the ratchet becomes `paired`, and the winner joins the pool

Today a search keeps "the best of what it drew", which is at least the starting score by construction. The
gate has to be: the winner beats the pool by a paired difference whose interval is clear of zero, on seeds the
search did not use. Then it is written to `learning/weights/`, and the next rung starts from it.

That last sentence is the whole loop. Everything before it exists.

- **Produces**: a run that ends by adding to the pool, so the next run has a harder panel and a better start.
- **Falsified by**: three consecutive rungs where nothing clears the paired gate. That is not a bug, it is
  the answer to the question — see A3.
- **Cost**: the hold-out already runs on any improving run (#137). The paired reading is seconds.

**Plan B2, when the pool cannot be ranked.** Non-transitivity is measured here, not hypothetical: `ci-69`
beats `search-4` head to head and loses twenty points to it against Greedy. If cycles appear, a single scalar
rating is the wrong object and "beats the pool on the mean" can promote an agent that loses to half of it.
Fall back to keeping a **covering set** — every agent that is not beaten by some other member — and gate on
"loses to nobody in the set". Stricter, smaller, and it never claims an order that does not exist.

### A3 — a stop condition

A loop that cannot stop burns runner hours restating a fixed point, which is what three turns of the clone
arm did. If N consecutive rungs fail the paired gate, the loop stops and says so. That is a result: the
nine-number form is exhausted, and the ceiling is functional rather than parametric.

- **Produces**: the trigger for Line B, with evidence.

---

## Line B: learn the evaluation

**The claim.** A league over nine numbers has its own ceiling: `ActionScorer`'s functional form. The only
lever that lifts it is an evaluation that is learned rather than written, which is what "a value of a
position" means. This is also the only remaining purpose of the policy arm.

**Why not first.** It needs the league to judge it — a learned evaluation is only interesting if it beats the
best hand-written one, and "best" needs a pool and a paired gate. And the value arm has never produced
anything but noise: `termsR2` 0.0004 on the searched teacher, a jackknife interval of 0.0000 to 0.3760.

### B1 — fit the value on what the search computed, not on who won

Every fit so far has been trained on the **episode return**: who won the match, a signal buried under
everything that happened afterwards. A search computes something far better and throws it away — what the
round was worth after playing it out, for every candidate it considered. That is a dense, local target
produced by the one component that works.

- **Produces**: a value fitted on search targets, playable as `lookahead:<value>` once the evaluation can be
  named (see "what is missing" below).
- **Falsified by**: an r-squared on held-out rounds no better than the episode-return fit. Then the target
  was not the problem and the encoding is.

**Plan B-alt, if regression stays noisy.** A search does not need a *value*, it needs an *order*: which of
these rounds is better. Fit a pairwise ranker over rounds instead of a regressor over returns. It is a weaker
object, cheaper to learn, and sufficient for what `ActionScorer.Best` is asked to do.

### B2 — the targeted coverage build, if a clone is still in the picture

Recording the boards a search hands its inner agent and fitting on those (journal, 2026-09-18) is **not**
refuted — an earlier draft claimed it was, on a ceiling that did not exist. Its headroom is real: the best
inner agent measured reaches 0.9125 where `lookahead:policy:ci-69` reads 0.8350.

What it still needs before it is worth building: a labeller established to be a *better guesser* than what it
would teach, on the query boards, which no match score can establish. `ActionScorer.Best` was proposed and
never tested — plain `lookahead` reaches those seats through a `HeuristicAgent` that applies
`Foresight.AlreadyDoomed`, so it is not the same function.

---

## What is missing in the engine either way

There is no spec for the cell that should be best: a **policy as the guesser with a strong weight set as the
evaluation**. `lookahead:policy:<file>` gives the built-in evaluation and `lookahead:<weights>` gives one
agent both jobs, so "the clone guesses, `stun-first` judges" cannot be written. The guesser is worth 0.3588
and the evaluation 0.1300, and **whether they compose is unmeasured because the engine cannot be asked**. It
is a parse change plus an ADR amending ADR 0055's "the evaluation is not replaced", and it is the cheapest
open question with real upside.

## The discipline this plan depends on

Tonight's failure was not a wrong measurement, it was four wrong readings of right measurements in a row: a
ceiling contradicted by the table printing it, an equivalence from overlapping intervals, a difference from
disjoint ones, and a tooling gap asserted while reading the files that held the data. The plan above is only
as good as the following holding:

- Every comparison goes through `paired`. A marginal interval is not a test of a difference in either
  direction, and the repository has said so in `search_weights.py` for longer than this plan has existed.
- One seed is not a measurement (ADR 0049), and a turn that is reproducible to sixteen decimal places is
  still one sample of the seed — `ci-150` reproduced `ci-149` exactly while its rows spanned 0.19.
- Before claiming a measurement cannot be made here, open the artifact. This project writes far more than its
  summaries print.

## ADRs owed before code

1. The league: the pool as the panel, the paired gate, and what happens on a cycle (A1, A2).
2. The stop condition and what it licenses (A3).
3. A searching agent naming its evaluation as well as its inner agent, amending ADR 0055.
4. The value target, if Line B starts: search rounds rather than episode returns (B1).
