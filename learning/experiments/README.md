# The next experiment

`next.json` holds the knobs the `Learning loop` workflow uses when nobody types any: change it, push it, and
the loop runs with those settings and reports back (`.github/workflows/iterate.yml`). Its git history is the
list of experiments that were run and why, which is the point of committing it rather than typing the numbers
into a form.

| Key | What it is | Default when the key is absent |
| --- | --- | --- |
| `why` | one paragraph on what this run is meant to answer; it is printed in the run summary | nothing |
| `matches` | matches recorded in the dataset | 200 |
| `seed` | base seed of that dataset | 1 |
| `explore` | share of decisions taken at random in the value dataset, or `"off"` (ADR 0014) | 0.2 |
| `value_alpha` | how strongly the value fit is pulled toward zero | 1.0 |
| `value_min_samples` | examples an action needs before it gets its own fit | 5 |

Every knob means exactly what the flag of the same name means in `scripts/iterate.sh --help`, and
`docs/learning/explained.md` ("The knobs") says when to touch each in plain words.

A dispatch of the workflow (Actions -> Learning loop -> Run workflow) overrides this file for that one run:
whatever you type in the form wins, and a field you leave empty falls back to the file.
