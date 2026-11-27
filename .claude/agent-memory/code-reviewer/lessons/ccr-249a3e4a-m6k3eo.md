# code-reviewer lessons (branch ccr-249a3e4a-m6k3eo)

- A word scanner over `scriptText` skips one-word literals passed to a helper (`text('span', 'd20', cls)`) or held in an exported constant (`RANDOM_TIE = 'Roll-off'`): the exact label the PR removed reverts green. Probe by reverting the removed label itself (F1, af13a9b).
- A "the app never says X" sweep scoped to `table/` and `battle/src/` misses the tutorials' coach lines in `scenarios/tutorials/*.json`, which the battle view shows (F1 "rolls for a critical", af13a9b).
