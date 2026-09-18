# Learning plan: a loop that climbs without being pushed

Status: **Draft** (2026-09-18). Nothing here is decided. It is one reading of what the measurements allow,
written so the next person — or the next session — starts from what is settled rather than from what sounded
right. Each hard-to-reverse choice it raises becomes an ADR before code depends on it.

## The objective, stated so it can fail

A loop that (1) runs without a person deciding each turn, (2) produces an agent measurably stronger than the
one it started from, and (3) cannot accept a regression it can measure. Three parts, and the third is the one
that makes the first two worth anything: a loop without a ratchet is a random walk that reports its best
step.

"A regression it can measure" is the honest form and the qualifier is load-bearing. No gate built on finite
seeds refuses an inferiority smaller than what those seeds resolve; A2's admits at most δ of one it cannot
see, and that tolerance is a number someone has to choose rather than a gap to be apologised for.

"Measurably" has a specific meaning here, and it is new: the paired difference over the benchmark seeds, from
`paired` (`docs/learning/training.md`). Not a marginal interval. That distinction cost this project a night
and is the reason the rest of this plan is phrased the way it is.

## The short version

**Two rungs of the loop have been climbed by hand.** `pressure-floor` was searched from `mixture-mean`, and
`stun-first` from `pressure-floor` *with `pressure-floor` in its panel*. It is **not** A1 and A2 already
performed, and two drafts of this sentence said otherwise. The panel was four opponents named by hand out of
a pool of eight, so A1's pool-derived sampling never happened; and putting the predecessor in the panel did
not make the candidate beat it, because `Score.floor` is the **low end** of the starting matchup's interval —
a candidate could sit below `pressure-floor`'s mean, stay above that floor, and win on the panel mean anyway.
So A2's ratchet idea was not exercised either. What establishes the link is the independent replay, not the
panel: on 200 seeds nothing in this project had played, `stun-first` beats `pressure-floor` **0.8037,
interval 0.762 to 0.846** (journal, 2026-09-16). That is already a paired reading — a head-to-head's interval
is built over the per-seed means of the two mirrored matches — so it is the instrument this plan insists on,
not a marginal score that happens to look large.

An earlier version of this section claimed a seven-rung chain, `greedy → search-2 → … → stun-first`. The
history does not support it: `search-2`, `search-3` and `search-4` are **independent** searches, each started
from the default weights against Greedy, on three different content hashes (`d4a21a55`, `938bef5e`,
`7e199df4`). They are three attempts at the same rung, not three steps of a climb, and `mixture-mean`'s own
starting point is unverified. So the evidence that the recurrence climbs is **two links long**, and what
follows builds on that rather than on six.

What those two links needed a person for is what remains: someone triggers each rung, and the opponent panel
is written by hand.

Three steps close it, in this order. Every runtime below is measured or extrapolated from a measured pace;
the implementation effort is **not** estimated, because nothing here has been built yet.

1. **A1 — the panel becomes the pool.** A rung scores its candidates against the top 4 of `learning/weights/`
   by rating plus 2 drawn at random, instead of three opponents named by hand — or, if the round robin below
   found a cycle, against the source components of the condensation graph taken whole plus a draw, since a
   cyclic pool has no top 4 and picking one by rating is the very thing a cycle forbids. Sampled because the
   whole pool would take ~195 min against `search.yml`'s 180-minute limit; six opponents land near two hours.
   But early on that source set is most of the pool, so the full-pool cost is the *expected* early case and
   **raising the timeout is part of this step**, not a contingency after it. This replaces Greedy, which
   `stun-first` beats in every match and which therefore cannot rank anything above itself any more.
2. **A2 — the ratchet.** The sample only *ranks*; it never admits. The search's top *m* finalists (m ≈ 5) are
   played against every pool member — ~73 s each — and one is admitted only if, against **every** incumbent,
   its paired difference shows no settled loss *and* has a lower bound above −δ, plus at least one settled
   win across the pool. Both halves are needed: the first refuses a matchup that says something bad, the
   second one that says nothing. Testing five finalists at once inflates that 95 %, so the bounds are either
   corrected for the family or one finalist is chosen first and gated alone — an open trade, not a detail.
   The gate's seeds must also be fresh **per rung**, not merely unused by the current search: the loop's own
   output feeds the next rung, so a fixed hold-out window is fitted a little more with every turn. Fresh
   windows are not enough either — a 5 % gate run ten times is a ~40 % chance of one false admission, and the
   loop climbs on from it — so a **sealed block gates every N rungs and rolls back** what it refuses, or the
   loop spends an alpha budget across rungs instead of 5 % each. An admitted finalist is written to
   `learning/weights/` and the next rung starts from it. **That sentence is the loop.** Everything it needs
   already exists, except the gate, which this plan has got wrong four times and hands to an ADR.
