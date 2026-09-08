# Viewer

One static page that opens the learning artifacts (`docs/learning/artifacts.md`) from disk: no server, no
build, no framework (ADR 0013, decision J). Open `index.html` in a browser and drop files on it, or use the
two buttons. Nothing leaves the page; the only network access is the Chart.js CDN, and the tables work
without it.

What it renders, by what you drop:

| Drop | View |
| --- | --- |
| A run directory (`manifest.json`, `steps.jsonl`, `episodes.jsonl`, `traces/`) or `simulation.csv` | **Batch**: win rates with 95% intervals, draw rate, rounds histogram, remaining-health distributions, intents per spell, fizzle and crit rates (when traces are included), the run stamp. |
| A trace (`traces/<match>.json` or the file of `play --trace`) | **Match**: round by round, every event with both teams after it: health bars, energy, defense, initiative, conditions as chips, known spells, intents, the timeline with revealed and resolved actions, outcomes, fizzles, and crits. Step with the buttons or the arrow keys, jump to a round. |
| `evaluation.json` (from `evaluate` or `benchmark`) | **Evaluation**: both agents with win rates and paired intervals, scores, remaining health, spell entropy, fizzle and crit rates, and every seed pair. |
| `training.jsonl` (written by the Python side, L6) | **Training run**: loss and evaluation win rate per iteration, best iteration marked. |
| Two artifacts of the same kind, through the Compare selects | **Comparison**: the metrics side by side with deltas, and the run stamp diff (what moved: content, engine, rules, schema, agents, seeds). |

Traces dropped alongside their run also feed the batch view (fizzle and crit rates) and appear on their own.

## Samples

`samples/` holds one small run recorded from a simplified engine so the page can be tried without building
anything: `random-vs-random/` (six matches of a two-on-two rule set capped at five rounds, with one full
trace), `simulation.csv`, `evaluation.json`, and a `training.jsonl`. Their shape is checked against a real recorded run by
`ViewerSamplesTests` (Infrastructure tests), so the viewer never drifts from what the engine writes. To
look at real data instead:

```bash
dotnet run --project src/DownfallArena.Cli -- simulate --matches 200 --seed 1 --record runs/random-vs-random
dotnet run --project src/DownfallArena.Cli -- play --seed 1 --trace match.trace.json
```

then drop `runs/random-vs-random` or `match.trace.json` on the page.
