# 0032. Measure the initiative weight, and move it from 0.5 to 2.1

Date: 2026-09-13
Status: Accepted

## Context

[ADR 0018](0018-price-initiative-in-the-agent-weights.md) gave the scoring weights an `initiative` term and
set it to **0.5** by reasoning — "the same as `buff`, on the reasoning that initiative is indirect in the way
defense is" — and said so in its own consequences: "0.5 is a guess, and the weight `search-weights` has the
least evidence about." Nothing has measured it since. `search-weights` moves all nine together, so no run has
ever isolated it, and `docs/learning/agents.md` has carried the admission in its table for four ADRs.

Two readings then pointed at it from opposite directions. `player1WinShare` — how often the first side wins
with both sides played equally well — has sat at **0.64** against a band of 0.45..0.55, the objective's
second-worst target, in a game that lasts under seven rounds. And the entry before this one measured a
line-wide `InitiativeDebuff` opener that took that reading to 0.575, the only thing this pass has found that
moves it, but that Greedy declared 86 times and won 0.279 with. A baseline that both suffers from tempo and
misbuys it is a baseline whose tempo price is wrong in some direction, and no content decision resting on that
price can be trusted until it is measured.

## Decision

We will move the `initiative` weight from 0.5 to **2.1**, chosen by sweeping it alone on fixed content
(`74f02621`, 400 benchmark seeds, all four evaluations). `explore:` wraps the compiled `GreedyAgent` rather
than reading a weights file, so the sweep patches `ScoringWeights.Default` and rebuilds at each point, which
is exactly what shipping the value does.

The play does not move smoothly with the price, for the reason [ADR 0028](0028-name-the-defense-weight-and-move-its-price-one-step-up.md)
gives: the agents take an argmax, so a reading changes only when an ordering flips.

| `initiative` | objective | rounds | entropy | `player1WinShare` | `tierWinSpread` | exploiter's win | `throwing_star` |
| --- | --- | --- | --- | --- | --- | --- | --- |
| **0.5 (before)** | 36.66 | 6.32 | 2.703 | 0.645 | 0.452 | 0.390 | 0.9 % |
| 1.0 | 32.99 | 5.99 | 2.414 | 0.610 | 0.500 | 0.338 | 0.3 % |
| 1.5 | 35.19 | 6.26 | 2.493 | 0.610 | 0.519 | 0.347 | 1.5 % |
| 1.75 | 53.37 | 7.08 | 2.787 | 0.485 | 0.795 | 0.338 | 26.5 % |
| 1.9 | 18.51 | 6.90 | 2.773 | 0.495 | 0.439 | 0.168 | 29.6 % |
| 2.0 | 12.22 | 6.91 | 2.751 | 0.490 | 0.292 | 0.170 | 30.0 % |
| **2.1** | **12.95** | **6.86** | **2.743** | **0.500** | **0.292** | **0.177** | 30.8 % |
| 2.2 | 12.99 | 6.87 | 2.727 | 0.505 | 0.273 | 0.177 | 31.2 % |
| 2.25 | 19.58 | 6.76 | 2.736 | 0.560 | 0.418 | 0.212 | 30.5 % |
| 2.5 | 21.40 | 6.75 | 2.708 | 0.585 | 0.417 | 0.215 | 30.4 % |
| 3.0 | 25.49 | 7.54 | 2.584 | 0.615 | 0.455 | 0.443 | 30.1 % |
| 3.5 | 137.05 | 7.88 | 2.541 | 0.730 | 0.750 | 0.795 | 40.1 % |

2.0, 2.1 and 2.2 read within 0.8 of each other on the objective and within 0.02 on every column; 1.9 and 2.25
are its edges. 2.1 is the middle of that step rather than its edge, so the choice is robust to a small error
in it — the same test 0.65 had to pass in ADR 0028. It is also confirmed by a reading the objective does not
contain: `exploit`, the searched agent `search-2` against this baseline, falls from **0.390 to 0.177**. The
baseline gets harder to exploit by an agent that was searched against the old one, which is not something a
metric artifact does.

## Consequences

- Good: **`player1WinShare` lands at 0.500**, dead centre of its band, for the first time in this project.
  Going first stops deciding the match. The lever is real and it is the one ADR 0018 put there: the bot buys
  base initiative at unlock, and that compresses the tempo gap it was losing to.
- Good: the baseline plays better, checked rather than assumed — the exploiter's edge more than halves, the
  fizzle rate falls 0.211 to 0.169, and `skill` holds at 0.998 against random.
