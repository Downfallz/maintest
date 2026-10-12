# Free activation: review and plan

Status: planning only. Nothing here is implemented. The plan is committed so the work of
[ADR 0110](../adr/0110-a-rule-set-may-declare-spells-at-activation.md) has a fixed reference; that commit is a
decision to build the switch and measure it, not to adopt the rule.
Date: 2026-10-11.

It reviews a design handoff brought to the owner on 2026-10-11 ("Suppression de l'Intent caché : choix du
sort et de la cible à l'activation"), which proposed choosing the spell and its targets together at the slot.

Vocabulary used here: **mode A** is today's rule (`HiddenIntent`: a hidden Intent per creature, then targets
at the slot, ADR 0083 and 0107). **Mode B** is the proposal (`AtActivation`: spell and targets chosen
together at the slot).

## 1. Verdict

Worth building, as an experiment behind a rule-set switch, with mode A staying the default. Not worth
adopting on the strength of the analysis alone, because the analysis underweights three things the
repository already knows, and the question it cannot answer (is the hidden Intent fun) is one only the
table can answer.

What the handoff gets right:

- The mechanics map is exact: targets are already chosen on the real board (ADR 0083), so the only thing
  mode B moves is *when the spell is chosen*. The resolution path (`ResolutionRules`, `CombatExecution`,
  `Advance`, `TargetingRules`) never reads an Intent, so the engine cost is small.
- The component argument is the strongest one. Mode B deletes the dial-or-deck fork of
  `component-options.md` §4, the Intent slot of `components.md` §3.7, and the reason a spell card exists in
  six copies (§5). That is a large part of the box.
- ADR 0107 becomes moot in mode B: the Overdrive-then-costly-spell plan works without a speculative
  declaration.
- The fizzle semantics simplify. Every creature starts with Focus (`wait`, free, on itself), so with today's
  content a living, unstunned creature always has a legal cast. In mode B a skipped slot can only be "dead
  or stunned since planning". The `NotEnoughEnergy` and `NoLegalTarget` fizzles disappear.

What it underweights:

1. **Combat becomes a perfect-information game.** The journal (2026-09-16, ablations) measured that the
   target decision "is the one with no hidden information" and the intent decision "is the one that
   guesses". Remove the Intent and the only guess left in combat is the d20. The minimax agent's own doc
   says it models "an opponent who sees the actor's move, which no opponent in this game does": in mode B
   every later creature does. That is not a tweak; it changes what kind of game the combat phase is.
   Evolution and Speed stay hidden, but Evolution is a build decision and Speed is a weak probabilistic
   signal (content review F2). "You commit before you see", which ADR 0089 made hold for every planning
   decision, would hold for none of the combat decisions.
2. **Standard gets two things for one.** Today Quick is a trade: act first, lose the critical. In mode B a
   Standard creature keeps its critical *and* chooses its spell with more of the round resolved. Quick buys
   tempo only. Expect the Quick share to fall and the last slot of the timeline to be the strongest. This
   is measurable with the bots before anyone sits at a table.
3. **Time at the table moves from parallel to serial.** In mode A at a cardboard table, both players think
   at once during Intent selection, six decisions each, and then each slot is a target choice (the app
   does not ask them at once yet, §5). In mode B one player
   thinks while the other waits, at every slot: six to twelve serial decisions a round over eight to
   fourteen rounds. The handoff says this is the lesser problem. The content review's priority 1 is
   spellbook decision load, so it is not the lesser problem for this project; it is the one being tested.
   Fewer manipulations may still be more minutes.

And one cost the handoff does not mention at all: **the balance history is mode A's.** Twenty-seven tuning
passes, the benchmark digests per content hash, the searched weights (search-40, lookahead-45), the policies
and the seatable list were all measured with a Greedy that declares before combat. In mode B every bot is
better informed, fizzles fall, matches get shorter and more efficient, and ADR 0039 showed what that does
to `player1WinShare`. Adopting mode B means re-tuning the catalogue, not only re-writing the rulebook.

So the honest prior is about even: mode B is a better box and possibly a faster table, mode A is the only
place the combat phase has a mind game. The flag is cheap enough to let the table decide.

## 2. Design decisions to settle in the ADR before coding

