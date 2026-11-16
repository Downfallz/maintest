# 2026-10-01. Tuning run 17 runs out of its six hours inside the opening sweep, so the sweep is played in slices

- **What happened.** Run 17 (`main` at `9aeecff`, 2 rounds of 6) started at 18:10 UTC and was still in its
  opening sweep at 5h45, with a 360-minute ceiling. The sweep plays one candidate per legal single step of
  every playable knob. On content `9659f610` that is 175 knobs and **263 candidates**; run 15 had 144 knobs
  and 225. A candidate now costs about **88 seconds** on the runner: run 16 played 20 in 29m13s, and run 15
  averaged about 65. The nine tier-2 spells of #245 give every decision more options, and the panel swapped
  stun-first for search-31. So the sweep alone is about **6h26**, more than a job gets, and the leader is
  only written after it. Locally the eight evaluations of one candidate take 57 seconds, and 41 of them are
  the five exploit sets.
- **What changed.** `tune.yml` now plays the sweep in a `sweep` job of six slices side by side. Each slice
  runs `tune-content --sweep-shard I/6` on about 44 candidates, roughly 65 minutes, and uploads what it
  measured. The search job fetches the six slices and runs `tune-content --swept ...`, and its memo takes
  their metrics instead of playing them. The engine is deterministic, so the proposal is the one a single
  job would have reached. A test checks that a search handed its sweep plays none of it again and keeps the
  same candidates and leader. The search refuses slices from another objective or content, and a set that
  is not the whole sweep. The search job then holds the catalogue, the rounds and the confirmations, about
  1h15 at the defaults. No number moves.
- **Not measured yet.** The 65 minutes a slice is the estimate above, not a run. The next tuning pass is
  the measurement.
