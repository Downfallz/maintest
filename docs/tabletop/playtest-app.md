# The playtest app

Status: **Specification** (2026-09-14; citations re-verified 2026-09-17). Phase 5 of [plan.md](plan.md). It
names the host, the transport, the client's shape, what a session records, and what is not in the first
version. The build plan that follows from it is [app-roadmap.md](app-roadmap.md).

## What this is, and what it is not

An app that puts the tabletop rule set on a screen so it can be played by two people and measured. It is not a
second engine, not a rules implementation, and not a game client in the usual sense: every option it shows is
computed by the Application layer, and every decision it takes is a command the aggregate already accepts or
refuses.

**No code is written in this branch, and no ADR number is claimed.** The decision record's text is the last
section, ready to be numbered and moved into `docs/adr/` by whoever lands the code.

What this document takes as given, from [plan.md](plan.md) and [components.md](components.md):

- A **faithful port** (plan.md:188-199). The app plays the engine's rules through the engine.
- A Match is **10 to 15 Rounds** and 15 to 30 minutes (plan.md:108-125). Wall-clock is the target the app is
  built to measure; the engine measures rounds and cannot measure minutes.
- The components of phase 3 are the screen layout. The initiative track, the creature board, the condition
  dock, the player area and the talent mat are specified in components.md:491-764, and the app renders those
  objects rather than inventing a second visual language for the same game.
- **Decision D is settled** (2026-09-14): hotseat, one screen, to start. Part 4 says what it buys and what it
  leaves for later.

---

## Part 1. The rules it plays

### 1.1 One engine, one truth, and where the client stops

The client renders. It does not decide. Concretely, the client must never:

| Forbidden in the client | Because the Application already answers it |
| --- | --- |
| Filter a spell list by what a Creature can afford | `PlayerOptionsProjection.CastableSpells` (`PlayerOptionsProjection.cs:109-110`) |
| Decide which Talent nodes are open | `TalentUnlocks.UnlockableSpells` through `PlayerOptionsProjection.cs:56` |
| Decide who is a legal target, or how many | `TargetingRules.LegalTargets` through `PlayerOptionsProjection.cs:99` |
| Order the timeline, or break a tie | `Round.Timeline.Slots`, carried on `PlayerBoardState.Timeline` (`PlayerBoardState.cs:43`) |
| Compute total Defense from the Condition chips | `CreatureSnapshot.TotalDefense` (`CreatureSnapshot.cs:27`); the floor applies to the total, not to an intermediate (`Creature.cs:58-60`, ADR 0035) |
| Compute Current initiative from the Condition chips | `CreatureSnapshot.CurrentInitiative` (`CreatureSnapshot.cs:33`) |
| Decide whether a cast fizzles | `IntentRules.CanAct` (`IntentRules.cs:50-69`) and the resolution rules behind it |
| Count down a Condition | `Condition.Tick` (`Condition.cs:53-65`); the client reads `RemainingRounds` |

The client's whole job is layout, formatting and input. Anything it computes, it computes about pixels.

If the app makes a rule feel wrong, the fix is an engine change with its own ADR (plan.md:224-231), never a
patch in the client. A client that "corrects" a rule is the one failure this whole effort exists to prevent.

### 1.2 What answers "what can I do now"

One query, one decision. `PlayerOptionsProjection.Build(match, slot, resources)` returns exactly one filled
section per sub-phase, "built from the same rules the aggregate enforces, so a choice taken from here is
accepted" (`PlayerOptions.cs:5-8`).

| Sub-phase | Kind | Section | The gate behind it |
| --- | --- | --- | --- |
| `Evolution` | `PlayerOptionsKind.Evolution` | `EvolutionOptions(RemainingPicks, Creatures)` (`EvolutionOptions.cs:7`), each `EvolutionOption(Creature, UnlockableSpells)` (`EvolutionOption.cs:5`) | `EvolutionRules.Evaluate(...).RemainingPicksOf(slot)` (`PlayerOptionsProjection.cs:48`) |
| `Speed` | `Speed` | `SpeedOptions(Missing)` (`SpeedOptions.cs:8`) | `SpeedRules.Evaluate(...).MissingOf(slot)` (`PlayerOptionsProjection.cs:70`) |
| `TieOrder` | `TieOrder` | `TieOrderOptions(Ties)` (`TieOrderOptions.cs`), the seat's creatures in each tie it holds two places in, as rolled | `TieOrderRules.Evaluate(round).Waiting` (ADR 0063) |
| `IntentSelection` | `Intent` | `IntentOptions(Creatures)` (`IntentOptions.cs:6`), each `IntentOption(Creature, CastableSpells)` (`IntentOption.cs:5`) | `IntentRules.Evaluate(round).Missing` (`PlayerOptionsProjection.cs:79`) |
| `RevealAndTarget` | `Target` | `TargetOptions(Actor, Spell, LegalTargets)` (`TargetOptions.cs:10`), with `MinTargets`, `MaxTargets`, `Candidates`, `IsCastable` (`LegalTargets.cs:8-10`) | `round.NextSlotToReveal` and `TargetingRules.LegalTargets` (`PlayerOptionsProjection.cs:89-107`) |
| `ActionResolution` | `Resolution` | none | "A combat action waits for resolution; any host may drive it" (`PlayerOptionsKind.cs:20-21`) |
| anything else | `Waiting` / `Ended` | none | `PlayerOptionsProjection.cs:22-31,44` |

