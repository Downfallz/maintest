# Arena UX parity: understand, plan and read the match

Date: 2026-11-04
Status (2026-11-05): AU1–AU13 merged; AU14 automated acceptance recorded.
Physical-device and new-player observation gates remain open; see [the acceptance record](arena-ux-acceptance.md).
Audit baseline: main `c2a1112b1596d7894cd41628db196df86fb86c7d`, including A7 / PR #432.
Baseline update: ART-2 / PR #436 merged as `c788cc2`; its WebP references are now on main.
Companion: [browser arena delivery plan](browser-arena-plan.md).

## Executive decision

At the original audit baseline, the arena supported the complete game loop without carrying the classic
table's accumulated decision support. The gaps below concerned players issuing a legal command without
understanding why it was their turn,
what a mutation buys, what upkeep already did, or what just hit their creatures.

Treat this as a dedicated UX workstream, **AU1–AU14**, separate from A1–A8 and ART-1–ART-6.
These 14 work items produce **15 implementation PRs**: AU3 is split into the required AU3a event
contract and AU3b receipt UI, in that order. The other IDs remain unchanged.
Recover the classic behaviour and its regression fixes, then adapt it to an immersive, viewport-bound arena.
Do not copy the scrolling page or import its monolithic entry point.

The first delivery milestone is AU1–AU5: a readable console, contextual guidance, visible upkeep,
comparable mutation cards, and one confirmation for the team's tempo drafts. Later slices deepen
history, round pacing, progression, combat decisions and reference tools.

| Maintainer's problem | Target experience | Delivery |
| --- | --- | --- |
| “The game starts; I did not know upkeep gave energy” | A start/round receipt explains recorded per-creature energy gains, ongoing ticks and the next decision; it remains in the console | AU1–AU3 |
| “What should I do? What is a mutation?” | Persistent current objective plus a short first-use explanation, dismissible and available again from Help | AU2 |
| “The webpage has three big choices, colours, tags and spell names” | Available mutations presented as comparable family-accented cards; the full tree is a separate reference | AU4, AU8 |
| “Choose all tempos in creature tabs, then commit” | Editable drafts for every eligible creature; one Confirm tempos action | AU5 |
| “I want a console explaining the round and each attack” | Chronological round journal, actual effects, expandable action details, filters and historical review | AU1, AU6–AU7 |

**Definition of success:** during a normal 3v3 game, the player can answer “what phase is this,
what must I choose, what will this choice buy, what just happened, and why did this creature change?”
without leaving the arena or remembering an animation that has disappeared.

## Evidence and limits of this audit

Compared the current source of `table/table.js`, `table/README.md`, the shared presentation/feed/card/
guidance/timeline modules, `arena/src/{live,planning,combat,match}.ts`, the corresponding arena
controllers, HTML/CSS, relevant ADRs, and merged UX PR descriptions across the repository history.
The historical ledger below identifies the changes to recover and the later corrections that supersede them.
This is a source/history audit, not a newly executed browser usability session. Runtime and visual acceptance
remain explicit work in each implementation PR.

