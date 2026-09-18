# Downfall Arena table

A static, responsive tabletop client served by the existing .NET table host. No framework, build step,
external fonts, or runtime dependencies. Game rules, legal options and card text still come from the host.

```bash
dotnet run --project src/DownfallArena.Cli -- table --p2 greedy
```

Open the seat link printed by the host. For two people sharing a screen, omit `--p2 greedy` and use the
hotseat link. See [the playtest specification](../docs/tabletop/playtest-app.md) for rules and recording.

## Preview

Illustrative browser fixture, not a recorded match. Card text and creature names in the actual app are
provided by the running host.

![Desktop tabletop preview](../docs/tabletop/images/table-ui-desktop.png)

## Playing

- The opponent, initiative order, your team and your spellbook keep the same reading order on every screen.
- On desktop, the next decision stays alongside the board. On phones, it stays in a bounded bottom sheet;
  the board and long lists of unlocks scroll independently.
- Gold identifies the acting creature, selected card or target, and the next action. Team names and text
  labels also identify the sides and selection state, so colour is never the only signal.
- Evolution presents one creature's unlocks at a time. Choose its numbered button, then the spell to unlock.
- The acting creature's hand is open. Other hands are expandable, including cards unavailable this turn.
- Intent and targeting still require confirmation. Cards and legal targets accept Enter or Space as well
  as a tap. The Talents tab is a reference; a new intent or target question returns to the battlefield.
- Round guide contains the host's round order and rule stamp. Match activity and playtest notes stay below
  the spellbook. The opaque handover screen remains the hotseat privacy boundary.

An unchanged poll leaves the DOM alone. Local selection does not wait for another network round trip,
scroll positions and keyboard focus survive a redraw, and a refusal stays visible until a successful
submission or a new question. Only one poll runs at a time, and an old response cannot redraw a board after
a decision has been sent.

## Verification

```bash
node --test studio/*.test.js table/*.test.js
dotnet build
dotnet test
dotnet format --verify-no-changes
```

`table.test.js` runs the shipped renderer in a minimal DOM double using Node's standard library. It covers
stable polling, keyboard selection, asking identity, target bounds, creature-specific unlock lists, the
handover fence, visible refusals, request failures and in-flight poll ordering. It complements the existing
projection and transport tests; it does not replace a visual browser check.

For a browser check, exercise Evolution, Speed, Intent and Target, pass the device between seats, inspect
Talents and expand another creature's hand. Check a narrow phone and a desktop, with a scrolled hand and a
slow connection. Verify that only the chosen action is submitted, unavailable cards remain readable, and
that the handover screen covers the entire board.
