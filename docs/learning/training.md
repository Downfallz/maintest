# Training

The Python side of the learning stack (phases L6 and L7 of the [learning roadmap](../learning-roadmap.md),
ADR 0013): the `learning/` project, managed with `uv`, that reads what the engine records and writes back
what the engine and the viewer read. The engine never learns; it records datasets, evaluates agents, and
hosts policies. New to this? Read [explained.md](explained.md) first.

## The project

```
learning/
  pyproject.toml, uv.lock       the project, its dependencies (numpy, pandas, scikit-learn), ruff and pytest
  src/downfall_learning/
    stamps.py                   the run stamp and what differs between two of them
    artifacts.py                manifests, steps, episodes, evaluations; the dataset they form
    features.py                 the feature schema versions this side can read
    policy.py                   the policy.json exchange format and its scorer
    report.py                   training.jsonl, the contract the viewer reads
    export.py                   weights files, the wide CSV projection of a dataset
    training.py                 the split by match and the feature scaling the learners share
    train_clone.py              behaviour cloning
    train_value.py              value regression
    search_weights.py           the weight search and the engine evaluator it runs
    evaluate_policy.py          playing a trained policy with the engine, the win rate into its log
    iteration.py                the report of one iteration and what moved since another
    cli.py                      the commands
  tests/                        pytest, with synthetic runs and the viewer samples as fixtures
  weights/                      scoring weights files for heuristic:<file> (greedy.json is the built-in set)
```

```bash
uv sync --project learning                              # installs the project and its dev tools
uv run --project learning ruff check learning           # what CI runs, with ruff format --check
cd learning && uv run pytest                            # the tests, with coverage in CI
```

CI runs ruff and pytest in their own job and imports the Python coverage into the SonarCloud analysis, so
the quality gate covers the Python code like the C# code. Dependabot keeps `uv.lock` current.

## Reading artifacts

`artifacts.load_run` reads a run directory (`docs/learning/artifacts.md`) and checks that every step was
observed under the manifest's schema id; `load_runs` reads several and refuses to mix run stamps unless
`allow_mixed` says so, since a dataset built on two engines or two contents measures neither. Two schema ids
never mix, even when allowed: they are two layouts. `build_dataset` joins every step with the return of its
episode; `kinds` keeps only some decisions (`Intent`, `Targets`, ...).

`features.check_schema` refuses a schema version this side does not know (`features:v1` today), the same
refusal the engine's policy agent will make (L7). `compare-stamps` prints what moved between two artifacts
(manifests, evaluations, policies) on the axes of ADR 0013: content, engine, rules, schema, agents, seed.

## Tuning the content

`search-weights` tunes the agent; `tune-content` tunes the game. The space it searches is declared in
`data/balance/knobs.json`: per spell, the numbers a pass may move and their bounds, what the spell is for in
words, and the objective as bands over the metrics `report.json` already publishes (ADR 0021).

| Command | Needs | Writes | How |
| --- | --- | --- | --- |
| `check-knobs` | the knobs file and `data/` | nothing | Fails when a spell has no entry, a pointer addresses nothing, or the authored value sits outside its own bounds. Lists the dominated and indistinguishable spells the catalogue already carries. |
| `tune-content` | the same, plus a built engine | `tune.json` and the changed spell files under `content/` | Hill climbs: play the content, then play neighbours of the best, one knob at a time. A candidate that breaks a constraint is redrawn before the engine sees it. `--apply` writes the winning numbers into `data/`. |
| `score-content` | the same, and a seed file | `score.json` | No search: plays the content as it stands on the seed file given and scores it by the objective. The check a proposal gets on seeds it was not searched on. |

