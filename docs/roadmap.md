# Roadmap: rebuild the engine from the Domain2 prototype

Status: Accepted (2026-09-08, decisions A to F settled in ADRs 0007 to 0011). This is the plan for carrying the best of `legacy/DownfallArena/DA.Game.Domain2`
(and its Application, Infrastructure, Shared and test projects) into the clean solution, refining as we go.
Each phase is one or two pull requests, ships with tests, and leaves `main` green. Phases are ordered by
dependency: nothing in phase N needs anything from phase N+1.

## Ground rules

- **Port behaviour, not files.** For each concept: read the legacy code and its tests, write the glossary entry,
  write the tests in the new solution, then the code. Legacy tests are a spec to mine (191 domain and shared
  test cases, plus ~1,450 lines of commented-out tests for `Round`, targeting and policies that document intent).
- **Fix while porting.** Known defects are listed per phase. A ported bug is a regression, not a port.
- **One catalogue of errors.** Every rule violation returns `DomainError` with a globally unique code
  (`Match.AlreadyStarted`, `Round.IntentAlreadySubmitted`, `Targeting.TargetIsDead`). Impossible states throw.
  The legacy `I`/`D` prefix idea survives as "throw vs return", not as string codes.
- **Namespaces mirror folders. English everywhere.** No `V1` suffix; versioning is a git concern until a
  second implementation actually exists.
- **Determinism is a feature.** Time and randomness are ports, injected, seedable. Simulations replay.
- **Every hard-to-reverse choice gets an ADR** before the code lands (candidates listed below).

## Target shape

```
src/DownfallArena.SharedKernel   no dependencies; shared by every layer
  Primitives/        Entity, AggregateRoot, DomainError, Result, IDomainEvent
  Identifiers/       MatchId, PlayerId, RoundId, CreatureId, SpellId, CreatureDefinitionId, TalentTreeId
  Stats/             Health, Energy, Defense, Initiative, CriticalChance
  Randomness/        IRandomSource (port)
src/DownfallArena.Domain         depends on SharedKernel only
  Resources/         Spell, TargetingSpec, Effect taxonomy, TalentTree, CreatureDefinition, IGameResources
  Matches/           Match aggregate, Round, Team, CombatCreature, conditions, phases, choices, events, errors
    Rules/           planning, combat, conditions, progression gates (pure services, no aggregate parameter)
    Views/           CreaturePerspective, board state, player options (read-only projections)
src/DownfallArena.Application
  Matches/           commands, queries, handlers, ports (IMatchRepository), agents (IPlayerAgent), match driver
src/DownfallArena.Infrastructure
  Resources/         consolidated game schema loading, alias resolution, content hash check
  Persistence/       in-memory repositories
  Randomness/        seeded random source
src/DownfallArena.Cli            plays a match (human vs bot, bot vs bot)
src/DownfallArena.Simulation     batch runner, metrics (phase 9)
tools/DownfallArena.DataBuilder  consolidates Data/** JSON into game.schema.json with a content hash
```

Decisions this shape rests on (settled in phase 0):

| # | Decision | Outcome | ADR |
| --- | --- | --- | --- |
| A | Where do shared ids, stats, and primitives live? | A dependency-free `SharedKernel` project below Domain. | 0007 |
| B | Mediator library? | No. Minimal `ICommandHandler` / `IQueryHandler` interfaces registered in DI. | 0008 |
| C | Game data pipeline | Keep the DataBuilder: it consolidates `Data/**` into one validated `game.schema.json` with a content hash, which Infrastructure loads. Spell tuning stays a JSON-editing workflow. | 0009 |
| D | Domain events | Framework-free records, dispatched by the Application after a save. | 0008 |
| E | Sub-phases | Keep the phase and sub-phase machines; `Planning_EvolutionResolution` is dropped. | 0010 |
| F | Win condition | Last team standing, plus a round cap from the rule set. | 0011 |

## Phases

### Phase 0. Decisions and vocabulary (docs only)

- ADRs for decisions A to F (0007 to 0011).
- Glossary: promote the `inherited` terms that phases 1 to 7 will implement to `decided`, with the exact
  names the code will use. Add: Activation slot, Reveal, Fizzle, Progression gate, Perspective, Content hash.
