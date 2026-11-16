---
name: ship
description: The standing orders as steps - commit, push, open the pull request from the template, subscribe to it, ask Codex for its review, answer every thread, merge main in on a conflict, and report ready only when every check is green and no thread is open. Use once the gate passed, or when the owner says "ouvre la PR", "ship", "abonne-toi".
---

# Ship

The standing orders in `CLAUDE.md`, in order. None of them is asked first.

1. The gate passed on this tree: `scripts/gate/state.sh changed` prints nothing. If it prints a path, run
   `/verify` first.
2. Commit in small, imperative commits (`Add the Stop hook`), with the attribution lines the session gives. Push
   the branch with `git push -u origin <branch>`, retrying a network failure up to four times.
3. Open the pull request against `main` with the GitHub tools, its body from `.github/PULL_REQUEST_TEMPLATE.md`:
   `## What` (what and why, with the plan's stage and the ADRs, and `Closes #<n>` for each issue it settles, so
   the merge closes it), the checklist ticked only where true, and
   `## Notes for the reviewer` (what the gate skipped, what was not verified, the steps left to the owner). The
   title is the change in plain words, with the stage id in parentheses (`... (W4)`).
4. Subscribe to its activity (`subscribe_pr_activity`).
5. Ask Codex: a comment `@codex review`, unless Codex has already started on this pull request.
6. Then end the turn. Events arrive on their own; never poll or sleep for them.
7. On each event, answer the round: every thread and failing check that has arrived and is not yet answered.
   - A review thread: fix it in a new commit, or reply why not. Every finding gets one of the two.
   - A failing check: read its log (`get_job_logs`) and fix the cause in a new commit. Never skip, disable or
     retry a test to get green; a flaky one is a `flaky` issue (`/wrap-up`).
   - A conflict: `git fetch origin main && git merge origin/main`, settled (a generated index is rebuilt with
     `UPDATE_INDEXES=1 dotnet test --project tests/DownfallArena.Architecture.Tests`, never edited by hand).
   - Then `/verify`, and push once for the whole round, with one exception. A push cancels the CI run still
     playing, and the minutes it spent are lost (`docs/engineering/ci-minutes-plan.md`): if that run has nothing
     red yet, the commits wait for its result and go with the round it opens. Schedule a check-in with
     `send_later` for when the run should end (30 minutes), so a run that ends green with no event still wakes
     the session, and nothing stays unpushed.
   - Reply to each thread with the commit that answers it, once it is on the branch, and resolve the thread.
   - An event that needs nothing: no answer.
8. Ready means: every check finished and green, the `gate` check among them, no commit left unpushed, and no
   thread unanswered. Only then tell the owner it is ready, in French, with the link. Never merge: the owner
   merges.
