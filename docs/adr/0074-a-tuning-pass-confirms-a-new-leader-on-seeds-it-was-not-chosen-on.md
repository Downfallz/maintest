# 0074. A tuning pass confirms a new leader on seeds it was not chosen on

Date: 2026-09-24

Status: Accepted

## Context

A tuning pass (ADR 0021) hill-climbs the balance knobs. It plays every candidate of a round on the benchmark
seeds and keeps the one with the lowest objective if it beats the leader. The objective is noisy on 200 seeds,
and the pass is built to find the lowest reading among many candidates.

Measured on the 30-health content (journal, 2026-09-23), the same catalogue on disjoint seed blocks:

| seeds per block | blocks | standard deviation of the objective |
| --- | --- | --- |
| 200 | 8 | 2.62 |
| 400 | 4 | 1.74 |
| 800 | 2 | 1.48 |

Three single moves of the first 30-health pass, each played on the same blocks as the content it came from.
The table gives the move's objective minus the content's, block by block (journal, 2026-09-24):

| move | on four blocks of 200 | on two blocks of 800 |
| --- | --- | --- |
| `basic_attack` damage 2 to 1 | -0.64, -0.28, -0.30, +1.33 | +3.34, +1.39 |
| `ironbound` initiative bonus 1 to 0 | -0.17, +0.29, +0.07, +0.62 | -1.97, -0.64 |
| `pummel` cost 1 to 2 | +2.49, -0.90, +1.96, -2.28 | +0.60, -1.32 |

Sharing the seeds cancels little of the noise: one step sends the matches down other paths.

- The `basic_attack` move reads as neutral at 200 seeds (+0.03 on average) and is 2.4 worse at 800.
- The `pummel` move swings 2.5 either way from one block to the next.

A round of four to six neighbours, and an opening of a few hundred, will almost always hold a candidate whose
reading is two points better than the catalogue really is. The pass keeps that candidate, and each gain builds
on the last. That pass read 8.35 to 2.39 on its own seeds. With one of its seven moves dropped, it read 12.50
on seeds it never saw, where the content as it stands reads 10.76.

## Decision

**A candidate that beats the leader on the search seeds takes the lead only if it also beats the leader on a
second, disjoint seed block.** That block is the objective's `confirmSeeds`:
`benchmarks/confirmation-seeds.json`, 400 seeds from 2000000. They are clear of the benchmark seeds and of the
200 above them, where the workflow's hold-out replays.

- The content as it stands is played on the confirmation seeds once.
- After the opening pass and after each round, only the best candidate of that batch is put to the
  confirmation seeds, and only if it beats the leader on the search seeds.
- A challenger the confirmation seeds do not back is set aside. The runner-up does not take its place, because
  the runner-up was not tested either.
- The search seeds still choose every candidate. The confirmation seeds only decide whether the choice stands.
- A check refuses a confirmation file that is missing or that shares a seed with the search seeds.
- `tune-content --no-confirm` runs the pass as before, and `--confirm-seeds` names another file.

## Consequences

- Good: a gain has to show up on matches the choice never saw. Only the challenger is tested, once per batch,
  so the confirmation carries no selection bias of its own.
- Good: the report and `tune.json` list every challenger with both readings and whether it took the lead.
  A reader can see how many gains were noise.
- Bad: each confirmation costs about two candidates, since it plays 400 seeds. A pass of six rounds adds seven
  confirmations and the content's own reading, about 16 candidates' worth, or roughly 16 minutes on the
  runner's 61.7 seconds a candidate.
- Bad: a real gain smaller than the noise of the 400-seed block can be set aside. That is what the confirmation
  is for.
- Neutral: a candidate's score is still its reading on the search seeds, and the objective's fingerprint does
  not include the confirmation seeds. Scores stay comparable with runs from before this ADR.
- Neutral: a move that changes nothing passes about half of its confirmations, by chance. It costs nothing in
  expectation, and the workflow's hold-out still replays the final proposal against the content it replaced.

## Alternatives considered

- **Play every candidate on more seeds.** 800 seeds cut the objective's noise from 2.62 to 1.48, not in half,
  and quadruple the cost of every candidate, including a sweep that already takes four hours.
- **Require a margin on the search seeds.** The bias comes from choosing the minimum. A margin shrinks it
  without removing it, and any fixed size is a guess.
- **Test every candidate of a round on the confirmation seeds.** That costs a whole round again, and choosing
  the best of those readings would bring the bias back on the second block.

## Follow-up

- `tune_content.tune_content`, `_Confirmer`, `TuneOptions.confirm`; `knobs.Objective.confirm_seeds` and its
  check; `tune-content --confirm-seeds/--no-confirm`; `data/balance/knobs.json`, `data/balance/README.md`.