**And every one of them says where it is while it runs.** A tuning pass plays several hundred evaluations
over several hours and a weight search is not far behind, so `tune-content`, `search-weights` and
`train-clone` report progress on **stderr** — one line per unit of work, with the count, how long it has
taken, how long is left where that can honestly be computed, and the best score so far. Stdout stays the
report a person reads and a script parses, and the two never interleave. `--quiet` turns the reporting off
and changes nothing else. The tuner counts up with no total while its opening pass runs — the opening's size
is not knowable until it has run, because the paired moves depend on which spells the single steps failed to
improve — and sets an exact total, with a time estimate, once the climb starts.

Two failures a long run used to hide, and both now fail in the first second instead of the fourth hour: a
command that was never built, and **one built before the code it is supposed to run**. The second is the
expensive one — the assembly is there, it runs, and every number it reports belongs to the engine you
replaced. `missing_engine` compares each build against the trees it is built from and names the file that
moved: the CLI against `src/`, the data builder against `src/` and `tools/`, since a tool may reference
Infrastructure. `tune-content` runs both and checks both. Rebuild with
`dotnet build --configuration Release`, which is the build these commands use by default.

**Every one of these runs explains its own result rather than printing it.** `tune-content` names what it
changed in the content's own words, which measurement the gain came from, what that gain cost elsewhere, and
what is still outside its range with the number it reads beside the number it should be. `search-weights`
names which weights moved, prints what it measured, and then says plainly that the run cannot settle whether
that is better: it keeps the best of what it drew, so the best score beats the initial one by construction,
and every candidate played the same fixed seeds, so comparing two marginal intervals is not a test of the
difference between them in either direction. What settles it is replaying the weights on seeds the search
never saw. `evaluate-policy` says whether its win rate's interval
contains one half, because a win rate of 0.54 whose interval runs 0.50 to 0.58 is not a 54% policy, it is a
policy that many matches could not measure.

The score is `sum(weight * (excess / scale) ** 2)` over the objective's targets, zero being on target. Seven
of them are `report.json` metrics under their own names, so a tuning run and a normal run are read the same
way. Six the tuner derives from `spellOutcomes`, because they need the catalogue as well as the evaluation:
`spellUsageShare`, `spellsNeverCast` and `spellsBarelyCast` over the whole catalogue, and three over a
**tier** — the spells
offered at one depth of the talent tree, which is the set a player chooses between. Depth is what a spell
requires as well as where it is written (ADR 0034): a class node holds its opener and both spells behind it,
so reading the node alone would call all three one tier. Reading the prerequisites gives the shape a player
climbs, 3 / 6 / 9 / 18 on today's content. `tierUsageShare` asks
whether one of them owns the tier, `tierDamageSpread` whether they hit comparably hard per landed cast, and
`tierWinSpread` whether they win comparably often. Each reports its worst tier, and each skips what it
cannot read: a tier nobody cast, a spell with no `Damage` effect, a spell too few sides declared for its
own number to mean anything. Damaging is read from the content, so an attack whose hits are absorbed widens
the spread rather than leaving it.

`docs/learning/explained.md` says what a band, a scale and a weight are in plain words, and how to point the
objective at a match length or at your own agent.

A candidate costs one content build plus one evaluation per objective entry, about fourteen seconds on the
benchmark seeds across the four the objective declares. The opening sweep is up to two candidates per playable
knob before the climb starts, plus up to two more per pair of knobs on a spell no single step could improve,
plus three more for each of the six pairs closest to paying off — on the nine-spell core content, up to 139
candidates before the climb starts. `--no-sweep` and `--no-pairs` turn the first two off; `--pair-depth 1`
leaves the pairs at one step each and turns off only the third. The `Tune the catalogue` workflow climbs 24
rounds of 6 on top of that, about 67 minutes of its 300-minute budget; the CLI defaults to 8 rounds of 4, so a
local run stays short enough to iterate on.

A catalogue the search has already played is served from what it measured the first time rather than replayed:
the same neighbour comes up in more than one round, and on the last full pass 30 of 149 candidates were
replays. The report says how many the engine actually had to play.

## Three learners

