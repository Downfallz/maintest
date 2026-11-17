# Documentation

| Section | What you will find |
| --- | --- |
| [roadmap.md](roadmap.md) | The phased plan that rebuilt the engine from the Domain2 prototype (complete). |
| [learning-roadmap.md](learning-roadmap.md) | The plan for smarter agents, the content and engine iteration loop, and the viewer. |
| [learning/features.md](learning/features.md) | The published feature schemas: what each index of an observation means. |
| [learning/artifacts.md](learning/artifacts.md) | What a recorded run writes: manifest, steps, episodes, traces, and their JSON conventions. |
| [learning/agents.md](learning/agents.md) | The baseline agents (random, greedy, heuristic), their lookahead, and the scoring weights. |
| [learning/training.md](learning/training.md) | The Python side: weight search, behaviour cloning, value regression, the policy file format, the loop. |
| [learning/explained.md](learning/explained.md) | The learning loop in plain words, for a newcomer: what the pieces are for and how to read a report. |
| [learning/journal.md](learning/journal.md) | One entry per change that moves a number, each a file under [learning/journal/](learning/journal/): how to write one, and the generated list. |
| [learning/plan.md](learning/plan.md) | Plan (draft): where the learning loop is going, and what has been refuted on the way. |
| [art/creature-art-direction.md](art/creature-art-direction.md) | Approved creature concepts, spell staging, mutation cues, and the ART-2–6 asset plan. |
| [art/asset-intake.md](art/asset-intake.md) | ART-2 WebP references, original PNG upload paths, hashes, alpha audit and ART-3 handoff. |
| [art/runtime-sprites.md](art/runtime-sprites.md) | ART-3 battle sprite selection, anchors, contrast and fallbacks. |
| [art/branding.md](art/branding.md) | The official UI palette and type (with `branding.css`; regenerate with `node scripts/sync-branding.mjs`), the bone and soot of ADR 0158, and the published design system built from them (`design-system/`). |
| [art/spell-vfx.md](art/spell-vfx.md) | Spell visual effects in the battle view. |
| [art/mutation-cues.md](art/mutation-cues.md) | Mutation cues on one arena body. |
| [art/battle-polish.md](art/battle-polish.md) | Playback polish, and the art pipeline's follow-ups. The ART-2 source list is `art/source-intake.json`. |
| [adr/](adr/README.md) | Architecture Decision Records: what was decided, why, and what it cost. The index is generated from the files. |
| [architecture/overview.md](architecture/overview.md) | Layers, dependency rule, folder layout, how a request flows. |
| [architecture/stateless-table-inventory.md](architecture/stateless-table-inventory.md) | What has to move for a table no process holds in memory (ADR 0159). |
| [engineering/ai-workflow-plan.md](engineering/ai-workflow-plan.md) | Plan: the AI workflow from an idea to a merged pull request, measured on 300 pull requests and 20 sessions, in nine stages. |
| [engineering/ci-minutes-plan.md](engineering/ci-minutes-plan.md) | Plan: CI that fits a private repository's Actions minutes, and a shorter wait for `gate` on every pull request. |
| [engineering/retros/](engineering/retros/README.md) | One file a week from `/retro`: the numbers, the findings that recur, and the rule or skill changed for each. |
| [engineering/game-releases.md](engineering/game-releases.md) | Proposed player-facing version numbers, release promotion and rollback. |
| [reference/commands.md](reference/commands.md) | Every command, flag and route, one heading per command (ADR 0181). |
| [domain/glossary.md](domain/glossary.md) | The ubiquitous language. Code must use these words. |
| [domain/game-rules.md](domain/game-rules.md) | The rules of the game as implemented or decided, plus open questions. |
| [domain/content-review.md](domain/content-review.md) | Review: an adversarial reading of the content and the rules as bot play shows them, the hypotheses a physical playtest should test, and what to do before the first session. |
| [domain/player-terminology.md](domain/player-terminology.md) | The player-facing words over the domain's own: Mutation, Stage, Tempo, Instinct/Precision, Upkeep → Command → Combat → Aftermath. |
| [domain/tier-evolution-plan.md](domain/tier-evolution-plan.md) | Plan: a planned change of the tiers, measured against [its audit](domain/tier-evolution-inventory.md). |
| [domain/free-activation-plan.md](domain/free-activation-plan.md) | Plan: a second combat declaration mode, spells chosen at the slot (ADR 0110). |
| [domain/package-renaming-plan.md](domain/package-renaming-plan.md) | Plan: the English display names of the packages and spells. |
| [tabletop/plan.md](tabletop/plan.md) | The plan for the board game and the playtest app: what is translated, in which order, and what is still open. |
| [tabletop/translation.md](tabletop/translation.md) | The audit: what every rule of the engine costs to track by hand, and the verdict on each. |
| [tabletop/components.md](tabletop/components.md) | What is in the box: the manifest and where each count comes from, the card face, the boards, and the generator's specification. |
| [tabletop/rulebook.md](tabletop/rulebook.md) | The print draft of the physical game, frozen on 2026-11-16: the official rulebook is `rules/` (ADR 0188). |
| [tabletop/player-aid.md](tabletop/player-aid.md) | One page: the round sequence, the timeline order, condition timing, what a critical multiplies. |
| [tabletop/d20-criticals.md](tabletop/d20-criticals.md) | Built (ADR 0100): every critical chance is a twentieth; why the offsets were legacy rather than tuning, the readings that priced the snap, and where the rule is enforced. |
| [tabletop/playtest-app.md](tabletop/playtest-app.md) | The playtest app's specification: what the client may never decide, the hidden-information boundary, and what a session records. |
| [tabletop/automa.md](tabletop/automa.md) | Draft: the Automa, the opponent a person executes from printed components; how it is built, measured and proved reproducible. |
| [tabletop/app-roadmap.md](tabletop/app-roadmap.md) | The app in seven stages: what each one ships, what it is tested with, and what a reviewer checks. |
| [tabletop/free-activation-playtest.md](tabletop/free-activation-playtest.md) | The free-activation playtest (ADR 0110). |
| [tabletop/accounts-plan.md](tabletop/accounts-plan.md) | Plan: player accounts with a Google sign-in, and the home page (ADR 0122). |
| [tabletop/friends-plan.md](tabletop/friends-plan.md) | Plan: friends, and inviting one of them to a table. |
| [tabletop/admin-plan.md](tabletop/admin-plan.md) | Plan: the admin panel as the host's headquarters: tabs, players, the jobs' runs, registration. |
| [tabletop/scenarios-plan.md](tabletop/scenarios-plan.md) | Plan: a recorded match cut at the top of a round, played as practice, shared as a link or a `scenario.json`, and the admin diagnosis of a stuck table. |
| [tabletop/onboarding-plan.md](tabletop/onboarding-plan.md) | Plan: onboarding, tutorials played from repository scenarios straight into combat, and concept cards for the rule words. |
| [tabletop/rulebook-plan.md](tabletop/rulebook-plan.md) | Plan: the official rulebook as one source in `rules/`, the app's edition, the `/rules` page the concept cards link to, and the keeper that holds it true. |
| [tabletop/feedback-plan.md](tabletop/feedback-plan.md) | Plan: feedback from the table. A bug report carries its position, is reproduced as a scenario, filed as a public issue from a redacted copy and kept as a regression test; an idea is grouped with its duplicates and led to a plan. |
| [tabletop/async-recap-plan.md](tabletop/async-recap-plan.md) | Plan: the recap a player reads in an asynchronous game, on coming back or at any time: where it stands, the two teams, the recent rounds with what was not seen marked, what they had chosen, what they are asked. |
| [tabletop/home-navigation.md](tabletop/home-navigation.md) | Home navigation: the signed-out introduction, the dashboard, the menu, one Settings, and the bell that counts what waits. |
| [tabletop/browser-battle-plan.md](tabletop/browser-battle-plan.md) | Plan: the battle in the browser, from the isolated lab (ADR 0123) to the game every seat opens in. |
| [tabletop/battle-ux-parity-plan.md](tabletop/battle-ux-parity-plan.md) | Plan: the battle view UX recovery, AU1–AU14: classic behaviours, reuse boundaries, acceptance criteria. AU3a records energy before AU3b renders receipts; AU12 adds public creature references and retained last-cast links, historical inspection staying on recorded frames; AU13 the final scorecard, recorded-seat notes, optional notices and haptics, unambiguous seat resume. |
| [tabletop/battle-ux-acceptance.md](tabletop/battle-ux-acceptance.md) | AU14: the cross-feature acceptance of the battle view. |
| [tabletop/battle-release-acceptance.md](tabletop/battle-release-acceptance.md) | A8 (ADR 0136): the rollout preference, budgets and release profiles; physical and new-player gates remain open. ADR 0132 versions event streams: new tables record ordinary energy, resumed legacy tables omit it before sequencing, preserving feed and checkpoint indices. |
| [../studio/README.md](../studio/README.md) | The content studio: browse, edit, version and try the game content. |
| [../scripts/README.md](../scripts/README.md) | The scripts: the gate, the learning loop's readers, the reports, what lives where. |
| [../viewer/README.md](../viewer/README.md) | The viewer: traces, batches, evaluations, training runs and comparisons. |

Conventions for writing docs are in `.claude/rules/docs.md` and apply to humans too.
