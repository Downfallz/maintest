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

**Search does not have one sign, *against Greedy*.** It gives a weak inner agent 0.1100 and takes 0.1700 from
the strongest agent this repository has. Both readings are against Greedy and only against Greedy, and the
negative one starts from 1.0000 — a saturated matchup where there is no upside left to measure, so part of
that 0.1700 is a ceiling rather than a property of search. What the two rows license is "search can help or
hurt against this opponent", not "search cannot improve an agent". The generalisation this file carried in an
earlier draft — that search is never an improver — is withdrawn: it would remove search as an operator
before the pool that Line A builds has ever measured it, and the same file argues two sections later that
strength here is matchup-dependent.

The mechanism remains a reasonable *hypothesis*, untested against anything but Greedy: search substitutes its
evaluation's judgement for the inner agent's in the positions it plays out, which helps only while the
evaluation is the better of the two. **Measuring it against the pool is a Line A deliverable**, not a
premise.

**Greedy is exhausted as a yardstick.** `stun-first` takes every match. A fixed opponent cannot drive a loop
past the point where you beat it always, and every gate in this project currently reads a score against
Greedy.

## What is refuted, so it is not tried again

- **Imitation as the climb.** Three turns: the better the clone copies, the closer to one half it lands
  against its own teacher (0.5513 to 0.6175 at 96 % copy, 0.4875 to 0.4925 at 98.5 %). A clone reaches its
  teacher; it does not pass it. Levers on the clone arm change how *fast* it arrives.
- **An ADR on the champion bar.** `ci-149` refuted the hypothesis behind it: the champion bar passed on all
  three seeds and the Greedy bar refused, for a real reason.
- **Copy accuracy as the arm's headline number.** It does not predict how well a policy guesses inside a
  search, which is the job that turned out to matter.

## What is open, and was called refuted in an earlier draft

**The carrier loop, `P(n+1) = clone(lookahead:policy:P(n))`.** An earlier version of this file put it under
"refuted" on one reading: searching over `P1` scored 0.6875 against Greedy where searching over `P0` scored
0.8350, so the second iteration looked lower than the first. That does not close the path, for two reasons
this repository measured itself:

- The `P1` in question was **not the loop's `P2` step**. It was a local clone trained on dataset seed 1 at
  2000 matches, used as a proxy because its bare scores fall inside `ci-149`'s three-seed bands. The actual
  next clone, over the seeds a turn runs, was never trained or played.
- The regression is **a property of that clone's dataset, not of the loop**. The same session fitted the same
  teacher on the exploring dataset instead, and the searched version read 0.8488 — indistinguishable from
  searching over `P0`. `--clone-on-explore` exists because of that measurement.

So the honest statement is that the carrier loop **has not been shown to climb**: the best reading of it is a
dead heat (0.5325 against searching over `P0`, interval across one half). Closing it needs the real next
clone, trained with `--clone-on-explore` over a turn's seeds, and its searched form compared to its
predecessor's by a paired reading. That is a loop turn plus one evaluation, and nothing above substitutes for
it.

## The three parts, and where each one stands

| | what it is | status |
| --- | --- | --- |
| **Operator** | something that makes the current agent better | search, measured against Greedy only: +0.1100 on a weak inner agent, −0.1700 on a saturated one. Whether it improves an agent against the pool is a Line A deliverable |
| **Distillation** | capturing that improvement back into the agent | cloning works as copying (99 %) and transfers a one-step policy, not a search |
| **Ratchet** | a gate that cannot accept a regression | `paired` exists now; the gates still read marginal numbers against a fixed Greedy |

The evaluation is the constraint this plan bets on: nine hand-written numbers that everything is scored by,
and worth 0.1300 on the one row where changing only the yardstick was measured. That bet is what Line A is
for, and Line A is also what would disprove it.

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
- **Cost, and it does not fit today.** Linear in pool size. At the measured **21.9 s per candidate** for a
  three-opponent panel, a ten-agent pool is roughly 73 s, and a 161-candidate search goes from 59 minutes to
  about **195 minutes** — before the build, the hold-out and the gate. `search.yml` sets
  `timeout-minutes: 180`. So A1 as written would be killed partway, and **raising that limit, or cutting the
  work, is part of A1 rather than a detail after it**. The six-hour figure an earlier draft used was
  `tune.yml`'s; the two workflows do not share a budget.

**Plan B1, when the pool outgrows the job — which is immediately.** Score against a sample rather than the
whole pool: the top *k* by current rating plus a random draw from the rest, so a candidate cannot win by
beating only the weak half and cannot avoid the champions. At *k* = 4 plus two drawn, a rung is back to about
two hours and fits the current limit without touching it. Given the arithmetic above, this is the likely
shape of A1 on day one rather than a contingency.

### A2 — the ratchet becomes `paired`, and the winner joins the pool

Today a search keeps "the best of what it drew", which is at least the starting score by construction. The
gate has to be: the winner beats the pool by a paired difference whose interval is clear of zero, on seeds the
search did not use. Then it is written to `learning/weights/`, and the next rung starts from it.

That last sentence is the whole loop. Everything before it exists.

- **Produces**: a run that ends by adding to the pool, so the next run has a harder panel and a better start.
- **Falsified by**: three consecutive rungs where nothing clears the paired gate. That is not a bug, it is
  the answer to the question — see A3.
- **Cost**: the hold-out already runs on any improving run (#137). The paired reading is seconds.

**Plan B2, when the pool cannot be ranked.** Non-transitivity is **suspected and not measured**, and the
distinction matters because an earlier draft of this file asserted it. What exists is `ci-69` at 0.5325
against `search-4` head to head — an interval containing one half, so neither a win nor a proven equivalence —
beside twenty points between them against Greedy. Two readings, one of them inconclusive, do not make a cycle.
A settled cycle needs three paired matchups that close, which the pool of A1 produces as a by-product and
nothing before it does.

If a cycle *is* found, a scalar rating is the wrong object: "beats the pool on the mean" can promote an agent
that loses to half of it. The fallback is the **top cycle** — the smallest non-empty set whose every member
beats every agent outside it — and the gate is "belongs to it". A set of agents "not beaten by any other
member" is the wrong object and was the wrong object in the earlier draft: under `A > B > C > A` it is
**empty**, "loses to nobody in it" is then vacuously true, and the ratchet accepts anything precisely when a
cycle is what it needed to handle. The top cycle is never empty, contains the whole cycle when there is one,
and reduces to the single champion when there is not.

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
