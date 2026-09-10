# 0021. Tune the catalogue with a declared search space, not with a model

Date: 2026-09-10

Status: Accepted

## Context

The loop measures balance already: a mirrored evaluation on the benchmark seeds reports the first-mover
share, the average rounds, the round cap share, the fizzle rate, the spell entropy, and since
[ADR 0020](0020-energy-regeneration-and-the-price-of-energy.md) what every spell's casts actually did.
What it has never had is anything that turns the manivelle: a change is a human editing a number, running
the loop, and reading the report. The catalogue is 36 inherited spells whose numbers were never chosen
(`docs/domain/spells.md`), so there is a lot of turning to do, and the last measured run says how much:
player 1 takes 64% of a mirrored run, matches last 5.8 rounds against a cap of 30, and `Greedy` declares
ten of the thirty-six spells. A search could do this, but a search needs two things the content cannot
supply: which numbers it is allowed to touch, and what it is aiming at. Handing that judgement to a language
model in the loop was considered and is what this decision refuses.

## Decision

We will tune the catalogue with a **numerical search over a declared space**, written in
`data/balance/knobs.json` and run by `uv run --project learning tune-content`. The file names, per spell,
the numbers a pass may move and their bounds; the intent of the spell in words, so a number is never tuned
into something the spell was not; the **objective**, as bands over the metrics `report.json` already
publishes, scored as the sum of `weight * (excess / scale) ** 2`; and the **constraints** a candidate may not
break. Only pointers the file lists may move, so a spell's targeting, its kinds of effect and its identity
are out of reach of the search by construction. The search sweeps every knob once and then hill climbs,
because each candidate costs a content build and one engine evaluation per objective entry, and it proposes
rather than commits: the run writes the winning spell files and a report, and applying them stays a human
act with a journal entry.

No language model runs inside the loop. What a model is good at here — reading the run and proposing which
spell to touch and why — happens in a session with a person, not in the search.

## Consequences

- Good: the numbers move against the same metrics the loop already reports, so a tuning run and a normal
  run are read the same way and compare on one axis.
- Good: the intent of every spell is written down next to its bounds, which is the first time this
  catalogue has said anywhere what each of its 36 spells is for.
- Good: constraints are checked before the engine is called, so an illegal candidate costs nothing, and
  `check-knobs` fails the build when the file and the content drift apart.
- Good: dominance is read against the talent tree, so a deeper spell outclassing a shallower one is the
  progression it is meant to be and only the pairs offered at once are reported.
- Bad: the objective is a choice with no proof behind it. Balanced is defined by the bands in that file,
  and a search will happily reach them in a way nobody wanted; the proposal is therefore read, not merged.
- Bad: the fitness is what `Greedy` does with the content, so the search tunes for one agent's taste. The
  `skill` evaluation guards the worst of it and a second weighted agent would guard more.
- Bad: a candidate costs a build plus two evaluations of 400 matches, which puts a real search in the
  minutes-to-hours range and out of reach of a pull request check.
- Neutral: the knobs file is authoring metadata. The data builder does not read it, so it never reaches
  `game.schema.json` and never moves the content hash.
- Neutral: the tuner refuses to widen a `:v2`. It writes the same versioned files it read, and cutting a
  new version stays the studio's job.

## Alternatives considered

- **A language model in the loop, opening a pull request per proposal**: it reads a report and proposes a
  spell change with a reason, which is the appealing half. It cannot be replayed from a seed, its proposal
  cannot be scored before it is played, and every run costs a review. Kept as a thing to do with a person
  in the room, not as a step of the loop.
- **Tuning nothing and authoring by hand**: what we do today. It is where the intent comes from and it is
  not going away, but 144 knobs over 36 spells is more turning than hand-authoring will get through.
- **A search over the whole document rather than declared knobs**: it can change targeting, effect kinds and
  target counts, and is how a search turns Rejuvenate into a nuke. Declaring the space is what keeps a
  spell the spell it was.
- **Reusing `search-weights`' cross-entropy method**: it samples a population per iteration and throws most
  of it away. At one content build plus two evaluations per sample the budget is better spent on the
  neighbours of the best candidate, and a small readable diff is worth more here than a global optimum.
- **Putting the objective in the tuner rather than in the content**: it would hide the one thing worth
  arguing about in Python. It lives next to the spells so that changing what balanced means is a content
  commit with a reason.

## Follow-up

- `data/balance/knobs.json` and `data/balance/README.md`: the search space, the intents, the objective.
- `learning/src/downfall_learning/knobs.py`, `tune_content.py`, and the `check-knobs` and `tune-content`
  commands in `cli.py` and `pyproject.toml`.
- `learning/tests/test_knobs.py` fails when a spell is added, retuned or cut without saying what it is for.
- `AGENTS.md` and `docs/learning/training.md` list the two new commands.
- `docs/domain/glossary.md` gains Balance knob, Balance objective, Content tuning, Strict dominance.