- `docs/domain/game-rules.md`: write the round sequence from the legacy `Match` (it is precise: energy gain,
  ongoing effects, evolution with 2 picks, speed, timeline Quick-then-Standard by initiative, intent, reveal
  and target, resolution, end of round, next round).

Done when the docs describe the engine we are about to build and nothing else.

### Phase 1. Shared kernel: primitives, ids, stats, ports (done, PR #5)

Create `src/DownfallArena.SharedKernel` (ADR 0007), move the primitives there, and carry over, close to
verbatim, with their tests (37 stat cases, id format tests):

- Ids: `MatchId`, `PlayerId`, `RoundId`, `CreatureId`, and the versioned content ids `SpellId`,
  `CreatureDefinitionId`, `TalentTreeId` (with the validation it never had) in the `kind:name:vN` format.
- Stats: `Health`, `Energy`, `Defense`, `Initiative` on a shared non-negative base; `CriticalChance` in [0, 1].
- Port: `IRandomSource` (`NextDouble`, `NextInt32(min, max)`); time stays `TimeProvider`.
- `TurnCursor` moves to phase 4 with `Round`: it is a match concept, not a shared one.

Fix: `SystemRandom.NextDouble` only produced 100 distinct values; the new implementation must be continuous.

### Phase 2. Game resources: spells, effects, talents, creature definitions (done, PR #5)

- `Spell` (id, name, type, class, initiative, energy cost, crit chance, targeting, effects), `TargetingSpec`,
  `TalentTree` with `allOf`/`anyOf` prerequisites, `CreatureDefinition`, `IGameResources` with symmetric
  `Get`/`TryGet` for all three catalogues.
- **Redesign the effect taxonomy** (ADR): a closed set of effect records (`Damage`, `Heal`, `EnergyGain`,
  `DefenseBuff`, `Bleed`, `Stun`, ...) with duration and stacking policy where relevant. The legacy `Effect.Kind`
  was never assigned and half the kinds were silently ignored; this is the root of most combat bugs.
- `tools/DownfallArena.DataBuilder` (ADR 0009): consolidates `Data/Creatures`, `Data/Spells`,
  `Data/TalentTrees`, `Data/aliases.json` into `game.schema.json` with a content hash, validating the schema
  with precise errors (the legacy TODO). Infrastructure loads the consolidated file; the hash becomes
  `IGameResources.Version`.
- Content: keep the 38 spell files as scaffolding but fix the schema drift (`characterClass`, `level`) and give
  `main.v1.json` defense, initiative, crit and class. Content design itself is out of scope here.

### Phase 3. Creatures, teams, conditions (done)

- `CombatCreature`: closed API. No public setters. Mutations through `TakeDamage`, `Heal`, `GainEnergy`,
  `SpendEnergy`, `UnlockSpell`, `Apply(Condition)`, `Stun`, `ClearRoundModifiers`. Legacy exposed 13 setters and
  a service bypassed the mutators.
- `ConditionInstance` and `ConditionCollection` with stacking (`Stack`, `RefreshDuration`, `NoStack`) and ticks.
- `Team`: fixed size from the rule set, `IsDefeated`. Creature ids assigned by the match, not hard-coded 1-6.
- `CreatureSnapshot` for read-only projections.

Legacy tests to mine: `CombatCreatureTests` (14), stats, `ConditionCollection`.

### Phase 4. Round: phases, sub-phases, choices, timeline (done)

- `RoundLifecycle`, `RoundSubPhaseLifecycle` (table-driven, forward-only, idempotent same-phase moves).
- `Round` entity as pure state: evolution choices, speed choices, intents, targeted actions, `CombatTimeline`,
  `RevealCursor`, `ResolveCursor`. It knows nothing about rules or resources; that property is the reason
  it is worth keeping.
- Choice value objects: `SpellUnlockChoice`, `SpeedChoice`, `CombatIntent`, `CombatAction`, `ActivationSlot`.

Legacy tests to mine: lifecycle tests (20), `TurnCursorTests` (11), the commented-out `RoundTests` (267 lines,
the best spec of the round contract).

### Phase 5. Planning rules

