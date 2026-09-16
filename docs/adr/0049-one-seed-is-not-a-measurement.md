# 0049. One seed is not a measurement

Date: 2026-09-16
Status: Accepted

## Context

Every learning-loop result in this journal is one run on one dataset seed, and the loop's numbers have been
compared as if that were enough. `ci-88`, `ci-90` and `ci-91` are the same configuration — lambda 0.9, the
ADR 0048 baseline, `search-4` as teacher and baseline, 1000 matches — differing only in the seed the dataset
is recorded from. The evaluation is on the fixed benchmark seeds in every case.

| at lambda 0.9, by dataset seed | 1 (`ci-88`) | 2 (`ci-90`) | 3 (`ci-91`) | 4 (`ci-92`) |
| --- | --- | --- | --- | --- |
| value against `search-4` | **0.6625** | 0.0975 | 0.30375 | **0.0** |
| value against `Greedy` | 0.0325 | 0.15375 | 0.03375 | 0.0 |
| clone against `Greedy` | 0.725 | 0.79875 | **0.82125** | **0.68125** |
| clone against `search-4` | 0.5325 | 0.5775 | 0.52 | 0.4625 |
| clone against the champion `ci-69` | 0.5 (itself) | **+0.53625** | **-0.42375** | 0.495 |

**The spread from the seed alone is 66 points** on the first row: the same configuration wins two matches in
three against `search-4` on one draw and **not a single one** on another. Every lambda, baseline and teacher
effect this project has measured is smaller than that, and each was measured on a single seed. `ci-88`
beating `search-4` — reported as the first agent ever to do it — is inside this spread and is not a result.

The fourth seed also killed the one cross-seed claim that had looked safe. Seeds 2 and 3 put the clone above
seed 1 against `Greedy`, which read as "seed 1 was the weak draw"; seed 4 came back at 0.68125, below all
three. There was no weak draw, only a wide one.

The clone rows are worse, because they disagree with each other. `ci-91`'s clone beats `Greedy` **0.82125**,
better than any policy the loop has produced, and loses to the committed champion `ci-69` at **0.42375**,
where `ci-69` itself beats `Greedy` only 0.725. A third-party score does not order two players, and the
champion bar caught it — for the second time in one night. Against the champion the four draws read
+0.536, −0.424 and 0.495: the same configuration is better than, worse than, and level with the policy it
would replace, depending only on which matches it was fitted on.

## Decision

**A number from one seed is a sample, and the loop stops reporting it as a measurement.** `iterate.sh` takes
`--seeds` (default `1 2 3`) and runs the whole of recording, training, evaluation and the report **once per
seed**, into `runs/<id>/seeds/<seed>/`. A new `spread` command ranges every win rate across those seeds into
`runs/<id>/spread.json`, and the turn ends on that table rather than on any one report. The commit gate reads
the spread's **minimum**, which is the same thing as "every seed cleared it", and plays the champion once per
seed, requiring every one of them to beat it measurably.

Three details the implementation had to settle, each of which could have quietly reintroduced the problem:

- **The baselines are evaluated once, not per seed.** They are agents playing the fixed benchmark seeds and
  never read the dataset seed, so they are copied into each seed's `evaluations/` for its report. Running
  them three times would triple the wall clock to print the same four rows.
- **The committed policy is the first seed's, chosen by position.** With three seeds there are three
  policies, and picking the best of them against fixed benchmark seeds is the very selection this ADR
  forbids. Position is arbitrary and therefore safe; `spread.json` is committed beside the policy so the
  file says what the other seeds did.
- **One seed still runs, and prints a width of zero with a sentence saying it is a sample.** Reproducing an
  old single-seed run must stay possible, and a width of zero must not read as agreement.

Every existing journal entry that compares two configurations measured on one seed each is a sample, and
this ADR is linked from the ones that matter.

## Consequences

- Good: the loop stops manufacturing findings. Three of this week's headlines — the lambda 0.95 peak, the
  lambda 0.9 win over `search-4`, and the size of ADR 0048's gain — are one-seed samples and would not have
  been written as findings under this rule.
- Good: "try seeds until one scores well" stops being available. The benchmark seeds are fixed, so choosing
  the dataset seed by its score is selection on the test set, and the gate reading a spread forecloses it.
- Bad: **the loop costs three to five times as much wall clock per turn.** A turn is roughly nine minutes
  today; a three-seed turn is half an hour. That is the price of the numbers meaning something.
- Bad: **ADR 0048's headline is not established.** It reported `baselineR2` and a win rate moving from
  0.10125 to 0.29625 against `Greedy`, both on seed 1. The fit improvement is real and is measured
  held-out; the **win rate claim is a one-seed sample inside the spread above** and this ADR says so. The
  ADR is Accepted and immutable, so this is the correction and the journal entry of 2026-09-16 carries it.
- Neutral: nothing about the engine, the content, the feature schema or `policy.json` changes. This is about
  how the loop reports, not what it computes.

## Alternatives considered

- **Keep single-seed runs and simply widen the intervals.** The intervals are already correct — they are
  over the 400 benchmark *matches*, and `ci-88`'s was 0.5988 to 0.7112, nowhere near `ci-90`'s 0.0975. The
  variation being missed is between *fits*, not between matches, and no match-level interval can see it.
- **Raise the matches per run from 1000 to 5000 instead.** Cheaper to implement and it does shrink the fit's
  variance, but it buys one better sample rather than a spread, and it cannot tell you how wide the spread
  is. It is also not free: the recording step already dominates a turn.
- **Average the seeds into one number.** Loses exactly the thing that matters here. A mean of 0.6625, 0.0975
  and 0.30375 is 0.35, which reads like a middling result rather than like three runs that disagree.
- **Only apply this to the commit gate, not to the journal.** The gate is not where the damage was done —
  it refused every one of these policies correctly. The damage was in entries that read a sample as a
  finding, so the journal is where the rule has to bite.

## Follow-up

Done in the change that accepted this ADR:

- `scripts/iterate.sh`: `--seeds` (default `1 2 3`), the per-seed layout under `seeds/<seed>/`, the
  baselines evaluated once, and step 9, the spread.
- `learning/src/downfall_learning/spread.py` and the `spread` command: `Range`, `build_spread`,
  `format_spread`, `write_spread`, with `learning/tests/test_spread.py` pinning the minimum a gate reads,
  the ordering of the seeds, an evaluation only some seeds ran, and that one seed is called a sample.
- `.github/workflows/iterate.yml`: the `seeds` input, `seeds_from_plan` (which still reads a scalar `seed`
  so an experiment written before this reproduces), the `cleared` step reading `spread.json` minima and
  playing the champion once per seed, and the committed policy taken from the first seed.
- `learning/experiments/next.json`: `seeds` is a list.
- `AGENTS.md` and `docs/learning/training.md`: the convention, so a turn is not proposed on one seed again.

Left open on purpose:

- The journal entries before 2026-09-16 are not rewritten. They are the record of what was believed at the
  time, and the entry of 2026-09-16 says which of their claims are samples.
- ADR 0048 cannot be edited; the correction to its win-rate claim lives in the Consequences above and in the
  journal. **Re-measuring that fix as a spread has not been done** and is the first thing worth a turn.
- `search.yml` and `tune.yml` have the same shape of problem and are untouched here.
