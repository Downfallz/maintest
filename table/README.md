# Downfall Arena table

A static tabletop client designed primarily for desktop, with responsive phone controls, served by the
existing .NET table host. No framework, build step, external fonts, or runtime dependencies. Game rules, legal options and card text still come from the host.

```bash
dotnet run --project src/DownfallArena.Cli -- table --p2 greedy
```

Open the seat link printed by the host. For two people sharing a screen, omit `--p2 greedy` and use the
hotseat link. See [the playtest specification](../docs/tabletop/playtest-app.md) for rules and recording.

## Preview

Earlier visual baseline, before the desktop workspace and floating atlas. These illustrative fixtures are
not recorded matches. Card text and creature names in the actual app come from the running host.

![Desktop tabletop preview](../docs/tabletop/images/table-ui-desktop.png)

![Completed-round recap (illustrative fixture)](../docs/tabletop/images/round-recap.png)

## Playing

- A completed round adds a compact recap button in the sticky top status bar. Open it to read the
  colour-coded casts, targets and outcomes in a floating panel; it never scrolls or pushes the battlefield.
  It remains available throughout the next round and after the match ends. Escape closes it.
- Newly completed rounds open an action-by-action resolution review in the decision column. Previous and
  Next traverse actual public results (including criticals, fizzles and dropped targets); Skip returns to the
  latest round or match results. The actor and targets are highlighted on the battlefield. Each action starts
  with the recorded Before state; Next applies its After state and shows actual outcomes and stat changes.
  Previous reverses either step. HP, energy, conditions, spellbooks and the timeline come from engine
  snapshots captured before cleanup and upkeep (ADR 0075). Skip restores the latest state including upkeep.
  Older recordings without snapshots explicitly label their battlefield as current totals.
  New-round controls and their asking acknowledgement wait until the review closes; replay controls send
  no decisions. Arrow keys step backward/forward and Escape skips. Reloading does not auto-play old history;
  the recap's Replay action by action button makes it available on demand.
- The sticky top bar leads with round, current phase and the acting position/creature from the host timeline.
  The decision repeats the phase and turn position above the creature/spell title; targeting starts with one
  short instruction and keeps confirmation help collapsed. Round flow, upkeep, announcements and recap are
  on-demand references in this same bar. Speeds reveal together;
  opposing spell choices reveal only with confirmed targets. The guide displays this distinction explicitly.
- The opponent, initiative order, your team and your spellbook keep the same reading order on every screen.
- Desktop keeps the battlefield beside a compact planning desk with the acting spellbook. The board stays
  visible while the page scrolls through longer spell lists; neither board nor hand has a clipped inner
  scrolling pane. Cards wrap into two columns and the active creature appears first. Phones retain the
  stacked flow and horizontal hand; jump links remain available below desktop width.
- Gold identifies the acting creature, selected card or target, and the next action. Team names and text
  labels also identify the sides and selection state, so colour is never the only signal.
- Evolution presents one creature's available packages at a time. Each pick buys all of a package's spells
  and its initiative bonus. The team shares two picks on rounds 1, 3, 5, etc. by default, capped by eligible
  living creatures, with at most one package per creature per opportunity (ADR 0066). Both the atlas and
  decision panel show effective remaining and spent picks; switching creature never resets them. The host
  supplies the next evolution round, displayed in the phase guide between opportunities.
- Speed opens the acting creature's spellbook as a readable reference. Each new Speed or Intent question
  keeps the battlefield and planning desk visible together on desktop; smaller screens guide to the
  active decision. Later polls and local selection
  preserve deliberate scrolling. Other hands remain expandable.
- Tap a spell once to select it, then again to declare it. Tap a selected target again to cast on the entire
  selected group once the host's minimum is met. Remove buttons let you correct a target set; single-target
  spells also let you switch by tapping another creature. Declare and Cast buttons remain available.
  Enter or Space works too; holding a key or tapping while a request is pending never submits again.
- The Talent atlas opens over the battlefield as a non-modal window: drag its title, resize its corner,
  maximize, reset or close it. Arrow keys on the title move it as well. Phones use a full-screen panel.
  Its sticky toolbar keeps the creature, round, Evolution pick number and remaining picks visible.
  Down from the last package or spell choice reaches the explorer; Enter opens it and Up returns to the choices.
