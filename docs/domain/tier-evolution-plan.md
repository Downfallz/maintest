# Downfall Arena — Tier evolution migration plan

Status: planning only. Nothing here is implemented or deployed. The plan itself is committed so the work has
a fixed reference; that commit is not a decision to do any of it.
Date: 2026-09-21.
Repository: https://github.com/Downfallz/maintest

**Read with `tier-evolution-inventory.md`**, the stage 0 audit. It confirms this plan's arithmetic, its split
of every specialization, and its reading of `ActionEncoder` — and it corrects one thing and adds three. The
class names below (Brute, Marauder, Prowler, …) are a **rename**: the tree in the repository says `Brawler`,
`Mercenary`, `Scoundrel`. Where the two documents disagree, the inventory is the one that was measured.

## 1. Goal and agreed direction

Reduce repeated build decisions while preserving free multiclassing, progressive learning during a match, and the existing spell catalogue. Evolution purchases a named tier as one package. Initiative belongs to that package, not to individual spells.

Planning baseline:

- One evolution pick per player, shared across that player's team, at rounds 1, 3, 5, etc.
- A pick selects one living owned creature and one eligible, unowned tier.
- One tier purchase grants every spell in its package, atomically, and its explicit initiative bonus exactly once.
- Picks can be passed and do not accumulate. Even rounds skip player input for evolution automatically.
- Any creature can enter multiple families and multiple sibling specializations. A tier's prerequisite must be owned by that same creature.
- Base Creature's three starting spells remain the starting kit, outside the three purchased tiers. No free starting class is introduced by this plan.
- Tier 1 has two spells, tier 2 one, tier 3 two, using the current content. There is no tier 4 in this migration.
- Tier initiative is an independently authored number. Initializing it from the sum of former spell bonuses is a proposed migration baseline, not a permanent runtime formula or a balance guarantee.
- Creature base initiative and temporary initiative effects remain distinct and continue to work.

Cadence consequence: one creature can reach tier 3 at round 5; three creatures require nine purchases and cannot all reach tier 3 before round 17, without multiclassing. Document this deliberate pacing baseline and measure it in playtests before changing it.

Combat reveal/target/resolve changes, automatic build paths, automatic spell upgrades, and a separate casual mode remain separate design ideas. They are not bundled into this migration, so its effect on evolution pacing can be assessed.

## 2. Canonical content mapping

Keep stable spell IDs and spell mechanics. Rename class metadata and restructure talent nodes; do not create replacement spells simply to rename their class.

| Tier 1 package | Tier 1 spells | Tier 2 package | Tier 2 spell | Tier 3 package | Tier 3 spells |
| --- | --- | --- | --- | --- | --- |
| Brute | Pummel, Guard | Marauder | Protective Slam | Warmonger | Chain Slash, Thundering Seal |
| Brute | Pummel, Guard | Ironbound | Full Plate | Dreadnought | Restorative Gush, Crushing Stomp |
| Brute | Pummel, Guard | Berserker | Enraged Charge | Ravager | Tornado, Psycho Rush |
| Prowler | Poison Slash, Throwing Star | Parasite | Parasite Jab | Soulreaver | Hateful Sacrifice, Soul Devourer |
| Prowler | Poison Slash, Throwing Star | Assassin | Momentum | Deathstalker | Death Squad, Mortal Wound |
| Prowler | Poison Slash, Throwing Star | Plague Doctor | Noxious Cure | Blightweaver | Tranquilizer Dart, Infectious Blast |
| Occultist | Lightning Bolt, Rejuvenate | Elementalist | Meteor | Harbinger | Engulfing Flames, Ice Spear |
| Occultist | Lightning Bolt, Rejuvenate | Necromancer | Summon Minions | Lich | Revenant Guards, Crazed Specter |
| Occultist | Lightning Bolt, Rejuvenate | Shaman | Healing Screech | Spiritcaller | Toxic Waves, Restorative Burst |

Repeated tier-1 rows refer to the same package, not duplicate purchases. Total: 3 tier-1 packages + 9 tier-2 packages + 9 tier-3 packages = 21 packages teaching 33 spells, plus 3 starting spells.

## 3. Domain and SharedKernel

### Resource model

Introduce a stable, strongly typed tier identifier, named consistently with the glossary and existing identifier conventions. A tier resource carries ID, display name, level, prerequisite tier IDs, spell IDs, and InitiativeBonus. Its name and its identity are separate: renaming a display label must not rewrite saved references.

