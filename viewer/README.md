# Viewer

One static page that opens the learning artifacts (`docs/learning/artifacts.md`) from disk: no server, no
build, no framework (ADR 0013, decision J). Open `index.html` in a browser and drop files on it, or use the
two buttons. Nothing leaves the page; the only network access is the Chart.js CDN, and the tables work
without it.

What it renders, by what you drop:

| Drop | View |
| --- | --- |
| A run directory (`manifest.json`, `steps.jsonl`, `episodes.jsonl`, `traces/`) or `simulation.csv` | **Batch**: win rates with 95% intervals, draw rate, rounds histogram, remaining-health distributions, intents per spell, fizzle and crit rates (when traces are included), the run stamp. |
| A trace (`traces/<match>.json` or the file of `play --trace`) | **Match**: what each side added up to (actions, fizzles, crits, energy, intents per spell, damage and conditions dealt), then round by round, every event with both teams after it: health bars, energy, defense, initiative, conditions as chips, known spells, intents, the timeline with revealed and resolved actions, outcomes, fizzles, and crits. Step with the buttons or the arrow keys, jump to a round. |
| `evaluation.json` (from `evaluate` or `benchmark`) | **Evaluation**: both agents with win rates and paired intervals, scores, remaining health, spell entropy, fizzle and crit rates, **which spells the winning side was holding**, what every number means, and every seed pair. Two agents of the same spec are called out: the mirrored pass then replays the same matches, so the even split is arithmetic. |
| `training.jsonl` (written by the Python side, L6) | **Training run**: loss and evaluation win rate per iteration, best iteration marked. |
| `report.json` (written by `report`, L7) | **Iteration report**: every evaluation of a run in one table with the balance signals, a bar chart of win rates and player 1 shares, and the stamp; compare two reports for the deltas, matched by evaluation name. |
| Two artifacts of the same kind, through the Compare selects | **Comparison**: the metrics side by side with deltas, and the run stamp diff (what moved: content, engine, rules, schema, agents, seeds). For two evaluations, also the spell-by-spell delta of the share of sides that declared a spell and won — keyed by spell name, so a spell cut as a new version lines up with the one it replaced. |

Traces dropped alongside their run also feed the batch view (fizzle and crit rates) and appear on their own.

## Samples

`samples/` holds one small run recorded from a simplified engine so the page can be tried without building
anything: `random-vs-random/` (six matches of a two-on-two rule set capped at five rounds, with one full
trace), `simulation.csv`, `evaluation.json`, a `training.jsonl`, and a `report.json`. Their shape is checked against a real recorded run by
`ViewerSamplesTests` (Infrastructure tests), so the viewer never drifts from what the engine writes. To
look at real data instead:

```bash
dotnet run --project src/DownfallArena.Cli -- simulate --matches 200 --seed 1 --record runs/random-vs-random
dotnet run --project src/DownfallArena.Cli -- play --seed 1 --trace match.trace.json
```

then drop `runs/random-vs-random` or `match.trace.json` on the page.

## Run pages

`scripts/iterate.sh` (and `uv run --project learning report <run>`) writes `runs/<id>/report.html`: this
page with `viewer.css` inlined and the run's `report.json`, evaluations, and `training.jsonl` files embedded
in a `<script type="application/json" id="embedded-artifacts">` block. The page reads that block at start,
lists the artifacts, and opens on the report — or on the first artifact when there is no report — so nothing
has to be dragged in; dropping more files still works, for a comparison with another run. A block whose
`compare` flag is set opens on the comparison of the two artifacts it carries instead, which is what the
content studio's compare page sets (`studio/README.md`); the flag is said rather than inferred from the count,
so a run carrying two evaluations and no report keeps opening on the first of them. The block is produced by
`learning/src/downfall_learning/viewer.py` and, for the studio, by `ViewerPage` in the Cli.
