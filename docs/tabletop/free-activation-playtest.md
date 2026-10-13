# The free-activation playtest

Status: open. First readings from the owner on 2026-10-13; no time readings yet.

This is PR 8 of [free-activation-plan.md](../domain/free-activation-plan.md) ([ADR 0110](../adr/0110-a-rule-set-may-declare-spells-at-activation.md)):
the questions only people can answer about choosing each spell at its creature's slot (**mode B**, `AtActivation`)
rather than declaring it face down before combat (**mode A**, `HiddenIntent`, the default). The bots answered
what they could (journal, 2026-10-12): the shape of the matches barely moves, and the balance bands move only for
the forced openings. They could not say whether the hidden declaration is missed, whether a late slot feels like a
counter, or whether a round is faster at the table.

Nothing here changes a rule or a number. The decision comes after, under the stop rule of the plan (§6).

## How to play it

**Rules and feel**, one person, in hotseat or against a bot:

```bash
dotnet run --project src/DownfallArena.Cli -- table --declaration at-activation --p2 greedy   # mode B against Greedy
dotnet run --project src/DownfallArena.Cli -- table --p2 greedy                              # mode A, the same
dotnet run --project src/DownfallArena.Cli -- table --practice                              # the scenarios below, set up
```

The admin panel opens either mode too: its **Spells** chooser beside the format.

**Time**, two people on two devices (a phone each, or a phone and a laptop):

```bash
dotnet run --project src/DownfallArena.Cli -- table --declaration at-activation --bind <your LAN address> --who <initials>
dotnet run --project src/DownfallArena.Cli -- table --bind <your LAN address> --who <initials>
uv run --project learning playtest-timing runs/playtest/<id>/ runs/playtest/<id>/ --two-devices
```

Each session is recorded under `runs/playtest/<id>/`. `playtest-timing` groups the sessions it is given by the
declaration their stamp names and prints, per mode:

- the thinking time of a decision, by sub-phase (pick, speed, intent, targets at the slot, action at the slot);
- with `--two-devices`, the time a round's combat took, from the first speed served to the last action, both
  seats together, picks left out (both seats pick at once under either rule);
- and how much the two seats overlapped: their summed thinking over that time. Above one, they thought at once;
  at one or below, one waited for the other.

Name the two-device sessions, not `runs/playtest/*/`: nothing a session records tells two devices from one
hotseat screen, and a hotseat page shows one seat at a time, so its rounds would read serial under either rule.
Without `--two-devices` only the thinking per decision is read. Play both modes with the same two people, and
swap seats between sessions.

## The seven scenarios

From the design handoff of 2026-10-11 (§6). Five of them can be set up and are practice recipes at the slot
(`table --practice`, "At the slot: …"). The other two need a whole match.

| # | Scenario | The question | Practice |
| --- | --- | --- | --- |
| 1 | Armor, then the answer | An enemy casts Carapace just before your attacker acts. Is adapting your spell satisfying, or does the counter become automatic? | At the slot: answer an armor |
| 2 | Overdrive and energy | An ally must act first to make a costly spell payable. Does choosing at the slot take too much risk out of the combo? | At the slot: an ally's Overdrive |
| 3 | Drain before the slot | The target holds spells of several costs. Is Energy Drain still a decision, or only a resource penalty? | At the slot: drained before your turn |
| 4 | Stun or kill before the slot | The activation must still be lost: a neutralized creature must not act. | At the slot: stunned before your turn |
| 5 | Quick or Standard | With a spell that can crit, one that cannot, and a healer: does anyone play late on purpose to adapt? | A match |
| 6 | Late game, a full spellbook | After several packages and a multiclass, how long does a choice at the slot take? | A match |
| 7 | Three allies in a row | The first sets the condition up, the second exploits it, the third finishes. Too easy against a precommitment? | At the slot: three allies in a row |

And the plan's own questions: is the hidden declaration missed, and did the late actor feel like a counter?

## First readings: the owner, 2026-10-13

The first games in mode B "feel good at first sight". Scenario by scenario:

1. **Armor, then the answer.** Comfortable: adapting is pleasant, not automatic.
2. **Overdrive.** It does not take too much risk out of the combo.
3. **Drain.** It has less impact. A drained creature simply casts a one-energy spell or falls back on Focus,
   which brings it back to four energy the next round instead of two or three. The owner may raise the energy
   drains.
4. **Stun or kill.** Good: the creature loses its slot.
5. **Quick or Standard.** The dynamic is much the same as before. This is the bots' blind spot (no agent's speed
   decision reads the declaration), and the first reading does not show Standard taking over.
6. **Late game.** The same thinking as before; it does not change much.
7. **Three allies in a row.** Not really: the interleaving of the creatures' turns and the opponent's pressure do
   not let a player chain everything they want.

These are one player's first games, alone, without the time readings. They answer the feel questions for
scenarios 1, 2, 4, 5, 6 and 7 the way the plan hoped, and raise one balance point (3).

## What is still open

- **The time readings.** Two people on two devices, both modes; the script above reads them.
- **Is the hidden declaration missed?** Not asked yet in these words; it is half of the stop rule.
- **Energy Drain under mode B.** Scenario 3 says it lost weight. The bots agree that it is rarely cast (0.1 % to
  0.5 % of the landed casts, journal 2026-10-12) in both modes, so a change to the drains is a content question
  for either mode, measured with `score-content --declaration at-activation` against the bands before and after.
- **A second player.** Every reading here is the owner's. The plan's stop rule reads "nobody misses it", not "one
  player does not".