- The atlas draws the package prerequisite graph in three rows (tiers 1–3). Names and edges come from
  catalogue packages, not the retired per-spell gates. Select a package to read its prerequisites by name,
  initiative bonus and all its spell faces side by side. Ownership comes from `acquiredTiers`, independently
  of known spells; only the host's `availableTiers` can enable a purchase.
- The authored first-level families use coherent cool, leaf and ember palettes, with shades inherited by
  specializations. These accents follow every card into the spellbook and unlock picker.
- Buy legal packages directly from the atlas. A creature that bought this opportunity remains inspectable
  but cannot buy again. New Speed, TieOrder, Intent and Target questions close the atlas to expose the board.
- The initiative strip retains d20 rolls and rerolls. TieOrder has its own phase reminder and guarded
  keyboard/pointer controls to order your creatures within your side's assigned places before declarations.
- Every battlefield creature receives a circular turn number once the host has built the timeline. The
  number follows the full server order, including ties; the active reveal/resolution slot is highlighted.
  Order numbers run from turquoise to violet; Quick tags are gold and Standard tags blue. Energy, defense
  and initiative have separate colours and retain text labels. The order strip spells out creature identity
  and initiative separately. Creature numbers replace repeated definition names in the play surface.
- Spell faces use restrained paper tints with labelled effect and critical badges derived by the catalogue
  projection. The client does not infer effect categories from spell names or parse effect text. The critical
  badge and reminder state the maintainer-confirmed rule: Quick cannot crit; Standard can, multiplying only
  direct damage/healing on targets. This rule is now enforced by the engine on main; the UI does not alter combat resolution.
- The energy cost has an icon and a visible label. Spell stats show critical chance, its Standard-only
  reminder and host-provided d20 threshold. Initiative and acquisition requirements appear once per package,
  never as obsolete spell stats. Targeting and effect cues retain their visual markers.
- Phase changes show a non-blocking announcement below the top bar for 15 seconds. It does
  not move focus, delay a decision or replay on selection/poll redraws. Reduced-motion preferences disable
  the entrance animation; hotseat handovers hide and cancel the departing seat's announcement.
  New rounds get a larger, gold-accented “Round N begins” announcement for 20 seconds, alongside upkeep
  results and the next task. Loading an existing round does not pretend that a new round just started.
  Hovering or focusing pauses expiry; Keep open pins the notice and Close dismisses it. Announcements holds
  the last twelve notices per seat for this page session. Replaying one stays open and is marked as an earlier
  announcement; it does not change the current phase, question or selection. The top bar remains current.
- Automatic upkeep remains readable through the dock's Upkeep control for the current round. It shows the
  configured energy allowance and actual applied ongoing energy, healing and damage ticks per creature,
  including zero/capped results, in engine order. These public events are retained separately from the short
  activity log. Escape closes the panel. A new round announces upkeep even when polling skipped that phase;
  missing events are never reconstructed from board deltas or guessed from conditions.
- Opponent spellbooks expand below their team and update from public known spells as unlocks appear.
  These reference cards never select an action and never expose the opponent's face-down choice.
- Each spell becomes public together with its confirmed targets, in timeline order (ADR 0070).
  The first creature sees no unrevealed enemy choices; the fifth can read the first four confirmed actions.
  Local target selections remain private until confirmation. Confirmed spells, targets and resolution status
  update on the battlefield. At the next round, the previous public action is explicitly
  labelled “Last round” until a new one is revealed; it is recovered from the seat's public feed on reload.
- Your creatures display their current spell directly below their stats: draft choices say Not declared,
  accepted private intents say Not revealed / No targets chosen yet, and local target selections say not
  confirmed. Public confirmation replaces that private summary with the real target names and reveal status.
  Opponent cards still consult confirmed public actions only. The duplicate face-down text strip and
  expandable revealed-action list below the board have been removed.
- Round guide contains the host's round order and rule stamp. Match activity and playtest notes stay below
  the spellbook. The opaque handover screen remains the hotseat privacy boundary.

An unchanged poll leaves the DOM alone. Local selection does not wait for another network round trip,
scroll positions and keyboard focus survive a redraw, and a refusal stays visible until a successful
submission or a new question. Only one poll runs at a time, and an old response cannot redraw a board after
a decision has been sent.

## Keyboard

