# Learning journal

One entry per change that moves a number: content, engine, agents, or the benchmark seeds. Each entry names
the run stamps involved so that any two results can be compared on one axis at a time (ADR 0013). Newest
first.

## 2026-09-09. First full turn of the loop (`scripts/iterate.sh`), run "premier"

- **What changed**: nothing in the engine or the content on purpose; this is the first end-to-end run of the
  L7 loop, on content `34c616d3…80d7` (the digest already committed), engine `d3f1fe3284bc-dirty` (the L7
  pull request's head at the time, `#23`, with local uncommitted state, so read this as a smoke test of the
  loop rather than a citable baseline; a clean re-run on the merged engine is the number to keep). 200
  matches of `Greedy` self-play recorded (`simulate --record`, base seed 1), a `train-value` and a
  `train-clone` policy trained on them, both evaluated against `Greedy` and `Random` on the 200 benchmark
  seeds, mirrored (seed set `733404048`).
- **Baselines** (match the committed digest and the L5/L6 journal entries, as expected since content and
  agents did not change): `Greedy` vs `Greedy` 39.25% each, 54.5% player 1 share, 21.5% draws, 22.0 rounds;
  `Greedy` vs `Random` and `Random` vs `Random` unchanged from before.
- **Clone policy**: 38.0% against `Greedy` (interval 34.7% to 41.3%, score 0.475) — statistically the same as
  `Greedy` playing itself (39.25%, interval 36.4% to 42.1%). This is the ceiling behaviour cloning is supposed
  to reach (`docs/learning/explained.md`: "the clone can never be better than what it copies") and it reached
  it: on 200 matches of `Greedy` self-play, the clone reproduced `Greedy`'s own strength almost exactly. 100%
  against `Random`.
- **Value policy — a red flag, not yet a conclusion**: 0.0% against `Greedy` (0 wins, 0 draws, 400 matches),
  but 99.5% against `Random`. A policy that loses every single mirrored match to a deterministic opponent
  while still beating a uniform one plays coherently (its fizzle rate is 0.08%, far below every other agent's
  4-5%, so it is not fizzling its way to a loss) but consistently picks the wrong action once the opponent
  fights back. The likely cause: `train-value` gives a rare action key (most `targets:...` keys, since target
  combinations are numerous) its own sample mean as a score once it has been seen at all — one lucky win on
  200 matches can give a bad move a high score with no dataset large enough yet to average it out. Two ways
  to check: raise `--min-samples` on `train-value` (fewer, better-supported action keys, more falling back to
  the global mean) or record a bigger dataset before retraining; a trace of one losing match would also show
  whether the policy repeats one specific bad move.
- **Why it matters**: the loop runs end to end, produces a report, and the two learners already diverge in an
  interesting way — cloning is a safe, boring reproduction of `Greedy`, value regression is more ambitious
  and currently exploitable. Neither policy is committed under `models/` yet: only `report.json` was shared
  here, not the trained files, and a citable number needs a clean (non-dirty) engine commit. Next: re-run on
  merged `main` with a larger dataset and a higher `--min-samples`, then commit whichever policy is worth
  keeping with its evaluation.

## 2026-09-08. Greedy against Random, the first measured gap

- **What changed**: nothing; this is the first measurement across two agents on the same engine and content,
  run by the `Evaluate` workflow (engine `d25c67db04d1`, the merge commit of the L5 pull request, content
  `34c616d3…80d7`, schema `features:v1+31987e1de3a9`, the 200 benchmark seeds, stamped as seed set
  `733404048`), `Greedy` against `Random`, mirrored. The `Seed` the command prints at start is the session
  seed of the interactive commands, drawn at random when `--seed` is absent; an evaluation seeds every match
  from the seed file and never uses it.
- **Numbers**: 400 matches, Greedy wins 400 (100.0%, interval 100.0% to 100.0%), score 1.000, 24.5 health
  left on average, no draw, every match by elimination in 15.5 rounds on average, none by the round cap.
  Intent entropy 0.20 bits for Greedy against 4.07 for Random; fizzles 4.0% against 1.6%; crits 4.9% against
  5.2%. Greedy casts `spell:heavy_strike:v1` 17914 times and `spell:basic_attack:v1` 582 times; Random
  spreads its 12000 or so casts over all 36 spells, `spell:heavy_strike:v1`, `spell:wait:v1` and
  `spell:basic_attack:v1` leading at about 1850 each.
- **Why it matters**: the one-step lookahead beats a uniform policy without losing a match, so the baseline
  is a real bar for the learned agents (L6, L7) rather than a coin flip. The gap is so wide that it says
  little about the content: a stronger opponent than Random is needed to grade the heuristic itself, which
  is what the seed pairs against a tuned heuristic will provide. Greedy's higher fizzle rate most likely
  comes from its focus: several creatures aim at the same enemy, the first kill leaves the later actions
  without a target, and they fizzle. Random spreads its targets and rarely loses one.

## 2026-09-08. Greedy replaces Random as the benchmark baseline

- **What changed**: nothing in the engine or the content; the greedy and heuristic agents arrived (learning
  phase L5) and the benchmark now plays `Greedy` against `Greedy`, so the digest is regenerated once here.
  The previous Random digest for the same content hash is superseded, not comparable.
- **Digest**: `benchmarks/34c616d340ed0d4ac67b0874ce3ac1895c126509b94ebe2a9963dfff45af80d7.json`, the same
  content hash as before, played by `Greedy` against `Greedy` on the 200 benchmark seeds, mirrored, under the
  default rule set. Generated by CI on the L5 pull request (engine `24e35df135f7`).
- **Numbers**: 400 matches, player 1 wins 218, player 2 wins 96, 86 draws by mutual elimination, every match
  by elimination between rounds 19 and 23 (22.0 on average, none by the round cap). Per agent: 157 wins,
  39.3% win rate (36.4% to 42.1%), score 0.500, intent entropy 0.23 bits, 5.1% fizzles, 4.9% crits.
  `spell:heavy_strike:v1` is cast 15039 times against 578 `spell:basic_attack:v1`; no other spell is used.
- **Why it matters**: two identical greedy agents give player 1 a 54.5% win share against 24.0%, far outside
  the noise of 400 matches, where Random against Random showed no edge. The first mover wins the damage race
  when both sides play the same deterministic plan; a rule change that tempers it (initiative, pick order)
  now has a number to move. The low entropy and the two-spell repertoire say the current content offers the
  greedy scorer little to choose from. Greedy against Random is measured by the `Evaluate` workflow, not by
  this digest.

## 2026-09-08. First benchmark digest

- **What changed**: nothing in the engine or the content; the evaluation harness and the benchmark digest
  arrived (learning phase L4).
- **Digest**: `benchmarks/34c616d340ed0d4ac67b0874ce3ac1895c126509b94ebe2a9963dfff45af80d7.json`, the
  content of `data/` at that hash, played by `Random` against `Random` on the 200 benchmark seeds, mirrored,
  under the default rule set (team of three, two energy and two picks per round, thirty rounds, double damage
  on a crit). Generated by CI on the merge commit of the L4 pull request (engine `0554a2aa8fe3`).
- **Numbers**: 400 matches, player 1 wins 206, player 2 wins 194, no draw, every match by elimination.
  Read as a first-mover check: 51.5% for player 1 with two identical random agents, inside the noise of 400
  matches, and the mirrored pairs cancel it in every evaluation anyway.
- **Why it matters**: from this commit, any engine or content change that alters an outcome on these seeds
  fails CI until the digest is regenerated here on purpose. The greedy agent (L5) will replace the random
  baseline and regenerate it once.