| Command | Needs | Writes | How |
| --- | --- | --- | --- |
| `search-weights -o <dir>` | the built engine and `data/dst`, no dataset | `weights.json`, `search.json` (every candidate, with its score per opponent and the floor it was held to when there were several), `evaluation.json`, `training.jsonl` | Cross-entropy method over the eight scoring weights. Each candidate is a weights file evaluated by the engine's `evaluate` as `<kind>:<file>` against `--opponent` (default `greedy`; several separated by commas score the mean over them, one evaluation each, under a floor: a candidate that falls below the start's interval against any of them ranks below every candidate that did not, so it cannot win by learning one opponent, which a plain mean would let it (1.0, 0.5 and 0.5 average above 0.6, 0.6 and 0.6) and which the worst matchup prevented by flattening every matchup instead (journal, 2026-09-16)) on `--seeds` (default the benchmark seeds), mirrored, where `--kind` is `heuristic` (the default, one step), `lookahead` or `minimax` (the round played out; ADR 0047): weights searched for one reading are only meaningful played by it, and `search.json` records the kind; the fitness is the mean score. The mean of the elite becomes the next mean, its spread the next spread; the current mean is always in the population, so the best is never lost. |
| `train-clone <runs> -o <dir>` | a recorded run (`simulate --record`) | `policy.json`, `training.jsonl` | Behaviour cloning as a conditional logit (ADR 0051): the policy's own score of every candidate the step offered, a softmax over those candidates alone, and the loss is the negative log probability of the action taken, minimized by Adam in mini-batches, one epoch per iteration, keeping the epoch whose choice among the candidates matches the data best on held-out matches. On a run that records `candidateTerms` the model also carries one weight per term, shared by every key, so a candidate is scored on what it does and not only on what the board is. Its `loss` is the negative log probability over the candidates offered, read once with the weights the epoch ends on rather than accumulated over the mini-batches, and it does not compare with the multinomial log loss the journal reports before ADR 0051, with or without terms. |
| `mean-policy <models...> -o <dir>` | two or more trained policies of one kind, schema and content | `policy.json` | A policy is linear, so the mean of several is exactly a policy that scores every candidate as the mean of their scores; a key one of them never saw counts as that one's `fallback`. Every policy given weighs the same, nothing is chosen, so it stays off the test set. The loop plays the mean of its seeds' value fits as its last step. |
| `train-value <runs> -o <dir>` | a recorded run, ideally an explored one (below) | `policy.json`, `training.jsonl` | Value regression in two parts (ADR 0016): one baseline over the observation alone, fitted on every step, then one ridge regression per action key over what the baseline leaves. A score is the baseline plus the action's row, so it still predicts the return, and the agent takes the candidate that scores best. A key seen fewer than `--min-samples` times keeps its mean, and an unseen key the mean of the data — both on top of the baseline. On a run that records `candidateTerms`, one ridge over the terms of the action taken is fitted to the advantages first, on every training step, and the rows are fitted on what it leaves (ADR 0051); `termsR2` in the metrics is what the terms alone explain on the held-out steps. |

Matches are held out whole (`--validation`, default one in five), so a validation step never comes from a
match the model saw. Features are standardized for the optimizer and the scaling is folded back into the
weights, so a policy file stays a plain dot product. `export-csv` writes the wide CSV projection of a dataset
(one row per step, one column per feature) for anything that prefers a table.

The weight search is the slow one: one engine run per candidate, sequentially, about **3.8 seconds** each on
400 matches on a four-core machine. The defaults play the mean once and then ten iterations of sixteen, 161
evaluations, so a run is around **ten minutes**. It was twenty until ADR 0030 played the matches of an
evaluation at once and the engine stopped going through `dotnet run`; the candidates themselves are still
played one after another, so a machine with more cores helps and a search that wants to be faster still has
that to take. A smaller seed file (`--seeds`) makes it faster and noisier; fewer iterations makes it faster
and shallower.

## How big a dataset fits

