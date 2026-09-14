# 0040. Remove the fizzle weight

Date: 2026-09-14
Status: Accepted

## Context

Four measurements, across three ADRs, all said the same thing about this weight.

[ADR 0037](0037-measure-the-energy-weight-and-move-it-from-0-2-to-0-3.md) swept it — then called `risk` —
from 0 to 100 and found every value plays the 400 benchmark seeds **identically, column for column**. Setting
it to zero changed not one match. Its only two outcomes were selected by whether the value was a multiple of
three, which is a floating-point tie-break and not an effect.

[ADR 0038](0038-a-wasted-action-is-a-fizzle-whatever-wasted-it.md) renamed it, on the reasoning that the term
was fine and only its name promised something it did not deliver. It also found a third reader nobody had
noticed, on the decision path, which still changed nothing.

[ADR 0039](0039-the-bot-binds-its-targets-on-a-board-that-has-not-happened-yet.md) gave it a real decision to
reach: the agent now writes off a target it expects to be dead before its action lands. A fresh sweep at 0, 1,
2, 3 and 5 read 78.44, 77.84, 77.25, 79.52 and 77.25 — a spread of 2.3, and not monotonic. The fix works by
scoring a doomed target at **nothing**, not by charging this weight for it.

A weight that prices nothing, measured four times, is not a weight.

## Decision

We will **remove `fizzle`**, leaving eight. Its three readers are replaced by nothing rather than by
zero-valued arithmetic:

- A **fizzled resolution scores 0**. It still loses to any action that does something, which is the whole of
  what the weight was there for.
- The **wasted-share penalty goes**. ADR 0039 already prices that waste the honest way — as the value the
  action no longer earns — and charging for it twice was the double-count this ADR removes.
- A **castable spell with no legal target scores 0** instead of `-fizzle`, in `HeuristicAgent.DecideIntent`.
  This is the one site where removal is not merely equivalent, and it is **better**: the bot now prefers doing
  nothing to an action whose expected score is negative, where before a spell that hurt an ally could beat a
  spell with nothing to hit.

Measured on content `91da955c`: the objective reads **85.68 against 83.77** with the weight kept at 2.0 —
1.9 points, inside the 2.3 spread that same weight showed across 0 to 5, so **indistinguishable from any
value it could have had**. `spellsNeverCast` goes 2 to 1 and `spellsBarelyCast` 3 to 2; every other column is
unchanged to three decimals.

## Consequences

- Good: the table holds eight weights that each price something, and the one that priced nothing is gone
  rather than parked at a number chosen because no number was better.
- Good: **the no-target case improves**, as above. It is a behaviour change, small, and argued for rather
  than inherited.
- Bad: **the fingerprint moves `1933f3ae` to `362b0496`** — the first time because the list got *shorter*
  rather than because a number moved. Every stamp written before this names a set of weights that no longer
  exists.
- Bad: **a weights file carrying `"fizzle"` now fails to load**, loudly on both sides:
  `JsonScoringWeightsSource` sets `UnmappedMemberHandling.Disallow` and `export.py` refuses a name outside
  `WEIGHT_NAMES`. `greedy.json` and `search-2.json` are converted here; any file outside the repository needs
  the same one-line edit.
- Bad: **the benchmark digest moves**, for 1.9 points of objective and nothing else measurable.
- Neutral: `search-weights` now searches eight dimensions instead of nine, which is strictly less work for the
  same result.
- Neutral: ADRs 0037, 0038 and 0039 describe a weight that no longer exists. They are accepted and stay as
  written; this ADR is the link forward.

## Alternatives considered

- **Keep it at 2.0 and note that it does nothing.** What ADR 0039 did, and the note was already written. It
  leaves a dimension in the search space, a box in the studio, a key in every weights file and a number the
  next person to tune will assume means something — for a term measured four times to mean nothing.
- **Keep the term and fix the pricing instead**, so that it charges for waste rather than merely failing to
  reward it. That is the double-count: ADR 0039's reading already removes the value a wasted target would
  have earned, and a penalty on top would price the same loss twice.
- **Keep it for the no-target case alone**, its one live reader. That case is better served by scoring zero,
  as the Decision says, so keeping the weight would preserve the worse behaviour in order to preserve the
  weight.
- **Set it to zero without removing it.** Identical in play and worse in every other way: the dimension, the
  box, the key and the misleading number all remain, and nothing records why the zero is there.

## Follow-up

- `ScoringWeights` (the record parameter, `Named`, the doc comment), `ActionScorer` (the fizzled branch and
  the wasted-share term), `HeuristicAgent.DecideIntent`, `JsonScoringWeightsSource` and its `WeightsFile`.
- `learning/weights/greedy.json`, `learning/weights/search-2.json`, `export.py`'s `WEIGHT_NAMES` and
  `DEFAULT_WEIGHTS`.
- `docs/learning/agents.md`, `docs/domain/glossary.md`, `docs/learning-roadmap.md`,
  `docs/learning/explained.md`.
- The benchmark digest for content `91da955c`, regenerated.
- `models/` when a policy is next trained: they load, but their win rates are against an agent scored on a
  weight set that no longer exists.
- The two things ADR 0039 left open are untouched by this: `Combat.NotEnoughEnergy` rising, and the actor
  stunned between declaring and acting.
