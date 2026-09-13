# 0037. Measure the energy weight, and move it from 0.2 to 0.3

Date: 2026-09-13
Status: Accepted

## Context

`energy` is the last weight in the table that was hand-set and never re-read. It was set to **0.2** in phase
L5, when it priced exactly one thing — the energy an actor keeps after paying a cost — and `agents.md` still
describes it in the words of that job: "enough to break a tie towards the cheaper spell, not enough to make
the bot hoard". Three decisions have since given it three more jobs without re-measuring it:
[ADR 0020](0020-energy-regeneration-and-the-price-of-energy.md) made it price energy *handed out* and
`EnergyRegeneration` over its rounds, [ADR 0026](0026-price-what-a-cast-costs-and-how-long-it-lasts.md) made
it price the part of an unlock's cost the actor cannot cover, and
[ADR 0035](0035-lowering-defense-and-taking-energy.md) made it price an `EnergyDrain` taken off an enemy. A
tie-breaker became four readings, and the number behind them is the one nobody chose.

The class-by-class content pass has also replaced the catalogue since
[ADR 0032](0032-measure-the-initiative-weight.md) measured `initiative` on content `74f02621`. Both weights
therefore need reading on the content as it now stands (`91da955c`), and the same sweep does both.

## Decision

We will move `energy` from 0.2 to **0.3**, and we will **leave `initiative` at 2.1**. Both were swept alone on
fixed content (`91da955c`, 400 benchmark seeds, all four evaluations), by patching `ScoringWeights.Default`
and rebuilding at each point — `greedy` and `explore:` compile the weights in, so a weights file cannot reach
them. The method is now `scripts/sweep-weight.py` rather than a hand-run, and it refuses to start over a
patched file, because two sweeps running together restore each other's patch and silently measure a weight
they did not set. The play moves in steps rather than smoothly, for the reason
[ADR 0028](0028-name-the-defense-weight-and-move-its-price-one-step-up.md) gives: the agents take an argmax,
so a reading changes only when an ordering flips.

| `energy` | objective | rounds | entropy | `player1WinShare` | `tierUsageShare` | `skill` | `exploit` |
| --- | --- | --- | --- | --- | --- | --- | --- |
| 0.0 | 100.09 | 7.59 | 2.619 | 0.720 | 0.794 | 0.985 | 0.335 |
| 0.1 | 61.40 | 6.36 | 3.249 | 0.620 | 0.700 | 0.988 | 0.212 |
| **0.2 (before)** | 54.34 | 7.16 | 3.437 | 0.575 | 0.692 | 0.990 | 0.195 |
| **0.3** | **49.32** | **7.80** | **3.530** | **0.510** | **0.688** | **0.985** | **0.198** |
| 0.4 | 50.12 | 7.27 | 2.955 | 0.525 | 0.672 | 0.943 | 0.233 |
| 0.5 | 108.33 | 8.22 | 2.588 | 0.535 | 0.739 | 0.905 | 0.790 |
| 0.6 | 73.12 | 8.31 | 2.626 | 0.520 | 0.739 | 0.915 | 0.708 |
| 0.8 | 63.26 | 13.10 | 2.474 | 0.535 | 0.772 | 0.800 | 0.670 |
| 1.0 | 81.40 | 12.42 | 2.428 | 0.530 | 0.778 | 0.802 | 0.710 |

0.2, 0.3 and 0.4 read within 5.1 of each other; 0.1 and 0.5 are the edges, and the right edge breaks hard rather
than sloping. **0.3 is the middle of that step rather than one of its ends** — the same test 0.65 had to pass
in ADR 0028 and 2.1 in ADR 0032 — and it is also the best single reading.

`initiative` was swept over eleven points on the same content and **2.1 is again the best (54.34)**, so the
value ADR 0032 chose survives a catalogue that has been rewritten class by class underneath it. It is kept.

## Consequences

- Good: **`player1WinShare` goes 0.575 to 0.510**, from outside its 0.45..0.55 band to its centre. This is the
  heaviest target in the objective (weight 3) and the reading ADR 0032 was chasing; the content pass had
  pushed it back out and the energy price pulls it back in.
- Good: the objective goes 54.34 to **49.32** on unchanged content, and `spellEntropyA` reaches **3.530**, the
  highest reading in either sweep. Casts spread rather than concentrate.