A run is loaded into one array of one row per step, and every step keeps a view on its row rather than its
own numbers. At the width of `features:v1` for a team of three (383 features) that array is about 3 MB per
thousand steps, and the loader's own overhead adds roughly half a kilobyte per step; measured peak resident
memory, which includes what the JSON parsing leaves behind, is closer to 9 MB per thousand steps. A recorded
match is a few hundred steps, so:

| Dataset | Steps | Peak while loading |
| --- | --- | --- |
| 200 matches | ~50,000 | ~0.5 GB |
| 1000 matches | ~320,000 | ~3 GB |

Training on several runs at once (`train-value a b --allow-mixed`) loads each of them and then copies the
selection into one array, so budget the sum plus one more copy. A machine that runs out is killed by the
kernel with no Python error and no output file: if a training command ends silently and leaves nothing
behind, check its exit code for `137` and record fewer matches.

## Why the value learner fits a baseline first

A step's return is the outcome of a whole match of about a hundred and fifty decisions, credited to every
decision in it. It therefore measures the position the player was in far more than the move they chose. When
each action key is fitted on the raw return, every row spends its capacity re-learning the position, on its
own slice of positions, and their intercepts end up calibrated on different worlds — which is exactly what
makes two of them incomparable at the moment of choosing.

So `train-value` fits one state-value model `V(s)` on every training step, with no split by action, and
regresses each action key on `return - V(s)`. The baseline sees the whole dataset, so it is the
best-determined part of the model; what is left for a row is the part its own action is responsible for,
which is the quantity the policy needs. Both go in `policy.json`: the score of a candidate is the baseline's
value plus the action's row, so it still reads as a predicted return, and since the baseline is the same
number for every candidate at a state it never changes which one wins.

`training.jsonl` and the policy's metrics carry `baselineR2` beside `r2`: what the position alone explains,
against what the position and the action explain together. A `baselineR2` close to `r2` means the action rows
are adding nothing. `fittedActions` beside `actions` says how many keys got a regression of their own rather
than a mean.

The runs that led here are in `docs/learning/journal.md`: seven straight losses of 400 matches to `Greedy`,
across exploration, five times the data, and thresholds from 5 to 50, with the held-out fit moving every time
and the win rate never.

## Why the value learner needs an exploring dataset

`Greedy` plays the same move in the same position, so a dataset of its own games never shows what a different
move would have given there. Each action key is then fitted on its own slice of states: `heavy_strike` on the
states where `Greedy` wanted it, `basic_attack` on the leftovers where it was not available. The return
measured on such a slice says how good those situations were, not how good the action is, and ranking two of
those rows at one position compares models calibrated on different worlds. Three trainings confirmed it, each
losing all 400 mirrored matches against `Greedy` while behaviour cloning on the same data reached `Greedy`'s
own strength (`docs/learning/journal.md`, ADR 0014).

The `explore:<rate>` agent is the fix: it plays its inner agent's move except for the given share of
decisions, which it takes uniformly at random. The states stay the ones a strong policy reaches, and every
action key now appears in some of them, so the rows become comparable.

```bash
dotnet run --project src/DownfallArena.Cli -- simulate --p1 explore:0.2 --p2 explore:0.2 --matches 200 --seed 1 --record runs/<id>/dataset-explore
scripts/iterate.sh --explore 0.2      # the same, inside the loop, keeping a pure dataset for the clone
```

It draws from the seeded random source, so a recorded run replays exactly, but it never plays a baseline: the
benchmark digest is defined by agents that draw nothing. Behaviour cloning keeps the pure dataset, since
imitating a bot that is wrong on purpose part of the time is not what the clone is for.

### Who the loop records, and why it is not always `Greedy`

`explore:<rate>` alone wraps `Greedy`, and whatever follows the rate is read as a whole agent spec that it
wraps instead — `explore:0.2:heuristic:<weights>`, `explore:0.2:policy:<file>`. A bare path after the rate
stays the shorthand for a weights file that every journal entry before 2026-09-15 uses. The loop's
`--teacher` sets both datasets at once: the pure one is recorded against that agent and the exploring one
deviates from the same agent, because the two policies of one turn learning from two different players is the
inconsistency this exists to prevent. `--teacher greedy` is the default and the original behaviour.