| Question | Recommendation |
| --- | --- |
| Where the mode lives | `RuleSet.CombatDeclaration` enum: `HiddenIntent` (default) and `AtActivation`. JSON key `combatDeclaration` in `RuleSetFile`, defaulting to `hiddenIntent` so every existing file and caller is unchanged. Mirrored in `RuleSetStamp` so runs and policies of the two modes never compare as the same rules. |
| The sub-phase | Keep `IntentSelection` in the enum and the flow table; in mode B the gate passes through it without asking anyone, the way `TieOrder` already passes through a round with no ties. Removing it breaks `RoundFlow`, `RoundFlowTests` and the observation encoder (sub-phase 5/9, 6/9) for nothing. |
| Declaring a spell one cannot pay | No, in mode B. The slot check is the only check; `ActionRules.ValidateAction` already does it. ADR 0107 stays valid for mode A. |
| No legal cast | Skipped, owner not asked, as ADR 0083 rules. With today's content only death and stun get there, since every creature starts with Focus; the engine does not rely on that. |
| What the skipped slot raises | A `CombatAction` needs a spell, and so do `ActionRevealed` and `CombatResolution.Fizzle`. Two options: a nullable spell on the action (ripples through every reader: viewer, traces, frames, invariants), or a new event `SlotSkipped(actor, reason)` plus `MarkSlotActivated`. Recommend `SlotSkipped`; `SeatVisibility.Classified` must list it (its test enforces that). |
| Voluntary pass | None. Focus is the pass and it gathers energy, as today. |
| When the choice becomes public | Spell and targets together on confirmation, as ADR 0070 already rules. The decision guide (ADR 0079) reads only the visible board, so previews work unchanged. |
| Changing the spell during target selection | Yes, until confirmation. A table concern, not an engine one. |
| Benchmark | `benchmark` refuses a rule set in mode B, the way it refuses `--format`: the digest is per content hash and would be compared against mode A's. A mode B digest can come later, keyed by mode, if the mode is adopted. |
| Hybrid (mode C) | Not built. The enum leaves room for it; the handoff is right that it would blur the experiment. |

## 3. What moves and what does not

Stays untouched: `ResolutionRules`, `CombatExecution`, `TargetingRules`, `Advance`, `Match.Activate`,
`CombatActionResolved` and its frame, the whole planning side (Evolution, Speed, timeline, tie order),
`PlayerBoardState` (its `Intents` is simply empty), `SeatVisibility` (plus one event), the trace recorder,
`DecisionGuideProjection`, all of Infrastructure, the Python readers (kinds are opaque strings to them),
the mode A benchmark digests, every recorded session, every policy and weights file.

Moves, by layer:

- **Domain**: `RuleSet` (+ enum, `Create` optional parameter, `Default`, `InFormat`), `Match.Step` (pass
  through `IntentSelection`), `Match.SubmitIntent` (refused in mode B), `Round.SubmitAction` (the
  `ActionDoesNotMatchIntent` check and the missing-intent throw become conditional; `Round` knows no rule, so
  `Match` passes what it needs), `Match.ActivateUnavailableSlot` and `ActionRules.CanTakeItsSlot` (an
  actor-level predicate beside the intent-level one), `CombatAction` (a factory that needs no intent), the
  `SlotSkipped` event. Domain tests: `Table.cs` helpers get a direct variant; the 15 to 20 intent tests get
  mode B mirrors.
- **Application**: `PlayerOptionsKind.Action`, `ActionOptions(Actor, spells with their legal targets)`,
  `PlayerDecision.Act`, `PlayerDecisionCheck`, `PlayerOptionsProjection` (branch on the rule set),
  `MatchDriver` (one new case; the `SubmitAction` command and handler already carry the spell),
  `IPlayerAgent.DecideAction` with a default that calls `DecideIntent` then `DecideTargets` on the current
  board. Every wrapper must forward it explicitly or the default runs on the wrapper and bypasses the inner
  agent: Greedy, Opening, Exploring, Recording, `IntentCounter.CountingAgent`, `SeatAgent` (PR 2), and the
  table's `HumanSeat` and `Journaled` (PR 5). `PracticeAgent` stays on the default: practice plays its own
  hidden-intent rules (`PracticeScenario.Rules`) and refuses `--declaration`.
  `PlayerAgentContractTests` names every agent left on the default and why. `ExploringAgent` draws uniformly
  over (spell, target set) pairs, not two stages. `IntentCounter` counts in the new method or the spells
  report goes blank. `RoundRollout.Combat` gets a no-intent branch with the lookahead (PR 3).
- **Lookahead and minimax**: the one real piece of new logic. `DecideIntent` plays out from slot 0 on a
  board that, at Activation, already contains the earlier slots, so the default cannot be used. A combined
  search from `ActivationCursor` over castable spells times target sets, reusing `PlayOut`, `First`,
  `Worst`, `Candidate`. `Guess` and `DeclaredSpells` become mode-aware: in mode B a non-actor picks its
  spell at its slot, which is what the journal called clairvoyant and refused for mode A, and is exact in
  mode B. The minimax floor becomes the right reading for enemies still ahead.