3. **A3 — a stop.** N rungs with nothing admitted ends the loop, so it stops burning runner hours restating a
   fixed point. It records every finalist that failed, against whom and by how much. It does **not** say why
   the loop stopped: the four possible causes are not distinguishable from what it observes, so it is a halt,
   not a verdict.

One thing precedes all three: a **round robin over the initial pool** (45 pairs, ~5½ min, once). Without it a
cycle among the ten agents already in `learning/weights/` can never become visible, because admissions only
ever add edges touching the newcomer.

**Line B — learning the evaluation instead of writing it — waits for the league, not for A3.** It needs A1
and A2 to exist, because a learned evaluation is only interesting if it clears A2's gate against the whole
hand-written pool — not if it beats one champion, which a cyclic pool may not even have. It is explicitly
**not** gated on A3 declaring the nine-number form exhausted,
because A3 cannot declare that; an earlier draft made it wait for a signal that never arrives.

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
negative one starts from 1.0000 — a saturated matchup with no upside left to measure. That ceiling makes the
row a one-sided test: it can show a loss and could not have shown a gain, so it is unfit to answer whether
search improves an agent in general. It does **not** account for any part of the 0.1700, which is entirely a
drop produced by applying search; an earlier version of this paragraph said part of it was the ceiling, which
is a causal split nothing measured. What the two rows license is "search can help or hurt against this
opponent", not "search cannot improve an agent". The generalisation this file carried in an
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
- The regression **may be a property of that clone's dataset rather than of the loop**, and that is a
  hypothesis, not a finding. The same session fitted the same teacher on the exploring dataset instead, and
  the searched version read 0.8488 — indistinguishable from searching over `P0`. But that fit moved two
  things at once: the coverage of the dataset *and* the labels, since a fifth of the exploring agent's
  decisions are deliberately random, so it teaches different actions and not only a wider board. It is also
  one seed-1 realisation, which is the reading this repository refuses to call a result anywhere else
  (`AGENTS.md`, ADR 0049). `--clone-on-explore` exists because of it; the cause of the regression does not
  follow from it.

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
| **Ratchet** | a gate that cannot accept a measured regression | `paired` exists now; the gates still read marginal numbers against a fixed Greedy |

The evaluation is the constraint this plan bets on: nine hand-written numbers that everything is scored by,
and worth 0.1300 on the one row where changing only the yardstick was measured. That bet is what Line A is
for, and Line A is also what would disprove it.

---

## Line A: close the weight ladder into a league

**The claim.** The recurrence has been run by hand and it climbed — twice. `pressure-floor` was searched from
`mixture-mean`; `stun-first` was searched from `pressure-floor`, and on 200 seeds nothing had played it beat
`pressure-floor` 0.8037, interval 0.762 to 0.846. That replay is the evidence, and it is the whole of it.

**Neither run performed A1 or A2.** The panel was four opponents named by a person out of a pool of eight,
which is not A1's sampling. And `pressure-floor` sitting in that panel did not force the candidate to beat
it: the floor a candidate must hold is `Score.floor`, the **low end** of the starting matchup's interval, so
a set slightly worse than `pressure-floor` clears the floor and can still win on the panel mean. The one idea
A2 turns into a rule — the winner must clear the incumbent — is therefore *not* what the ladder ran on. Line
A is not automating something already proven; it is building machinery that two links suggest is worth
building, and the second of those links held in spite of the search's rule rather than because of it.