It matters more than the plumbing suggests. A clone can only be as good as what it imitates, and on
`7e199df4` `Greedy` is beaten 92.75% by a searched weight set, so cloning `Greedy` caps the clone below
`Greedy` by construction. Recording against `search-4` instead took the clone from **32.0% to 71.0%** against
`Greedy` and from 94.0% to 99.25% against `Random`, which made it the first learned agent here to beat the
baseline (`docs/learning/journal.md`, 2026-09-15).

```bash
scripts/iterate.sh --teacher heuristic:learning/weights/search-4.json --explore 0.2
```

Any agent spec can be the teacher, a trained policy included, because `ExploringAgent` now wraps whatever the
spec after the rate names. That is what closes the loop: the policy a turn trains can record the dataset of
the next one, which is policy iteration rather than one isolated fit per turn.

```bash
scripts/iterate.sh --teacher policy:runs/<previous>/value/policy.json --explore 0.2
```

A clone of a policy is still capped by that policy, so this is worth doing with `train-value`, whose target is
the return rather than the teacher's choice, and not as a way to distil a clone into a better clone.

## Playing a policy

`policy:<file>` seats a `PolicyAgent` in the engine: every decision builds the observation of the board,
lists the candidate actions the options offer in the order a dataset records them, scores each key with the
policy's row (or its fallback), adds the candidate's terms under the policy's candidate weights when it
carries any (ADR 0051), and takes the best, the first on a tie. `AgentFactory` refuses a policy whose
schema id is not the one the current content and rule set give, with a message naming both ids; the spec is
stamped `Policy:<path>@<fingerprint>`, eight hex digits of the file's bytes.

```bash
dotnet run --project src/DownfallArena.Cli -- evaluate --p1 policy:models/value/v1/policy.json --p2 greedy --seeds benchmarks/benchmark-seeds.json
uv run --project learning evaluate-policy models/value/v1 --opponent greedy     # the same, and the win rate into training.jsonl
```

`evaluate-policy` writes `evaluation-vs-<opponent>.json` next to the model (or `--output`) and fills
`winRate`, its interval, and `matches` on the kept iteration of the model's `training.jsonl`, unless
`--no-log`. The engine's refusal of a foreign schema surfaces as an error with its message.

## The `policy.json` exchange format

A policy is a linear scorer over action keys, read without an ML runtime:

| Field | Meaning |
| --- | --- |
| `kind` | `clone` (rows are classifier logits) or `value` (rows are predicted returns). Both are read the same way. |
| `stamp` | The run stamp of the training data. |
| `schemaId`, `schemaVersion` | The feature schema of the observations (`docs/learning/features.md`); a reader refuses a version it does not know. |
| `featureNames` | The feature names, in observation order; the width of every row. |
| `actionKeys` | One entry per row, the action keys the policy knows (`docs/learning/features.md`, action encoding). |
| `weights`, `bias` | One row and one bias per action key, written to six decimals (`WEIGHT_DECIMALS`). |
| `fallback` | The score of a candidate action the policy never saw. |
| `candidateTermNames`, `candidateWeights` | Optional (ADR 0051): the scoring weight names in the engine's order, and one weight per term shared by every key. A reader refuses names in another order, and a policy without them scores the keys alone. |
| `trainedAt`, `metrics` | When, and what the training measured (loss, accuracy, r2, step counts). |

