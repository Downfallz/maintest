# host-reviewer memory

- RepositoryScenarios is read by the API process alone at startup; a bad file is dropped with a console line, never a crash. LessonReach runs only in tests and scenario-check, not at the host (O2, tutorial block).
- ScenariosApi.Hidden (ScenariosApi.cs:80 since #646, was :45) opens a shipped scenario (sc-repo-*, tutorials included) to any signed-in account when `--admins` is empty; recheck whenever shipped scenarios gain a new kind (O2).
  - Second time: sc-repo-bug-* (Ships && !Teaches) carries both seats' answers past the cut and opens to any signed-in account when --admins is empty (R6).
- Content-only diffs under scenarios/ need no route test, but a tutorial outside scenarios/tutorials/ still counts in `Tutorials`: check the directory, not just the block (O2).
- A seat's asking (`JournaledStepper.Asking`) moves only on that seat's own decisions: a Late from DecideAsync on a seat answered in the background means someone else answered it, so the state moved; never drop the re-look on Late (O3a, TutorialPlay.AnswerAsync).
- Practices (ScenarioPractices) live in the API process's memory by ADR 0179: rule questions 4-5 are answered by that ADR, but a POST practice retry replaces the practice and resets anything kept on it (O3a: tutorial `done`/`shown`).
- A background answerer that only logs a failure, without handing the seat back, leaves a question the page is told not to draw: check every failure path ends in a state the page can act on (O3a).
- A hand-back changes the view, not the match: it reaches the stream only through `tutorial.GaveBack += api.Changes.Bump` (ScenarioPlay.cs:69); the #646 test fails if that line goes, so keep it when ScenarioPlay is reworked (#646).
- A build script that takes an output folder and `rm -r`s it before a rename (showcase/build.mjs) deletes whatever path it is given, `.` included: check the guard whenever a destination becomes an argument (#613).
- A test that reads a bot-vs-bot table "under way" races the match's end; hold it with a person seat (`Person!.Arrive()` + `Session.Begin()`), and assert `IsOver` false after the read (#598).
- A finished table stays held (swept after FinishedFor), so `TableRegistry.OpenAsync` drops it via `IsOver` from both the held list and the index list; an "ended is not counted" test is deterministic once `Session.Outcome` completes (#598).
- A new table/*.js needs no new host test: TableFilesTests.Every_module_beside_the_page_is_served fails on any module missing from TableFiles.cs; check the Dockerfile copies it (COPY table/*.js) only for a new subdirectory (O3b).
- A new TutorialLesson check makes a shipped tutorial that breaks it vanish from the series at startup (RepositoryScenarios drops it, ScenarioStore.Series shrinks): grep scenarios/tutorials/ for the field before approving (O3b: no file used pointsAt.control).
- `JobsApi` once held its one-pass gate in a static field, so parallel test classes turned each other away with 409 Jobs.Passing (#666); it is per instance now. A gate that a host holds once is kept on the instance, not the process.
- A test that swaps Console.Out belongs in `[Collection(ConsoleOutput.Name)]` (ConsoleOutput.cs); one left parallel on purpose must say why, and its captured text carries every parallel class's lines (#649 diagnostics PR).
- Moving an event out of a lock opens a window where the state reads changed before the event is said: a test that polls the state (`Assisted`, `Shown`) then asserts a count of the event races it; wait on the event itself (#658, TutorialPlayTests late hand-back).
- A hand-back is said by TutorialPlay itself since #658 (HandsBack/SayGaveBack); `AssistedSeat.GaveBack` has no host subscriber left, and `AnswerAsync` still calls `table.Read()` under `_gate` (#658).
- Account documents written read-modify-write (tutorials beside preferences) are serialized in the API process only, and AccountApi.ForgetAsync does not join that chain: a note in flight can rewrite a deleted account's document; check every new per-account document against ForgetAsync ordering (O4).
  - Second time: a document written on behalf of another account (the notice log: AdminApi.VoidAsync -> NotifyVoidAsync reads accounts off the record) gives a deleted account `notices/<id>.json` back (N1, #1108).
  - Third time: MarkGoneAsync after a 410 writes even when the account holds no such device, recreating `devices/<id>.json` mid-delete; a change that finds nothing to change writes nothing (N5, #1129).
- A proof that lives in a practice (ScenarioPractices, API memory) is gone after a restart or a practice retry: a route that checks it (`/api/tutorials/<id>/done`) answers 409 to an honest retry, and a page that says it once never says it again (O4).
- HttpHost.HandleAsync (Hosting/HttpHost.cs:193) answers any unhandled exception as 500 with `exception.Message`: a new route whose store or body parsing can throw leaks that text unless the route catches; check `[null]` inside a posted list too (System.Text.Json yields a null element) (R1 feedback).
  - Second time: exception text still reached answers outside HttpHost (TableServer.cs:163/:206 absolute paths, PracticeTable.cs:77, Push test 502 `failure.Message`); grep `\.Message` across Table/ (#691/#692/#755 PR). HttpHost's catch-all logging `METHOD AbsolutePath` puts `/j/<code>` join codes in the console: check what a path carries before logging it.
  - Third time: AdminApi.cs:375 and :506 still answered `failure.Message` (operator-only) after #786; grep AdminApi too (#786 PR). A "said to the person" marker on an exception (`PracticeRefusal`, `Exception.Data`) is lost if anything wraps it: the route test through `/api/practice` proves it survives.
  - Fourth time: a fixed sentence replacing every exception of a catch also hides our own operator-facing words (PolicyFile's InvalidDataException); ask which exception types carry a path before blanking them all (#828 PR). PracticeTable.cs:76 catches only InvalidDataException/OCE, so a corrupt schema there is still 500 Host.Unexpected (#846 PR).
- A guard read-then-write over a store (count recent, then save) needs a gate in the process; the API runs maxReplicas 1 (infra/main.bicep), so a SemaphoreSlim suffices, but the hourly job and ForgetAsync paths stay outside it (R1 feedback).
- A rate guard that refuses a repeat (TooSoon) makes a phone's retry after a timeout read as a failure though the first was kept: ask for an idempotency key or answer the kept id on an identical repeat (R1 feedback).
- `CF-Connecting-IP` is spoofable while `onlyCloudflare` is false (its default, infra/main.bicep:45): believe it only from a Cloudflare peer (R1 feedback).
- A practice's feedback with attach carries the journal since the cut (FeedbackApi.cs:139, ScenarioPlay.SinceTheCutAsync), both seats' decisions: page copy promising "nothing of your opponent's hidden choices" is false there; operator-only, so copy, not a leak (R2).
- FeedbackApi answers every SendFeedback refusal but Invalid as 429 (FeedbackApi.cs:100); a page branch on `403 Feedback.Muted` is dead until R3 adds the code: check the status the host really sends before approving a page's refusal map (R2).
- The working tree can move under a review (another session fixing other findings): review `origin/main...HEAD`, say which findings the uncommitted edits already address (R2).
  - Second time: HEAD moved during a review (a fix commit pushed by the authoring session): note `git rev-parse HEAD` at the start and re-diff before reporting (R4 async recap).
- A static-only route (TableFiles entry) answers questions 3-7 trivially; the checks that bite are the csproj `Content` (a new extension, e.g. table/rules.json, is not in the `*.js/*.css` globs) and `.dockerignore`, an allowlist where `table/` ships whole (R3 rulebook).
- A service worker fallback "when the host cannot be reached" fires only on a network error: behind Cloudflare an unreachable origin is a 52x response, passed through; the local browser spec that stops the host cannot see that difference (R3 rulebook, ADR 0191).
- A worker that keeps a navigation's response must refuse `response.redirected`: a redirected copy served later to a navigation is a network error in Chromium (R3 rulebook).
- Build the test project before trusting a "tests pass": `JsonElement.GetString()` is `string?`, so a `List<string?>` ShouldBe a `List<string>` is CS8620, an error under warnings-as-errors; the whole Cli.Tests run fails (R4, RulebookConceptCardsTests.cs:44).
- A text moved to a new source (concepts.js cards -> rules.json) deletes the tests that pinned its content: diff the old assertions against the new source, line by line (R4: the Defense ceiling and the healing cap, pinned in #682, are in no card of the book).
- `/battle` sits at the root, so a page's `new URL('rules.json', document.baseURI)` is `/rules.json`; the browser specs serve it under `/table/`, so a relative fetch passes there and an absolute one would not (R4).
- A page switch that stores the choice before its fetch succeeds, and early-returns when the shown book already matches, leaves a failed switch stuck (chrome in the new language over the old book, the other button a no-op) and makes the next offline load try only the stored book: check the failure path of every client-side switch, not only the success spec (R6, rules.js load).
- A field added to a record written read-modify-write (the activity record's `FeedbackMute`) is copied forward forever unless the write drops it once expired; check privacy text that says "until it ends" (R3).
  - Second time: AccountNoticeLog drops expired entries only when it reads or writes; nothing sweeps `notices/` and infra/main.bicep has no lifecycle rule, so an idle account keeps other players' old names at rest (N2, #1117).
  - Third time: a dead-letter queue kept for good (DeadLetterWorker, InfiniteTimeSpan) holds account id, device endpoint and payload, so the privacy pane's "until you turn them off" is false for every given-up notice; check every new queue's poison body against the privacy pane (N5, #1129).
- An operator status route that re-saves the same status moves `ClosedAt` and reruns its side effects (the automatic mute): ask for a no-op when nothing changes (R3).
- Feedback groups are chains (`MergedInto` names the idea joined, root found at read): any write that clears or moves one link (dismissing a merged idea) carries every idea merged into it along; check children of the link, not just the link (I1).
- `IFeedbackStore.FindAsync` answers null for a missing record and for one that cannot be parsed alike: a chain walk that reads null as "gone" (LetGoOfClosedFeedback) deletes on a corrupt record; tell them apart through `IdsAsync` (I1).
  - Second time: `StoredSessions.RecordAsync` folds missing and unparseable into null (and throws on a store failure); `ReadRecordAsync` (#814/#815) tells them apart, a face-down guard reads it; a guard that fails closed on null still 500s on a blob outage unless it catches like TableServer.StillOpenAsync (#790, #814/#815 PR). TableServer's `/session/<id>` answers every StillOpenAsync true with the same 409 and no console line: give each cause its own text and a line (#815).
- An admin page that pages through a list to fill a picker multiplies the host's full-store read per page: count the requests one sheet open costs, not the route (I1 merge picker, 1 + 5 AllAsync).
- A list kept beside the links it mirrors (`Joined` vs `MergedInto`) drifts on a cut write; a guard that trusts the list alone either refuses forever (stale extra) or lets through (missing): check each listed record still names it (I1 round 2).
- An operator POST that creates then links (Reproduce: read `Reproduction`, cut + PutAsync, then `ReproducedAsync`) with no per-target gate makes two scenarios on a double tap; admin-feedback.js `change()` has no in-flight guard, so the page never stops it (R4; third duplicate-on-repeat after R1, I1).
  - Fourth time: a write path wrapped so a second store is written after the first (FeedbackIndex Indexed.SaveAsync) answers 503 for a kept write; the retry reads TooSoon (#866 PR).
- An idea keeps Table/Scenario/Round but no Seat (FeedbackApi.cs:84) and keeps it when turned into a bug: anything that reads a report's seat must treat a missing one as unknown, not as player1 (R4 Reproduce).
- A scenario cut from a table still under way carries `After`, the opponent's committed plan for the round; a cut scenario opens for any signed-in account holding its id (ScenariosApi.Hidden hides only shipped ones): check every new path that cuts a live table (R4).
  - Second time: a refusal added at cut time leaves scenarios already cut from a live table openable and listable (PUT .../listing checks no source); ask the PR to say what happens to them (#736 PR).
- A practice scenario's Source.Run is the table it was cut from: a guard that checks only the report's `Context.Table` for "under way" misses a practice opened from a live table's cut (R5 download; same root as R4's live-table cut).
- An under-way guard that reads a missing or unparseable record (`StoredSessions.RecordAsync` null) as "over" fails open: for a face-down guard, null must refuse (R5).
- A test that forces a race from an event handler must fire it from the handler that sits in the window (`GaveBack`, after the hand-back's own bump), not a broader one (`Bumped` on Assisted empty), or the race it claims to reproduce is not guaranteed (#674/#683 PR).
- A race's regression test that says it "fails every time" must be measured against the old code (`git show origin/main:<file>` over the source, rebuild, run 10+ times): the #679 sweep test of ten tables failed 9/12, not 12/12 (#679/#666 PR).
- `ConsoleOutputTests` (Architecture.Tests) first checked per file; since this review it checks each top-level class and exempts its own file. A helper that swaps the console for its callers still passes it, as text scanning cannot see a call chain (#656 PR).
- `LetGoOfIdleTables` reads NeverBegan under `_gate` before `Close()`, and `Close()` reads `!IsOver` again to mark the record closed: two reads, so a sweep may report a table never-began whose record was not marked closed if its session ends between them (#679 PR, accepted as unlikely).

## Folded at the 2026-W48 retro

### A test pins the words, not the wiring
- A static payload test (`PushNotices.Payload`) pins the words: dropping the argument at the call in NoticeAsync stays green; ask for one test through the notice path (R4 async recap).
- Second time: a `Changes` sink set as a property in TableHost (`accounts?.Changes = lobbyChanges`) and tested by setting it on the api directly: dropping the host line stays green, the #812/UX10 shape again (N2, #1117).
- Third time: a per-host limit passed as an optional argument (StudioServer MaxBodyBytes) has no test; dropping it breaks the studio's knobs save (#691/#692/#755 PR).
- Fourth time: host tests building the desk over `new FeedbackIndex(store, store, clock)` but sending through the raw store never exercise production's send-through-index wiring (TableHost passes `feedbackIndex.Records`) (#866 PR).
- Fifth time: a read-time fix of a race (`over` vs `practice.tutorial.done`) is pinned only by a route test that reads once without `WaitingForAsync`; the polling tests stay green if the `Follow(SteppedSession)` wiring breaks (#912 PR, 800200d).
- A page spec that mocks `/api/lobby/push` for every token hides a host refusal by seat kind: ask which moments of a new page flow reach the real host (N4, #1124).
- A test named "read from the record, not memory" proves nothing unless one row has the two disagree (#736 round 2). A browser spec that holds a question and releases it must assert it was asked (#824 PR, feedback.spec.js).
- A race test must fire from the handler in the window; a store test must say which catch it enters (Blob 416 then 404 is not reached by deleting before the call) (#874 round 2).

### A deleted run comes back
- PlaytestRun.WriteRecordAfterAsync -> WriteOverAsync writes over a missing record with expected null: a run held in another process recreates `table.json` after a delete (#818 PR). FileArtifactWriter.Resolve calls Directory.CreateDirectory on deletes too, so a deleted run returns as an empty directory RunsAsync lists (#818).
- A take-back after a first write (`DeleteIfAsync(path, tag)`) should not be conditional on the tag once the record is gone (#818). An order inside an adapter (BlobArtifactStore.DeleteAsync taking `table.json` first) is pinned only if made observable, e.g. a lease on `table.json` (#818).
- Second time: a "record gone" rule needs the watch too (TableWatch.LookAsync let go only of a record rewritten; fixed with `PlaytestRun.Deleted`); the worker's journal append in that window still recreates the run, a ghost in the Sessions tab (#848 PR, two rounds). "This process saw the record" is `_seenStored`, not `_revision > 0` (records from before revisions are 0); a Reopen with a null record sets it false (#848/#850).
- Third time: a "record still there" check before a side file's first write is check-then-write against BlobArtifactStore.DeleteAsync's list-then-delete; QuiesceAsync drains the run, not TableReactions/TableVisits in flight; ask for a re-check after the write and a conditional delete (#789 PR). PlayedTable is in the registry before `RecordAsync` lands, so a latch on a missing table.json can latch on a table being born; a failed first write removes the table at once (#789 rounds 1-2).
- Fourth time: a "gone" latch read at load (`Kept.Found`, `Kept.Reached`) does not cover a delete between load and append; at mark 0 both stores create the journal, so the append must be told the journal is expected; probe with `JournalStore.BeforeAppend = journal.Delete` (#874 PR, measured). Journal.Gone keys on Mark > 0, which LetGoAsync and a Moved refusal null (#856 PR). BlobDecisionJournal.ReadAfterAsync at mark 0 folds 404 into 0 via 416 then `EndAsync` (#874).
- The run-delete order lives in the caller since #851 (`first: TableRecord.File` required on the port).

### A read-modify-write document across a revision overlap
- A store that skips unknown entries on read and rewrites the whole document drops a later revision's entries (NoticeDocuments, rollout overlap or a lagging worker image) (N1, #1108).
- Second time: a field an older image ignores on read and drops on write (DeviceRecord `gone`) turns a marker into a live entry; recognise the marker by shape too (N5, #1129).
- A stored report field whose unit changes (`FeedbackContext.JournalLength`) leaves saved reports in the old unit; ask what happens to them (#737/#750 PR). An index healed by a listing of ids heals only membership: ask for a per-record version in the index; rows are trusted on the fast path, so an old-revision writer leaves counts wrong (#866 rounds 1-2).

### Store reads added per page load or per poll
- A question asked from `navigation.js` or `mountFeedback` runs on every page load: count the reads it adds per view (#717). Cover the signed-in branch of a seat-or-account check, not only the guest one (#717).
- Second time: ScenarioSources.HeldBackAsync reads one record per scenario on every `GET /api/scenarios` and catches no store failure, so a blob outage 500s the Practice list (#833 PR).
- Third time: every lobby card reads its last move through `TableApi.LastMoveAtAsync`, one console line per call on failure (#775 round 2); `LobbyApi.DescribeAsync` already reads `composer.LastMoveAsync`, a decoration reading it again doubles the journal reads of `/api/lobby/mine` (R4 async recap).
- Fourth time: the bell's refresh (inbox.js) runs on every account change and stream open of every page, not throttled by `fresh` (N2, #1117).
- Fifth time: LobbyApi's `Claims` is one static SemaphoreSlim for every claim, leave, invitation and seat subscribe; the battle page re-PUTs `/api/lobby/push` per held seat on every load, so a store write inside SubscribeAsync's gate is paid by every claim; ask for it after `Claims.Release()` (N3, #1121).

### A console line or sink that the tests cannot see
- A "give the class a sink" PR leaves `Console.Error.WriteLineAsync` (TableApi:738, TableComposer:748, JobsApi:141): grep `Console\.` whole (#799/#800 PR).
- Second time: AdminApi has a `Said` sink beside direct `Console.WriteLine`: a new line bypassing it is invisible to the sink's tests (#828 PR).
- Third time: a sink given to one host class (`AdminApi.Said`) does not reach the classes it calls (TableComposer.NotifyVoidAsync, OperatorNotes): a privacy test moved to the sink covers fewer lines (#756 PR).
- A log line meant to tie to a session names the table id (`run?.SessionId`), not `session.MatchId` (#847 PR). A per-char filter sees an astral format character as two surrogates; use `Rune` (#847 round 2).

### A route or prose line the docs miss
- A change to what `GET /api/scenarios/<id>/practice` resumes is said in commands.md and table/README.md#routes; #912's PR updated only commands.md (e804574).
- Second time: `POST /api/lobby/invitations` and `POST /api/lobby/claim` have no row in table/README.md#routes; the invitations answer hands any link holder the inviter's `accountId` via `SeatSummary` (UX7 PR, pre-existing).
- A CSS change that moves shared tokens can leave table/README.md's prose false; grep the README for token names (UX4).

### Running a review's tests on a copy
- Run a review's tests from `git archive <sha> | tar -x` into the scratchpad: the whole tracked tree comes along (#874 round 2; a hand copy needed viewer/, studio/ and .github/workflows/pages.yml, MSB3030). Never in the checkout (#814/#815).
- Second time: a /reviewers copy has no battle/node_modules: symlink the checkout's and `npm run build` there, else 4 TableFilesTests fail for want of `battle-build/*.js` (#1016 PR, UX7 PR).
- The first `dotnet build` of Cli.Tests in a copy can report 1 error a rebuild clears (N1). "Failed before" is measurable without git: revert the clause in a copy, rebuild, run the class (#846 PR).
- The Dockerfile's `battle-build` stage copies only battle/, table/*.js, table/battle-art/ and data/presentation/spell-art.json: a build.mjs reading more fails in the image; reproduce the stage's layout (BC4 PR).

### Once each
- Cloudflare never caches a POST nor an extension-less GET by default: `NotKept` there is defence in depth (#752 PR).
- A refusal reused for a new cause (`Reaction.Busy` 503 "Try again" for a deleted run) keeps the page retrying; check how the page maps the status (#739). The battle page's seen sender treats every non-ok answer alike (#850).
- Reactions store failures are not caught at the route (TableApi.ReactAsync): 500 Host.Unexpected, not 503 Reaction.Busy (#789).
- The catalogue's ETag is the SHA-256 of its body, not the content hash (BC4 PR).
- A hand-committed recorded fixture (battle-recap.fixture.json) needs its rebuild command (R2 async recap).
- `round` on `/api/lobby/mine` (RoundNowAsync, null after 200 ms) is asserted by no Lobby host test (UX9).
- A file only offline.html loads (offline.js) is fetched by the worker at install alone, so it drifts from its kit across deploys until sw.js changes (S10).
- Re-deriving the picture kit deletes old hashed sizes from table/page-art/: a tab loaded before the deploy 404s them; accepted (#1016 PR).
- A folder of art named from data needs a test holding the two together (#1004, resolved by moving the portraits into `/page-art`).
- A practice or tutorial seat token lives in ScenarioPractices: `PUT /api/lobby/push` answers it 403 and battle-services then unsubscribes the browser's one subscription; check `practice` reaches noticeButton (N4, #1124).
- Once a seat's turn goes only to its account's devices, a seat subscription the account does not hold is never sent to while the seat's push test says it works (N3, #1121). The `Turn` kind gates TurnTimeouts' warning too (N3).
- A wrapper adding a channel inside a bare `catch { restore }` (LobbyApi.VacateAsync) must catch its own store failures (N1). Work moved after a route's answer (LobbyApi HeldNotices) must catch everything but cancellation (N5, #1129).
- A listing narrowed to a span (`IdsSentAsync(after, until)`) gains an upper bound that drops an id stamped after `now` (#693 PR). A refactor that only moves use cases: compare moved bodies against `git show origin/main:<file>` (#723 PR).
- A refusal added to `RedactedAsync` is not predicted by the sheet's flags; the docs line must name both tables (#814). Fail-closed on a missing record offers a Diagnose that 404s and makes "unread" permanent after an operator delete; a guard shared by a read and a refusal catches StoreFails into its unknown state (#790 rounds 1-2).
- A sheet flag restating the offer's conditions drifts; ask for one predicate both read (#736 round 2).
- An exemption from a privacy scan keyed on "looks like an agent word" lets `kind:file.json` through; exempt only a bare kind (#866 round 2).
- `ScenarioPractices.Journals` lets a test give a practice a faulting journal (#931 PR). A token passed to `NoteTutorialAsync` bounds only the store calls, not the wait on the account's previous change (#931, b045896). ScenarioPractices' done/over read, keep, re-take gate lets go on a stale read; only GET/DELETE retry a failed keepDone, OpenAsync and SweepAsync do not (#931, 1282a21, 8f7082f). Narrowing `Tutorial.Done` needs Resume to let go of `Session.IsOver` too, and the note belongs inside the practices gate (#912).
- A seat "asked" says nothing of whether the other seat's bot has journaled the same phase: wait for its entries (#737/#750 PR).
- A token check fixed by grep for `string.Equals` misses `==` (TableRegistry.ByTokenAsync, `seat.Invitation == invitation`, `seat.Code == code`); SecretComparisonTests matches only `string.Equals` (#862 PR).