Make tier prerequisites authoritative. The tree layout may express the same relationships for rendering, but must not become a competing eligibility rule. Remove migrated per-spell acquisition gates; spell affordability and casting rules remain spell concerns. The root/start kit is not a purchasable tier.

Remove SpellInitiative from SpellStats. Do not remove InitiativeBuff or InitiativeDebuff spell effects: those are combat effects, not acquisition bonuses.

### Creature and match state

Store acquired tier IDs explicitly on Creature and CreatureSnapshot, alongside KnownSpells. Do not infer tier ownership from possessing its spells: starting kits, future shared spells, and historical partial unlocks make that ambiguous.

Validate the whole purchase before mutation. Then add the tier, union its spells into KnownSpells, raise BaseInitiative by its bonus, consume one pick, and emit a tier-level evolution event. An invalid purchase changes none of these. Replaying/restoring a snapshot must not apply the bonus again.

Already-known spells in a valid package are idempotent grants; owning the tier itself blocks buying it again. A stunned but living creature keeps the existing eligibility to evolve unless a separate rule changes that.

Update EvolutionChoice to address Creature + Tier, EvolutionRules, TalentUnlocks, Match, Round, resource lookups, snapshots, hypothetical state reconstruction, and events. Separate structural reachability through the tree from which purchases are available in a particular round.

### Cadence

Put the schedule in RuleSet and its serialized form, not in UI conditionals. Proposed parameters: first evolution round = 1, interval = 2, picks per evolution opportunity = 1. Use the same schedule function for validation, progression gates, projections, simulation, and next-opportunity labels.

An even round has zero effective picks. Both players passing, having no living creature, or having no eligible tier completes the phase without deadlock. Preserve upkeep ordering and the rest of the round flow.

## 4. Content, schema, Infrastructure, and DataBuilder

Migrate data/TalentTrees, data/Spells, class labels, aliases where relevant, fixtures, and alternative/disabled content configurations. Split existing specialization nodes into opener and advanced package. Ensure each of the 33 acquired spells is assigned to its intended package.

Update Infrastructure/Resources/Schema, GameSchemaBuilder, GameSchemaMapper, resources loading and serialization, and the DataBuilder entry point. Version the consolidated schema because this changes contract meaning, not just numeric content.

Builder validation must cover unique IDs, valid referenced tiers/spells, nonnegative initiative, nonempty purchasable packages, prerequisite cycles, valid level progression, correct starting-kit references, and deterministic ordering/hash generation. For a general model that allows a spell in multiple packages, define the rule explicitly rather than silently duplicating grants or rejecting it accidentally.

Preserve strict unknown-field validation: migrated spells carrying the obsolete acquisition initiative field must fail visibly. Disabled-content handling must not erase a prerequisite and accidentally expose a descendant. Disabling a tier with enabled dependents should require disabling or explicitly migrating those dependents. Do not silently shrink an authored package when one of its spells is disabled; surface a coherent-content error or an explicit authored replacement.

Create a one-time, reproducible migration that records old-to-new node mappings and proposed bonus values. All enabled and disabled authored variants must either migrate successfully or receive an explicit unsupported-version error. Rebuild generated data/dst artifacts; do not commit generated output contrary to repository policy.

Update content fingerprints, caches, and version references together. Store new rules in the run stamp so identical spell values cannot conceal a different progression cadence.

## 5. Application contracts and projections

Change SubmitEvolutionChoice and its handler, EvolutionDecision, EvolutionOption/Options, PlayerOptionsProjection, PlayerBoardState/Projection, catalogue DTOs, workflow dispatch, and serializers to use a tier ID for evolution. Casting still uses a spell ID.

Expose eligible tier IDs and catalogue details, acquired tiers, remaining picks, and the next evolution round. Derive legal choices from Domain rules; clients must not implement their own odd-round or prerequisite checks as authority.

Catalogue projections should provide tier names, prerequisites, package membership, and initiative. Spell projections lose their acquisition-initiative field. ContentAudit and SpellReach must move initiative analysis to tiers and follow the new reachability rules. Duplicate-spell signatures should describe the spell itself, with package accessibility analyzed separately.

Preserve hidden-information rules for combat. A contract change for evolution must not reveal private spell intentions or speed choices earlier.

## 6. CLI, HTTP hosts, and session orchestration

Update ConsoleAgent to show packages, their spells, and their initiative bonus. HumanSeat and playtest decision payloads accept a tier field for Evolution, not an overloaded spell field. Validate malformed/old requests clearly.