To choose: for each candidate action the options offer, its row's dot product with the observation plus
its bias, or `fallback` when the policy has no row for that key, plus, when the policy carries
`candidateWeights`, their dot product with that candidate's terms as the engine reads them (`CandidateTerms`,
the same numbers a dataset records); the best-scoring candidate wins, the first on a tie. `Policy.choose`
does it in Python; the engine's `PolicyAgent` does the same, so a policy plays identically on both sides.
A policy whose candidate weights are the built-in scoring weights and whose rows are zero plays Greedy's
combat decisions exactly, which is the invariant the terms rest on. The terms are read under the built-in
weights whatever agent is recorded, so an intent's terms are those of the target set Greedy would bind; a
heuristic teacher playing other weights may bind another, and the invariant is then a reading of the board
rather than of that teacher (ADR 0051).

A weight keeps six decimals because a committed policy is read back for the life of its catalogue and what
git stores for one is what compounds: `ci-69` is 0.57 MB compressed at full precision and **0.30 MB** at six
decimals. Writing the file without its indentation as well saves 0.03 MB more, which is not worth an artifact
nobody can read. Six is chosen with room to spare, not at the edge: the same clone plays the benchmark seeds
identically at six, four and three decimals, and over 5326 recorded steps not one argmax differs at any of
them. The rounding happens on the way out, so a policy in memory is exact and only the file is short.

## Model files

A trained policy is committed under `models/<name>/<version>/` (`policy.json`, `training.jsonl`, and the
`evaluation.json` of its evaluation against the baselines once L7 runs it). Datasets stay out of git
(`runs/`); they are regenerated from seeds. `learning/weights/` holds the weights files the heuristic agent
reads, the searched ones next to the built-in `greedy.json`.

## The loop (L7)

`scripts/iterate.sh [--run <id>] [--against <id>] [--matches N] [--seeds "1 5001 10001"]` runs one iteration
and leaves everything under `runs/<id>/`:

1. Build the engine and the content; check the benchmark digest (`benchmark`).
2. Baselines on the benchmark seeds, mirrored: `random-vs-random`, `greedy-vs-greedy`, `greedy-vs-random`.
   Once for the whole turn, into `baselines/`: they never read the dataset seed.
3. Then, **once per dataset seed**, into `seeds/<seed>/` (ADR 0049):
   1. Record a self-play dataset from that seed (`simulate --record`).
   2. Train the value and clone policies on it (`train-value`, `train-clone`).
   3. Evaluate each policy against greedy and against random (`evaluate-policy`).
   4. With `--against`, replay the previous run's value policy on this content into
      `previous-value-vs-greedy.json`; when its feature schema no longer applies, say so and go on.
   5. Write that seed's `report.json` and `report.html`.
4. Range every win rate across the seeds into `spread.json` (`spread`).
5. With more than one seed, average the seeds' value fits into one policy (`mean-policy`), under `mean/value/`,
   and play it against the same opponents into `mean/evaluations/`. It is not in the spread and not gated: it
   is one policy, so its three numbers are one sample each, and the question they answer is whether the fits'
   disagreement in play (journal, 2026-09-16) is variance a mean removes or a place the mean collapses too.
6. With three seeds or more, the jackknife of that mean: one mean per seed with that seed's fit left out,
   under `mean/without-<seed>/`, each played against Greedy and the baseline, and `mean/jackknife.json`
   (`jackknife`) with the standard error those replicates give the mean's score and an interval of two errors
   each way. That is how far the mean's number moves with the draw of its fits, which the spread across the
   seeds does not say: two turns' means differ when their intervals are clear of each other, not before.

**Two evaluations are compared seed by seed, not interval by interval.** Every agent plays the same fixed
benchmark seeds, so two agents' scores rise and fall together: a seed hard for one is hard for the other. A
marginal interval says where one agent's score sits and neither says anything about the *difference*, in
either direction — `search_weights.format_search` has said so for as long as it has printed one. `evaluate`
already writes what the difference needs: `pairs`, one entry per seed carrying `scoreOfA`, with a seed's two
mirrored matches already averaged into it. `paired a.json b.json` subtracts those per-seed scores and gives
the mean difference its own interval, which is *tighter* than the marginals suggest because the shared seed
difficulty cancels instead of adding. It refuses two files that played different seeds or different content,
and names both agents and both opponents so a reader sees which comparison was made. Nine evaluations were
once read as marginal intervals with the paired data sitting in the same files (journal, 2026-09-18).

