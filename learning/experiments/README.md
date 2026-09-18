# The next experiment

Two files, one shape: each holds what a workflow runs with when nobody types anything into its form, so asking
for a run is a commit and the parameters of every experiment stay in git next to the entry that reports it.
`next.json` is the learning loop's, below; `search.json` is the weight search's, at the end.

`next.json` holds the knobs the `Learning loop` workflow uses when nobody types any: change it, push it, and
the loop runs with those settings and reports back (`.github/workflows/iterate.yml`). Its git history is the
list of experiments that were run and why, which is the point of committing it rather than typing the numbers
into a form.

| Key | What it is | Default when the key is absent |
| --- | --- | --- |
| `why` | one paragraph on what this run is meant to answer; it is printed in the run summary | nothing |
| `matches` | matches recorded in the dataset | 200 |
| `seeds` | the dataset seeds of the turn, one dataset each (ADR 0049), spaced by at least `matches`: match `i` plays seed `s + i`, so closer seeds record the same matches shifted | three seeds spaced by `matches` |
| `seed` | one base seed, the form the files before ADR 0049 carry; read as a one-entry list | — |
| `explore` | share of decisions taken at random in the value dataset, or `"off"` (ADR 0014) | 0.2 |
| `clone_on_explore` | fit the clone on the exploring dataset rather than the pure one; needs a non-`off` `explore`. For a clone meant to be a searching agent's inner agent rather than a player: search asks it to *guess* on hypothetical boards that pure self-play never visits (journal, 2026-09-18) | false |
| `value_alpha` | how strongly the value fit is pulled toward zero | 1.0 |
| `value_min_samples` | examples an action needs before it gets its own fit | 5 |

Every knob means exactly what the flag of the same name means in `scripts/iterate.sh --help`, and
`docs/learning/explained.md` ("The knobs") says when to touch each in plain words.

A dispatch of the workflow (Actions -> Learning loop -> Run workflow) overrides this file for that one run:
whatever you type in the form wins, and a field you leave empty falls back to the file.

## The next weight search

`search.json` is the same idea for `Search the agent weights` (`.github/workflows/search.yml`): a pull request
that touches it runs a search with what it says. The ladder is a sequence of these, each starting from the set
the last one found, and the file is where a rung says what it is for before it is played.

| Key | What it is | Default when the key is absent |
| --- | --- | --- |
| `why` | one paragraph on what this rung is for; it is printed in the run summary | nothing |
| `initial` | the weights the search starts from, which is what lets the ladder climb | the built-in ones, which `greedy` plays |
| `opponent` | agent B of every evaluation, or several separated by commas: a candidate scores the mean over them, and one that falls below the start against any of them ranks last, so it cannot win by learning one | `greedy` |
| `check_opponent` | a second opponent the hold-out replays both agents against, off the line the search was run on | `random` |
| `kind` | the agent kind that plays each candidate: `heuristic`, `lookahead` or `minimax` | `heuristic` |
| `seeds` | the seed file of every evaluation | `benchmarks/benchmark-seeds.json` |
| `iterations`, `population`, `seed` | rounds of the search, candidates per round, and the search seed | 10, 16, 0 |

**A committed experiment cannot `apply`.** Pushing the proposal branch needs the credential the checkout keeps
only on a dispatch, so a file asks for a *measurement* and a person asks for a commit: read the run summary,
and dispatch the same parameters with `apply` checked if the set is worth keeping.

That review is the reason every field of the form defaults to **empty**. GitHub hands a dispatch the declared
defaults whether or not anybody typed in them, so a non-empty default would make "check `apply` and press the
button" search something other than the experiment just reviewed. Empty means "ask the file"; a field you fill
in wins for that run; and `none` is how the form asks for *nothing* where the file names something — `initial:
none` starts from the built-in weights, `check_opponent: none` runs no second check.

The hold-out — both agents replayed on seeds no candidate saw, and against the check opponent — runs on **any
run that found something better**, not only on one that applies it. It is the only part of a run that can say
whether a set is worth keeping: the search's own numbers come from the seeds the winner was picked on.