**What it is not.** An earlier draft called this a seven-rung chain running back to `greedy`. It is not:
`search-2`, `search-3` and `search-4` were each searched from the default weights against Greedy, on three
different content hashes, so they are three tries at one rung rather than three steps; and `mixture-mean`'s
starting point has not been checked. Two links is the evidence, and two links is thin — this line is first
because it is the only arm with *any* measured climb, not because the climb is long. `learning/weights/`
holds ten sets, and the ones that are not rungs are what a pool is made of.

**Why this line first.** Every component exists (`search-weights`, the workflow, the proposal branch, the
pool directory, and now `paired`), and it produces the thing Line B needs in order to be judged at all.

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

**Plan B1, when the pool outgrows the job — which is immediately.** Search against a sample rather than the
whole pool: the top *k* by current rating plus a random draw from the rest, so a candidate cannot win by
beating only the weak half and cannot avoid the champions. At *k* = 4 plus two drawn, a rung is back to about
two hours and fits the current limit without touching it. Given the arithmetic above, this is the likely
shape of A1 on day one rather than a contingency.

"The top *k* by rating" presumes the pool can be ranked, which the round robin that precedes A1 may refuse:
under a cycle the panel is drawn by the rule under Plan B2 instead, and the ordering matters — the round
robin runs first precisely so this is known before a panel is chosen rather than after.

**The sample ranks candidates; it never admits one.** A sample of six from a pool of ten leaves four agents
unplayed, so a winner could regress badly against one of them and still be let in — which is the failure A2
exists to prevent, reintroduced by the thing that made A1 affordable. It would also hollow out B2 below,
which counts on A1 producing every matchup as a by-product; a sampled rung produces a sparse table instead.
So the split is explicit: **the sample is the search's scoring function, and the admission gate of A2 runs
the finalists against every pool member.** At the same 7.3 s per opponent, one candidate against a ten-agent
pool costs about 73 seconds, which is nothing next to the rung that produced it. Sampling buys the search's
cost down and buys nothing off the ratchet.

**Finalists, plural, and that is not a detail.** A sample of six ranks candidates by a different objective
than the full pool does, so the candidate it puts first need not be the one the full pool would. Gating only
that one can reject it while an admissible runner-up is never played — and three rungs of that would end in a
halt that says only "the *sampled* winner failed", which is one of the readings A3 below refuses to dress up
as a verdict. So the gate takes the top *m* of the search (m ≈ 5, still under 7 minutes).

**And testing five costs something two drafts of this paragraph took for free.** Five finalists judged
against the same admission data at 95 % each is five chances for sampling error to lift one over the bar; the
probability that *something* passes is not the 5 % the interval names. A gate that admits "the best of those
that clear it" is selecting on exactly that error.

**The family is larger than the finalists, which the first correction missed.** Two of the three conditions
are universal — no settled loss, lower bound above −δ, *against every incumbent* — and a universal claim over
more comparisons gets harder, not easier, so multiplicity does not flatter it. The third does the opposite:
"at least one settled win" is **existential**, and an existential claim is searched for. With five finalists
and ten incumbents it is chosen from as many as fifty matchups, and even a single finalist searches ten. A
candidate with no real win anywhere has many chances to be handed a spurious one, and `z = 2.576` — which
corrects for choosing among five finalists — does nothing about that at all.

So the win condition needs its own correction across finalist–incumbent pairs, or the matchup that must
supply the win has to be **named before the data is played**. Predeclaring is cheaper and it is not arbitrary:
the obvious choice is the incumbent the rung started from, which is the agent a new rung is supposed to
surpass. At fifty comparisons a Bonferroni reading would move the z to about 3.29 and the half-width past
0.09, which on 200 seeds is wider than most differences this project has ever settled — that is the honest
size of the problem, and it argues for predeclaring rather than correcting.

Two ways out for the finalists themselves, and the plan does not choose: correct for the family — at m = 5,
reading each bound at 1 − 0.05/5 moves the z from 1.96 to 2.576 and the half-width from 0.0563 to about
0.0740 — or pick one finalist on the sample and put only that one through a gate on data nothing else
touched. The first keeps round four's benefit and pays in strictness; the second is cleaner statistically and
reopens the miss the finalists were added to close. **This is a real trade and it belongs in the ADR**, not
in a sentence that picks the convenient half.

The sampled objective also steers the search's own iterations, which *m* finalists soften and do not cure.

### A2 — the ratchet becomes `paired`, and the winner joins the pool