**Read the spread, not a seed.** One seed is a sample: three runs of one configuration differing only in the
dataset seed scored 0.6625, 0.0975 and 0.30375 against `search-4` (`ci-88`, `ci-90`, `ci-91`), a 56-point
spread that is wider than every effect this project has measured. The default is three seeds and a turn
therefore costs about half an hour. `--seed S` still runs one, to reproduce an older run exactly; it prints a
width of zero and says, in as many words, that this is a sample. A gate reads the **minimum** across the
seeds and never the maximum — the benchmark seeds are fixed, so choosing the dataset seed that scored best is
selection on the test set.

**Space the seeds by the match count.** Match `i` of a dataset recorded from seed `s` plays seed `s + i`, so
seeds closer than the match count record the same matches shifted by their distance: `1 2 3` at 5000 matches
is one dataset three times, sharing 4999 matches, and the fifth each holds out is the next one's training
matches (journal, 2026-09-16). The three spreads above, and every one before that entry, were measured that
way: they are the fit's sensitivity to which fifth was held out, not three draws of the data. The default is
now `1`, `1 + matches`, `1 + 2 * matches`, and the script refuses a closer list before playing a match.

`report`, at step 3.5 above, reads every `evaluations/*.json` of one seed, refuses to mix engines or
contents, writes that seed's `report.json`, prints the table, and with `--against` the deltas and the stamp
axis that moved. It also writes `report.html` (unless `--no-html`): the viewer page with the stylesheet
inlined and the seed's `report.json`, evaluations, and `training.jsonl` files embedded as one JSON block the
page reads at start, so the file opens on the report with nothing to drag in. `--viewer <dir>` points at
another copy of the viewer.

```
runs/<id>/
  baselines/*.json          the agent-vs-agent baselines, once for the turn
  seeds/<seed>/
    evaluations/*.json      one evaluation per pair of agents, baselines copied in
    dataset/, dataset.csv   the recorded run and its summary
    value/, clone/          policy.json, training.jsonl, evaluation-vs-*.json
    report.json             the summary below, for this seed alone
    report.html             the viewer, carrying this seed: open it and it starts on the report
  spread.json               every win rate ranged across the seeds -- the result of the turn
```

The script takes every tuning knob as a flag (`--matches`, `--value-alpha`, `--value-min-samples`,
`--clone-epochs`, `--clone-alpha`, `--validation`), passed through to the learners, so a run is tuned from
the command line and its `--help` explains each in plain words; `docs/learning/explained.md` has the table.

The same turn runs unattended in `.github/workflows/iterate.yml` (Actions -> "Learning loop"): on a push to
`main` that touches `src/`, `data/`, `learning/`, `benchmarks/` or the script, on a pull request that changes
`learning/experiments/`, weekly, and on demand. The knobs come from `learning/experiments/next.json`, so an
experiment is committed rather than typed; a workflow input, when a dispatch fills one in, wins over the file
for that run, and `explore` set to `off` skips the exploring dataset. The report table goes in the run
summary, under the file's `why`; `report.html`, `report.json`, the evaluations and the two policies are the
run's artifact. The datasets are not uploaded: the seed reproduces them. A CI runner keeps nothing between
runs, so `--against` stays a local comparison. A run on `main` finishes even when another push lands while it
is running; the next one queues behind it, and only the newest of the queued ones survives. A run on a pull
request is the opposite: a newer push cancels it, because the answer wanted is the one on the last commit.

Two more turns of the same crank run on the runners and nowhere else in particular, so asking for one needs
no local SDK and no machine left on:

| Workflow | Dispatch inputs | What comes back |
| --- | --- | --- |
| **Tune the catalogue** (`tune.yml`) | search seed, rounds, neighbours, knobs per proposal, and whether to apply | The proposal in the run summary, and, when it moved something, a **branch** carrying the changed spell files and a regenerated benchmark digest, with a link that opens it as a pull request. |
| **Search the agent weights** (`search.yml`) | opponent, a check opponent for the hold-out, the agent kind, the weights to start from, seed file, rounds, population, search seed, and whether to apply | The weights in the run summary, as ratios to `damage`, beside the baseline's, and, when asked and when a candidate beat the set it started from, a **branch** carrying them as `learning/weights/search-<run>.json`, with a link that opens it as a pull request. |

Both can propose a branch; what a branch may contain is where they differ. A tuning pass proposes content, and
content is reviewed as a diff, so its branch changes the spell files themselves. A weight search proposes an
*agent*, and `Greedy`'s weights are the baseline every learned agent is measured against, so its branch never
touches them: it adds what the search found under its own name, which is what `agents.md` says to do with a
searched set, and leaves `ScoringWeights.Default` and `greedy.json` alone. Adopting a searched set as the
baseline stays a separate change with a journal entry that says why. The 2026-09-10 entry is the worked
example: the search found a better agent and the entry decided against making it the baseline.

Applying is off by default there and on by default for the tuning pass, for the same reason: a tuning proposal
is a diff someone reads and deletes, a weights file is a claim that something is better, and the run that
produced it cannot establish that. So an applying weight search makes the comparison it cannot: it replays the
found weights *and* the baseline on seeds no candidate played — consecutive integers starting past the largest
in the seed file, so they cannot overlap it — and puts both scores in the run summary and in the commit
message. Against `greedy` the baseline's side of that is even by construction, which is what makes it a check
on the seed set rather than a second opinion. Unseen seeds answer "did it fit the seed file" and not "did it
fit the opponent", since the opponent is the same on both sides, so the same two agents are replayed once more
against a `check_opponent`, `random` unless the dispatch says otherwise: a set that beats the opponent it was
searched against and scores below what it started from there has learned that opponent, not the game (the
2026-09-16 entry on the searched lookahead weights is the case). Four evaluations, under a minute after a
twenty-minute search, and it is the difference between a number and a claim. An applying tuning pass makes the same
comparison with `score-content`: the proposal and the catalogue it started from, both played on a window of
seeds clear of the objective's seed file, both scores in the run summary and the commit message. There the
score is a penalty and lower is better.

**Both workflows push the branch and stop there, on purpose.** A pull request opened by a workflow
does not start the `pull_request` workflows, so it would arrive with no CI and no SonarCloud quality gate —
which this file's own rule requires on every pull request. The run summary carries the link that opens the
branch as a pull request instead: one tap, the repository's real checks run, and because the branch holds a
single commit GitHub fills the description in from its message, which the workflow wrote for that purpose.

Before it pushes anything each job runs the gate a contributor runs — the .NET build, tests and format check,
and the learning project's ruff, format check and pytest — so a proposal that breaks any of them never becomes
a branch. Both proposals land under a directory that gate covers, so both run all of it. A weights file is
content the test projects copy to their output directory, so the weight search builds again before it tests:
with `--no-build` the tests would read the copy from before the file existed. Both jobs call the learning
project as a module (`python -m downfall_learning.cli`), the way
`scripts/iterate.sh` does: the runners install the locked dependencies without installing the project itself,
so its console scripts are not on the path there.

`report.json` holds the run's stamp and, per evaluation, the agents, the matches, and the metrics: `winRateA`
with its interval, `scoreA`, `player1WinShare` (the share of matches player 1 won, near one half when the
agents are identical), `drawRate`, `averageRounds`, `roundCapShare`, `spellEntropyA`/`B`, `fizzleRateA`/`B`.
These are the balance signals the roadmap asks the loop to show every time. `report --against <run>` adds the
deltas for every evaluation both runs hold and lists the stamp axes that differ (content, engine, rules,
schema); agents and seeds are expected to differ between evaluations and are not axes of a report.

Then one entry in `journal.md`: what changed, the numbers, the decision.