Exercise play, human, table, simulate, evaluate, benchmark, studio, studio --export, custom rules files, launch profiles, and bot-to-human handover. The round driver must skip even-round evolution for both human and bot seats. All hosts must resolve the same effective RuleSet.

Update JSON event registration, recorders, request/response schemas, session snapshots, logs, any restore/replay paths, and pilot status displays. Pilot remains an operator view with its existing information boundary.

## 7. Playtest UI and physical components

The evolution UI selects a creature, then a tier card containing name, tier number, prerequisites, granted spells, and +N initiative. One confirmation purchases the whole package. Show acquired packages across all branches without forcing a single exclusive class label on a multiclass creature.

Even rounds show normal combat flow, with the next evolution round available as context rather than a disabled selection screen. The event feed reports one named tier purchase and its gains. Refreshing or reconnecting must preserve the purchase and pick count.

Remove acquisition initiative from reusable spell cards, tooltips, catalogue cards, and print layouts. Keep initiative on creature stats, the timeline, tier cards, and actual initiative-effect descriptions. Recheck mobile selection, long names, tree navigation, and the existing paced resolution playback.

Update the tabletop rulebook, player aid, component manifest, talent mat, pick tokens/instructions, and any implemented print generator. Distinguish specified printshop work from code that actually exists; do not claim an unimplemented generator was migrated.

## 8. Studio authoring and export

Add tier editing for ID/name, tier level, prerequisite, spell package, and initiative bonus. Remove spell acquisition initiative controls, defaults, and summaries. Update overview tree rendering, Used by links, class filters, cloning/versioning, deletion/deactivation checks, and class labels.

Apply the same validation in local and hosted modes through their proper authorities. Browser checks improve feedback; DataBuilder remains authoritative. Hosted saves must commit coherent tree/content/balance metadata together. Update localBackend, hostedBackend, githubBackend contracts as needed, and studio --export catalogue/audit/weights outputs.

Rebuilding, reloading, and exporting must not reintroduce old spell initiative fields through templates or stale cached data. Test save/reload of an edited tier bonus and editing of all three tiers.

## 9. Agents and action scorer

Migrate RandomAgent, HeuristicAgent/Greedy, PolicyAgent, exploring wrappers, Lookahead/Minimax evolution delegation, and any hypothetical advance path to the new legal candidates. One candidate is one Creature + Tier purchase, not one candidate per granted spell.

Replace spell UnlockValue/UnlockTerms with tier-aware evaluation. Keep cast valuation separate from permanent acquisition initiative. A package does not grant multiple activations or charge the energy cost of all its spells immediately.

Proposed initial heuristic: evaluate the incremental improvement of the creature's available kit, then add the tier's initiative bonus once. Compare the best adjusted combat option before and after the hypothetical grant; apply affordability adjustments to candidate casts, not a sum of all package costs. Select a whole score-term vector using the active weights; never take independent per-term maxima from incompatible actions.

This current-board heuristic is only a baseline: it can miss healing useful next round, flexible attack/support kits, and a weak opener unlocking a strong descendant. Compare it against a bounded future-state or successor-access term before declaring the evolution AI satisfactory. Keep any such term explicit and measurable rather than inventing hidden hand-tuned class preferences.

CandidateTerms and the heuristic must share the same valuation implementation. Avoid summing former per-spell unlock scores: that double-counts acquisition initiative and treats alternative spells as simultaneous actions. Re-evaluate saved heuristic weight sets under the new semantics; file compatibility alone is not evidence of equivalent strength.

## 10. Learning, action encoding, and models

ActionEncoder currently encodes evolution using choice.Spell and schema.SpellIndex. Introduce a distinct tier index/ID for evolution; do not silently reinterpret a spellIndex as a tierIndex. Preserve spell indexing for intent and target decisions.

Update ActionCode, action keys/candidates, FeatureSchema, ObservationBuilder, owned-tier features, candidate term encoding, dataset manifests, episodes/steps serializers, model readers, policy feature construction, training, cloning, value fitting, evaluation, and report generation in both C# and Python.

Publish new schema versions/fingerprints for changed semantics. Include rule schedule and content identity in compatibility checks; candidate-term semantics also require an explicit version/signature, even if the vector length stays unchanged. Existing published feature layouts remain immutable.