The classic UI is the behavioural reference, not an infallible specification: some old PRs describe
auto-opening recaps or second-tap submission that later work changed. Current code and later decisions win.
A8 PR [#437](https://github.com/Downfallz/maintest/pull/437), inspected at
`29ab0ba21d34ecb35999c134dd17fcf4fd46feaf`, is still open at audit time. It owns opt-in lobby preference,
sound/recovery, asset budgets and release checks. Do not report those as merged or redo them here.
Refresh main and inspect A8 fixes before each implementation. ART-2
[#436](https://github.com/Downfallz/maintest/pull/436) has merged as `c788cc2`; its WebP references
are on main. AU4/AU12 must use that asset baseline and subsequent approved art changes. Art production
remains a separate workstream and does not block readable cards, guides or the console.

## Gap matrix

Updated against main `9c354652` (AU13 and branding #463). “Covered” denotes automated implementation evidence,
not physical-device certification or an observed novice playtest. Each row maps to the
[acceptance checklist](arena-ux-acceptance.md#gap-checklist); partial rows retain explicit follow-ups.

| Area | Classic table evidence / existing behaviour | Current arena status / merged evidence | Required change / owner |
| --- | --- | --- | --- |
| Phase and next action | `renderPhaseGuide`, Round guide, round cap, next evolution, decision/turn headings | Covered — contextual objective, round cap and next step; [AU2 #442](https://github.com/Downfallz/maintest/pull/442) | Explain current objective and upcoming step without exposing private choices; AU2 |
| First-use explanation | Phase notices, replayable announcements, mute/keep-open; no need to invent an elaborate tutorial | Covered automatically — mutation/tempo/mode-specific tips, mute and Help; [AU2 #442](https://github.com/Downfallz/maintest/pull/442). Novice observation pending | Short contextual introduction to mutation, tempo and targeting; AU2 |
| Opening energy | `upkeepEnergy` reads `catalogue.rules.energyPerRound`; waiting-room guard (#373) | Covered — actual grant event [AU3a #447](https://github.com/Downfallz/maintest/pull/447), receipt [AU3b #451](https://github.com/Downfallz/maintest/pull/451) | Record actual gains at the grant boundary (AU3a), then render the receipt (AU3b) |
| Upkeep details | Applied energy/heal/bleed ticks, condition chips, countdowns, per-kind steps (#250/#255) | Covered — actual/zero ticks, sources and missing-data explanations; [AU3b #451](https://github.com/Downfallz/maintest/pull/451) | Readable receipt with affected creatures and discoverable details; AU3 |
| Round turnover | Last cast → cleanup → upkeep → next question (#253/#410) | Covered — ordered actions/aftermath/upkeep, Auto/Step/Skip; [AU7 #460](https://github.com/Downfallz/maintest/pull/460) | One presentation sequence including meaningful turnover, consistent historical context; AU7 |
| Activity console | Activity feed, last-action line, structured round recap and announcements | Covered — retained public journal [AU1 #440](https://github.com/Downfallz/maintest/pull/440), filtering/pinned detail [AU6 #456](https://github.com/Downfallz/maintest/pull/456) | Docked/drawer console with structured records; AU1/AU6 |
| Mutation chooser | `evolutionButtons` / `packageCard`: offered cards, family colour, tags, spells, prerequisites, budget | Covered — comparable offered cards, tags, spells and budget; [AU4 #453](https://github.com/Downfallz/maintest/pull/453) | Available-now cards first, reference tree second; AU4 |
| Mutation reference | Inspectable tree outside purchase questions, ownership, paths, passives, shared picks | Covered — persistent read-only tree, ownership and public unlock receipt; [AU8 #457](https://github.com/Downfallz/maintest/pull/457) | Always-reachable read-only tree and unlock receipt; AU8 |
| Tempo | #411: tabs, editable team draft, one confirmation, compact spell reference | Covered — editable team tabs, one frozen batch and explicit interrupted continuation; [AU5 #454](https://github.com/Downfallz/maintest/pull/454) | Sequential host protocol retained; AU5 |
| Tempo consequences | Short rule and compact cost/critical spell reference | Covered — per-creature energy, cost/critical reference, catalogue fallback; [AU5 #454](https://github.com/Downfallz/maintest/pull/454) | Instinct/Precision tradeoff and expandable spell details; AU5 |
| Initiative / ties | Turn number, initiative, d20 rerolls, turn-order panel, completed slots | Covered — exact order/roll reference and same-team ordering; [AU11 #458](https://github.com/Downfallz/maintest/pull/458) | Public reveal only; same-team choices retain the host protocol |
| Spell selection | Compact grouped cards, detailed view, stable size, availability and cost | Covered — compact grouped cards, full details and retained browsing; [AU9 #455](https://github.com/Downfallz/maintest/pull/455) | Selected-target preview remains AU10 |
| Targeting predictions | Host guidance: remaining energy, normal/critical effects, caster effects and caveats | Covered — current-question estimates, costs, caster effects and fallback; [AU10 #459](https://github.com/Downfallz/maintest/pull/459) | Compact confirmation summary and current-board estimates; AU10 |
| Action result | Caster → spell → targets, critical/fizzle, applied effects, dropped targets, before/after | Covered — applied effects, recorded before/after and linked journal; [AU6 #456](https://github.com/Downfallz/maintest/pull/456) | Persist structured detail and link scene/result to its console entry; AU1/AU6 |
| Creature inspection | Conditions, source/duration, public mutations and spell faces, ally/opponent references | Covered — public mutations/spells/conditions and last action; [AU12 #461](https://github.com/Downfallz/maintest/pull/461) | Current or explicitly recorded/pinned reference; AU12 |
| Match conclusion | Victory/Defeat/Draw, reason, survivors/HP, full replay (#407/#377) | Covered — authoritative scorecard and replay through final result; [AU13 #462](https://github.com/Downfallz/maintest/pull/462) | Recover useful final summary and console/replay entry points; AU13 |
| Privacy and reconnect | Seat-filtered data, opaque hotseat, stable focus, waiting/ack fences | Covered for automated seat/lifecycle scenarios — [AU13 #462](https://github.com/Downfallz/maintest/pull/462); physical PWA resume remains open | Preserve fences; remove unnecessary single-seat friction after discovery; AU13/AU14 |
| Service tools | Push offer/test, playtest notes, audio/haptics, PWA Back/resume | Partial release gate — notes/notices/haptics [AU13 #462](https://github.com/Downfallz/maintest/pull/462), audio [ART-6 #450](https://github.com/Downfallz/maintest/pull/450); A8 #437 still open | Reconcile A8, carry over missing relevant tools with existing authorization; AU13 |
| Viewport/accessibility | Fixed decisions, scroll/focus retention, mobile target space, landscape fixes | Partial — AU14 keyboard and viewport acceptance; native zoom, screen readers and physical devices pending | Per-PR criteria, final parity pass AU14; not “overflow hidden = done” |

## Interaction specification

### Shell and console

Keep the battlefield as the primary surface. The compact header shows round, player-facing phase,
whose action it is, and labelled Help and History controls. A single stable command area states the next
action and contains the primary confirmation. Technical connection states are secondary unless interaction
is blocked; player-facing UI should not repeatedly say “confirmed by the host”.

On desktop, the console is a collapsible/resizable panel inside the viewport, with a readable recent-event
preview while collapsed. On portrait phones it is a drawer; short landscape uses an internal side panel.
Opening references must preserve the local draft. Closing them returns focus and the same selection.
Only one foreground reference surface should demand attention at a time.

Console defaults to the current round, with access to earlier rounds. Entries are ordered by authoritative
event sequence and grouped by actual event round (`event.roundId` when supplied).
A new-event count appears while the player reads older entries; do not force-scroll them to the bottom.
“Latest” returns to live follow mode. A filter must never turn a filtered result into “nothing happened”.

An action entry names the caster, spell, targets and actual effects with team labels, numeric changes and
textual critical/fizzle/defeat state. Expanded detail shows cost and before/after values only where recorded,
condition source/duration, dropped targets and reasons. Predicted damage is never presented as applied damage.
If the payload does not explain an exact mitigation calculation or die result, say that the detail was not
recorded; do not reverse-engineer it from final HP.

### Guidance and opening round

A persistent objective is always readable: for example, “Choose a creature and acquire a mutation.
1 team pick remains”, with values taken from this seat's current offers.

First exposure to a phase shows a short contextual help panel explaining the action and its purpose.
Provide Close, “Do not show phase tips again”, and Help to reopen it. Preserve the classic distinction:
muting instructional pop-ups does not erase factual upkeep/action history. Help never advances the engine,
submits a choice or acknowledges a covered question. Use real control labels, not a separate vocabulary.

Before `waiting` clears, including `waiting.seats: []`, say “Waiting for players”; do not announce
“Upkeep complete” merely because the engine prepared the first board.
Once play starts, show a compact upkeep receipt followed by the mutation objective. On a mid-game reload,
show “Round N · current state” and recovered history, without pretending the player watched a new round begin.

The receipt must show recorded ordinary round-energy gains even when no ongoing effect ticked.
The example “+2 energy” is not hardcoded or inferred from the catalogue: AU3a records the actual
per-creature gain before AU3b presents it, including passive contributions and zero gains.
Keep subsequent energy-regeneration ticks separate. Older recordings explicitly say when ordinary
energy details were not recorded; current board totals cannot fill that historical gap.

### Mutation choice versus Mutation Tree

The default chooser answers **“what can I take now, for this creature?”**. The tree answers
**“what paths can I build toward?”**. They are separate views over the same catalogue.

For the normal opening with three offered families, show three prominent comparable cards on desktop.
Use a readable stacked/paged mobile arrangement without shrinking the type. Do not assume there are
always three legal choices: later opportunities, different content and missing catalogue entries still work.

Each choice shows family accent plus text identity, mutation name, Stage, authored role tags, initiative
gain, included spell names, and a short spell/passive preview. Exact details remain one action away.
Keep creature tabs, effective remaining picks and the confirm footer visible. No tag exists for some
capstones; do not invent one. Stage-4 passives and shared ownership must not be rendered as new spells.

Selecting previews the mutation; confirmation explicitly names **creature + mutation + pick consumed**.
Switching creature clears a selection for the old recipient, preserving the A7 review fix.
Pass uses the existing host semantics and copy must state its actual effect on the remaining opportunity.
Do not silently turn “pass” into “skip only this creature”. No batch mutation protocol is introduced.

The read-only tree remains available during other phases. It shows acquired, privately picked, available,
locked and future states, multiclass paths and prerequisites. Public acquisition produces a short unlock
receipt with new spells/passive/initiative, stored in the console; no fake unlock on first load.

### Team tempo draft

One tab per eligible creature, labelled with identity and selected tempo or “Not chosen”.
Choosing a tempo advances to the next incomplete tab, but every tab remains editable.
Show a roster summary and enable **Confirm tempos · N/N** only when all required choices are complete.

Nothing is submitted before Confirm. After it, freeze the batch and answer the host's current creature,
one acknowledged `waitingAsked` at a time, using the refreshed question before the next write.
This is one player action over the existing protocol, not a new atomic server transaction.
Already accepted choices cannot be rolled back if a later request fails.

Stop on refusal, uncertain response, seat/round change, hidden page or access loss.
After a partial/uncertain result, reconcile accepted creatures from the authoritative seat view,
show which choices remain, and require explicit confirmation to resume. Never blindly resend the batch.
Ordinary same-question polls retain drafts; an unexpected external answer invalidates/reconciles them.
Do not reset the entire draft merely because successful batch progress advances `waitingAsked`.

The tab shows energy and compact known-spell cost/critical references. Host guidance is exact only for the
currently asked creature; for another tab use clearly labelled catalogue information, as #411 does.
Do not apply one creature's guidance to another.

### Reading combat and the round boundary

Offer Auto, Step and Skip to current state, separate from Help preferences and reduced motion.
Step lets the player read an opponent action at their pace; Auto remains quick; Skip never discards history.
Do not require a confirmation after every ordinary self-action in the default flow.

Read in order: closing action of round N → meaningful aftermath → round N+1 upkeep receipt →
next live objective. Keep phase, round, resources, spellbook and any budget consistent with the frame being
presented. A historical frame never supplies legal options or authorizes a write.
When a turnover event lacks an exact board, show a labelled textual receipt rather than fabricated before/after.
“No ongoing ticks” does not mean “no energy was granted”.

Opening a next-choice control may explicitly skip pending presentation to the latest state, as A6 allows.
The console still contains every received fact. No animation completion sends a decision or acknowledges
a hidden/covered question. A long reconnect catches up with a summary and unread history, not a mandatory
queue of old animations.

## Data and reuse boundaries

| Source | What to reuse | What must not be inferred |
| --- | --- | --- |
| `table/feed.js` | Exported `resolvedSince`, `lastResolved`, `roundRecap`, `roundUpkeep`, `turnoverSteps`, `matchReplay`, effect text | No parsing narration to recover event types; no arbitrary poll subtraction |
| `table/presentation.js`, `replay.js`, `arena-playback.js` | Latest seat vs presented frame, explicit action before/after, cancellation/watermarks | No historical board used for commands |
| `table/mat.js`, `card.js`, `hand.js` | Prerequisites, ownership, tags, passive text, card fields, availability | No legality from family colour, spell ownership or asset names |
| `table/guidance.js`, `slot.js` | Host guidance and adapted Action/Intent/Target options | No local combat simulation; no guarantees from previews |
| `table/timeline.js`, `ties.js`, `terms.js` | Order, d20 details, grouped ties, player-facing labels | No client sorting or rule renaming |
| Classic `table.js` | Extract narrow pure models for phase guidance, mutation faces and tempo drafts, with classic regression coverage | Do not start its poller or port its entire DOM/state machine |
| `arena-live.js`, transport/session modules | One selected seat, SSE invalidation + authoritative reads, write fence, history reads | Never fetch another seat or an unrestricted trace to complete history |

`recapAction` is private to `feed.js`. Reuse its normalized results through the exports above; this
plan does not require exporting it or widening the module's public surface.

Proposed new modules such as `table/arena-history.js` and `table/arena-guidance.js` are implementation
names, not existing APIs. Keep normalization testable outside Phaser and DOM rendering in `arena/src`.
Use a single event presentation model for console, receipts and action details, avoiding divergent strings
and independently “seen” histories.

**History is not animation.** Store/de-duplicate eligible public records independently of the playback
watermark. First load must populate the journal even when it deliberately plays no old animations.
Use table/seat/sequence identity, respecting `feedNext` as an examined-entry cursor: private filtering
creates legitimate sequence gaps. Keep a bounded render/cache window; expose “Load earlier rounds”
rather than silently implying that the last two rounds are the whole match.
The existing selected-seat `history()` rereads from zero. Reuse it on explicit history demand and after
reload; do not fetch from zero on every poll. Never move the live cursor backwards after an older read.
If larger sessions require pagination, make that a measured follow-up to the same filtered endpoint.

The live console's factual feed excludes unrevealed own/opponent planning entries, even if the selected seat
can see its own private events. Local drafts and accepted-but-hidden picks belong in the command UI, clearly
labelled. Purchases and tempos enter shared factual history only at their authoritative public reveal.
Transient errors and help notices can have separate labels and must not impersonate domain events.
Clear all seat-bound text, filters with creature identities, drafts and cached frames on viewpoint/access changes.
Do not persist tokens or private event payloads into a new local history store.

### Original evidence gap: ordinary upkeep energy (addressed by AU3a)

`OngoingEffectsApplied` contains ongoing energy-regeneration, healing and bleed ticks.
`RoundStarted` contains the round identity, not ordinary energy gains.
At the original audit, `UpkeepRules.EnergyGain` granted the rule allowance plus passive upkeep energy
without a per-creature receipt; the normal seat feed removes trace boards. Thus the existing “no ongoing ticks”
line cannot explain all upkeep, and the catalogue allowance is not proof that every creature actually gained it.

Decide and implement the missing event contract first, in required PR **AU3a**, before the **AU3b**
receipt UI. Capture the actual result at `UpkeepRules.EnergyGain`'s grant boundary and publish it
through the existing seat-safe feed. The shared presentation model then consumes recorded facts once,
without first shipping a nominal-allowance receipt that a later PR must replace.

The persistent extension needs an ADR, additive serialization and compatibility tests for old recordings,
event consumers and session rebuild/checkpoints. Older recordings retain an explicit “ordinary energy
details were not recorded” fallback; that is compatibility handling, not the normal new-match experience.
Do not infer historical gains from the catalogue or two polls, rewrite old events, or change balance.

## PR sequence

IDs below are planning IDs, not GitHub PR numbers. Keep each slice independently reviewable, with its
own reviewable result, source-based checks and regression cases. UI slices also require user-visible
acceptance and browser screenshots; AU3a is validated as an event/compatibility change before AU3b uses it.
Dependencies are functional dependencies; default delivery order is the table order.
No implementation slice is completed by this document.

| ID | Reviewable result | Main areas | Depends on |
| --- | --- | --- | --- |
| AU1 (implemented) | Always-available basic combat console, current/previous round entries and expanded action facts | New history model/UI, `feed.js`, `arena/src/live.ts`, shell | A7 |
| AU2 (implemented) | Current objective, Round guide and dismissible contextual phase tips | Guidance model/UI, `terms.js`, current legal question | AU1 |
| AU3a (implemented) | Record ordinary upkeep energy at the grant boundary; additive public event and compatibility ADR | Domain upkeep/event, serialization, seat visibility, consumers and rebuild tests | AU1; before AU3b |
| AU3b (implemented) | Opening/round upkeep receipt from recorded ordinary gains and ongoing ticks | `arena-match.js`, shared upkeep model, console, guidance | AU1–AU2, AU3a |
| AU4 (implemented) | Available mutation cards, creature/pick context and explicit confirmation | `planning.ts`, `arena-planning.js`, shared mutation/card helpers | AU2 |
| AU5 (implemented) | Team tempo tabs and one explicit batch confirmation | Pure tempo draft/batch controller, planning UI, connection fences | AU2 |
| AU6 (implemented) | Console filters, earlier rounds, detailed action inspection and live round review | History loading/rendering and recorded action boundaries | AU1, AU3b |
| AU7 | Step/Auto/Skip and coherent last-action → aftermath → upkeep pacing | Playback coordinator, match model, historical display | AU3b, AU6 |
| AU8 (implemented) | Persistent Mutation Tree reference and public unlock receipts | Tree reference, inspection, shared acquisition data | AU4, AU1 |
| AU9 | Compact grouped spellbook with stable selection and full details (implemented) | `combat.ts`, `card.js`, `hand.js`, responsive UI | AU2 |
| AU10 (implemented) | Host-backed targeting preview and readable confirmation summary | `guidance.js`, combat controls and target HUD | AU9 |
| AU11 | Readable initiative/ties and reveal summary | `timeline.js`, `ties.js`, planning + scene overlays | AU5, AU1 |
| AU12 | Creature inspector with public mutations, spell details and effect sources | `live.ts`, inspection surface, cards/conditions | AU8–AU11 |
| AU13 | Result/replay and missing service-tool parity after A8 reconciliation | Match UI, seat discovery, existing push/notes/feedback/session helpers | AU6, AU12; reconcile #437 |
| AU14 | End-to-end parity acceptance and device/accessibility polish | All changed journeys, acceptance record and screenshots | AU1–AU13 |

AU3 means the combined upkeep milestone, completed only when **AU3a and AU3b** have landed.
AU3a's event contract is a prerequisite, not an optional follow-up. AU1 and AU2 can proceed independently;
the new receipt UI must wait for the recorded facts. No implementation or ADR is added by this plan.

### AU1 — A useful console immediately

Deliver a labelled History control, a collapsed last-event preview, chronological current/previous-round
list, and expandable caster/spell/target/effect rows. Reuse actual applied outcomes and name lookup.
Public upkeep/aftermath/reveal events have their own row types, with explicit fallback text for unknown kinds.
The display is DOM-based and readable without sound, motion or colour.

Acceptance: two or more casts in one response all remain readable; own and enemy actions are included;
critical, no-critical, fizzle, caster effect, multiple targets, dropped target and death are distinguishable.
A new page loads history without replaying it. Duplicate polls/SSE do not duplicate entries; missing frames
show text. No unrevealed pick/tempo/intent appears. Opening/closing preserves command drafts and focus.
Tests: pure normalization/deduplication/visibility tests and desktop/phone console journeys.
Do not wait for filtering or a perfect replay UI to deliver this slice.

Delivered: `table/arena-history.js` and `arena/src/history.ts`, described in
[the arena README](../../arena/README.md#au1-recent-match-history).

### AU2 — Know what to do

Deliver the persistent objective, round flow, current decision/creature, next mutation opportunity and Help.
Introduce mutation as a whole acquisition (spells or passive plus initiative), shared team picks, and
Instinct versus Precision. Use the actual declaration mode when explaining combat.
Tips appear once per relevant phase/context, not on every poll or creature click; replay and mute are explicit.
Store only preference/help-version information, with no new credential-derived storage scheme.

Acceptance: start, later even round, depleted picks, opponent turn and tie question each state the correct
next action. Waiting-room events do not trigger tips/receipts prematurely. Help is keyboard-operable,
can be reopened, never obscures the sole confirmation, and does not mark a covered question shown.
Test both declaration modes and “mute tips but keep history”.

Delivered: `table/arena-guidance.js` and `arena/src/guidance.ts`, described in
[the arena README](../../arena/README.md#au2-objectives-and-contextual-help).

### AU3a — Record ordinary energy before designing its receipt

Implementation: `RoundEnergyGranted`, public journal/replay readers and versioned recording compatibility
are specified in [ADR 0132](../adr/0132-record-ordinary-round-energy-without-renumbering-old-tables.md).
New matches record actual grants; legacy resumed matches retain their original event sequences for life.
The automatic receipt is implemented separately in AU3b below.

Write the persistent-contract ADR and add a public event at the ordinary energy-grant boundary.
For each eligible living creature, record the configured base and passive request amounts, actual combined
gain and before/after energy. Preserve the existing single combined `GainEnergy` call and its ordering.
The current engine adds the requested amount for a living creature and has no gameplay energy cap;
do not introduce one for the receipt. Read the grant's return value instead of recomputing it in the UI.
Zero applied gains remain observable. Creatures already dead receive no grant; a creature killed later
by ongoing damage still has its earlier energy receipt.

Expose the event through the existing seat-safe feed. Update event classification, serialization and
consumers exhaustively, including journal normalization and legacy readers; no unrestricted trace access.
Audit session rebuild/checkpoints, recording compatibility and event-sequence consumers. Keep gameplay
outcomes, decision order and randomness unchanged; update event-based fixtures only for the added record.
The ADR defines the final event name/fields and compatibility policy before the UI depends on them.

Acceptance: non-default rule allowance, passive bonus, zero gain, already-dead creature,
energy gained before lethal ongoing damage, both seats, round-one arrivals, deterministic rebuild and
older recordings without the event. Assert that the receipt equals the amount actually returned by the
grant and that the before/after values agree. No expected total is reconstructed in the browser.

### AU3b — Make automatic upkeep visible from recorded facts

Implemented after merged AU3a #447, preserving its classic replay fixes and ADR 0132 compatibility.
See [the receipt implementation and verification](../../arena/README.md#au3b-automatic-upkeep-receipts).
The automatic receipt waits for arrivals, the recorded ongoing-effects event, playback and open dialogs;
History/round review/replay keep recorded zero values and explicit legacy gaps. A bounded tab-local
presentation marker prevents repeats without saving event facts or acknowledging questions.

After AU3a, deliver a compact first-round receipt once arrivals clear and a receipt on each observed
round transition. Show recorded ordinary gains, then ongoing energy, healing and damage in engine order.
Show before/after energy and recorded base/passive context in the details; separate current-condition
durations from historical facts. Link affected creatures to inspection and group rows rather than demanding
a click for each creature. No-tick rounds still show their ordinary-energy receipt, including zero gains.
The console retains the receipt after dismissal; replay and action details share its fact model.

Acceptance: non-default rules, passive bonus, zero ordinary gain, no ongoing effects, healing
before lethal bleed, capped zero healing, additional energy, dead creature and expired condition.
Old recordings show missing-energy-detail text alongside the facts they do contain; they never receive
an invented exact total or an ordinary-energy event synthesized from a nominal allowance.
Do not repeat a receipt after reconnect or attribute next-round facts to a previous-round attack.
Waiting-room events do not announce upkeep before arrivals clear. AU7 later adds paced choreography.

### AU4 — Compare mutations at a glance

Deliver the available-now card chooser specified above, restoring family palette, tags, spell names and
initiative from served content. Separate details from selection and confirmation; preserve input focus on polls.
Show effective remaining picks, ineligible/already-picked creature context, empty offers and Pass.
The full forest remains reachable as a reference but is no longer the mandatory purchase interface.

Acceptance: opening three families, later multiple legal upgrades, multiclass, capped picks with dead allies,
same-family capstone/passive/shared state, missing tags/catalogue fallback and long names.
Confirm names the selected recipient. Switching creature cannot buy for the previous one.
Screenshots must compare three actual opening cards on desktop and readable phone cards, not only a tree.

Delivered: `table/arena-mutations.js` and `arena/src/mutations.ts`, integrated with the existing
planning controller. See [implementation and real-host captures](../../arena/README.md#au4-compare-offered-mutations).
The tree remains read-only during Evolution; AU8 adds persistent public reference access.

### AU5 — Draft the whole team's tempos

Extract the behaviour of #411 into a narrow model/controller instead of importing `table.js`.
Implement tabs, reference cards, local editing, completion summary and frozen confirmed-batch execution.
Keep own choices private until the host reveal, including partial submission and reconnect.
Document that the first acknowledged creature's timing includes the team's draft time, matching #411.

Acceptance: 1/2/3 eligible creatures; select all, return to change the first, no writes until Confirm;
sequential host questions each answered once; double-click/held-key protection; intermediate refusal;
a lost response after acceptance; external answer; hidden tab mid-batch; seat/round change and reconnection.
Compare the opposite seat's entire board, options, feed and asking before/after private choices.
No “atomic” claim: interrupted batches reconcile, then require explicit user continuation.

Implemented by `table/arena-tempo.js` and `arena/src/tempo.ts`, through the existing planning facade.
[ADR 0135](../adr/0135-draft-team-tempos-before-sequential-confirmation.md) replaces the one-creature UI
without changing the host protocol. Local drafts are never persisted; reload reconstructs accepted choices
only. Focused controller/browser tests and real-host private-seat journeys cover the acceptance cases above.

### AU6 — Read the round and earlier attacks

Add round/creature/team/event-kind filters, unread count, stable scroll, “Latest” and “Load earlier rounds”.
Use separate action details rather than one long unformatted sentence.
Add read-only Previous/Next and Before/After for a selected round/action using exact frames;
return to the latest live state explicitly. An older receipt without a frame never borrows current stats
and calls them historical.

Acceptance: round rollover while reading; reload and earlier history; old read arriving after newer events;
filtered-empty state versus unavailable history; access loss clears details; a live question remains unacknowledged
while covered by history. End-of-match journal includes the final attack.
Keep DOM/memory bounded and measure a long session. Do not fetch another seat or an unrestricted trace.

Delivered in `table/arena-history.js` and `arena/src/history.ts`: typed filters, explicit earlier-page
reads through the selected seat's existing `history()`, structured action facts, Previous/Next and
recorded Before/After inspection inside the journal. The arena underneath remains live; historical
snapshots never supply commands. See [AU6 behavior, limits and captures](../../arena/README.md#au6-filter-and-inspect-match-history).

### AU7 — Let the player follow what happened

Implemented in #460, based on merged AU6 #456 (including its early next-round feed
fix), AU5 #454, AU9 #455 and merged AU8 #457. Auto/Step/Skip share the action/turnover queue. Missing turnover snapshots use
labelled text-only receipts; Next tracks explicit reading separately from receipt/sequence consumption.
Opening a live command skips to the authoritative state. Upkeep presented in the queue, or passed over by the player's Skip, does not reopen as a
second automatic modal. Round 1 retains its arrival-gated introduction; existing drafts defer notices.
See `arena/README.md` for bounds, lifecycle behavior and acceptance tests.

Unify the presentation coordinator for action results and meaningful turnover receipts, with Step/Auto/Skip.
Build on A6 cancellation and exact frames. Step adds user pacing; it is not a second game driver.
Avoid repeated upkeep notices when the same receipt was already read.
Use separate state for received, presented and explicitly read items so skipping animation preserves facts.

Acceptance: the player's own closing attack, several bot actions in one read, cleanup + next upkeep,
kill during upkeep, match ending before another decision, long catch-up and missing frames.
Check the phase, round, actor, resources and visible spellbook at every step. Next-round purchase budget
must not appear over the old round. Skip immediately restores current command eligibility.
Rotation, reduced-motion changes, hidden tab and revocation cancel stale timers.

### AU8 — Understand growth and new abilities

Implemented with Help/inspection access, public creature progress, shared authored tree details and
a compact reveal receipt. New own spells are marked on their first visible spell choice; private
picks remain in the command sheet. See `arena/README.md` for lifecycle and validation details.

Make the Mutation Tree available from Help/inspection outside Evolution. Show paths and prerequisites
using the authored graph, acquired/picked/available states, multiclass progress and passives.
Read-only reference never acknowledges a question. A public reveal generates one compact unlock receipt
and highlights new spells on their first relevant choice, without forcing an extra modal.

Acceptance: own hidden picks stay in the private command context, opponent purchases appear only at reveal;
no false unlocks on reload; next opportunity comes from host data; shared passives are labelled correctly.
Graph and detail navigation work on phone and keyboard; close restores the original draft.

### AU9 — Choose a spell without fighting the list

Use served card types/family accents for compact grouping, visible cost and positive critical indicators,
availability reasons, plus an explicit full-details mode. Show actor, energy and turn context together.
Use paging/internal scrolling appropriate to the viewport; keep confirmation stable.
The selected row must not grow under the pointer, and polls must not reset focus/scroll.
Show zero energy cost; omit meaningless zero critical badges.

Acceptance: late multiclass spellbook, long names, expensive/unoffered cards, no critical, free spell,
hidden-intent energy warning and unknown catalogue entry. Changing creature starts at its own reference
position; a same-question poll preserves deliberate browsing. No second tap submits a spell accidentally.

Implemented by `table/arena-spellbook.js` and `arena/src/spellbook.ts` through the existing combat
controller. Served types/palette remain presentation-only; current host offers alone control eligibility.
The reference preserves its DOM, focus and scroll through same-question reads and field-target round trips;
a new question clears it. Full details exposes long names and complete card text, including unavailable
cards. The fixed footer retains explicit confirmation and the existing Action/Intent/Target protocol.

### AU10 — Understand targets and cost before confirming

Render host guidance for the selected action/targets: cost and remaining energy, expected plain/critical
effects, caster effects and availability reason. Keep it compact beside the fixed confirm summary.
Show creature identity and spell name, selected count and legal range, with explicit change/cancel actions.
Preview is labelled as an estimate; actual outcomes later come from the console.
Only use guidance present for this question, with a useful “preview unavailable” fallback.

Acceptance: enemy, ally, self, multi-target and no-target spells; too few targets; dropped/dead target;
stun/refusal; resource change; unseen opponent choices and at-activation versus hidden-intent.
Legal/selected/unavailable targets are distinguishable by text/shape as well as colour, with at least
44px touch controls and adequate separation. Selection never auto-submits.

Implemented by `table/arena-targets.js` and `arena/src/target-preview.ts` through the existing combat
controller. Cost and effects come only from this asking's guidance. The fixed confirmation remains outside
the scrollable estimate; selected targets group equal effects and caster effects appear once. Text/shape
cues distinguish target states. Missing outcomes and targetless caster guidance stay explicitly unavailable;
no catalogue calculation fills gaps. Hidden plans retain the existing intent protocol. Unit, responsive
browser and real-engine combat tests cover the presentation and command boundary.

### AU11 — Explain the order and the dice

Deliver an inspectable turn order with tempo bands, position, initiative, actual d20 rolls/rerolls,
current/completed/upcoming states and explicit team labels. Link entries to creatures.
Summarize tempo reveal once and make it recoverable from the console; do not cover an unanswered tie form.
Tie controls show which places are fixed by the dice and which same-team order remains editable.

Acceptance: no ties, same-team ties, cross-team rerolls, multiple tie groups, defeated/stunned actors,
and both declarations. Keep exact host order. No opponent tempo summary before reveal.
The compact strip does not force horizontal page scrolling; detail handles the long explanation.

Implemented in [PR #458](https://github.com/Downfallz/maintest/pull/458), `table/arena-order.js` and `arena/src/order.ts`, based on merged AU5 #454.
See [AU11 behavior and limits](../../arena/README.md#au11-turn-order-and-recorded-dice).

### AU12 — Make every creature inspectable

Extend existing stats/condition-source inspection with public mutation ownership, passive/initiative
information, full known-spell details, last public action and its linked history entry.
Expose defense/current initiative and condition cues at a glance where legible; use inspection for detail.
Names/IDs must disambiguate identical creature art and remain stable across scene and console.

Acceptance: ally and opponent inspection, duplicate names, acquired versus known spells,
new/permanent/expiring conditions, stun immunity, missing art and a defeated creature.
No private plans inferred from artwork or public spellbooks. A historical action's inspector uses its
recorded frame or clearly states unavailable detail; it never silently substitutes live state.

Implemented in [PR #461](https://github.com/Downfallz/maintest/pull/461): `table/arena-inspection.js`, `arena/src/inspection.ts` and the journal's bounded last-cast
references. See [AU12 behavior and validation](../../arena/README.md#au12-public-creature-inspection).
The current Mutation Tree is hidden in historical inspection; recorded ownership and missing-field labels
remain explicit. Desktop captions expose defense/initiative and conditions where space permits. The journal
keeps one extra sanitized last cast per roster creature; it neither retains unrestricted traces nor changes
its 200-row page cap. Physical-device validation remains part of AU14.

Validation (2026-11-05): all sixteen required arena suites were exercised across desktop, 390px phone and
320px phone: **684 passing cases, 21 existing viewport skips**, including successful targeted reruns after
reference-selector and short-screen fixes. **Eight real-host journeys pass**: complete games and recorded
replay in both declarations, two-seat privacy, and both-team public inspection after a purchase, on phone
and desktop. **1,099 JavaScript tests** and **2,276 .NET tests** pass (26 existing storage-emulator skips).
Bundle/build and changed-file whitespace checks pass; full format passed in CI on the implementation commit
(the local MSBuild/named-pipe restriction prevents that full check). Local browser verification used Chromium
headless shell 142; CI uses the pinned Playwright browser. Captures above are labelled fixture/real host.


### AU13 — Finish the surrounding play experience

Implemented: host-derived result card and final replay frame, recorded-seat notes, explicit notification
controls and haptic opt-in, single-seat resume with two-seat privacy. Existing A8 sound, recovery,
presentation preference and budgets remain in place. See `arena/README.md` for verification.

Recover a clear Victory/Defeat/Draw panel with authoritative reason, final survivors/HP, console and replay.
Preserve concession confirmation, accepted-concession fencing, empty/legacy replay handling and the current
recorded-session permission boundary. Do not add unrestricted player access to operator-only recordings.

Reconcile #437 before editing: retain its sound opt-in, same-seat recovery, rollout preference and budgets.
Then carry over missing service tools through existing helpers: optional haptic/selection feedback,
notification offer/status/test where supported, and playtest Lookup/Misplay/end-comment only when recorded.
These settings are user actions, never automatic subscriptions or unsolicited messages.
A single unambiguous accepted seat should resume directly; two held seats still require explicit viewpoint
selection with a fully cleared private surface. Preserve invitation acceptance and account ownership.

Acceptance: win/loss/draw/concede/round cap; full replay through final result; empty/unrecorded sessions;
one seat, two seats, revoked invitation/account and operator replay; notification unavailable/denied and
both-seats-on-one-device suppression; unrecorded notes hidden. Existing services are not reimplemented.

### AU14 — Parity acceptance, not another feature bundle

Run the cross-cutting matrix below and fix observed failures within this scope.
Recheck main/A8/ART changes and update the gap matrix with actual implementation PRs and evidence.
Keep the classic fallback. A8's technical release status must not be used as proof of completed UX parity.
Do not make the arena default as a side effect of this plan.

Acceptance record contains actual desktop/phone captures, test results, physical-device checks
where performed, open limitations and a checklist for each gap row.
AU14 evidence: [acceptance record and current captures](arena-ux-acceptance.md). Automated checks cover
the information needed for the following journey; a human observation has not yet been performed.
A new player must be able to explain the opening energy, select a mutation knowingly, prepare all tempos,
identify why a creature lost HP and reopen a previous event. Observe that journey instead of relying only
on assertions that a button exists.

## Historical recovery ledger

PRs are evidence, not patches to cherry-pick blindly. Read subsequent fixes and current code first.
All numbers link to this repository.

| Historical PRs | Behaviour to preserve / later correction |
| --- | --- |
| [#136](https://github.com/Downfallz/maintest/pull/136), [#147](https://github.com/Downfallz/maintest/pull/147) | Phone board, seat-safe feed, retained finished feed and honest decision timing |
| [#154](https://github.com/Downfallz/maintest/pull/154) | Hierarchy, creature-scoped choices, stable polling/focus, stale-answer and hotseat guards |
| [#155](https://github.com/Downfallz/maintest/pull/155), [#157](https://github.com/Downfallz/maintest/pull/157), [#159](https://github.com/Downfallz/maintest/pull/159) | Visible selected spell, structured recap, desktop desk, atlas, phase/upkeep guidance and keyboard control; later code makes recap on demand |
| [#175](https://github.com/Downfallz/maintest/pull/175), [#176](https://github.com/Downfallz/maintest/pull/176) | Same-team tie ordering and public d20/reroll explanation |
| [#218](https://github.com/Downfallz/maintest/pull/218) | Host-backed decision previews and reproducible practice scenarios |
| [#223](https://github.com/Downfallz/maintest/pull/223), [#225](https://github.com/Downfallz/maintest/pull/225), [#226](https://github.com/Downfallz/maintest/pull/226) | Compact phone guide/reference, battlefield always reachable, keyboard/overflow fixes |
| [#227](https://github.com/Downfallz/maintest/pull/227), [#231](https://github.com/Downfallz/maintest/pull/231), [#235](https://github.com/Downfallz/maintest/pull/235) | Immediate confirmed resolution, opponent steps, mute/turn-order controls, correct action's turn heading |
| [#240](https://github.com/Downfallz/maintest/pull/240), [#243](https://github.com/Downfallz/maintest/pull/243), [#250](https://github.com/Downfallz/maintest/pull/250) | Fixed decision context, acquisition recap, applied upkeep, stable compact cards, per-creature scroll and targeting estimates |
| [#253](https://github.com/Downfallz/maintest/pull/253), [#255](https://github.com/Downfallz/maintest/pull/255) | Read attacks before next-round notice; affected-creature tick chips, condition durations and reachable match tools |
| [#261](https://github.com/Downfallz/maintest/pull/261), [#265](https://github.com/Downfallz/maintest/pull/265), [#266](https://github.com/Downfallz/maintest/pull/266) | Face-down purchases, wait for players, saved-table continuity |
| [#285](https://github.com/Downfallz/maintest/pull/285), [#289](https://github.com/Downfallz/maintest/pull/289), [#292](https://github.com/Downfallz/maintest/pull/292), [#389](https://github.com/Downfallz/maintest/pull/389), [#390](https://github.com/Downfallz/maintest/pull/390) | Authored package tags and names; Mutation/Stage/Tempo and phase terminology; no hardcoded retired aliases |
| [#321](https://github.com/Downfallz/maintest/pull/321), [#337](https://github.com/Downfallz/maintest/pull/337), [#338](https://github.com/Downfallz/maintest/pull/338), [#341](https://github.com/Downfallz/maintest/pull/341) | Honest energy-shortfall warning, simultaneous planning, at-activation default and hidden-intent support |
| [#373](https://github.com/Downfallz/maintest/pull/373), [#374](https://github.com/Downfallz/maintest/pull/374), [#375](https://github.com/Downfallz/maintest/pull/375), [#376](https://github.com/Downfallz/maintest/pull/376) | No premature upkeep/solo handover; larger target zones; explicit invitation; all own picks without waiting between them |
| [#377](https://github.com/Downfallz/maintest/pull/377), [#378](https://github.com/Downfallz/maintest/pull/378) | Whole-match and recorded-session replay with existing permissions |
| [#379](https://github.com/Downfallz/maintest/pull/379), [#380](https://github.com/Downfallz/maintest/pull/380), [#381](https://github.com/Downfallz/maintest/pull/381), [#382](https://github.com/Downfallz/maintest/pull/382), [#383](https://github.com/Downfallz/maintest/pull/383), [#384](https://github.com/Downfallz/maintest/pull/384), [#392](https://github.com/Downfallz/maintest/pull/392) | Visible-question timing, SSE refresh, notification offer/test/diagnosis, no notices for a device holding both seats |
| [#394](https://github.com/Downfallz/maintest/pull/394)–[#398](https://github.com/Downfallz/maintest/pull/398) | Reduced-motion foundation, button/spell/target selection, nonblocking overlay transitions |
| [#399](https://github.com/Downfallz/maintest/pull/399)–[#405](https://github.com/Downfallz/maintest/pull/405) | Confirmed combat effects, HP/defense/status/death, initiative, public reveal, phase motion and replay effects |
| [#406](https://github.com/Downfallz/maintest/pull/406), [#407](https://github.com/Downfallz/maintest/pull/407), [#408](https://github.com/Downfallz/maintest/pull/408), [#409](https://github.com/Downfallz/maintest/pull/409) | Mutation/unlock cues, results, connection/PWA Back/resume, opt-in sound/haptics |
| [#410](https://github.com/Downfallz/maintest/pull/410), [#411](https://github.com/Downfallz/maintest/pull/411) | Latest essential corrections: ordered turnover without future-round UI; all tempo drafts before one confirmation |
| [#412](https://github.com/Downfallz/maintest/pull/412), [#413](https://github.com/Downfallz/maintest/pull/413), [#428](https://github.com/Downfallz/maintest/pull/428) | Landscape composition/contrast/overflow and clear player identities |
| [#420](https://github.com/Downfallz/maintest/pull/420), [#425](https://github.com/Downfallz/maintest/pull/425), [#427](https://github.com/Downfallz/maintest/pull/427), [#430](https://github.com/Downfallz/maintest/pull/430), [#431](https://github.com/Downfallz/maintest/pull/431), [#432](https://github.com/Downfallz/maintest/pull/432) | Preserve arena isolation, presentation boundary, authenticated seat, explicit commands, cancellable confirmed frames and complete-loop fixes |

### Deliberate adaptations, not regressions

- Keep explicit confirmation in the arena. Do not reintroduce old second-tap-to-cast behaviour.
- Keep the arena viewport-bound. Reuse classic information architecture, not page scrolling or every draggable window.
- Retain existing per-seat privacy; do not transplant classic hotseat code into two simultaneous arena pollers.
- Reuse catalogue names and the current art direction. Approved-but-unimplemented art renames are not a UX migration.
- Neither guides nor animation settings change rules, timing of engine resolution, randomness or balance.
- A current summary of conditions is not historical evidence of a tick or expiration.
- New onboarding is a proposed improvement built on recovered phase guidance; do not claim an old full tutorial existed.

## Validation and completion gates

Each behaviour-changing PR must run the repository checks required by AGENTS.md:
`dotnet build`, `dotnet test`, `dotnet format --verify-no-changes`,
`node --test studio/*.test.js table/*.test.js`, the arena bundle build, and the arena browser suites.
When shared helpers change, run the classic table journeys as well. Use real-host journeys for command
sequencing, public reveal, partial batch acceptance and complete matches; mocked DOM checks alone are insufficient.
Update TableFiles/packaging tests if adding served modules. Keep CI/Sonar gates and report any unavailable
local check honestly. No new tests or gameplay execution are claimed by this documentation-only commit.

| Scenario | Required evidence |
| --- | --- |
| Fresh 3v3 vs bot | Opening receipt, understood mutation, batch tempo, targeting, final result; normal-length game with later evolution |
| Two human seats | Different browser contexts; private selections do not change the other seat's board/options/feed/question until reveal |
| One/two/three eligible creatures | Correct draft count, budget, focus and empty states; not only the opening six-living-creature case |
| Both declaration modes | Distinct declaration/target stages preserved; guides never teach the wrong mode |
| Action + turnover in one response | Every cast retained; final cast before aftermath/upkeep; no future-round stats under an old action |
| Loss of connection | Delayed/stale read, duplicate SSE, uncertain write and partial tempo batch; no silent retries |
| History/replay | First load, reload, current/earlier rounds, missing frames, recorded session, final action and return to live |
| Overlay lifecycle | Open Help/History/inspect over draft, Escape/system Back, restored focus, resize and tab suspension |
| Accessibility | Full keyboard journey, non-colour states, meaningful labels/live announcements, reduced motion and 200% zoom |
| Layout | 1440×1000 desktop, 844×390 landscape, 390×844 and 320×568 portrait; no page overflow or clipped primary controls |
| Real devices | Android/iOS browser/PWA, browser chrome and touch targets; distinguish physical checks from emulation |
| Assets/performance | Readable fallback art, bounded journal DOM/cache, no repeated catalogue/list rebuild; retain A8 budgets |
| Service parity | Default silence, opt-in feedback, denied/unavailable push, both-seat suppression, recorded/unrecorded notes |

Capture opening upkeep, three opening mutation choices, completed editable tempo draft, expanded attack detail,
round transition and late-game spellbook on desktop and phone. Screenshots must come from the actual
implemented build and identify fixture versus real-host data.

**Completion tracking:** after each PR merges, add its GitHub link and verified result to this plan,
mark the corresponding gap rows complete/partial, and record remaining limitations. AU1–AU5 can be playtested
as the first milestone; “complete loop” alone is not the acceptance criterion for this workstream.