- `TalentUnlockService` (unlockable spells from the tree and known spells; validation with error catalogue).
- `SpeedChoicePolicy`, `CombatTimelineBuilder` (Quick before Standard, initiative descending, deterministic
  tie-break by slot then creature id: legacy had arbitrary order because all initiatives were 0).
- Progression gates: `EvolutionGate` (picks per round from the rule set, not a constant), `SpeedGate`.
  Gates return what is missing (creature ids, remaining picks) so the UI and bots share one source of truth.

Fix: legacy unlocked the spell on the creature before the round accepted the choice; a rejected duplicate left
the spell unlocked. Validate fully, then mutate.

### Phase 6. Combat rules

- Intent validation (phase, alive, not stunned, knows spell, can afford), targeting policy producing a full
  `TargetingReport` (all failures, per target), legal targets resolver, `CombatActionGate`.
- Resolution pipeline: global targeting failure fizzles the action, per-target failure filters targets,
  effects computed per target, **crit multiplier applied**, **energy spent**, result applied to creatures,
  conditions attached. Condition ticks at start of round and end of round (legacy only did start).
- Conditions and effects follow the phase 2 taxonomy; unknown kinds are a compile error, not a `NotSupported`.

Legacy tests to mine: resolution, crit, effect, execution, damage tests, plus the commented-out targeting
(469 lines), resolution policy and cost policy tests.

### Phase 7. Match aggregate and phase driver

- `Match`: join, start, one public method per player action, `ResolveNextCombatStep`, `EndTurn` removed.
- A `PhaseDriver` (or equivalent) owns "gate says advance, raise event, initialize next sub-phase" so the
  four copies of that logic in the legacy `Match` become one.
- Complete event set: `MatchEnded` (missing), `CombatActionResolved` (never raised), consistent event payloads.
- `MatchLifecycle` failures become exceptions (invariant) instead of silently swallowed results.
- Win condition from decision F. Round cap from the rule set.

Fix: `ResolveNextCombatStep` reported the next round's id after finalizing; report the completed round.

Done when a scripted match (two players, fixed choices, seeded random) plays to the end in a domain test.

### Phase 8. Application: use cases, projections, agents

- Commands: `CreateMatch`, `JoinMatch`, `SubmitEvolutionChoice`, `SubmitSpeedChoice`, `SubmitCombatIntent`,
  `RevealNextActionAndBindTargets`, `ResolveNextCombatStep`. Queries: `GetBoardStateForPlayer`,
  `GetPlayerOptions`. One handler each, no mediator (decision B).
- Projections: `PlayerBoardState`, `PlayerOptions` (exactly one options set per sub-phase).
- Ports: `IMatchRepository`, `IPlayerAgent` (options-driven: evolution, speed, intent, targets).
- `RandomAgent` that only ever picks from `GetPlayerOptions`, with the injected random source.
- `MatchDriver` that plays a match to completion through the public commands.
- Infrastructure: in-memory repository, seeded random source.

Dropped from legacy: `ITurnDecider`/`PlayerAction` string bag, `PlayTurn`, the second simulation stack,
AutoMapper, FluentValidation pipeline, MediatR behaviours, ML folder (kept in `legacy/` for later).

### Phase 9. Hosts: CLI and simulation

- CLI: play bot vs bot with a readable log; then human vs bot with prompts (Spectre.Console is fine here,
  it is a host).
- `DownfallArena.Simulation`: batch runner (N matches, seed per match), metrics (win rate by slot, rounds,
  damage), CSV output. This is the balance tool and the future ML dataset source.

### Backlog (after phase 9)

- Content authoring: real spells, classes, talent prerequisites; schema documentation.
- Persistence beyond memory; API host; UI (Blazor or web front) per ADR when needed.
- Learning: feature extraction and policies from simulation output (legacy `Learning/` as reference).
- Property-based tests for resolution ordering and timeline determinism.

## Working agreement per phase

1. Open the phase with the glossary and game-rules updates and the ADRs it needs.
2. Write the tests from the legacy spec, then the code. Run `/verify`.
3. Ask `domain-reviewer` for a pass on Domain changes and `code-reviewer` before the PR.
4. One PR per phase (two for phases 6 and 8). The PR body lists what was carried, fixed, and dropped.
5. Delete the corresponding legacy folder once the phase is merged, so `legacy/` shrinks to zero by phase 9.
