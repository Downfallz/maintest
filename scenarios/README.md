# scenarios/ — the match scenarios the repository ships

Every `*.json` here, and every one under `tutorials/` and `bugs/`, is a match scenario (ADR 0179,
docs/tabletop/scenarios-plan.md, S7): a match cut at the top of a round. A host that records its tables reads
this directory when it starts and lists each file in the admin panel's **Scenarios** tab, marked *Repository*,
with a **play** link. Nothing else is needed: add a file, deploy, and it is there.

- The id is the file's name: `greedy-1v1-round-3.json` is `sc-repo-greedy-1v1-round-3`, opened at
  `/battle?scenario=sc-repo-greedy-1v1-round-3` by a signed-in admin account (any signed-in account on a host that
  names no admin account).
- They are listed in the admin panel only, except the tutorials, which every signed-in account can open. A
  scenario here is never listed under Practice and never removed at the host. To remove one, delete its file.
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
file with a `tutorial` block, which you write by hand after the `seat` key. Its id is
`sc-repo-tutorial-<file name>`. A file with the block anywhere else is left out, and so is a file here without
it. The host lists it in the admin panel with the others, marked *Tutorial n*, and offers it to every signed-in
account: `GET /api/tutorials` lists the series, and one is opened as a practice like any scenario. While it
plays, the host answers the assisted questions for the person, and says in the seat's view how many steps have
been shown and whether the lesson is done (docs/reference/commands.md).

```json
"tutorial": {
  "order": 1,
  "title": "Your first combat",
  "teaches": ["Action"],
  "steps": [
    {
      "at": { "kind": "Action", "round": 2, "creature": 2 },
      "says": ["Your creature acts. Pick a spell, then its target.", "One enemy is wounded. Strike it."],
      "concepts": ["health"],
      "pointsAt": { "creature": 4 }
    }
  ]
}
```

- `order` is its place in the series, from 1; no two tutorials share one. `title` is what the person reads.
- `teaches` names the question kinds the person answers, among `Evolution`, `Speed`, `TieOrder`, `Intent`,
  `Target` and `Action`. Every other kind is an assisted question: answered from the recorded answers.
- A step shows at the question `at` names: its `kind`, one the lesson teaches, and its `round`.
- `creature` is the creature asked about. Leave it out for any creature. Never give it for `Evolution` or
  `TieOrder`, which are asked of the player.
- A question has at most one step. Write the steps in the order they show. The tutorial ends at the last
  step's round.
- `says` is one or two lines, each 160 characters at most. `concepts` are the concept cards the step opens, among
  `health`, `energy`, `defense`, `initiative`, `tempo`, `critical`, `intent`, `fizzle`, `conditions` and
  `mutation`. `pointsAt` is omitted, or is `{ "creature": N }`, or is `{ "control": "<name>" }`, the name among `spells`
  (Choose spell), `cast` (Confirm cast), `command` (the mutations or the tie order), `order` (the turn order) and
  `history`.

`ScenarioFileTests` rebuilds a tutorial like any scenario, and `TutorialFileTests` plays its recorded answers:
every step must be reached, in order, about creatures on its board. To check one by hand:

```bash
dotnet run --project src/DownfallArena.Cli -- scenario-check --scenario scenarios/tutorials/your-first-combat.json
```

## Bugs

A file under `scenarios/bugs/` is a **bug scenario** (docs/tabletop/feedback-plan.md, R6): the redacted scenario of
a player's bug report, attached to its issue as `report-<id>.zip` from the admin panel's Feedback tab. It is named
`<issue>-<slug>.json`, its id is `sc-repo-bug-<file name>`, and the Scenarios tab marks it *Bug #N*, so the operator
can play the round again once the fix deploys. A file here not named after its issue, or with a `tutorial` block, is left
out, and so is a file elsewhere whose name makes a bug's id.

```bash
dotnet run --project src/DownfallArena.Cli -- scenario-import --zip report-<id>.zip --issue 812
dotnet run --project src/DownfallArena.Cli -- scenario-check --trace --scenario scenarios/bugs/812-round-4.json
```

`scenario-import` refuses a zip that is not redacted, does not carry its content, does not rebuild, or holds anything
else. Import only a zip the operator's own account attached to the issue: names were checked when it was downloaded,
and cannot be checked again.
`scenario-check --trace` prints each seat's creatures after each entry past the cut, to find the entry where the
rule went wrong. The redaction keeps the recorded answers only up to where the player wrote, so `ScenarioFileTests`
asks a bug file's answers to apply that far and no further. A fix may change what the recorded player could do
before that point. It then shortens the file's `after` to the entry where the rule went wrong, and says so in its pull
request.

Each bug has one C# test, landed with its fix: a class under `tests/DownfallArena.Infrastructure.Tests/Matches/Bugs/`
named as the behaviour the fix restores, and carrying its issue's number in a comment. `BugScenario.ReplayAsync(file, entries)`
rebuilds the file at its cut, replays that many entries past it, and hands back both seats' boards and the events
raised, for the test to assert what the rule says. The test fails before the fix and passes after. `ExampleBugTests` there
is the pattern, on a redacted greedy match kept beside it rather than here, since it is no issue.

## What is here

| File | What it is |
| --- | --- |
| `greedy-1v1-round-3.json` | A test scenario: a greedy 1v1 mirror on seed 5, taken over as player 1 at the top of round 3. |
| `tutorials/your-first-combat.json` | Tutorial 1: a greedy 3v3 on seed 5 under the hosted table's rules, taken over as player 1 at the top of round 2, with three lesson steps on the person's three actions. |
| `tutorials/who-acts-first.json` | Tutorial 2: random against greedy, 3v3 on seed 51, at the top of round 2; the person chooses the tempos, the tie order and the spells. |
| `tutorials/grow-by-mutating.json` | Tutorial 3: a greedy 3v3 on seed 5, at the top of round 3; the person also chooses the mutations. |
| `tutorials/to-the-end.json` | Tutorial 4: a greedy 3v3 on seed 3 at the top of round 5, played to the match's end in round 8: the conditions, the stun and the end of a match. |