What a seat sees of the board is `PlayerBoardStateProjection.Build(match, slot)`: both teams as snapshots, the
round position, the timeline, the actions revealed so far — "public to both players" (`PlayerBoardState.cs:45-46`)
— and the seat's **own** hidden choices only (`PlayerBoardState.cs:32-41`).

A Condition chip has everything it needs: `ConditionSnapshot(Effect, RemainingRounds, Source)`
(`ConditionSnapshot.cs:6`), so a chip prints its kind, its amount, how many Rounds are left, and which cast put
it there (ADR 0027). That is the condition dock of components.md:533-550 with no extra work.

### 1.3 What is missing today

Three things, all of them Application's, none of them a rule.

| Missing | Why it is needed | Shape |
| --- | --- | --- |
| **A card projection** | `IntentOption.CastableSpells` and `EvolutionOption.UnlockableSpells` carry `SpellId`s and nothing else. The client cannot print a cost, an effect, a Duration or a Critical chance from an id. | A read-only projection over `IGameResources`: the card face of components.md:290-489, plus the enabled Talent tree for the mat. It reads the **built** catalogue, never the authored files: the studio's `/api/catalogue` answers `ContentStore.Read()` (`StudioApi.cs:197`), which lists documents a build prunes, including `Enabled = false` ones (`ContentStore.cs:382`). The printshop reads the same built schema (components.md:771-781), so the card on the screen and the card on the sheet cannot disagree. A Critical chance prints as the threshold the card carries — `d20: 14+` ([d20-criticals.md](d20-criticals.md)) — and that threshold is a property of the **card** only because a Creature carries no chance of its own: the engine sums the two (`ResolutionRules.cs:56`) and `CreatureSnapshot` still has the field (`CreatureSnapshot.cs:35`). If a Creature ever carries one again, the threshold becomes a property of the caster and has to move out of this seat-independent, cacheable projection into the per-seat payload of 2.3. |
| **A seat's view of the event stream** | `MatchTraceRecorder` keeps every event with **both** boards (`MatchTraceRecorder.cs:40-49`). `IntentSubmitted` carries the declared Spell and says so: "Hidden from the other player until revealed; the application layer decides who sees it" (`IntentSubmitted.cs:8-10`). `SpeedChoiceSubmitted` carries the Speed (`SpeedChoiceSubmitted.cs:10`). Neither may reach the other seat. | A projection from one `IMatchEvent` and one `PlayerSlot` to "this seat may see it, or not", **default-deny**: a closed switch over the fifteen events in `src/DownfallArena.Domain/Matches/Events/`, with a test that enumerates every `IMatchEvent` in the Domain assembly and fails when one is unclassified. Today exactly two events are private. The board projection filters a third, `EvolutionChoiceSubmitted` (`PlayerBoardStateProjection.cs:35`), but that one is public information anyway: both teams' snapshots are served whole (`PlayerBoardStateProjection.cs:19-20`) and a snapshot carries `KnownSpells` (`CreatureSnapshot.cs:39`), so an unlock is already on the opponent's board. Each arm of the switch carries its reason, because a default-deny table is only worth its test if every allow is argued. |
| **A playtest note** | Nothing records how long a decision took, what a player got wrong, or which rule was looked up. Part 5.3. | A record beside `StepRecord`, written through the existing `IArtifactWriter` (`IArtifactWriter.cs:7-16`) and timed with `TimeProvider`, which Application already registers (architecture/overview.md:41-42). |

**No new port.** Every adapter this app needs already has one: `IMatchRepository` (`IMatchRepository.cs:11,13`)
for the match, `IArtifactWriter` for what a session writes, `IRandomSource` for the critical roll,
`TimeProvider` for the clock. The three items above are projections and records, which Application owns
outright. If a later version wants a session to survive a host restart, that is a file-backed adapter for
`IMatchRepository`, not a new port either: today the only adapter is
`src/DownfallArena.Infrastructure/Matches/InMemoryMatchRepository.cs`.

---

## Part 2. The host and the transport

### 2.1 The host

A new CLI command, `table`, with its own loopback HTTP host, built the way the studio's is: a fixed static
route table (`StudioFiles.cs:7-33`), an `HttpListener` loop (`StudioServer.cs:37-56`) and the same-origin
fence that refuses a request another page made on the author's behalf (`StudioServer.cs:141-156`, ADR 0015).

```bash
dotnet run --project src/DownfallArena.Cli -- table --port 5100 --rules <file> --seed 7
```

