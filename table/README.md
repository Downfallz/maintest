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

![Completed-round recap (illustrative fixture)](../docs/tabletop/images/round-recap.png)

## Playing

- A completed round opens a colour-coded recap above the board: caster, spell, chosen targets and actual
  applied outcomes, in resolution order. Reopen it throughout the next round; it stays available after the
  match ends. Critical casts, failed casts and skipped targets are labelled as well as coloured.
- The opponent, initiative order, your team and your spellbook keep the same reading order on every screen.
- On desktop, the next decision stays alongside the board. On phones, it stays in a bounded bottom sheet;
  the board and long lists of unlocks scroll independently.
- Gold identifies the acting creature, selected card or target, and the next action. Team names and text
  labels also identify the sides and selection state, so colour is never the only signal.
- Evolution presents one creature's unlocks at a time. Choose its numbered button, then the spell to unlock.
- Speed opens the acting creature's spellbook as a readable reference. Each new Speed or Intent question
  scrolls to that hand if it is outside the usable viewport; a Target question brings a legal creature into
  view. Later polls and local selection preserve deliberate scrolling. Other hands remain expandable.
- Tap a spell once to select it, then again to declare it. Tap a selected target again to cast on the entire
  selected group once the host's minimum is met. Remove buttons let you correct a target set; single-target
  spells also let you switch by tapping another creature. Declare and Cast buttons remain available.
  Enter or Space works too; holding a key or tapping while a request is pending never submits again.
- The Talents reference groups classes by colour and spells by the host's tiers. Filter by class and inspect
  each creature's known spells, exact prerequisites and current unlock offers. Desktop tiers read left to
  right; phone tiers stack. The same class accents appear on hand and unlock cards. Open the reference from
  the spellbook or Evolution sheet; a new Speed, Intent or Target question returns to the battlefield.
- Every battlefield creature receives a circular turn number once the host has built the timeline. The
  number follows the full server order, including ties; the active reveal/resolution slot is highlighted.
- Opponent spellbooks expand below their team and update from public known spells as unlocks appear.
  These reference cards never select an action and never expose the opponent's face-down choice.
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
handover fence, visible refusals, request failures, in-flight poll ordering, decision scrolling, second-tap
confirmation, public opponent books, talent filters and server-derived turn numbers. The feed tests also cover
completed-round recap formatting, event round identity, applied outcomes, failed casts and independent
two-round retention. It complements the existing
projection and transport tests; it does not replace a visual browser check.

For a browser check, exercise Evolution, Speed, Intent and Target, pass the device between seats, inspect
Talents and expand another creature's hand. Check a narrow phone and a desktop, with a scrolled hand and a
slow connection. Verify that only the chosen action is submitted, unavailable cards remain readable, and
that the handover screen covers the entire board.
