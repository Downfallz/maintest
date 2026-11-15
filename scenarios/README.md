# scenarios/ — the match scenarios the repository ships

Every `*.json` here is a match scenario (ADR 0179, docs/tabletop/scenarios-plan.md, S7): a match cut at the top of
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

## What is here

| File | What it is |
| --- | --- |
| `greedy-1v1-round-3.json` | A test scenario: a greedy 1v1 mirror on seed 5, taken over as player 1 at the top of round 3. |