- **Learning**: `ActionKind.Act`, `ActionEncoder.Candidates` (union of the target candidates over castable
  spells, under an `act:{slot}:{spell}:{targets}` key of its own: a `targets:` key already carries the spell,
  but a spell chosen with its target is another decision than one declared before combat, and the two must
  not share a policy row), `CandidateTerms`,
  `RecordingAgent`, `RuleSetStamp`. No feature schema bump: the observation does not encode intents and
  the mode sits in the stamp. Python: nothing required; a note in `train_value`'s kind list.
- **Cli**: `RuleSetFile` (+ field, `Describe`, the not-found message), `TableDecisionBody` (kind `Action`
  with creature, spell, targets), `HumanSeat`, `DecisionJournal.Journaled` and `JournalEntry` (the schema
  already has `Spell` and `Targets`), `TableGate`, `SeatAgent`, `ConsoleAgent` (spell menu then target
  loop at the slot), `TableApi.StillFaceDown` (zero in mode B; `opponentIntents` is dead on the page
  today), `play`/`simulate`/`evaluate` need a way to name the mode (`--rules` is table-only today; a
  `--declare at-activation` flag or `--rules` on every command). `PracticeScenario` keeps its own mode A
  rules; the three Target recipes pre-declare the person's spell and would need mode B versions later.
- **Table page**: about 330 lines of intent-specific code in `table.js` and all of `hand.js`. Mode B adds
  one screen, "your slot: pick a card, then targets", and changes the phase guide, `titleOf`,
  `decisionPhase`, `plannedTurn`, `ownChoice`, the keyboard and focus maps, and the `guidance.js` caveat.
  Mode A's screens stay. About 20 of 129 `table.test.js` tests and the 12 of `hand.test.js` are mode A
  fixtures and stay; mode B gets its own.
- **Viewer**: `IntentSubmitted` narration, "Intents per spell", the "declared most" column and the intent
  chips fall back to `ActionRevealed` when a trace holds no intents.
- **Docs**: the ADR; `game-rules.md` (the mode under Implemented, since the engine plays it); the glossary
  (`Combat declaration`, `Free activation`); `rulebook.md` and `player-aid.md` get a boxed variant, not a
  rewrite, until the mode is adopted; `automa.md` (the Automa draws its card at the slot); the components
  notes get one paragraph on what mode B would delete; `content-review.md` records that the owner lifted
  the freeze for this one experiment.

## 4. The plan, one pull request a line

Mode A is the default at every step, CI stays green, the digest never moves until phase 8.

| # | PR | Layer | Done when |
| --- | --- | --- | --- |
| 0 | ADR 0110, Proposed, and this plan | docs | The ADR and this file are committed; `game-rules.md` lists the mode under its open questions; the content review records that the freeze is lifted for this experiment. |
| 1 | The mode in the engine | Domain | `RuleSet.CombatDeclaration`; `IntentSelection` passes through; `SubmitAction` takes any castable spell; `SlotSkipped`; domain tests mirrored. `benchmark` passes unchanged. |
| 2 | The decision model and the one-step agents | Application, Cli (console only) | `Action` kind, options, decision, check, projection, driver; `DecideAction` on every agent and wrapper; `IntentCounter`; `play`/`human`/`simulate`/`evaluate` name the mode with `--declaration`, and `benchmark`, `studio` and `table` refuse it; the rule-set stamp carries the mode, written only when it is not the default; a mode B fixture in `Invariants/PlayedMatches` runs every invariant, since it plays through the driver. Random, Greedy, Heuristic, Opening, Explore, Policy play mode B; the lookahead and minimax are refused when built, until PR 3. A recording wrote a chosen action as an Intent step and a Targets step until PR 4. |
| 3 | The searching agents in mode B | Application | The combined search from the cursor; mode-aware guesses; `RoundRollout` without an intent pass, its only reader being the lookahead; `RoundRolloutTests` in both modes; a measurement of lookahead and minimax against Greedy in mode B in the journal. |
| 4 | Recording and encoding | Application (Learning), learning/ | `ActionKind.Act`, encoder, terms, recorder; a mode B dataset recorded and a clone trained as a smoke test; `compare-stamps` tells the modes apart (the stamp carries the mode since PR 2). A chosen action is one `Act` step among every (spell, target set) pair, which is what the exploring agent's pair draw is uniform over. A policy that learned `act:` keys scores them; one fitted on hidden intents declares then targets on the keys it knows. |
| 5 | The table host | Cli | `RuleSetFile` (`combatDeclaration`), `--declaration` on `table`, a table request's and a table record's `declaration` (a rebuilt table comes back in it), the admin listing; `TableDecisionBody` and the journal carry an `Action`; `HumanSeat` asks one question at the slot; `TableRestorer` rebuilds a mode B table; tests mirrored. The gate, the seat agent and the API payload needed nothing: they already carry what PR 2 added. Until PR 6 a mode B table seats only bots, since the page cannot ask a person for an action. |
| 5b | Both seats at once | Application (driver), table/ | An ADR amending 0092: `MatchDriver` asks both seats' Speed and Intent at once, as it asks their picks, and submits the answers in seat order, so mode A keeps its parallel phase in the app (§5) and the engine sees the same commands. The hotseat page keeps the device with the seat that is still answering instead of handing it over between two of its questions. Split from PR 5 because it changes the default mode and the hotseat screen, which the table host for mode B does not. |
| 6 | The table page and the viewer | table/, viewer/ | The slot screen; the admin panel's declaration chooser, beside its format chooser; phase guide and texts per mode; viewer fallbacks; the viewer's rules row names the declaration, since it already flags two runs of different modes as different rules; `node --test` green in both modes. Practice stays mode A. |
| 7 | The bot reading | docs (journal), scripts | The A/B table of §5 on the same seeds and content, with the caveat that strength does not compare across modes. No tuning pass. |
| 8 | The table playtest | none | The seven scenarios of the handoff played in both modes on the app, no cardboard changes. Rules and feel can be tried in hotseat; the time readings of §5 come from two people on two devices. |
| 9a | Drop | all | One PR removes the mode: the flag makes it a clean removal. ADR 0110 is marked Deprecated, with the numbers. |
| 9b | Adopt | all | A second ADR supersedes parts of 0010, 0070, 0083 and 0107; mode B becomes the default; a mode B benchmark digest; a tuning pass under mode B; rulebook, player aid, components, Automa rewritten; mode A removed one or two releases later. |

