# scenarios/ — the match scenarios the repository ships

Every `*.json` here, and under `tutorials/`, is a match scenario (ADR 0179, docs/tabletop/scenarios-plan.md, S7): a match cut at the top of
a round. A host that records its tables reads this directory when it starts and lists each file in the admin
panel's **Scenarios** tab, marked *Repository*, with a **play** link. Nothing else is needed: add a file, deploy,
and it is there.

- The id is the file's name: `greedy-1v1-round-3.json` is `sc-repo-greedy-1v1-round-3`, opened at
  `/battle?scenario=sc-repo-greedy-1v1-round-3` by a signed-in admin account (any signed-in account on a host that
  names no admin account).
- For now they are the admin panel's only: one is never listed under Practice, and never removed at the host. To
  remove one, delete its file.
- Each file carries the content it was cut on, so it keeps playing after the content moves.
- Each file is also a regression test: `ScenarioFileTests` rebuilds every one on the engine. A file an engine
  change breaks is generated or cut again, with the change explained.

## Add one

A match of two bots, cut at the top of a round (run it without `--round` first to read the rounds it offers):

```bash
dotnet run --project src/DownfallArena.Cli -- scenario-generate --p1 greedy --p2 greedy --seed 5 --format 1v1 --round 3
```

It writes `scenarios/greedy-v-greedy-seed-5-round-3.json`, and refuses to write over a file already there. `--out` names another file, `--seat player2` the other
seat, `--title` and `--goal` what the person reads, `--rules docs/tabletop/playtest.rules.json` the hosted table's
rules. Change the seed, the bots or the round for another one.

From a session people played, `scenario-cut --session runs/playtest/<id> --round N --with-content --out
scenarios/<name>.json` does the same. Both commands are in docs/reference/commands.md.

## Tutorials

A file under `scenarios/tutorials/` is a **tutorial** (ADR 0187, docs/tabletop/onboarding-plan.md): a scenario
file with a `tutorial` block, written by hand after `seat`. Its id is `sc-repo-tutorial-<file name>`. Until the
hosted tutorials are built (stage O3a) the host reads it and lists it with the others, in the admin panel only.

```json
"tutorial": {
  "order": 1,
  "title": "Your first combat",
  "teaches": ["Action"],
  "steps": [
    {
      "at": { "kind": "Action", "round": 2, "creature": 2 },
      "says": ["Your creature acts. Pick a spell, then its target.", "One enemy is wounded. Strike it."],
      "concepts": ["intent", "health"],
      "pointsAt": { "creature": 4 }
    }
  ]
}
```

- `order` is its place in the series, from 1; no two tutorials share one. `title` is what the person reads.
- `teaches` names the question kinds the person answers, among `Evolution`, `Speed`, `TieOrder`, `Intent`,
  `Target` and `Action`. Every other kind is an assisted question: answered from the recorded answers.
- A step shows at the question `at` names: its `kind` (one the lesson teaches), its `round`, and its `creature`
  (left out for any creature; never given for `Evolution` or `TieOrder`, which are asked of the player). One
  step a question. The steps are written in the order they show: the last one ends the tutorial with its round.
- `says` is one or two lines of 160 characters at most. `concepts` are the concept cards the step opens, among
  `health`, `energy`, `defense`, `initiative`, `tempo`, `critical`, `intent`, `fizzle`, `conditions` and
  `mutation`. `pointsAt` is left out, or `{ "creature": N }`, or `{ "control": "<name>" }`.

`ScenarioFileTests` rebuilds a tutorial like any scenario, and `TutorialFileTests` plays its recorded answers:
every step must be reached, in order, about creatures on its board. To check one by hand:

```bash
dotnet run --project src/DownfallArena.Cli -- scenario-check --scenario scenarios/tutorials/your-first-combat.json
```

## What is here

| File | What it is |
| --- | --- |
| `greedy-1v1-round-3.json` | A test scenario: a greedy 1v1 mirror on seed 5, taken over as player 1 at the top of round 3. |
| `tutorials/your-first-combat.json` | Tutorial 1: a greedy 3v3 on seed 5 under the hosted table's rules, taken over as player 1 at the top of round 2, with three lesson steps on the person's three actions. |
