# AU14 — Battle UX acceptance record

Date: 2026-11-05. Source: main `9c354652`, including AU13 #462 and branding #463.
Scope: cross-feature verification of AU1–AU13, reachable controls, recorded evidence and remaining gates.
The battle view remains opt-in at `/battle`; `/play` remains available. No rules or default-route change.

**Disposition:** automated acceptance is distinct from release approval. Physical-device checks, native
browser zoom/screen-reader testing and an observed first-time player remain unperformed. This record does
not claim that a person understood the game merely because an automated journey found its explanations.

## Baseline reconciliation

- [AU13 #462](https://github.com/Downfallz/maintest/pull/462) includes the review fix that retains an unsaved
  end comment on same-seat visibility recovery. Its draft belongs to the table/seat; another seat, revoked
  access or an unrecorded view clears it. `battle-services.spec.js` retains this regression coverage.
- [Branding #463](https://github.com/Downfallz/maintest/pull/463) is included. Existing battle appearance and
  the shared slate/gold/copper application branding are retained.
- [A8 #437](https://github.com/Downfallz/maintest/pull/437) was **open and unmerged** when
  checked for this record. Its release-profile CI, lobby preference, renderer recovery refinements and
  build-enforced byte budgets are not merged evidence. Integrate/rebase A8 separately, preserving these
  AU changes. Earlier green results on its old branch do not validate today's main.
- ART-2 #436, ART-3 #439, ART-4 #441, ART-5 #445 and [ART-6 #450](https://github.com/Downfallz/maintest/pull/450)
  are merged. ART-6 supplies explicit opt-in sound, cancellation, reduced motion and speed controls on main.
  No asset regeneration or art-direction changes belong to this acceptance pass.

## Gap checklist

“Covered” below means implemented and exercised by automated tests. The plan links the actual merged
implementation PR for each row. Test paths are under `studio/browser/` unless stated otherwise.

| Gap from the plan | Status | Acceptance evidence |
| --- | --- | --- |
| Phase / next action | Covered | `battle-guidance.spec.js`: live objective, next evolution, budgets, waiting and mode-specific explanation |
| First-use explanation | Automated coverage; human gate open | `battle.loop.js`: first-use tips during actual games; `battle-parity.spec.js`: keyboard opening journey |
| Opening energy | Covered | Actual host receipt: six grants, +2 energy and 0 → 2 before any shown acknowledgement; retained History |
| Upkeep detail | Covered | `battle-upkeep.spec.js`: non-default/passive/zero energy, zero healing, lethal ticks, expired conditions, missing data |
| Round turnover | Covered | `battle-pacing.spec.js`: closing casts → aftermath → upkeep; no future-round values under old actions; real-host turnover captures |
| Activity console | Covered | `battle-history.spec.js`: historical paging, filters, reload, stale reads, kept focus and pinned events |
| Mutation chooser | Covered | `battle-mutations.spec.js`: three roots, later offers, shared budget, depleted picks and keyboard selection; real-host cards |
| Mutation reference | Covered | `battle-growth.spec.js` / `battle.growth.js`: read-only tree, actual public reveal, ownership and hidden choices |
| Team tempo draft | Covered | `battle-tempo.spec.js`: 1/2/3 eligible creatures, edit before one confirm, partial/lost responses and explicit continuation |
| Tempo consequences | Covered | Current/catalogue critical values, cost/energy and Instinct/Precision labels; AU14 revisits the first draft with keyboard Home |
| Initiative / ties | Covered | `battle-order.spec.js` and real-host loop: same-team order, rerolls, public reveal, historical order and defeated actors |
| Spell selection | Covered | `battle-spellbook.spec.js`: long names, many spells, cost/critical, unavailable choices, compact/full detail and retained browsing |
| Target predictions | Covered | `battle-targets.spec.js`: host estimates, self/ally/enemy/multiple/empty targets, energy shortfall and unavailable fallback |
| Applied action result | Covered | `battle-parity.spec.js`: keyboard cast, recorded 7 damage, before 20/20 and after 13/20; real-host expanded event |
| Creature inspection | Covered | `battle-inspection.spec.js`: public spells/mutations/conditions, duplicate names, recorded knowledge and last-cast link |
| Match conclusion | Covered | `battle-services.spec.js`: win/loss/draw/round cap/concession; `battle.loop.js`: actual final result and final replay frame |
| Privacy / reconnect | Automated coverage | Two independent actual-host browser contexts; stale read/write fences, one/two-seat choice, revocation, hidden-tab resets and retained same-seat comment |
| Service tools | Covered on main; release scope partial | `battle-services.spec.js`, `table.push.js`: optional feedback, denied/unsupported notices, both-seat suppression, recorded-only notes; A8 still open |
| Viewport / accessibility | Partial | AU14 keyboard journey and short-view sweep pass; native zoom, assistive technology and physical device checks remain below |

## Cross-cutting matrix

| Required scenario | Evidence / limits |
| --- | --- |
| Fresh normal-length 3v3 vs bot | Real host, at-activation, cap 12, later evolution and elimination; no fixture simulates this engine path |
| Hidden-intent | Separate actual-host game, cap 2 to exercise round-cap conclusion; distinct declaration/target stages; not claimed as a normal-length game |
| Two humans | Separate browser contexts, simultaneous planning, other-seat board/options/feed/question unchanged before reveal |
| 1/2/3 eligible creatures | Tempo and mutation fixtures include reduced/dead rosters, empty states and budget limits |
| Batched casts and turnover | Playback/pacing fixtures deliver several events together; real-host games read every retained ending action |
| Lost connection / uncertain writes | Existing live/combat/planning suites cover delayed reads, duplicate SSE, refused/accepted-lost commands and partial tempo batches |
| History / replay | Existing history/loop/services suites cover current/earlier pages, legacy missing frames, operator recordings, final action and return |
| Overlay lifecycle | Escape restores focus; Help preserves drafts; tab suspension clears private surfaces; same-seat end comment survives resume. Physical system Back remains pending |
| Keyboard / non-colour state | AU14 uses Tab/Enter/Home/Escape throughout energy → mutation → tempos → cast → history; no pointer or programmatic focus in this journey. Selection has aria-pressed/selected and text; HP changes use numbers |
| Reduced motion | AU14 uses the browser preference; existing playback suites additionally toggle it during active effects and check cancelled callbacks |
| Layout | 1440×1000, 390×844, 320×568 and 844×390; AU14 also checks 720×500 CSS pixels, the reflow space of a 1440×1000 window at 200% zoom |
| 200% zoom | 720×500 is a reflow proxy, **not** native browser zoom. Existing suites double selected guide/upkeep/mutation text. Full native zoom and OS text scaling remain pending |
| Art / performance | Missing-art fallback remains selectable; bounded journal DOM/cache and stable spell nodes covered by existing tests. See byte sample below; no physical FPS/GPU claim |
| Services | Silent default, explicit sound/vibration, push denial/unavailability, no diagnostic without a click, no both-seat alerts, notes only when recorded |

## Captures from this build

The following desktop (1440×1000) and phone (390×844, Chromium touch emulation) pairs come from the
**actual .NET host**, at-activation 3v3 journey. They are not designs or generated images.
A transition may use the paced receipt or the standalone upkeep notice when its events arrive under an
open command dialog; both retain actual gains before the next decision. The real-host journey accepts
these two existing paths, while `battle-pacing.spec.js` checks exact playback ordering.
The late spellbook is the naturally acquired hand after subsequent evolution, not a fabricated maximum
hand; the separate 19-spell/long-name fixture in `battle-spellbook.spec.js` exercises dense late-game layout.

| Moment | Desktop | Phone |
| --- | --- | --- |
| Opening upkeep | [Desktop](images/battle-acceptance/desktop-upkeep.jpg) | [Phone](images/battle-acceptance/phone-upkeep.jpg) |
| Three opening mutations | [Desktop](images/battle-acceptance/desktop-mutations.jpg) | [Phone](images/battle-acceptance/phone-mutations.jpg) |
| Completed, edited tempo draft | [Desktop](images/battle-acceptance/desktop-tempos.jpg) | [Phone](images/battle-acceptance/phone-tempos.jpg) |
| Expanded recorded attack | [Desktop](images/battle-acceptance/desktop-history.jpg) | [Phone](images/battle-acceptance/phone-history.jpg) |
| Round-transition receipt | [Desktop](images/battle-acceptance/desktop-turnover.jpg) | [Phone](images/battle-acceptance/phone-turnover.jpg) |
| Later spellbook | [Desktop](images/battle-acceptance/desktop-spellbook.jpg) | [Phone](images/battle-acceptance/phone-spellbook.jpg) |

## Verification

| Check | Result |
| --- | --- |
| Battle TypeScript / bundle | Passed |
| .NET solution build | Passed; 0 warnings / 0 errors |
| JavaScript unit tests | 1,116 passed |
| .NET tests via compiled runners | 2,278 passed; 26 existing skips (storage emulator / existing skipped sample) |
| Full battle browser matrix | 738 passed; 21 existing duplicate-profile skips; 759 cases total (12.9 minutes) |
| Real-engine battle + push journeys | 16 scenarios passed: 12 unaffected scenarios plus 4 complete-match reruns (3.9 minutes) after correcting the turnover test |
| Local .NET test / format launchers | Blocked by named-pipe `SocketException (13)`; test DLL runners above succeeded. Full solution formatting must be confirmed by CI |
| Diff whitespace | Passed |

The first real-host run had two phone failures because it required the paced receipt even when the
existing standalone upkeep notice was displayed. The test now validates either path; the four affected
complete-match variants were rerun successfully without raising timeouts or skipping assertions.

Commands are in `battle/README.md` and `AGENTS.md`. Local .NET used SDK 10.0.403 and single-worker build;
browser checks used Chromium headless shell 142.0.7444.61 with Playwright 1.63.0. Automated tests use
software rendering and do not benchmark mobile GPU performance. The new `battle-parity.spec.js` is
included automatically by the existing browser CI glob. Real-engine changes extend `battle.loop.js`,
already included in the host CI configuration. CI/Sonar remain required before merge; consult the PR's
checks for the exact commit rather than treating this local record as a CI result.

## Resource sample and limits

Local production build, before transport headers: `live.js` **1,556,136 bytes raw / 425,910 gzip**;
lab `game.js` **1,419,124 / 379,624**. Gzip is a local compression sample, not an assertion about server
Content-Encoding. Four creature WebPs total **1,212,726 bytes**, the background **315,644**: **1,528,370**
image bytes. Lab and live are separate entry points; they are not both required for a live visit.
These are observations, not newly imposed release budgets. A8 owns the budget enforcement.

History keeps at most 200 recent rows plus a separate 200-row reading page; at most 200 list rows and one
event inspection are mounted. Existing 10,000-event tests exercise those caps. A full historical HTTP
response still grows with the entire feed before normalization. The earlier AU6 measurement of 19,268,091
bytes is documented in `battle/README.md`; this pass does not claim to have solved that transfer cost.

## Remaining acceptance gates

- [ ] **New player:** observe an uninterrupted first match without coaching. Ask them to explain the +2
  opening energy from the receipt, choose a mutation using tags/spells, prepare and revise all tempos,
  identify why a creature lost HP, and reopen that specific event. Record misunderstandings and corrective
  actions. The automated run checks the information and navigation, not comprehension.
- [ ] **Physical Android browser and installed PWA:** device/model, OS/browser versions, browser chrome,
  keyboard, touch, rotation, system Back, suspend/resume, feedback and actual notification delivery.
- [ ] **Physical iPhone/iPad Safari and installed PWA:** same checklist plus standalone push eligibility.
  Chromium mobile emulation is not Safari or a physical-device check.
- [ ] **Assistive technology and native zoom:** NVDA/VoiceOver/TalkBack announcements, focus order,
  dialog return, native 200% browser zoom and OS text scaling. Automated accessible names are not a
  screen-reader listening session or a contrast audit.
- [ ] **A8 release reconciliation:** rebase #437 onto these merged UX changes; re-run its WebKit/release
  profiles, same-seat renderer recovery, rollout preference and byte budgets. Keep the classic route.
- [ ] **Device performance:** physical frame/memory/thermal measurements over a complete match, including
  dense history. Revisit server-side history pagination separately while preserving seat privacy.

No physical devices or human first-time participants were available in this workspace. These gates are
left open explicitly; AU14's automated evidence does not authorize making the battle view the default.