Keep historical datasets readable for historical analysis. Refuse incompatible models for new live matches with a clear explanation. Re-record training data, retrain policies, and evaluate against compatible agents. Do not recycle old benchmark results or describe old policies as migrated merely because they deserialize.

## 11. Content scorer, tuning, and analytics

The Python content scorer is a separate workstream from ActionScorer. Update learning/src/downfall_learning/knobs.py and tune_content.py, check-knobs, score-content, tune-content, Studio Balance views, reports, and CI workflows that invoke them.

Move acquisition initiative tuning pointers from spells to explicitly declared tier targets with their own bounds, step, intent, and invariants. A tuner must not mutate undeclared rules, cadence, prerequisites, or membership. Keep cast damage/cost/effect knobs on spells.

Replace implicit tier-depth reconstruction based on per-spell prerequisites with the explicit acquisition-package mapping. Historical ADR 0034 describes the old method and should be superseded, not rewritten as though the new rule always existed.

Separate these readings:

- Acquisition: which packages were offered, chosen, owned, and reached at each round; specialization versus multiclass paths; concentration of picks on one creature.
- Combat: which learned spells were affordable/usable, cast, resolved, and contributed damage, healing, control or other effects.
- Outcome: match duration, round-cap rate, first tier-3 round, remaining health, fizzle/crit rates, and relative agent strength.

Raw spell ownership no longer measures a player's individual spell preference: two spells can be acquired together. Uncast spells are not automatically unreachable or bad; some were never acquired or had few opportunities under slower progression. Retain useful cast metrics with correct labels and opportunity denominators. Tier popularity and conditional win rate are descriptive, not causal evidence of balance.

Audit tierUsageShare, tierDamageSpread, tierWinSpread, unlock frequencies, entropy, dominance checks, and their objective bands. A one-spell tier-2 package must not become a meaningless 100% concentration penalty. Keep old metric definitions/version labels for history; version any changed objective and document which comparisons remain valid.

Run new mirror/variety/skill/exploit evaluations on fixed seed panels, plus held-out seeds for tuning proposals. Follow the repository's multi-dataset-seed policy for learning-loop claims. Bots can measure pacing and outcomes; human playtests must measure decision time, fatigue, and ease of learning.

## 12. Viewer and historical artifacts

Render evolution as one package event with recipient, granted spells, initiative change, and round. Add acquired-tier state to creature views and acquisition summaries while preserving per-spell combat statistics.

Update trace readers, run comparisons, embedded reports, Studio run pages, sample artifacts, and ViewerSamplesTests. Use stable IDs plus stamped catalogue labels for new comparisons, rather than relying exclusively on display names that are being renamed.

Version readers for old spell-evolution traces and new tier-evolution traces. Historical data must keep historical labels and meanings; never infer a full tier from one old learned spell. A viewer that displays recorded events is different from an engine that re-executes old decisions. Cross-ruleset comparisons may be displayed with explicit differences, but cannot be presented as a like-for-like balance delta.

Proposed compatibility boundary: historical artifacts remain inspectable; old in-progress matches and old models are not auto-converted into the new live rules. Deterministic old execution requires its original engine/content/rules. Preserve historical files instead of rewriting them.

## 13. Delivery sequence

Use one integration branch, with buildable dependency-aware commits and temporary explicit adapters only where necessary. Merge the default switch only when all consumers are migrated. Do not ship a new core behind stale studio or playtest clients.

| Stage | Deliverable | Exit criterion |
| --- | --- | --- |
| 0 | Full checkout inventory and decision record | Confirm current paths/consumers, document schedule/model/version contracts, map old nodes to 21 packages |
| 1 | Tier resources, schema, content, migration | Builder validates all content; old fields rejected; deterministic generated schema |
| 2 | Domain and application behavior | Atomic package unlock, cadence, snapshots/events/options and multiclass tests pass |
| 3 | CLI/hosts, agent candidates/scorer, encoding contracts | Human and bot full matches work; no even-round input stall; invalid old policies rejected |
| 4 | Playtest UI, Studio, Viewer, exports and history readers | Author, build, play, record and inspect the same tier model end to end |
| 5 | Python scoring/tuning/training and metrics | New contracts load; tier knobs work; fresh policies/evaluations use new schemas |
| 6 | Docs, fixtures, benchmark baseline and CI | Required gates pass; new deterministic baseline documented; first human pacing playtest recorded |

Tests and relevant documentation ship alongside each stage, not only at the end. A fresh model is required before claiming the PolicyAgent/learning workflow has been fully migrated; a clear rejection of old models is the interim safety boundary, not completion of that workstream.