Today a search keeps "the best of what it drew", which is at least the starting score by construction. The
gate has to be a paired reading against the pool, on seeds the search did not use; a finalist that clears it
is written to `learning/weights/`, and the next rung starts from it.

"Clears it" is the hard part and it is not "an interval clear of zero against the pool mean" — a mean hides
the incumbent the candidate regressed against, which is the failure this gate exists to catch. The rule is
per incumbent, and what it has to be is worked out under Plan B2 below, because getting it wrong is the same
mistake in both places.

**"Seeds the search did not use" is not the same as unseen, once the loop turns.** `search.yml` takes its
hold-out as the window starting at `max(seeds) + 1`, which is deterministic: every rung replays on the *same*
seeds. That is fine once and corrosive in a loop, because rung *n + 1* starts from the weights rung *n*'s
window admitted, so by rung three those seeds have shaped the lineage as surely as the search seeds did.
Nothing catches it, because each rung is individually honest — this is ADR 0049's rule ("never pick the seed
that scored best") applied across time rather than within a run, and the repository has no guard for that
version of it. The loop would report a rising score on a test set it had been quietly fitting.

So the gate needs seeds that are fresh **per rung**, not merely fresh per search: a window advanced by the
rung index, recorded with the admission so a window is never reused. The seed space is large enough that this
does not run out.

**Fresh windows fix reuse and not repetition, which is the larger problem.** A gate that is wrong 5 % of the
time is wrong 5 % of the time *per rung*, and a loop is many rungs: at ten, the chance of at least one false
admission is 1 − 0.95¹⁰ ≈ **40 %**. Every window being new does nothing about that — the errors are
independent, which is exactly why they accumulate. And a false admission is not a bad rung that passes and is
forgotten; it becomes the starting point of the next one, so the loop keeps climbing from it.

That also disposes of the audit as this paragraph first proposed it. A sealed block **played only to measure
drift** reports the damage after the candidate and its descendants are in the lineage, which is a
post-mortem, not a ratchet. So the sealed block has to **gate**: every N rungs the current agent is played on
it, and an agent that fails is removed along with the admissions that followed it, the loop restarting from
the last one the sealed block passed. Rolling back N rungs is expensive and that is the point — it bounds how
far a false admission can carry.

The alternative, and they are not exclusive, is to stop spending a fresh 5 % per rung: an **alpha budget
across the loop**, or a confidence sequence that stays valid however many times it is read. A loop meant to
run indefinitely cannot use a fixed per-test rate and call the result a ratchet. Which of the two, and what N
is, belongs to the same ADR as the gate.

"Written to `learning/weights/`, and the next rung starts from it" is the whole loop. Everything it needs
exists.

- **Produces**: a run that ends by adding to the pool, so the next run has a harder panel and a better start.
- **Falsified by**: nothing this counter can establish on its own. Three consecutive rungs admitting no
  finalist says the loop is not climbing *here*, which is worth stopping for; it does not say the form is
  spent, the candidates were weak, or the seeds cannot separate them, because those look the same from
  inside (A3).
