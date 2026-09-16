# 0049. One seed is not a measurement

Date: 2026-09-16
Status: Proposed

## Context

Every learning-loop result in this journal is one run on one dataset seed, and the loop's numbers have been
compared as if that were enough. `ci-88`, `ci-90` and `ci-91` are the same configuration — lambda 0.9, the
ADR 0048 baseline, `search-4` as teacher and baseline, 1000 matches — differing only in the seed the dataset
is recorded from. The evaluation is on the fixed benchmark seeds in every case.

| at lambda 0.9, by dataset seed | 1 (`ci-88`) | 2 (`ci-90`) | 3 (`ci-91`) |
| --- | --- | --- | --- |
| value against `search-4` | **0.6625** | **0.0975** | 0.30375 |
| value against `Greedy` | 0.0325 | 0.15375 | 0.03375 |
| clone against `Greedy` | 0.725 | 0.79875 | **0.82125** |
| clone against the champion `ci-69` | 0.5 (itself) | **+0.53625** | **-0.42375** |

**The spread from the seed alone is 56 points** on the first row. Every lambda, baseline and teacher effect
this project has measured is smaller than that, and each was measured on a single seed. `ci-88` beating
`search-4` — reported as the first agent ever to do it — is inside this spread and is not a result.

The clone rows are worse, because they disagree with each other. `ci-91`'s clone beats `Greedy` **0.82125**,
better than any policy the loop has produced, and loses to the committed champion `ci-69` at **0.42375**,
where `ci-69` itself beats `Greedy` only 0.725. A third-party score does not order two players, and the
champion bar caught it — for the second time in one night.

## Decision

**A number from one seed is a sample, and the loop must stop reporting it as a measurement.** Concretely:
`iterate.sh` and the workflow gain a way to run the same configuration over **at least three dataset seeds**
and report each win rate as a spread across them, not as a point. The commit gate reads the spread: a policy
clears a bar when it clears it on every seed, not when one seed clears it. The journal reports ranges.

Until that exists, every existing entry that compares two configurations measured on one seed each is to be
read as a sample, and this ADR is linked from the ones that matter.

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

- `scripts/iterate.sh`: a `--seeds` that takes a list, and a report that aggregates across them.
- `.github/workflows/iterate.yml`: the `cleared` step reads a per-seed result and requires every seed.
- `learning/experiments/next.json`: `seed` becomes a list.
- `AGENTS.md` and `docs/learning/training.md`: the convention, so a turn is not proposed on one seed again.
- The journal entries of 2026-09-15 and 2026-09-16 link here. ADR 0048 cannot be edited; the correction to
  its win-rate claim lives in the Consequences above and in the journal.