PRs 1 to 4 are sequential. PR 5 depends on 2. PR 6 depends on 5. PR 7 needs 3. PR 8 needs 6, and its time
readings need 5b. PR 3 is the
one with design risk; 4 is small.

## 5. What to measure

With the bots, same seeds, same content, both modes, in the journal:

- Rounds per match and share at the round cap.
- `player1WinShare` (the seat), on the exploring self-play as ADR 0089 read it.
- Quick share, overall and per spell family; who acts last and how often that side wins the round's
  exchange.
- Fizzle count and reasons (mode B should show only dead and stunned).
- Spell usage: the shares of Energy Drain, Stun and Focus, and the distinct spells cast per creature
  per match (the handoff's redistribution hypotheses, §4.4).
- `score-content` in mode B against the balance bands, to size the re-tuning mode B would need.
- Lookahead and minimax against Greedy in mode B, since the reading that lost in mode A is the right one
  in mode B.

Caveat to write at the top of the entry: every bot is better informed in mode B, so a win rate across modes
measures nothing; only the shape of the system does.

At the table, from `notes.jsonl` and not from `decisions.jsonl`: a journal entry is stamped only when an
answer is accepted, so the gap between two of them mixes resolution, handover and waiting. A `Decision` note
carries `elapsedMs`, measured from the moment the question was served to a person looking at it
(`DecisionClock`), with its round and sub-phase. From it:

- thinking time per decision, per activation in mode B and per Intent in mode A, summed per seat and round;
- time per round, from the earliest served moment (`At` less `elapsedMs`) to the last accepted one, both
  seats together; that sum against the per-seat sums is the parallel-versus-serial question of §1.

Only seats a person held have notes, so the time readings need two people. They also need mode A's
Intent selection to be parallel in the app, as it is in cardboard, and today it is not, for two reasons:

- in hotseat, the pass-the-device screen serializes it;
- on two devices too, because `MatchDriver` asks both seats at once only for Evolution (ADR 0092), and
  asks Speed and Intent seat by seat, so Player 2 waits for Player 1's last Intent.

Measured as it stands, mode A would lose a parallel phase it has at the table, and mode B would be credited
with the saving. PR 5b therefore asks both seats' Speed and Intent at once, the way ADR 0092 asks their
picks: the decisions are hidden from each other and independent, and the answers are still submitted in
seat order, so the engine sees the same commands. The time readings come from two people on two devices
after that. If they turn out too coarse, round start and end stamps in the notes are a small addition to
the same PR.

And the handoff's human questions: did the late actor feel like a counter, did Overdrive combos feel earned
or free, did anyone miss the bluff.

## 6. Stop rule

Decide after the playtest, not after the bots. Drop mode B if the table is not faster per round *and*
players miss the hidden declaration. Adopt it if the table is faster and nobody misses it. Anything in
between: a second playtest with the roles swapped, and still no mode C.
