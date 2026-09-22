# Architecture Decision Records

An ADR captures one decision that is hard to reverse: an architecture choice, a dependency, a convention,
or a settled game-design question. Copy `0000-template.md`, take the next number, keep it under a page.
Accepted ADRs are immutable. To change a decision, write a new ADR that supersedes the old one.

| # | Title | Status |
| --- | --- | --- |
| [0001](0001-record-architecture-decisions.md) | Record architecture decisions | Accepted |
| [0002](0002-clean-slate-restart.md) | Restart from a clean slate, freeze the prototypes | Accepted |
| [0003](0003-dotnet-10-layered-solution.md) | .NET 10 solution with enforced clean-architecture layering | Accepted |
| [0004](0004-testing-stack.md) | Testing stack: xUnit v3, Microsoft.Testing.Platform, Shouldly, NSubstitute, NetArchTest | Accepted |
| [0005](0005-ai-assisted-development-kit.md) | AGENTS.md and Claude Code kit as first-class project assets | Accepted |
| [0006](0006-sonarcloud-analysis-from-ci.md) | Run SonarCloud analysis from CI with the scanner for .NET | Accepted |
| [0007](0007-shared-kernel-project.md) | A dependency-free SharedKernel project below Domain | Accepted |
| [0008](0008-messaging-without-a-mediator.md) | Use cases and domain events without a mediator library | Accepted |
| [0009](0009-game-data-pipeline.md) | Keep the data builder: one consolidated, hashed game schema | Accepted |
| [0010](0010-round-phases-and-sub-phases.md) | Rounds are driven by a phase and sub-phase state machine | Accepted |
| [0011](0011-win-condition-and-round-cap.md) | Win condition: last team standing, with a round cap | Accepted |
| [0012](0012-effect-taxonomy.md) | A closed taxonomy of spell effects | Accepted |
| [0013](0013-learning-stack.md) | Learning stack: Python trains, the engine records and hosts policies, a static viewer | Accepted |
| [0014](0014-exploration-in-recorded-datasets.md) | Record datasets with an exploring agent instead of reaching for reinforcement learning | Accepted |
| [0015](0015-content-studio.md) | A local content studio: browse, edit, version and try the game content | Accepted |
| [0016](0016-value-learning-on-an-advantage-baseline.md) | Learn action values against a state baseline instead of the raw match return | Accepted |
| [0017](0017-spell-initiative-on-unlock.md) | Unlocking a spell raises the creature's initiative | Accepted |
| [0018](0018-price-initiative-in-the-agent-weights.md) | Price initiative in the heuristic agents' weights | Accepted |
| [0019](0019-regeneration-the-healing-counterpart-of-bleed.md) | Regeneration, the healing counterpart of Bleed | Accepted |
| [0020](0020-energy-regeneration-and-the-price-of-energy.md) | Energy regeneration, and the price of energy | Accepted |
| [0021](0021-tune-the-catalogue-with-a-declared-search-space.md) | Tune the catalogue with a declared search space, not with a model | Accepted |
| [0022](0022-price-a-defensive-effect-by-the-damage-it-prevents.md) | Price a defensive effect by the damage it prevents | Accepted |
| [0023](0023-a-hosted-studio-with-github-as-its-backend.md) | A hosted studio, with GitHub as its backend | Accepted |
| [0024](0024-test-the-studio-page-with-nodes-own-runner.md) | Test the studio page with Node's own test runner | Accepted |
| [0025](0025-the-balance-knobs-are-a-part-of-a-studio-change.md) | The balance knobs are a part of a studio change, like the alias map | Accepted |
| [0026](0026-price-what-a-cast-costs-and-how-long-it-lasts.md) | Price the part of a cost the combat reading hides, and how long an effect lasts | Accepted |
| [0027](0027-a-condition-remembers-the-spell-that-applied-it.md) | A condition remembers the spell that applied it | Accepted |
| [0028](0028-name-the-defense-weight-and-move-its-price-one-step-up.md) | Name the defense weight, and move its price one step up | Accepted |
| [0029](0029-read-variety-on-an-exploring-run.md) | Read variety on an exploring run, not on the greedy mirror | Accepted |
| [0030](0030-play-the-matches-of-a-batch-at-the-same-time.md) | Play the matches of a batch at the same time | Accepted |
| [0031](0031-an-effect-that-lands-on-the-caster.md) | An effect that lands on the caster | Accepted |
| [0032](0032-measure-the-initiative-weight.md) | Measure the initiative weight, and move it from 0.5 to 2.1 | Accepted |
| [0033](0033-a-critical-cast-multiplies-a-direct-heal.md) | A critical cast multiplies a direct heal | Accepted |
| [0034](0034-a-tier-is-a-depth-a-player-climbs.md) | A tier is a depth a player climbs, not a node in the file | Accepted |
| [0035](0035-lowering-defense-and-taking-energy.md) | Lowering defense and taking energy | Accepted |
| [0036](0036-raising-initiative-the-mirror-that-was-left-out.md) | Raising initiative, the mirror that was left out | Accepted |
| [0037](0037-measure-the-energy-weight-and-move-it-from-0-2-to-0-3.md) | Measure the energy weight, and move it from 0.2 to 0.3 | Accepted |
| [0038](0038-a-wasted-action-is-a-fizzle-whatever-wasted-it.md) | A wasted action is a fizzle, whatever wasted it | Accepted |
| [0039](0039-the-bot-binds-its-targets-on-a-board-that-has-not-happened-yet.md) | The bot binds its targets on a board that has not happened yet | Accepted |
| [0040](0040-remove-the-fizzle-weight.md) | Remove the fizzle weight | Accepted |
| [0041](0041-a-condition-stacks-unless-it-is-a-stun.md) | A Condition stacks unless it is a Stun | Accepted |
| [0042](0042-a-creature-has-no-base-critical-chance.md) | A Creature has no base critical chance | Accepted |
| [0043](0043-a-control-spell-is-not-an-attack-and-reach-is-not-force.md) | A control spell is not an attack, and reach is not force | Accepted |
| [0044](0044-the-exploit-target-compares-against-a-yardstick-that-does-not-hold.md) | The exploit target compares against a yardstick that does not hold | Superseded by [0052](0052-read-the-exploit-term-as-the-best-of-a-panel.md) |
| [0045](0045-a-better-fit-is-not-a-better-player.md) | A better fit is not a better player | Accepted |
| [0046](0046-credit-a-move-along-its-trajectory-not-from-the-end-of-the-match.md) | Credit a move along its trajectory, not from the end of the match | Accepted |
| [0047](0047-a-lookahead-agent-needs-a-hypothetical-board.md) | A lookahead agent needs a hypothetical board | Accepted |
| [0048](0048-the-baseline-is-a-training-time-device-and-was-fitted-as-if-it-were-not.md) | The baseline is a training-time device, and was fitted as if it were not | Accepted |
| [0049](0049-one-seed-is-not-a-measurement.md) | One seed is not a measurement | Accepted |
| [0050](0050-price-how-close-a-hit-brings-its-target-to-a-kill.md) | Price how close a hit brings its target to a kill, and leave the baseline at zero | Accepted |
| [0051](0051-the-policy-sees-what-the-heuristic-sees.md) | The policy sees what the heuristic sees | Accepted |
| [0052](0052-read-the-exploit-term-as-the-best-of-a-panel.md) | Read the exploit term as the best of a panel, not as one named agent | Accepted |
| [0053](0053-score-the-exploit-term-on-the-clock-not-on-the-win-rate.md) | Score the exploit term on how fast the best exploiter wins, not on whether it wins | Accepted |
| [0054](0054-a-playtest-app-on-the-same-engine.md) | A playtest app: the tabletop rule set on a screen, through the same engine | Accepted |
| [0055](0055-a-searching-agent-may-be-built-on-a-policy.md) | A searching agent may be built on a policy, so the loop has an operator that improves one | Proposed |
| [0056](0056-a-pick-buys-a-package-every-other-round.md) | A pick buys a package, and two of them arrive every other round | Accepted |