| Key | Action |
| --- | --- |
| 1 / 2 during Speed | Quick / Standard |
| 1–9 during Intent or Target | Select the numbered card or legal target; repeated numbers do not commit |
| Enter | Confirm the selected intent or valid target set; activate a focused button normally |
| 1–9 during Evolution or in the atlas | Choose the creature to evolve or inspect |
| T | Open / close the Talent atlas |
| Escape | Close the atlas; otherwise clear the pending card/target selection |
| ? | Show / hide contextual shortcut help |
| Tab, Enter / Space | Navigate and activate controls, including class nodes and unlocks |
| ← / → | Switch Evolution creatures, or focus the next speed, spell or legal target |
| ↓ from an Evolution creature | Focus its first offered spell |
| ↑ / ↓ within choices | Move to the closest choice in the preceding / following visual row; ↑ from the first Evolution row returns to the creature picker |
| Enter on a spell / target | First selects, then confirms the existing selection; Evolution packages use their normal single activation |
| Arrow keys on the atlas title | Move the desktop window |

Shortcuts ignore text fields, selectors, contenteditable areas, modifier chords, key repeats, in-flight
requests and the hotseat fence. Card and target numbers match the host's option order. Arrows follow the visible layout; they never cast or
declare on their own and do not change the engine's acting creature.

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
confirmation, public opponent books, live public actions, talent filters, keyboard guards and server-derived
turn numbers. Hierarchy and palette tests cover reordered nodes, tree-scoped parents and descendant shades. The feed tests also cover
completed-round recap formatting, event round identity, applied outcomes, failed casts and independent
two-round retention. It complements the existing
projection and transport tests; it does not replace a visual browser check.

For a browser check, exercise Evolution, Speed, Intent and Target, pass the device between seats, inspect
Talents and expand another creature's hand. Check a narrow phone and a desktop, with a scrolled hand and a
slow connection. Verify that only the chosen action is submitted, unavailable cards remain readable, and
that the handover screen covers the entire board.
## Decision guidance and practice

Before confirming a spell or target, the table shows the engine's energy cost, energy
left after paying it, turn position, and plain/critical effects against the current
targets. Choosing speed shows the Quick/Standard trade and each spell's critical
chance. Unaffordable cards keep the engine's reason visible. Caster effects appear
once, separately from target effects.

The preview is explicitly conditional: earlier actions can change the board, rolls
are unknown, and computed effects still go through execution caps and condition
stacking. It never reads hidden enemy choices or consumes the match's random stream.
The Before/After replay remains the record of what actually happened (ADR 0079).

Start a separate practice table with the standard built catalogue:

```bash
dotnet run --project tools/DownfallArena.DataBuilder -- data data/dst
dotnet run --project src/DownfallArena.Cli -- table --practice
```

In Visual Studio, open `DownfallArena.slnx`, set `DownfallArena.Cli` as the startup
project, select the `table (practice scenarios)` launch profile, and press F5. If
the compiled schema is missing, run the DataBuilder command above once first.

Open the practice link printed by the host. Select a scenario and press **Start
scenario**; **Restart this scenario** returns to the same question with the same
seed. The page goes straight to your decision; simulated setup rounds do not open a
replay. `--bind <your LAN IPv4 address>` and `--port N` also work for a phone.

| Scenario | Starting point | Try |
| --- | --- | --- |
| Choose your target | Round 1, Lightning Bolt targets | Compare targets and the energy left after cost |
| Build a multiclass creature | Round 3, evolution | Add a different level-1 package to Brute or take a level-2 upgrade |
| Interrupt an action | Round 5, Tranquilizer Dart targets | Stun before an enemy acts; follow skipped actions and later immunity |
| Read a layered resolution | Round 5, Toxic Waves targets | Follow multiple targets, bleed and Psycho Rush's caster effect |

Practice uses seed 17 and a ten-round cap, plays its setup through the actual engine,
and continues normally after handing player 1 to you. It writes no playtest files or
training episodes. A reset creates a new match and invalidates its previous seat
token. It cannot reset an ordinary table. Custom content that cannot satisfy the
recipe fails with an explanation; rebuild the standard catalogue to use these recipes.

Browser checks against the live engine, on phone and desktop:

```bash
dotnet build src/DownfallArena.Cli
cd studio/browser
npm ci --ignore-scripts
node node_modules/playwright/cli.js install chromium
node node_modules/playwright/cli.js test --config table.config.js
```