## 14. Verification and definition of done

- Domain: odd/even round scheduling, round-1 opportunity, independent player budgets, pass/no eligible choice, living/dead/stunned cases, prerequisite ownership on the same creature, sibling and cross-family choices, tier-3 gating, atomic failure, no repeat purchase, no double initiative, snapshot reconstruction and initiative buffs/debuffs.
- Content: all 21 packages, the 33 acquired spells and 3 starting spells; aliases and alternate trees; invalid IDs/cycles/disabled dependents; coherent migration; deterministic hashing and version checks.
- Contracts: console and HTTP accept tier IDs for evolution and spell IDs for combat; catalogue, options and domain agree; no hidden combat information leak.
- Interfaces: create/edit/save/reload/export a tier; evolve at round 1, skip round 2, evolve at round 3; multiclass path; mobile view; inspect new and old recorded traces.
- Agents/learning: legal package candidates; initiative counted once; two granted alternatives not scored as two free casts; newly opened successor paths considered; stable action encoding; incompatible schemas rejected; new dataset-to-policy-to-evaluation cycle.
- Analytics: known package purchases and casts produce expected separate counts; low acquisition opportunity does not masquerade as poor spell use; old/new objective labels remain distinct.
- Repository gates: dotnet build, dotnet test, dotnet format --verify-no-changes; node --test studio/*.test.js table/*.test.js plus any actual viewer tests; Python ruff check, ruff format --check and pytest; DataBuilder, check-knobs, benchmark and the Sonar quality gate.
- Regenerate the benchmark digest only after behavior changes are understood; retain fixed seeds. Update launch profiles, sample content, viewer samples, export/deployment workflows, documentation and fixtures.
- Final code audit outside legacy/: no active per-spell acquisition initiative, no spell-addressed evolution command, no duplicate client-side progression authority. Preserve legacy/ and historical ADRs/artifacts as historical references.

Completion means the same change works through authoring → build → load → choose tier → resolve match → record → view → score → train/evaluate. Compilation or a working playtest page alone is insufficient.

## 15. Grounding and remaining implementation inventory

This plan is grounded in targeted GitHub reads and searches, not a full local checkout audit. Before coding, enumerate all callers, serializers, fixtures and workflows with rg; re-read current AGENTS.md and relevant ADRs. A path not listed here is not automatically out of scope.

Verified anchors include:

- data/TalentTrees/talent_tree.v1.json; data/README.md; data/balance/README.md.
- src/DownfallArena.Domain/Resources/SpellStats.cs; Matches/Creatures/Creature.cs; Matches/Rounds/EvolutionChoice.cs; Matches/RuleSet.cs; Matches/Rules/Planning/TalentUnlocks.cs and EvolutionRules.cs.
- src/DownfallArena.Application/Matches/Commands/SubmitEvolutionChoiceHandler.cs; Matches/Projections/EvolutionOption.cs and PlayerBoardState.cs; Catalogue/CatalogueProjection.cs; Content/ContentAudit.cs and SpellReach.cs.
- src/DownfallArena.Application/Agents/ActionScorer.cs, HeuristicAgent.cs, RandomAgent.cs, PolicyAgent.cs; Learning/ActionEncoder.cs and CandidateTerms.cs.
- src/DownfallArena.Infrastructure/Resources/Schema/GameSchema.cs; Resources/GameSchemaBuilder.cs and GameSchemaMapper.cs.
- src/DownfallArena.Cli/ConsoleAgent.cs; Table/HumanSeat.cs; table/table.js; studio/README.md; viewer/README.md.
- learning/src/downfall_learning/knobs.py and tune_content.py; docs/learning/features.md, artifacts.md, training.md and agents.md.
- docs/adr/0017-spell-initiative-on-unlock.md and 0034-a-tier-is-a-depth-a-player-climbs.md; docs/tabletop/playtest-app.md, translation.md, components.md and rulebook.md.

Create a new ADR for the tier purchase model, cadence and acquisition initiative. Add explicit schema/scoring decisions where their scope warrants separate ADRs; use the next available numbers at implementation time. Update the glossary, game rules, authoring docs, studio/viewer docs, learning schemas/objective docs and journal. Historical ADRs receive supersession links; frozen legacy code is not edited.

Final tuning still requires measurements: initiative values per package, evolution heuristic horizon/weights, balance objective bands and match-length fit. These are identified tuning tasks, not reasons to omit any migration surface above.
