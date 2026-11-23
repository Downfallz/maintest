# code-reviewer lessons (one line each, with where they came from)

Folded by `/retro` (2026-W48). A line seen more than once carries its count and its sources; a lesson that
holds moves into `.claude/rules/`, an area's `AGENTS.md` or `.claude/agents/code-reviewer.md` and leaves here.

## Reviewing a moving target

- The branch, the index or the checkout moves while you review (the author session keeps pushing, staging or
  editing; a sibling reviewer's memory lands uncommitted). Seen over 20 times: #612/#613/#615, #658, M2, R2,
  R1 feedback, R3, M4, R1 recap, R4 feedback, R7, R5, #674/#683, #679/#666, R2 recap, showcase follow-ups,
  R3 recap, R4 recap, #772, #756, #737/#750, #739, scene paintings S3 and S4. What holds: review a
  `git archive HEAD` copy (or `git checkout-index -a --prefix=` of the index, hashed with `git write-tree` at
  start and end), never the checkout; `git rev-parse HEAD` and diff `<reviewed sha>..HEAD` before writing up a
  finding and again before the report; name the sha the findings were checked on and what the newer tree claims
  to address; rebuild after any HEAD move before reporting a failure (stale binaries failed 2 tests, #739).
  Belongs in `.claude/agents/code-reviewer.md`, not memory.
- On a second round, map each first-round finding to a test that breaks if its fix is reverted: answers can land
  as code only (#1076, 85b09ee).
- The author edited `lessons.md` directly in a fix PR (#707/#711): flag it, sessions write `lessons/<branch>.md`.

## The gate on a copy

- A `git archive` copy needs what the checkout ignores: `table/battle-build` copied in before `dotnet build`
  (TableFilesTests and the bundle tests read it, O3b, I1), `data/dst` built for the real-engine specs (#735),
  `battle/node_modules` and `studio/browser/node_modules` linked (O3b, T1), and here DOTNET_ROOT=/usr/lib/dotnet,
  PLAYWRIGHT_BROWSERS_PATH=/opt/pw-browsers. Four times.
- `dotnet format --verify-no-changes` catches what build and test do not: a `using DownfallArena.*` above
  `using System.*` (#786, R6), an expression-bodied generic with a `where` and an initializer (#866, round 2).
  Three times: run format on every round.
- A test project that never compiled cannot have run (R4, CS8620); a mutation probe that does not compile leaves
  the old binary and `--no-build` reports it green: read the build's error count (#874).
- Run every `node --test` file, not only the ones the stage lists, and on the final HEAD after any prose edit to a
  skill a test reads (M4 prompts.test.js, UX12 P2 journey-briefs.test.js placeholder path, #740 git-ignored path
  needs its trailing slash).
- CI lints `scripts/` with learning's ruff (ci.yml): run ruff on any new scripts/*.py (R2 recap).
- Every bare-command `execFileSync`/`spawnSync` under `scripts/` carries `// NOSONAR` with an S4036 reason; Sonar
  raises the hotspot the local gate cannot see. Twice (UX5 pictures.mjs, UX6 round.mjs).
- Sonar's new-code coverage excludes Cli, table and battle: a sub-80% gate on a front-end PR comes from the few
  Application/Infrastructure lines, usually an untested refusal branch (#1004). Sonar's issue list answers without
  a token (`api/issues/search?componentKeys=Downfallz_maintest&pullRequest=<n>`): match each issue to the diff
  before accepting a "Closes" on a Sonar loose end (0d000b2).
- A Markdown-only diff gets `build=light` (`git diff --name-only | node studio/browser/plan.js --pull-request`):
  no bundle, no format, but the node tests run (#740).
- A CI change that cannot run outside a runner needs a run on the pushed branch before approval (#892).

## A test that passes with the fix deleted

- Delete the guard, the clear, the wiring or the filter and re-run: the test often stays green. Seen over ten
  times: #771 (pending flag), R2 recap (fixture fills the primary source), R6 import (earlier guard refuses
  first), R5 round 2 (old key set to the same value), #736 round 2 (fixtures agree on both sources), N5
  (worker shares the page's memory), UX12b (frame holds only the targets), #936/#937 (passes on main; a getter
  asserted only at its default, also #812, #918), #1076 (second clause of a title unasserted), #848 (no negative
  test), #921 (fetch-wrapper guard no suite runs). Make old and new sources disagree in the fixture; ask for the
  test to fail on `origin/main` or be called a guard. Belongs in `.claude/rules/tests.md`.
- A test whose assertion is computed from the output it checks holds whenever the value is in range (#737/#750).
- A test name says one fixture and the fixture builds another: read the fixture against every word of the name.
  Twice (showcase catalogue.test.js:197, then #770 :102); also #912 (Dispose takes the let-go path, not a fault).
- A regression test documented as failing "every time" must be run 10+ times on the old source (17 of 20, #679).
- A battle spec of a `table/` module runs the bundle (`table/battle-build/live.js`): mutate the bundle or rebuild
  before calling it proven (#824).
- A test the PR lists under "failed before its fix" can be red after it too: run it on HEAD (#912).
- Proving a pure refactor equivalent: import the `git show origin/main:` copy beside the new one and fuzz both
  (0d000b2).

## What a route sends, tested through the route

- A field added to a route's JSON is tested only at the projection, the pure mapper or a spec that writes the
  field by hand; nulling it at the host leaves every Cli test green. Ten times: `/api/catalogue` `visual`
  (#812, UX10, #1004, #1010, UX12 P3 `figures`, UX12 P5 `artwork`, #918 SpellBeat), R4 recap `Payload`, #739
  refusal mapping (409 falls to 503), R6 `bug` in GET /api/admin/scenarios; R4 recap clips copied untested.
  host.md:80 names the rule and it keeps being missed: PackageTagsTests asserts only family/motif/portrait.
  Ask for one test through the route per field, and a machine check if it recurs.
- A setter or optional dependency wired only in the composition root (`accounts?.Changes`, `push?.Accounts`,
  `Lobby.Devices`, `Outbox`, `MutationArt? art = null`) is untested when every test assigns it by hand. Four
  times: UX10, N2, N3, N5 (#1109 holds it). Make it required, or test through the composed host.
- A per-host override of a shared limit (StudioServer.MaxBodyBytes) needs a test through the real host (73732af).
- A new public member on a type sent whole through `StudioResponse.OfJson` (`TutorialPlay`) makes every answer
  500 (#756): grep for the type being serialized before adding one.

## Exact lists a new item breaks

- A new item meets a test's exact list: scenarios/ in ScenariosApiTests (O2, O5), `table/navigation.js` in
  lobby-home.spec.js (R2 feedback), a fetch at load in `toEqual(['/api/` (#717), a split module in
  picture-kit.spec.js's KIT and plan.js SHOWCASE_FILES (#1016), a `table/*.js` in `TableFiles.cs` (BC2), a screen
  in journeys.mjs and baseline.md (UX3). Six times: grep tests for the old name and its list.
- A rename must be grepped in every suite that asserts it: the real-engine journeys (`Play again`, UX8), a
  control's `toHaveText` once its name moves into `.unseen` spans (#1087), a new id colliding with another
  (`opponent-friend`, #1092). Three times.

## Probing a scanner, a validator or a source check

- A text or source scanner built from one spelling, one list or one heading passes the case it exists for.
  Seen over twelve times: word scanners' exemptions (W8, R1, #687 renamed Pummel drew 0; lowercase "speed";
  "Tier" refusing French "tiers", R6), a closed list of in-between words (#687), an allowlist reused for a second
  exemption (#687), `SkipWhile(heading)` passing empty once renamed (#676), "one reader" guards keyed on parse
  literals (#675 twice), a refusal on "no path" reading `fatal:` as a path (#716), forward windows and waits
  (#784, #857 three ways: allowlist keyed on file + text, `.not.`, `(?:not\.)?`; own helpers that click), a
  single trap spelling (#921), `\([^)]*\)` eating `min()` (#967), a width regex blind to the default (UX12 P4),
  a name list from one table (#1022), `Number(cell)` and `spans[cell]` (BC3), escape hatches checked with
  `startsWith` (UX3, twice), a Markdown subset (R1), a test file holding its own needle (#656). Probe with a
  rogue input written differently, rename each data name and count refusals, print the list the scanner builds.
  Belongs in `.claude/rules/tests.md`.
- A parser returning null for "a variable" is fail-open for everything it cannot read (#784).
- An optional declaration field written only when truthy: probe null, {} and an array (UX12 P2).
- An offset map built on code points but indexed by UTF-16 units, and `slice` before `encodeURIComponent`, break
  on emoji. Twice (#884 twice, R5).
- A .NET `^...$` validator without `\z` accepts a trailing `\n` (#733).
- A same-site guard `startsWith('/')` admits `//host/` and `/\host/` (N2).

## Null and shape in posted JSON

- `[null]` in an array of records deserializes and throws NRE, answered 500 with the exception's text; a fix
  covers the DTOs the issue names and leaves siblings, then `"x": null` on a list, then on a scalar reference.
  Four times (O2, R1 feedback, #890, #890 round 2): probe element, list and scalar. Belongs in host.md.
- A null walker must guard every list it dereferences, its own included (#890).

## Guards, refusals and their words

- `RecordAsync(x) is { Status: Open }` fails open on a missing record. Three times (R5, R5 round 2, #790): grep
  `is { Status: TableRecord.Open` whenever host.md gains a guard rule.
- A guard widened to fail closed feeds every other reader of the bool, and keeps the old refusal's words for a
  state that will never resolve. Three times (#736 page note, #790, #815): read each sentence against every
  state that reaches it, and ask for a third state (unknown).
- One error code shared by two guards with different conditions: read the message against each raiser (#814).
- A replaced exception text naming one cause is wrong when the catch is wider (#828 StoreFails); a widened catch
  on one caller leaves its sibling narrow, and misses the loader's own type (InvalidGameContentException) (#846).
- An exception-text sweep must cover the whole host (#786, #828); now in host.md.
- A new console line naming an operator must go through `scripts/ops/report.mjs` `mask()`; a masker keyed on a
  line's shape must be grepped against every writer and probed with framework text ("reset by peer"). Three
  times (#624 twice, #691/#692/#755). Belongs in host.md.
- Log `GetType().Name: Message` on one line: `{exception}` writes a stack, one ops "error line" per frame (#828).
- A class with a `Said` sink: a new `Console.WriteLine` bypasses it (#828). A host sink narrows a "never said on
  the console" assertion to the host's own lines (#756).
- The console-swap lesson is a rule and a test (`ConsoleOutputTests`) since #656: check the test.

## Races and read-then-write

- A check read outside the write gate is check-then-act: rate guards (R1 feedback), "no idea merged" (I1),
  "kept already?" before a slow side effect (R4 Reproduce), "record still there" against a prefix delete in name
  order (#789), "deleted since I read it" vs the step's own append (#874), an in-flight set filled after start
  (#849). Six times: re-check inside the gate, register before the work starts.
- A process-wide gate around a storage write serializes every account (#931); each "and wait for X" adds its own
  timeout (#849); `FileArtifactWriter.Resolve` recreates a deleted run's directory (#818).
- Moving a send onto a queue or another sender moves its per-process memory with it (dead endpoints, 410s).
  Twice (N3, N5). A send that swallowed failures replaced by a throwing queue write turns callers into 500s (N5).
- A non-essential read inside a fire-and-forget try loses the notice through the catch-all: degrade to null (R4).
- A per-row storage read added to a listing or a poll route: count round trips per row and give it a degrade
  path. Twice (R1 recap, R4 recap). Belongs in host.md.
- Moving an event's raise out of a lock races every test that counts it after waiting on the state (#658).
- A status switch built from one base that clears side fields drops data on untested transitions (I1).
- A static gate shared by instances collides across parallel test classes (#649).
- `SteppedSession.IsOver` is true for faulted and cancelled sessions and lags the board; narrowing the host's
  `done` leaves the page's `over` wider (#912).
- `.WaitAsync(limit)` is no deadlock guard when the driver runs on the caller's thread: `[Fact(Timeout)]` (#829).
- Two notes in one request share the clock's instant: assert a tied pair unordered (R3 feedback).

## Flakes and timing

- A spec set that passes alone can fail at `--workers=4`, and old and new copies side by side share the CPU:
  run the changed set at load beside its `origin/main` copies, then alone. Five times (#784/#785, #819,
  7d2c2cc, #1035 caption 6 of 8 at 4 workers, #737/#750 solution-wide `dotnet test` 4 of 5). Here: workers=4,
  one project. Reproduce a flake with the old spec copied in beside the new one, `--repeat-each=4` (#749).
- A flake fixed "by speed" must carry measured times against the budget; a `page.evaluate` costs ~300 ms on the
  software canvas like a locator call (#735, #921). CI's real-engine durations come from the artifact's
  screenshot timestamps (#735). A flake reproduced by scaling a deadline down proves nothing about the original
  (#832); a CPU-starvation flake is not closed by a wall-clock assertion (#733).
- A UI wait is causal only if the UI change comes from the host's answer (#816/#831); battle specs pause the
  clock in `mount` (7d2c2cc); `page.clock.fastForward(n)` runs each due timer once (#749).
- A Playwright `page.on('response', async)` handler is not awaited; collect in an `addInitScript` fetch wrapper,
  and keep a clone's `.json()` off stream routes (#903/#908). `request.post` does not throw on 4xx (#941); a
  helper waiting for one `/decision` reads the first of a batch (#941, #787 tempo pump).
- A child's `exitCode !== null` misses a death by signal: `exitCode ?? signalCode` (#941).
- A page clock driven by Phaser's frame delta runs in slow motion on a slow renderer (BC2); `headlessStep` skips
  the render hooks (#961); Phaser tweens run on `Date.now` (#842); DOM listeners on `#phaser-mount` never fire.
- A film made deterministic must be run three times per value of the new knob; a flag read after `finally
  { stop() }` reads the reset state (BC9). A strip's `act` runs before frame 1 (#978); `walkJourney` in strip
  mode drops `blocked` (f1b5e36); film every new strip at 320 (UX12 P1).
- `await img.decode()` on a lazy image out of view never settles (S6).
- A diagnostic thrown after a 20 s `toPass` rarely reaches a 30 s test's report (#819).
- A shard count must be checked with `playwright test --list --shard=i/n` on HEAD (M3).

## Fixtures and mocks unlike the host

- A fixture or mock shaped unlike what the host sends hides the bug: a delta feed given as a full one (#858), the
  whole feed served once `over` (#904), a frame holding only its targets (UX12b), one table id for every practice
  POST (T1, #904), a mock still offering an action the host now refuses (#736), a `since=0` stub matching the
  first poll (#1073), a fetch fixture counting every caller of the route (#768). Seven times: give the fixture the
  route's real shape. Belongs in `.claude/rules/tests.md`.
- `#loading` hides before the first board; `#connection` reading LIVE means it arrived (#768).
- A newest-view guard keyed on (feedNext, waitingAsked) drops the held-answer gap view (#787).
- A client "answered already" guard keyed on askedKey holds only through the host's Decided+1 chain (#772).

## Pictures, sizes and layout

- A picture's `sizes`, media switch or box must be probed in a browser at each breakpoint: `currentSrc` at 390@2
  against the `<dialog>`'s UA max-width (S11), a test whose rows pick the same file under old and new values
  (#1070), a fixed share on an auto-fill grid (S8), a portrait tablet under `(orientation: landscape)` (#1090),
  a width that follows its height (UX6), an absolute `<img>` that ignores `right` and a crop any edge check
  passes (S2 twice), a phone width compared with the CSS box (S11), a portrait source for a landscape slot
  (#1103). Eight times in the week. Belongs in `.claude/rules/pages.md` or the design-reviewer's prompt.
- A later rule at equal or higher specificity wins: `display:` after `[hidden]` (O5), id to class (#699),
  identity.css's `.scene-plate > .picture` (S9), the 11 px floor after the practice band (f1b5e36), `:is()` with
  an id (UX12), a `transform` already set by a more specific rule (#1035), `body:has(dialog:modal)` against the
  modals the app opens itself (#1087). Seven times: grep the element's competing rules and measure.
- A size or fit check passes on a neighbour: probe the label's own `scrollWidth`/`scrollHeight` (f1b5e36, 9f033d5),
  the header's own box (T3 #669), each control's own floor (#1029), every `<dialog>` not a hand list (#1068), the
  settings button past 320 (R3 recap). Five times.
- A negative margin must be read against every rule narrowing the gap (#1092); a `:has(> .picture)` layout still
  matches when the picture fails, and a cached picture never 404s (UX12 P5).
- A contrast claim must be probed with words filling the room the CSS gives them (S6); a new walk measure needs a
  column in baseline.mjs and a failure (S6).
- A count moved onto an `aria-hidden` badge drops it for a screen reader (UX11); digits to Roman numerals reach
  aria text (showcase follow-ups).
- `querySelectorAll('dialog:modal').at(-1)` is DOM order, not the top layer (UX12); a focus-trap spec of 14
  alternating Tabs never reaches deep (UX12).
- A busy flag latched before `location.assign` freezes on Back from bfcache: reset on `pageshow` (UX9); a redraw on
  `pointerup` re-chooses before `click` (UX9).
- A fallback page shown at the failed URL needs root-relative subresources, and a keep-list change reaches every
  page relying on its absence (S10); a worker fallback widened to 5xx applies to every file (R3).
- A layout that drops canonical on a lookup miss turns a typo into noindex (S5); a decoration worn twice reads
  state captured before its own undress (S7).

## Docs and plans the change makes stale

- A changed value, word, order or job leaves its old sentence elsewhere: the glossary's row (#757), commands.md
  and the ADR (#732), `NN px` in docs/art (UX12 P4), a brief's breakpoint prose (#1090), a plan's sentence on a
  removed `ignoreOrder` (#674), the sentences that list a budget (#676), `no install` across AGENTS.md (BC1), a
  comment "offline it draws without X" (S10), prose naming a folded CI job (M3), a rule's caveat deleted while the
  gap remains (#874), an earlier plan's decision overruled without "supersedes" (#1103). Eleven times: grep docs/
  and comments for the old sentence.
- A contradicted accepted ADR needs an amending ADR, not a README edit (#732); an ADR's index row is generated
  (`UPDATE_INDEXES=1`), never written by hand. Twice (R6, showcase follow-ups). Belongs in `/adr`.
- A new value source in `scripts/rules/build.mjs` must be taught to `reach.mjs`, probed with a real `git diff`.
  Twice (#687, #805); now in `.claude/rules/scripts.md`.
- Templated rule text must redo the engine's arithmetic and every value it assumes. Twice (#651, R2); a
  "list contains X" fact is probed by adding an item (#805); a list read from one data field can be constrained by
  a sibling (`anyOf`, #879); tutorial lines read against `knownSpells` and the step's creature surviving (T2).
- A "Closes #N" that settles part of an issue drops its other items: ask for "Part of" (#904, #832).
- A "you finished every X" line keyed on the last item's order (UX8); a "ready" flag skipped by early returns
  (N4); chips moved after an early return (UX12 P3). Read moved or added state against every early return.
- A brief's beats read against the code's hide paths (N4); a privacy retention sentence against a sweep (N2); an
  operator sentence against the player page (R3 feedback).

## Skills, prompts and scheduled tasks

- A scheduled task that attaches the repo mid-session gets skills, agents and hooks as files only: the prompt is
  the code. Twice (W6 digest, #632). Two "never while the other runs" clauses are a mutual exclusion forever.
- An agent prompt that names a heading, label or path must be grepped for it (W8); a skill step that grows a
  pinned list must carry every file the test couples to it (UX3); a skill that fans out subagents must be read
  against where each writes (BC3); a cleanup step needs the earlier step to echo its path (#740).
- A path router keyed on extension misses the JS registries that hold the look (see MEMORY.md).
- Widening a path-scoped rule's `paths:` makes every line apply to the new folder (#613).
- A CI job given a `name:` must be grepped against workflow-metrics.py's JOB_GROUPS (M2); narrowing what a PR
  plays must keep the full set for the paths that define the narrowed job (M2); a script importing a sibling
  widens what plan.js must play (BC10); folding a job as a `continue-on-error` step: read its position, split its
  commands (M3); count the empty jobs a narrowed PR spins up (M3).
- A cache keyed on something slower than its contents never learns (#892); moving a secret to step env does not
  keep it from the PR's code (#709).

## Reports, metrics and the rest

- A summary's "all clear" must be gated on every input being known (W6); one attempt per field shown (W6);
  GitHub list endpoints truncate without pagination and /issues returns PRs (W6); `state_reason` has `duplicate`
  (R5); a per-item fallback under a rate limit must rotate its start (#751); a capped list filtered on the client
  spends the cap (R5).
- An alert keyed on a log substring must be grepped against every writer (W7); a cumulative counter read with
  Maximum measures the lifetime (W7); a Bicep `utcNow()` must be updatable in place (W7).
- A "last N days ending today" window counts a partial day: end at yesterday (M1); page a live list by id, size a
  fan-out against the rate limit (M1).
- A "vanished id" check must take its history from the base branch (R1); a page reversing a built list trusts an
  order the build never sorts (R3); a fetch on a tap must let the latest win (R6, #732 side effect before keep).
- An address guard reading `CF-Connecting-IP` is spoofable while `onlyCloudflare` is false (R1 feedback).
- A kept reference to a deletable object renders a dead link: resolve at read time (R4 Reproduce).
- A hand PATH lookup must check `isFile()` and X_OK, spawn as found, not realpath'd; a hashed port can land on a
  local service (#707, #711); `mkdtemp(tmpdir())` + `renameSync` fails EXDEV (UX5); a generator statically
  importing its own output loses its guard (S3).
- "Came over byte for byte" is checked with `git ls-tree -r` blob hashes (S1); an id set must come from the folder
  that owns the kind (S1).
- An arc lifted in screen y collapses on a vertical throw: probe vertical, level and both directions (BC7); a
  flag that makes a primitive follow a path needs its position asserted (BC7); a test over a hand-picked subset
  skips the failing cases, and dividing both sides by the rate checks nothing (BC5); a reduced-motion check must
  fail without `emulateMedia` (BC10); a "names exist" check proves the written names, not the "Removed" prose
  (BC1); a refactor sold as order-independent can move the dependency (#936); a label an effect writes about
  itself proves the label (#918), and an aimed beat falls back to UP on a self cast (#918).
- A JS Set's for-of tolerates deleting visited entries: read an explanation against the language (0d000b2).
- A redirect added on load must be checked against every intent the address carries (O4); a cross-origin link
  drops the reader's language (R7); a select given a value with no option reads '' (BC2).
- A background answerer must not drop "again" on a refused attempt (O3a); `TutorialPlay.GaveBack` is raised after
  the gate: poll the event (#658); a fault injected on "the first event that sees X" can be spent by another thread
  (#683); a test of `Session.Dispose()` takes the let-go path (#912).
- An index reconciled by id alone never re-verifies a known id (#866); dropping an overload from a port leaves the
  adapters' public one (#851); STJ's JsonException positions are zero-based and nullable (#847); a log word's id
  must match the other lines' (#847); a degrading reader swapped for a throwing one changes 500 to null (#775).
- A "replace the destination" guard keyed on two names wipes a folder holding more (#613).