- Good: the objective goes **36.66 to 12.95** on unchanged content, the largest single move this pass. Casts
  spread: `spellUsageShare` 0.301 to 0.253 and `tierUsageShare` 0.733 to 0.669, both their best readings yet,
  and `protective_slam` goes 1.6 % to 7.0 % — the debuff half of the weight moving with the unlock half.
- Bad: **five spells go from cast to barely cast**, `spellsBarelyCast` 0 to 5. `pummel` 5.0 % to 0.0 %,
  `noxious_cure` 0.6 % to 0.0 %, `summon_minions` 0.7 % to 0.0 %, `full_plate` and `healing_screech` to
  under 0.2 %. This is a bill the next content pass inherits, exactly as ADR 0028 left one.
- Bad: **`throwing_star` takes 30.8 % of the mirror's casts**, up from 0.9 %. It is one of three enabled spells
  with a Spell initiative other than 1, and at this price +3 permanent initiative for 1 energy is the best
  unlock in the catalogue. Defensible in a game the first mover was winning 64 % of, but it is one spell's
  stat carrying a weight decision, and it will not survive a content pass that gives other spells a tempo.
- Bad: **the weight is nearly a flat bonus.** Fifteen of the eighteen enabled spells have Spell initiative 1,
  so the unlock term mostly cancels out and what this value really re-prices is `throwing_star` (3),
  `momentum` (3) and `pummel` (0), plus every `InitiativeDebuff`. `pummel` dying is the same fact read from
  the other side: at 2.1 its initiative of 0 is a 2.1-point penalty against every rival. The open question in
  `docs/domain/spells.md` — whether those 1-to-3 numbers were ever chosen for this job — is now load-bearing.
- Bad: **the benchmark digest moves** and the agent fingerprint with it, so no run stamped before this compares
  term by term with one after it. `Greedy` is the baseline every learned agent is measured against.
- Bad: `knobs.py` reads `learning/weights/greedy.json`, so every `cast_value` carrying an `InitiativeDebuff`
  moves and **the `check-knobs` findings go from four to eight**. `protective_slam` reads 7.33 a round to
  **13.73** and now outclasses `enraged_charge`, `meteor` and `parasite_jab` on paper, and `summon_minions`
  outclasses `noxious_cure`. The paper reading and the play disagree — `protective_slam` takes 7.0 % of the
  mirror's casts against `meteor`'s 10.8 % — because `cast_value` prices a full board of targets at full
  health while `Expected` prices the board in front of it. The four new findings are a bounds problem for the
  next content pass, not a defect in this decision, but they are four more than there were.
- Neutral: `models/` policies were cloned from datasets a 0.5 baseline generated. They still load — the
  feature schema is unchanged — but their reported win rates are against an agent that no longer exists.

## Alternatives considered

- **Leave it at 0.5.** It is the value nothing chose, and ADR 0018 said as much when it set it. It also leaves
  `player1WinShare` at 0.645 and makes every tempo spell either dead or a trap. Keeping it needed an argument
  as much as moving it did, and the sweep is what that argument has to beat.
- **Lower it instead.** The entry before this one guessed the weight was too *high*, because Greedy over-bought
  a tempo opener and lost with it. The sweep says the opposite: 0 and 0.1 read 50.98 and 40.94 with
  `player1WinShare` at 0.705 and 0.680, the worst in the sweep. The bot was losing to tempo because it would
  not buy it, and buying one bad tempo spell is what that looks like from inside a wrong price.
- **2.0, the round number and the best single reading.** 0.7 better on the objective, and 0.1 from the 1.9
  edge instead of in the middle. The step is worth more than the decimal.
- **3.0 or above.** Every column turns: the exploiter's edge goes back to 0.443 and then 0.795, and
  `player1WinShare` returns to 0.73. Past the step the bot buys tempo it cannot spend.
- **Let `search-weights` find it.** It moves all nine at once, so what it returns for this term is confounded
  with eight others — which is why four years of runs have left this weight unmeasured. A sweep of one weight
  is the thing a search cannot produce.

## Follow-up

- `src/DownfallArena.Application/Agents/ScoringWeights.cs`: the default and the comment that explains it.
- `learning/weights/greedy.json` and `learning/src/downfall_learning/export.py`'s `DEFAULT_WEIGHTS`.
- `docs/learning/agents.md`: the built-in weights table, whose row for this term records that it was a guess.
- `docs/domain/spells.md`: the open question that says the weight is set on reasoning alone.
- `data/balance/knobs.json`: `protective_slam`'s note, which declines a debuff-heavy shape because the weight
  behind it was unmeasured.
- The benchmark digest for content `74f02621`, regenerated and verified.
- A content pass against this baseline: five spells it no longer casts, and `throwing_star`'s 30 % share.