- **Cost**: the hold-out already runs on any improving run (#137). The readings are minutes: *m* finalists
  against ten incumbents, plus the one-time round robin that starts the table.

**Plan B2, when the pool cannot be ranked.** Non-transitivity is **suspected and not measured**, and the
distinction matters because an earlier draft of this file asserted it. What exists is `ci-69` at 0.5325
against `search-4` head to head — an interval containing one half, so neither a win nor a proven equivalence —
beside twenty points between them against Greedy. Two readings, one of them inconclusive, do not make a cycle.
A settled cycle needs three paired matchups that close, and the table that would show one does not exist yet.

**The admission gate alone never builds it.** Each admission adds only the edges touching the newcomer; the
ten agents already in the starting pool have never been played against each other, only scattered readings
against Greedy and each other's neighbours. A cycle sitting entirely among the incumbents would stay hidden
for as long as the loop runs, because nothing ever looks there. So the table starts with an explicit
**round robin over the initial pool**, once: 45 pairs at roughly 7.3 s each is about 5½ minutes, a one-time
cost against a rung of two hours. After that the admission gate keeps it complete, one newcomer at a time.
The *search* sample never contributes: it plays six of ten, and a cycle hides in the cells it skips.

If a cycle *is* found, two things need an answer, and earlier drafts answered only one of them.

**The panel.** A1 picks its six opponents as "the top four by rating plus two drawn", and under a cycle there
is no top four: a scalar rating over a cyclic table is exactly the object this section rejects, so the round
robin would detect a cycle and then hand it straight to a rule that cannot represent one.

"The cycle's members" is not a set, which a draft of this paragraph assumed it was: a table can hold several
cycles, overlapping, and which one a detector meets first is an accident of traversal order. Two runs would
then draw different panels of different sizes from the same table. What is canonical is the **strongly
connected components** of the settled sub-relation — the partition of the agents into groups where everyone
reaches everyone, computed the same way whatever order the edges arrive in.

A draft then said to take "the component containing the best-rated agent", which walks straight back into the
object this section rejects — and is worse than circular: if the best-rated agent sits *outside* the cycle,
its component is a singleton, the real cyclic component is left to the random draw, and members get dropped
one sentence after saying that dropping them picks a winner by omission.

The selection has to be graph-based. Contract each component to a node and the settled relation becomes a
**condensation**, which is acyclic by construction; its **source** components — those no other component
beats into — are exactly the agents nothing outside them defeats, and they are canonical without any rating.
So: **the panel is the union of the condensation's source components, taken whole, plus a draw from the rest
up to the budget.** Whole, because inside a component nothing orders anybody. The union rather than one of
them, because an incomplete relation can leave several sources and choosing between them needs precisely the
rating that does not exist.

**And that union is normally most of the pool, which a draft of this paragraph waved away.** A singleton with
no incoming settled edge is a source component, so every agent nobody has yet settled a win against is in it.
Early in the loop — where the settled relation is sparse even after the round robin, because a round robin
plays every pair but settles only the separable ones — that is most of the ten. So the cyclic branch is not a
rare fallback: it is the ordinary state of the first several rungs, and "the rung costs more or the budget
moves" is not an executable step, it is the 195-minute full-pool search that does not fit in 180 minutes
arriving by another door.

Two consequences the plan takes rather than dodges. **Raising `search.yml`'s timeout is part of A1, not a
contingency** — the step must budget for the full pool because that is the expected early case, not the worst
one. And a source set covering most of the pool is itself a **reading**: it says the agents are not yet
separable at 200 seeds, and the answer to that is more seeds per matchup rather than more opponents per
candidate. A rung facing a near-total source set should deepen before it widens.

**The gate.** "Beats the pool on the mean" can promote an agent that loses to half of it. An earlier draft
answered with a set of agents "not beaten by any other member", which is worse than imprecise: under
`A > B > C > A` that set is **empty**, "loses to nobody in it" is vacuously true, and the ratchet accepts
anything precisely when a cycle is what it needed to handle.

The **top cycle** — the smallest non-empty set whose every member beats every agent outside it — is the right
object *on a tournament*, where every pair has a winner. This pool is not one. `paired` returns settled or
not settled, and not settled is an **absent edge, not a draw**: the relation is incomplete by construction,
and this plan says so two paragraphs above. On an incomplete relation the top cycle degenerates the same way
the covering set did. Take a pool of three where `A` beats `B` and a newcomer `X` settles against neither:
no proper subset dominates, because no subset excluding `X` beats `X`. The top cycle is the whole pool, `X`
belongs to it, and `X` has beaten nobody. So **membership is not a gate**, and the claim that the top cycle
reduces to a single champion when there is no cycle is false here — it reduces to "everyone whose matchups
did not settle, plus the champion".

What the gate needs instead is an explicit rule for absent edges, and the safe direction is that a missing
edge counts *against* the newcomer, because the ratchet's job is to refuse without evidence rather than to
admit without it. **This has now been got wrong four times, each time by a rule that looked like it did that
and did not**, so the failures are worth stating before the rule:

1. "Loses to nobody in the set" — **empty** under a cycle, so it passes vacuously.
2. "Belongs to the top cycle" — on an incomplete relation the top cycle is everyone whose matchups did not
   settle, so a newcomer that settled against nothing belongs to it.
3. "One settled win, and no settled loss" — a candidate that beats the weakest member and is unsettled
   against all nine others, champions included, has no settled loss, so it passes. **Failing to establish a
   loss is not evidence of not having regressed**, which is the whole thing the rule claimed to encode.
4. "One settled win, and a lower bound above −δ" — at δ = 0.05 a matchup reading `[−0.04, −0.01]` clears the
   margin, so a **measured** loss is admitted by a ratchet whose whole point is refusing one.

The first three share one mistake: they treat an absent edge as neutral while announcing that it is not. The
fourth makes the opposite one — a rule aimed at absent edges that stopped checking the present ones. Making
an absent edge count requires a criterion it can actually **fail**, which the settled/not-settled reading
alone does not provide. The one that does is a **non-inferiority margin**, and it takes three conditions
rather than the two the fourth draft of this paragraph had. Against every incumbent:

- the paired difference's **upper** bound must not sit below zero — a settled loss is refused outright,
  however small;
- the paired difference's **lower** bound must sit above −δ;

and across the pool, at least one settled win, so a candidate cannot enter having beaten nothing.

The first condition is not redundant, and leaving it out was the fourth vacuity. With the margin alone, a
matchup reading `[−0.04, −0.01]` at δ = 0.05 has a lower bound above −δ and passes — a **measured** regression
admitted by the ratchet whose stated invariant is that it cannot accept one. The two conditions catch
different things: the margin refuses a matchup that says nothing, and the upper bound refuses one that says
something bad.

What the pair still tolerates is an *unmeasured* inferiority of up to δ — an interval like `[−0.045, +0.002]`
passes. That is what a non-inferiority margin means and the objective at the top of this file has to say so:
the ratchet cannot accept a regression it can see, and accepts at most δ of one it cannot. Calling it
"cannot accept a regression" without that clause is a promise the arithmetic does not keep.

The arithmetic decides δ, and it is tight. The settled readings on 200 seeds have standard errors near
0.0287, so a 95 % half-width near 0.0563. A matchup that carries no information is an interval centred on
zero with a lower bound near −0.0563, so **δ must be below that or an uninformative matchup passes** — at
δ = 0.07 the counterexample above is admitted again, one margin later. At δ = 0.05, admission needs a point
estimate above 0.0063 against *every* incumbent: at or above parity, in practice. That is severe, and the
severity is a property of the seed count rather than of the rule — the half-width falls as 1/√n, so a
gentler δ is bought with more seeds and in no other way.

This is the fifth attempt at one paragraph, so it is a proposal and not a decision: it goes to an ADR,
decided on the matchup table A1 produces rather than in advance of it, and the four failures above are the
test cases that ADR has to survive. Four wrong rules in a row, each of which read as correct when written,
is the strongest argument in this file for not letting the loop run on a gate nobody has tried to break.

### A3 — a stop condition

A loop that cannot stop burns runner hours restating a fixed point, which is what three turns of the clone
arm did. If N consecutive rungs fail the paired gate, the loop stops and says so.

**What it cannot say is why, and a previous draft claimed it could.** "N rungs failed the gate" has at least
four causes: the nine-weight form is exhausted; the candidates were ordinary losers; nothing settled, because
the seeds do not separate agents this close; or the sample ranked the wrong finalists and the gate never saw
the one that would have passed. That draft said the per-incumbent margins and the settled/unsettled split
distinguish them. **They do not.** Every one of those reports describes only the finalists that were played,
so the reports are identical whether an untested candidate would have passed or no candidate in the space
can; and a search that simply drew weak candidates looks exactly like a space with nothing left in it.

So A3 stops the loop and reports what it observed, and it **does not name a cause**. In particular it cannot
certify exhaustion, and nothing downstream may be gated on it certifying exhaustion. Establishing that the
nine-number form is spent needs evidence A3 does not produce — a coverage argument about what the search
actually explored, or a different search shape entirely — and that is an open question, not a step.

A cycle is **not** one of them, and an earlier draft had this backwards in both directions. Repeated
rejection is not evidence of a cycle, since the causes above produce it without one; and a cycle does not
force rejection, since a newcomer that beats every member of the cycle is admitted by any of these rules. A
cycle is a property of the matchup table, so it is found by **testing the table** — the round robin plus the
edges each admission adds — and it is a separate outcome with a separate response (the panel rule under Plan
B2), not a reading of this counter.

- **Produces**: a loop that halts instead of restating a fixed point, and a record of every finalist that
  failed and by how much against whom. Not a verdict on the functional form.

---

## Line B: learn the evaluation

**The claim.** A league over nine numbers has its own ceiling: `ActionScorer`'s functional form. The only
lever that lifts it is an evaluation that is learned rather than written, which is what "a value of a
position" means. This is also the only remaining purpose of the policy arm.

**Why not first.** It needs the league to judge it — a learned evaluation is only interesting if it clears
A2's gate against every hand-written set in the pool, and that needs a pool and a paired reading. And the
value arm has never produced anything but noise: `termsR2` 0.0004 on the searched teacher, a jackknife
interval of 0.0000 to 0.3760.

### B1 — fit the value on what the search computed, not on who won

Every fit so far has been trained on the **episode return**: who won the match, a signal buried under
everything that happened afterwards. A search computes something far better and throws it away — what the
round was worth after playing it out, for every candidate it considered. That is a dense, local target
produced by the one component that works.

**And it is, on most rounds, the current evaluation wearing a different hat.** `LookaheadAgent.PlayOut`
builds a round's value as `value += sign * _scorer.Score(...)` over the slots: a signed sum of the same
nine-weight `ActionScorer` this line exists to escape. Only the `Outcome` component is independent of it, and
that is non-zero only on a round that ends the match — a small minority. So a model fitted on this target and
scoring well has demonstrated that it can **imitate `ActionScorer` cheaply**, which is compression, not a
route past its functional ceiling. A high held-out r-squared would be the *expected* outcome of a successful
distillation and would say nothing about strength.

That does not kill B1, but it moves what "success" means: the target is worth fitting because a rollout sum
is denser and less noisy than an episode return, and because the search's *lookahead* is folded into it —
the value of a round already played out is not a thing `ActionScorer` can state about the board it starts
from. The acceptance criterion has to be grounded outside the fit.

- **Produces**: a value fitted on search targets, playable as `lookahead:<value>` once the evaluation can be
  named (see "what is missing" below).
- **Judged by**: **A2's own gate against every hand-written set in the pool**, with the guesser and every
  other role held fixed so only the evaluation differs. Not the built-in
  weights: `stun-first` already beats those by a settled 0.1300, so a learned evaluation could clear that bar
  and still be weaker than what a person wrote. And not "the champion" either, which an earlier draft named:
  if the pool is cyclic there is no unique strongest hand-written set — the section above rejects exactly
  that object — and beating one member while losing to another establishes nothing about a ceiling. The gate
  Line A builds is already the right shape for this, so B1 uses it rather than inventing a second one. And
  not an r-squared in either direction: a low one does not condemn the fit and a high one is what distilling
  the scorer looks like.
- **Falsified by**: not one rejection. A2 refusing a fitted evaluation says the same four things A3's counter
  says — the fit is weak, the seeds lack power against one incumbent, the finalist selection missed a viable
  fit, or the approach is wrong — and the section above spent a round removing exactly that inference from
  A3. Line B may not inherit it one page later. B1 is falsified by **several fits across the encodings and
  targets it has to offer**, all refused, with the coverage stated: what was tried and what was not. One
  rejection is a result about one fit.
- **Watch for**: a fit whose ranking of rounds agrees with `ActionScorer`'s almost everywhere. That is the
  signature of compression, and it is measurable directly — compare the two orders on held-out rounds before
  spending a league run on it.

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
- **A correction is not safer than what it corrected.** Four of the five findings in the second review of
  this file landed on sentences written to answer the first one, and two of those invented a causal split
  nothing had measured: that part of search's 0.1700 loss "was the ceiling", and that the carrier loop's
  regression "is a property of the dataset". Withdrawing an overreach tempts a smaller one in its place.
  Corrections get reviewed like anything else, and this file was merged before its first round of them was.

## ADRs owed before code

1. The league: the pool as the panel, the paired gate, and what happens on a cycle (A1, A2).
2. The stop condition and what it licenses (A3).
3. A searching agent naming its evaluation as well as its inner agent, amending ADR 0055.
4. The value target, if Line B starts: search rounds rather than episode returns (B1).
