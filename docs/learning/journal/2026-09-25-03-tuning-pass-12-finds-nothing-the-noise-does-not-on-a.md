# 2026-09-25. Tuning pass 12 finds nothing the noise does not, on a catalogue that had already moved

- **What ran.** #216, [workflow run 12](https://github.com/Downfallz/maintest/actions/runs/36063145023):
  `tune-content --seed 1 --iterations 2 --neighbours 6 --max-changes 12 --pair-depth 2`, no pairs, on content
  `0f036b75`. That is the catalogue before Momentum changed (ADR 0078). It played 236 versions in 4h06: 1848
  evaluations, and 32 on the confirmation seeds.
- **What it moved.** `tranquilizer_dart` stun duration 2 to 1, and `death_squad` cost 2 to 3. The objective went
  from 2.277 to 2.222 on its own seeds, and from 2.926 to 2.738 on the confirmation seeds. The confirmation
  set aside the opening pass's best, which read 1.64 on the search seeds and 3.263 on the confirmation seeds.
  It also read 4.051 to 3.441 on the workflow's hold-out.
- **On the catalogue as it is now.** The two moves were replayed on `813bb91b`, with Momentum a free strike,
  using `score-content`:

  | seeds | `813bb91b` | with the two moves |
  | --- | --- | --- |
  | 200 from 995317 | 4.132 | **4.719** |
  | 800 from 3000000 | 5.191 | **3.951** |

  One block says worse and the other better. The better one is mostly `tierUsageShare`, which moved 0.96 to
  0.12 on a gain of 0.06 on the seeds the pass chose on.
- **Verdict.** Not applied, and #216 is closed. The gain the pass found is a tenth of the objective's noise.
  The two readings on the current catalogue disagree in sign. Its digest was written for a content hash that
  `main` no longer builds. The confirmation seeds did their job: they caught the one challenger that would
  have been a large false gain. The next pass runs on `813bb91b`.