- Good: **the weight was never the tie-breaker it was documented as.** At 0.0 the first mover wins 0.720 of the
  mirror and the objective reads 100.09, the worst point of the sweep below 0.5. A term that decides a fifth of
  `player1WinShare` between 0.0 and 0.3 was mis-described, not mis-set, and that description is now corrected.
- Bad: **it does not make the agent play better, and the ADR must not be read as saying so.** The independent
  check — `heuristic:` at 0.3 against the compiled `greedy` at 0.2, each seed played from both sides — is a
  **dead heat: 0.505, CI [0.481, 0.529]**, against a same-weights control that reads 0.495. `exploit` is flat
  too (0.195 to 0.198). ADR 0032 could show its baseline getting harder to exploit; this one cannot. What 0.3
  buys is what two equally strong bots make of the content, not how strong they are.
- Bad: **`spellsNeverCast` goes 1 to 2**, at the target's limit rather than past it. One more spell the
  argmax never reaches, and a bill the next content pass inherits.
- Bad: **the benchmark digest moves** and the agent fingerprint with it, so no run stamped before this
  compares term by term with one after it.
- Neutral: `knobs.py` reads `learning/weights/greedy.json`, so `cast_value` moves for the four spells carrying
  an energy effect. **The `check-knobs` findings stay at ten**: no finding is cleared and none is added, two
  ceilings rise (`momentum` 1.60 to 2.40, `restorative_burst` 3.80 to 4.10) and neither rise is enough to make
  either spell a choice. ADR 0032 doubled the findings; this one moves two numbers.
- Neutral: `initiative` is unchanged, so nothing priced by it moves.
- Neutral: [ADR 0036](0036-raising-initiative-the-mirror-that-was-left-out.md) reasons about Death Squad's
  discarded substitution at "0.2 a point". That sentence stays as written — an accepted ADR is not edited —
  and the conclusion it supports is unaffected: at 0.3 the substitution would have read 0.90 against a
  tier-3 band of 8 to 14, the same dead spell. `docs/domain/spells.md` and `data/balance/knobs.json` carry
  the current price, since they describe what is true now rather than what was decided when.

## Alternatives considered

- **0.4, the other half of the step.** It reads 50.12, within 0.8 of 0.3, and loses on every column that is not
  the objective: `skill` falls 0.985 to 0.943, `spellEntropyA` 3.530 to 2.955, `spellsBarelyCast` 2 to 4. Past
  the step the matches stop ending: 0.8 and 1.0 run 13.10 and 12.42 rounds. The step is not homogeneous and
  its right half is the bad half.
- **Leave it at 0.2.** Defensible — it is inside the step, and the head-to-head says the agent is no better at
  0.3. It loses on the one reading that is not about agent strength: at 0.2 going first wins 0.575 of the
  mirror, outside the band, and that is what the objective is for.
- **Move `initiative` too.** The sweep does not support it. 2.1 is the best point, but it is a **narrow
  minimum rather than a flat step**: 1.95 reads 62.51 and 2.25 reads 60.01, both worse by 6 to 8. What makes
  keeping it safe is the wider plateau 2.1..2.55 (54 to 60, against 62 to 70 on either side), inside which 2.1
  is best on the objective, on `skill` and on `exploit` at once. That is weaker evidence than ADR 0032 had —
  it found three points within 0.8 of each other — and it is recorded here rather than in that ADR, which is
  accepted and stays as written.
- **Sweep both weights together.** Two weights make a surface, and every point of it costs a rebuild and four
  400-seed evaluations. The one-at-a-time sweep is what the two previous weight ADRs did and what makes their
  results comparable; a joint search is `search-weights`, which moves all nine and therefore isolates none.
- **Let `search-weights` find it.** Same reason ADR 0032 gave: it confounds this term with eight others, which
  is why the weight went four ADRs without being read.

## Follow-up

- `src/DownfallArena.Application/Agents/ScoringWeights.cs`: the default and the paragraph that explains it.
- `learning/weights/greedy.json` and `learning/src/downfall_learning/export.py`'s `DEFAULT_WEIGHTS`.
- `docs/learning/agents.md`: the `energy` row, and the provenance section that said none of the nine was
  measured.
- The benchmark digest for content `91da955c`, regenerated and verified.
- `models/` when a policy is next trained: the existing ones load, but their win rates are against a baseline
  that no longer exists.
- The six weights still on their phase-L5 values: `damage` is the unit, and `kill`, `heal`, `stun`, `bleed`
  and `risk` have never been swept.