**A separate command and a separate host, not a mode of `studio`.** The studio's API writes content files,
deletes them, and takes an agent spec like `heuristic:<path>` the engine then reads — the reason ADR 0023 gave
for refusing to unbind it from the loopback address. The table host writes nothing but its own session
directory, deletes nothing, and takes no path from a request. Keeping them apart is what makes 2.4 thinkable.

The static client lives in `table/`, beside `viewer/`, `studio/` and the `printshop/` of components.md:807-817:
`index.html`, one stylesheet, ES modules, no framework, no build step. Its transport is injected, as ADR 0024
requires, and it is tested with `node --test table/*.test.js` alongside the studio's.

### 2.2 How a decision reaches the aggregate

The host plays the match with `MatchDriver.PlayAsync(matchId, seat1, seat2)`, which "asks what they can do,
lets their agent decide, submits, and drives the resolution" (`MatchDriver.cs:11-18`). A **seat** is an
`IPlayerAgent` whose four `Decide*` methods block until the HTTP host hands them the answer a player tapped.

This is not a new shape. `IPlayerAgent` is documented as "a bot, a scripted test, **a UI adapter**"
(`IPlayerAgent.cs:8`), and `ConsoleAgent` is already a human who blocks the driver on `Console.ReadLine`
(`ConsoleAgent.cs:13`), wired by the `human` command (`GameSession.cs:93-95`). The seat is the web's
`ConsoleAgent`.

Three things come free with it, and each is a thing a hand-rolled REST loop would have had to get right again:

- **ADR 0039.** The driver re-reads the board between two intents of the same player, because the second
  Creature declares knowing what the first declared (`MatchDriver.cs:71-87`).
- **The resolution sub-phase** is driven without a client button (`MatchDriver.cs:91-93`).
- **The dataset.** `RunRecorder.Wrap` returns a `RecordingAgent` around any agent (`RunRecorder.cs:58-69`), so a
  human session writes `steps.jsonl` in exactly the bot format. See Part 5.

One thing must change for a human, and it is a requirement, not a detail: `MatchDriver` throws when a decision
is refused, because "an agent's decision was refused" is a bug in a bot (`MatchDriver.cs:122-128`). A human
gets a refusal for a reason — a stale screen, a double tap, a Creature that died between the fetch and the tap.
The host therefore validates the decision against the seat's own pending `PlayerOptions` **before** handing it
to the seat, and answers `409` with the `DomainError` code and message when it does not match. The driver never
sees a decision the options did not offer, so its invariant holds, and the refusal becomes a recorded note
(Part 5.3) instead of a dead session.

### 2.3 The transport

Plain HTTP, JSON, request and response. Polling, not push.

| Method | Route | Answers |
| --- | --- | --- |
| `GET` | `/`, `/table.css`, `/table.js`, ... | The page, from a fixed route table. |
| `GET` | `/api/session` | The session stamp — engine version, content hash, rule set, seed, session id — the two seat names, and **which seat has a pending decision and of what kind**. No board, no options. |
| `GET` | `/api/catalogue` | The card faces and the enabled Talent tree (1.3). Seat-independent, read-only, cacheable. |
| `GET` | `/api/seat/{slot}?since=N` | That seat's `PlayerBoardState`, its `PlayerOptions`, and the events it may see since sequence `N`. |
| `POST` | `/api/seat/{slot}/decision` | One decision. `200` with the new seat payload, or `409` with the error code. |
| `POST` | `/api/notes` | One playtest note. |
| `GET` | `/session/{id}` | The finished session in the viewer, rendered by `ViewerPage.Render` exactly as the studio serves `/runs/<id>` (`StudioApi.cs:112-126`). |
| `GET` | `/pilot`, `/pilot.css`, `/pilot.js` | The operator's own page (§ the roadmap's stage 6), from the same fixed route table. It is served to anybody who asks; what it can read is fenced by the token it is opened with, not by the page being secret. |
| `GET` | `/api/pilot` | The session, the two seats, who is playing each one, which seat is being asked and for what kind of decision, and the swap each seat is waiting to make. **No board, no hand, no Intent** — the operator is usually one of the two players. |
| `POST` | `/api/pilot/seats/{slot}` | `{"agent": "greedy", "round": 7}`: who plays that seat from the top of a round the match has not reached. `200` with the swap, or `409` with the code (`Table.SwapMidRound`, `Table.NoSuchAgent`, `Table.MatchOver`). |

The three pilot routes carry the pilot's own token instead, which is never a seat's: a pilot can move both
seats, so a seat token that could also pilot would let either player hand their opponent's seat to a bot — and
in hotseat both seat tokens live in the same browser as the page a player is looking at. Every other API
request carries exactly one seat token and answers for exactly one seat. Long-polling, server-sent
events and WebSockets are all refused for the first version: hotseat has nothing to push to, the idle seat in a
two-device session needs a one-second poll and no more, and each of the three is a second thing to keep alive
across a phone's screen lock.

### 2.4 The hidden-information boundary

**The server never sends a seat what that seat may not see.** Not "sends it and hides it". Three mechanisms,
all of them server-side:

1. `PlayerBoardStateProjection` already builds a board **per slot**: the Speed choices are filtered to the
   seat's own Creatures (`PlayerBoardStateProjection.cs:37`), the intents to the seat's own slot
   (`PlayerBoardStateProjection.cs:38`), the Evolution choices likewise (`:35`). A board fetched for Player 1
   does not contain Player 2's face-down card.
2. `PlayerOptionsProjection` answers `Waiting` for a decision that is not this seat's
   (`PlayerOptionsProjection.cs:44,49-52,71-73,91-94`).
3. The event feed is filtered by the default-deny projection of 1.3, because the raw stream is not safe: the
   trace deliberately carries both boards, "hidden intents included, since a trace is a debugging record rather
   than something a player sees" (`docs/learning/artifacts.md:152-154`).

So **the trace is never served during a session.** It is served after the outcome, at `/session/{id}`.

The aggregate is the last fence. Every command carries the slot and the rules refuse another player's Creature:
`IntentRules.ValidateIntent` returns `NotYourCreature` (`IntentRules.cs:27-30`), `SpeedRules.ValidateChoice`
the same (`SpeedRules.cs:24-27`), and `SubmitAction` is documented "Only the owner of that intent may do so"
(`Match.cs:230-233`). A seat token that lies still cannot act for the other seat.

**What a client-side hide would allow.** Suppose the host sent one payload with both boards and the page hid
the opponent's half. A player with the network tab open, or one who reads the DOM, learns two things before
choosing: the opponent's six face-down intents, and their six Speed cards. Those are the only two hidden
decisions in the game (translation.md:110, translation.md:95; plan.md:76-78) and they are the ones the round is
built around. Knowing the enemy's Speeds, you build the timeline before choosing yours. Knowing that enemy
Creature 4 declared a Stun, you pick Quick and target it first, or you simply pick a different Intent.

The damage is not that someone might cheat. It is that **the resulting playtest is silently worthless**: the
trace records a sequence of perfectly legal decisions, the dataset records them as human choices, and nothing
in either file says the player was looking at the answer. A crash you notice. This you do not.

---

## Part 3. The client's shape

### 3.1 What it renders

The objects of components.md, one for one, so a player who has seen the cardboard recognises the screen:

| Component | On screen | Source |
| --- | --- | --- |
| Initiative track, Quick band then Standard (components.md:608-626) | A horizontal strip of up to six chips under the header, with a divider between the Quick chips and the Standard ones, the acting slot highlighted | `PlayerBoardState.Timeline`, whose `ActivationSlot(Owner, Creature, Speed, Initiative)` carries the band and the value (`ActivationSlot.cs:9`) |
| Creature board (components.md:496-531) | One row per Creature: its number, a Health bar with the number, Energy, total Defense, Current initiative, a Stun badge, an `immune to stun` badge in the Round after a Stun ends (ADR 0072; the table's Immune token), a Speed badge once the timeline is built | `CreatureSnapshot` (`CreatureSnapshot.cs:11-45`; `IsStunImmune` for the immunity) |
| Condition dock, four lanes (components.md:533-550) | Chips grouped by remaining Rounds — `new`, `3`, `2`, `1` — with kind, amount and source, and permanent Conditions in their own group | `ConditionSnapshot(Effect, RemainingRounds, Source)` (`ConditionSnapshot.cs:6`); `RemainingRounds` is `null` when permanent (`Condition.cs:29-36`) |
| The hand (components.md:649-679) | The seat's own Creatures' known Spells as cards, the castable ones enabled | `CreatureSnapshot.KnownSpells` (`CreatureSnapshot.cs:39`) for the hand, `IntentOption.CastableSpells` for what is enabled |
| Face-down intent | A card back on each Creature that has declared. The seat's own back is tappable and reads its own card; the opponent's back carries no data at all | `PlayerBoardState.Intents` is the seat's own (`PlayerBoardState.cs:40-41`); the opponent's is a count, never a card |
| Target markers and the `Targeted by` row (components.md:649-679) | Tapping a legal target marks it; each Creature row shows which casters point at it | `TargetOptions.LegalTargets` and `PlayerBoardState.RevealedActions` |
| Talent mat (components.md:694-764) | A separate tab: three class bands, every Spell with a pip box per Creature, the gates printed on the band | The card projection of 1.3 for the tree, `KnownSpells` for the pips, `EvolutionOption.UnlockableSpells` for what is tappable now |
| Round track (components.md:628-647) | `Round 7 of 20` in the header, with the Round's shape as a collapsible strip | `PlayerBoardState.RoundNumber`, `Phase`, `SubPhase`; the cap from the session stamp |

The player aid's two load-bearing orderings — healing before bleeding, and the critical applied before Defense
is subtracted (components.md:645-647) — are on the round strip, because they are the two a player gets wrong.

### 3.2 The decision

One screen at a time, driven by `PlayerOptionsKind`, and never more than the one section that is filled. The
decision lives in the planning panel with the active hand, above the board it is about. Evolution lists the available
packages with a `pass` at the end, exactly as `ConsoleAgent` does (`ConsoleAgent.cs:15-24`). Speed is two
buttons. Intent is the hand, filtered by the server. Target is a tap on a legal Creature, with `done` enabled
once `MinTargets` is met and disabled past `MaxTargets` — the same walk `ConsoleAgent.cs:41-67` does at the
console, with the same numbers from the same `LegalTargets`.

A Spell with no legal target is offered anyway, and says it will fizzle, because that is what the engine does
(`TargetOptions.cs:6-8`).

### 3.3 A phone

Desktop is now the primary interaction target (maintainer direction, 2026-09-18). Phones remain playable.
The original mobile constraints below document the first design; the current shared page flow is described
in the desktop workspace section that follows.

- **One column.** Nothing side by side. The enemy team, the initiative strip, your team, your hand, in that
  order, scrolling.
- **Numbers, not rails.** A 31-cell Health rail is a cardboard affordance for a marker. On a screen the same
  information is `14/30` and a bar, and it fits.
- **The talent mat is a tab**, not a panel. It is the one component that is A4 portrait
  (components.md:702) and it is only touched during Evolution.
- **The decision sheet is pinned to the bottom** and sized in `dvh`, so the browser chrome does not eat it.
  The board scrolls behind it.
- **Everything is a tap.** No hover, no drag, no keyboard. A long press is never the only way to do anything.
- **Two taps to commit anything destructive**: an Intent and a target set are confirmed, because a mis-tap on a
  phone is the misplay this app will produce most, and there is no undo (Part 6).
- **The chrome is legible in daylight**: the class colour is a strip *and* a printed class name, for the same
  reason components.md:805 gives for the print sheets.

---

### Desktop workspace and responsive fallback (2026-09-18)

The table keeps a compact planning desk beside the battlefield on desktop and an ordinary page flow on phones. Evolution
groups available packages by creature. Speed opens the acting creature’s spellbook as a reference, and a new Speed or
Intent question brings the controls and that hand into view together when needed. Cards and legal targets support keyboard activation and a second tap on
an existing selection confirms it; the explicit confirmation buttons remain. Selected targets have separate
removal buttons so a multi-target set can still be corrected.

Desktop uses normal page scrolling: the battlefield stays visible beside the main decision and acting
spellbook, cards wrap without height clipping, and the active hand appears first. Neither board nor hand has
a nested scrolling pane. Smaller screens retain links between board and decision.
Talents opens a movable, resizable, maximizable non-modal atlas; a phone gets a full-screen
panel. The atlas draws the package prerequisite graph as connected rows for tiers 1–3. Package names,
prerequisite ids, contained spells and initiative bonuses come from the catalogue; spell cards carry no
acquisition gates. The original `TalentBand.ParentCode` links still supply the family palette.
The first-level authored families receive cool, leaf and ember palettes, inherited by their specializations
and shared across all card surfaces. Selecting a package shows all its spells and whether the host currently offers it;
a legal Evolution pick can be submitted directly from this inspector. The sticky toolbar keeps the inspected
creature, Evolution pick number and remaining picks visible while reading spells. Down from the last spell
row reaches the explorer button; Enter opens it.

Keyboard shortcuts select numbered options, confirm with Enter, toggle the atlas with T, and close or clear
with Escape. Arrows switch Evolution creatures, enter their offered packages, and navigate the visible spell
or target rows without committing; focused controls retain their normal Enter behavior. They ignore text entry, repeat events and modifier chords and use the same asking and submission
guards as pointer input. Battlefield cards keep the creature's timeline position; enemy cards also retain
public speed, revealed spell, targets and resolution state. Each spell becomes public together with its
confirmed targets in timeline order (ADR 0070). Unconfirmed enemy choices stay hidden throughout targeting;
local selection reveals nothing. The previous round's
public action is labelled separately at the next round, using the retained public resolution feed.

Every offered option, card face, stat and ordering still comes from the host. The opaque hotseat handover
remains the privacy boundary. See [table/README.md](../../table/README.md) for controls and verification.

The round recap now opens on demand from a compact floating button, without moving the battlefield. A
persistent top phase guide keeps the round limit, current task and acting turn position visible. Creature-number headings,
labelled initiative slots, coloured stats/speeds and an ordered colour gradient make the board readable
without decoding pairs of numbers. The atlas displays spells side by side and keeps the shared team-pick
budget visible; picks are shared per evolution opportunity and capped by eligible creatures, with at most
one package bought per creature (ADR 0066). The guide shows the host-projected next evolution round.
TieOrder has its own phase label and keyboard controls, and timeline slots show the d20 roll-off. `CardCue` metadata supplies
short semantic badges for spell faces, with a critical reminder from the host. The maintainer confirmed that
Quick cannot crit and Standard can; main now enforces this rule in the engine.
These presentation changes do not alter combat resolution or the team's evolution allowance.

---

## Part 4. Hotseat, one screen

**Settled by the maintainer (2026-09-14): hotseat, one screen, to start.** The bill for each option is kept
below, because two devices is the next step and this is what it will cost when it is taken.

One fact reframes the choice, and it is worth stating before the table: **if the app is played on a phone, the
host must be reachable from that phone in both designs.** A loopback host is only enough when both players use
the browser on the machine running the engine. So the LAN bind is not what separates the two options.

| Day one | Hotseat, one device | Two devices |
| --- | --- | --- |
| Host binding | Loopback if played on the host machine; a LAN address if played on a phone | A LAN address, always |
| Seats | One page holds both seat tokens and shows one at a time; a full-screen "pass the device to Player 2" between seats | One token per device; a join screen, a short code or a QR, and the token kept in `localStorage` |
| Hidden information | The server sends one seat per request; the fence between the two is **the ritual** | The server sends one seat per token; the fence is **the token** |
| Waiting | None. The page that is showing is always the one that acts | The idle seat polls and shows what the other is doing, without saying what they chose |
| Reconnect | A reload re-fetches the pending seat | The same, plus a device that slept mid-Round must come back to the right seat |
| Closest failure | A player glances while the other chooses | A phone locks in the middle of a Round and the session waits |
| Closeness to the cardboard | High. One table, one shared board, one device passed | Lower. Two people looking down at their own screens |

**Why it is the right first target.** The thing being playtested is a board game. Its components put one shared board on
the table and a mat in front of each player (components.md:608-679), and the reading we want — is this
teachable, is this 15 to 30 minutes, does the tenth Round still make sense — comes from two people at one
table arguing about one board. A second screen changes the experiment before the first session.

Two things keep the door open to two devices, and they are part of the decision:

- The API is **per seat from day one** (Part 2.3), even though hotseat could have been served one combined
  payload. Two devices is then a transport and a token-distribution change, not a redesign.
- The seat token exists from day one, because the LAN bind needs it anyway.

**What hotseat means for hidden information, said plainly.** Both seat tokens live in one browser. The server
still answers one seat per request and never holds both in one payload, so nothing is *hidden on the client* —
but nothing stops that browser from asking for the other seat either. The fence is the pass-the-device screen
and the two people agreeing to use it. That is exactly the fence the cardboard has, and it is one more reason
hotseat is the honest first target: it does not pretend to a boundary it does not have. Two devices is where
the boundary becomes real.

---

## Part 5. What a session records

### 5.1 The formats that already exist

A session is a **recorded run of one match**, in the layout of `docs/learning/artifacts.md:17-25`:

```
runs/playtest/<session-id>/
  manifest.json          the run stamp, the feature schema, the counts
  steps.jsonl            one line per human decision
  episodes.jsonl         two lines, one per seat
  traces/<match-id>.json the full trace
  notes.jsonl            what a playtest needs and neither of the above carries (5.3)
```

Nothing new is written to produce the first four. `RunRecorder` writes exactly that directory
(`RunRecorder.cs:16-27`): `StartAsync` opens the files and writes the manifest with zero counts so an
interrupted session still says what it was (`RunRecorder.cs:47-56`), `Wrap` returns a `RecordingAgent`
(`RunRecorder.cs:58-69`), `MatchPlayedAsync` closes the episodes and the trace (`RunRecorder.cs:71-93`), and
`FinishAsync` rewrites the manifest with the final counts (`RunRecorder.cs:96`). The session id follows the
studio's shape (`StudioRunner.cs:59-63`).

Two details that matter for the dataset to be comparable with a bot run:

- **The client may show three Creatures' Speeds at once; the seat submits one decision at a time.**
  `RecordingAgent` records the candidates of the one Creature being asked (`RecordingAgent.cs:27-36` for
  Speed, `:38-48` for Intent). A batch submit would write steps whose `candidates` span three Creatures, and
  the file would no longer line up with `runs/greedy`.
- **`RecordingAgent` refuses an action the options did not offer** (`RecordingAgent.cs:64-69`). Combined with
  the host's pre-check of 2.2, a human session cannot write a step whose `action` is not in its `candidates`.

The stamp carries who played: `RunStamp.Player1Agent` is a string, and the console human is already stamped
`"Human"` (`GameSession.cs:94`). A human session stamps `human` (or `human:<initials>`), so
`compare-stamps` reports the agents axis as the difference (`RunStamp.cs:64`) and nobody mixes one human match
into a training set by accident.

**The trace is checkpointed.** `MatchTraceRecorder.EntriesOf` hands back the entries recorded so far without
forgetting the match (`MatchTraceRecorder.cs:53`), so the host can write the trace after every accepted
decision. A session abandoned at Round 9 still leaves a readable partial trace; a bot batch never needed this
because a bot batch never gets interrupted by dinner.

**It lands in `viewer/` with no change to the viewer.** The run directory opens as a Batch and the trace as a
Match (`viewer/README.md:12-13`). `notes.jsonl` is ignored: the viewer classifies a `.jsonl` by the fields of
its first row and answers `unknown` for anything that is not a training run, a step or an episode
(`viewer/index.html:58-64`). And `/session/{id}` serves the finished session through `ViewerPage.Render`
(`ViewerPage.cs:19-27`), so a playtest ends on a link rather than on a file to drag.

### 5.2 What a human game adds that a bot run does not

| What | Why a bot run cannot give it |
| --- | --- |
| **A dataset of human play** | `steps.jsonl` from a human is the input `train-clone` already reads, with no new format. Every bot dataset today clones a scoring function back onto itself. |
| **Disagreement with the heuristic, on the same board** | A step carries `candidates` and the chosen `action` (`artifacts.md:56-68`), so a bot can be scored on a human's board and the gap read per decision. That gap is where a weight is wrong, measured without a sweep (ADR 0037). |
| **Minutes** | Phase 2's target is 15 to 30 minutes (plan.md:110-118). The engine measures Rounds and has no clock in it by design — `TimeProvider` is a port precisely so a simulation is reproducible (architecture/overview.md:69-74). Only a human session can time a Round. |
| **Which decision was hard** | A bot's decision takes microseconds whatever the board. A human's does not, and the long ones are the rules that do not teach. |
| **A refusal** | A bot cannot make an illegal move: the projection only offers legal ones. A human tries, and *what they tried* is the rule they expected to exist. This is the single most useful signal a playtest produces and no existing artifact has a field for it. |
| **Which rule had to be looked up** | The rulebook of phase 4 is graded by this and nothing else. |

### 5.3 `notes.jsonl`, the one new format

One line per note, written through `IArtifactWriter.AppendJsonLinesAsync` (`IArtifactWriter.cs:16`) and timed
with `TimeProvider`. Same JSON conventions as every other artifact (`artifacts.md:31-43`).

| Field | Meaning |
| --- | --- |
| `sessionId`, `matchId`, `slot` | Which session, which match, which seat. |
| `round`, `subPhase` | Where in the match, as in `StepRecord`. |
| `at` | When (UTC). |
| `kind` | `Decision`, `Refused`, `Lookup`, `Misplay`, `Comment`. |
| `elapsedMs` | For a `Decision`: from the moment the seat's options were served to the moment the decision was accepted. |
| `code`, `message` | For a `Refused`: the `DomainError` the aggregate or the host's pre-check returned. |
| `text` | For a `Lookup`, a `Misplay` or a `Comment`: what the player typed or picked. |

Alignment with `steps.jsonl` is by order: the *n*-th `Decision` note of a seat is the *n*-th step of that seat,
because both are appended in the order the seat decided. No new identifier is introduced on `StepRecord`. A tie
order (ADR 0063) is the one decision that writes neither: no step is recorded for it, so no note is either, and
how long a player took over one is not measured yet.

Three buttons in the client produce the last three kinds: **"I had to look this up"** (with the Round's step
as the default subject), **"that was a misplay"**, and a free comment box on the end screen. They are one tap,
because a note that takes three taps is a note nobody writes.

`Decision` notes are automatic. Nothing about the timing is asked of the player.

---

## Part 6. The content hash and the rule set

components.md:907-913 raises it and this app has the same problem: **the content hash does not cover the
`RuleSet`.** The hash is over the consolidated catalogue (ADR 0009); the rule set is `RuleSet.Default`, a
static in the Domain (`RuleSet.cs:20`), pinned for every command by another static
(`GameSession.cs:30-31`). A session is reproducible against a hash **and** a rule set, or it is not
reproducible.

What the app does about it:

1. **It stamps both, on every artifact.** `RunStamp` already carries `ContentHash` and a `RuleSetStamp`
   (`RunStamp.cs:14-16`), the five numbers as plain values (`RuleSetStamp.cs:8`). The viewer's comparison
   already reports `rules` as its own axis (`RunStamp.cs:62`). Nothing is added; it only has to not be skipped.
2. **It takes the rule set as an input, and never silently defaults.** `table --rules <file>` builds it through
   `RuleSet.Create` (`RuleSet.cs:35-47`). With no `--rules`, the setup screen says, in words, that it is playing
   `RuleSet.Default` with a 30-Round cap — which is the simulator's number and not the table's
   (components.md:53-54).
3. **The setup screen prints the same one line the print sheets carry.** Every sheet the generator emits
   carries its content hash and its rule set (components.md:819-828). The app prints the same line, in the same
   order. Checking that a deck and an app are the same game is then one glance, by a human, which is the only
   check available until question 6 of components.md is answered.
4. **It does not answer that question.** Whether the rule set becomes a document under `data/`, a `studio
   --export` output, or five flags is one decision for the generator and the app together, and it belongs
   wherever components.md:907-913 is answered. What this app requires is only that **both** read the same
   answer: a printed deck and an app that disagree about the Round cap is a playtest of neither.

**And when the content changes while a session is open.** It will, often: the maintainer is tuning toward
10 to 15 Rounds. Nothing happens to the open session. `IGameResources` is resolved once, as a singleton, from
the built schema (`InfrastructureServiceCollectionExtensions.cs:38-46`), the `Match` keeps the hash it was
created with (`Match.cs:38,46`), and that hash is on every board the client fetches
(`PlayerBoardState.cs:20`). The host **must not reload**: a Match whose rules changed mid-Round is a playtest
of neither rule set. What it does instead is report the drift — `/api/session` compares
`IGameResources.Version` with `data/dst/game.schema.sha256` on disk (`GameSchemaJson.cs:13`) and says, in one
line, which content the session is playing and which is now on disk. A banner, not a refusal.

---

## Part 7. What is not in the first version

Stated, not implied.

| Not in v1 | Why, and what it would cost later |
| --- | --- |
| **Two devices** | Part 4. A transport and token-distribution change, on an API that is already per seat. |
| **Undo, rewind, and playing the current position out** | A `Match` has no undo, `InMemoryMatchRepository` keeps no history, and the aggregate cannot be copied: the repository holds the object itself (`InMemoryMatchRepository.cs:13,17-25`). So every one of these is the same mechanism — a **replay**, not a fork. A match is deterministic given its seed and its decision sequence, and `steps.jsonl` is that sequence, so an undo rebuilds the match and replays every step but the last, and "play this position out ten times with bots" replays the whole sequence into ten fresh matches. That replay is a decision of its own and it is not in v1. In v1 a misplay stays on the board and becomes a `Misplay` note — which is what happened at the table, and therefore the truth. |
| **A session that survives a host restart** | The match lives in `InMemoryMatchRepository`. A restart abandons the session; the checkpointed trace (5.1) is what is left. A file-backed adapter for the existing `IMatchRepository` would fix it. |
| **A push transport** | Part 2.3. |
| **A per-event narration of resolution beyond the whitelist** | v1 shows the board before and after, plus the whitelisted events. Anything richer must pass the default-deny filter of 1.3 first. |
| **A human-rolled die** | The engine rolls, through `IRandomSource`. Letting a player roll a physical d20 and type the face would be an adapter on that **existing** port, and it is the only way to playtest the die of decision B (plan.md:201-222) rather than the probability. **Which die is settled — a d20, with every chance a whole number of twentieths ([d20-criticals.md](d20-criticals.md))** — so the reason it is still out of v1 is not the die but the record: a typed face is an input nothing replays from a seed, and a number a player can mistype with no undo. v1 prints the threshold the card carries (`d20: 14+`) and says whether the roll landed. It cannot print the face: `CombatResolution` carries `IsCritical` and not the value behind it (`CombatResolution.cs:43`), so a face would mean a Domain change with its own ADR. |
| **Content editing** | That is the studio (ADR 0015). The table host reads the built schema and never writes content. |
| **Printing** | That is the printshop of components.md:766-860. |
| **Accounts, matchmaking, a lobby, spectators, chat** | There is one session per host process, and two people who are in the same room. |
| **Rendering `notes.jsonl` in the viewer** | The viewer ignores it today (5.1). A first pass of playtests should say what is worth charting before anything is charted. |
| **Animation, art, sound, i18n** | None of them is the measurement. |

A bot in seat 2 **is** in v1, and costs one option: a seat is an `IPlayerAgent`, so `--p2 greedy` puts the
existing agent in the other chair, exactly as the `human` command does (`GameSession.cs:94`). It is how one
person tests the app itself between playtests.

---

## Open questions

1. **Binding a LAN address with `HttpListener`.** ~~A non-loopback prefix needs a URL reservation on
   Windows.~~ Settled in stage 2: `table --bind <address>` takes one explicit IPv4 interface address and
   refuses a wildcard, which is what needs the reservation. It also refuses a shorthand (`192.168.1` is read
   as 192.168.0.1 by `IPAddress`) and an IPv6 literal, whose bracketed prefix this runtime's `HttpListener`
   cannot parse back. A LAN bind on the maintainer's own machine was checked; Windows is still unverified,
   and an address the machine does not answer on is one line and exit 1 rather than a stack.
2. **Two sessions at once.** One host, one session, is the assumption everywhere above. Two tables at a
   playtest evening means either two ports or a session id in every route.
3. **Who the players were.** A seat name is free text in the stamp. Whether a playtest wants a person's name in
   a committed artifact is a question for the first playtest, not for this document.

---

## The ADR

The decision record landed as [ADR 0054](../adr/0054-a-playtest-app-on-the-same-engine.md), Accepted on
2026-09-17, and the text that was held here moved into it. The record is the short form — the decision, what
it costs, and the alternatives that lost. This document stays the long form: what the client may never decide,
where the hidden-information boundary is, the screens, and what a session records.

One phrase changed on the way. The record says the host is bound to the loopback address **by default**, where
the text here said "a loopback HTTP host": stage 2 of the roadmap gives the table `--bind`, and the record's
own consequence list had already accepted it — "playing on a phone means the host leaves the loopback address."
