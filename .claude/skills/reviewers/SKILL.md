---
name: reviewers
description: Review the current branch, or a pull request, with the reviewers its paths call for (scripts/review/reviewers.mjs) - code, domain, host, pages, design, copy, infrastructure, security, statistics - run in parallel, then check each finding against the code and sort it - fix now, an issue, or dismissed with the reason. Use before /ship, after /verify, or when the owner says "relis", "review", "fais relire". Named reviewers because Claude Code has its own /review.
---

# Reviewers

The repository's reviewers read a change from one angle each. This picks them, runs them, and does not take
their word for it.

1. The commit. Commit what is to be reviewed first, so a reviewer reads one named state of the tree. The
   current branch: `git fetch origin main`, the sha of `HEAD`, and the range `origin/main...<sha>`. A pull
   request: its head fetched by name (`git fetch origin <branch>`), the sha of `FETCH_HEAD`, and
   `origin/main...<sha>`. Then a copy of that commit, outside the checkout:
   `dir="$(mktemp -d)/<short sha>" && mkdir -p "$dir" && git archive <sha> | tar -x -C "$dir"`. A reviewer that
   builds or runs the tests there needs the ignored inputs too: copy `table/battle-build` in before
   `dotnet build` (the Cli tests read it), and build `data/dst` for the real-engine specs.
2. The reviewers: `git diff origin/main...<sha> | node scripts/review/reviewers.mjs`. The whole diff, not
   `--name-only`: a page script that adds a raw colour, an inline style or a class calls for `design-reviewer`
   by what it adds, not by its name. Add `archivist` only to answer a question a finding raises ("is this
   decided?"); it is not a reviewer.
3. Run them in parallel and in the foreground: one Agent call each in the same message, with
   `run_in_background: false`. The memory writes of `code-reviewer` and `host-reviewer` are allowed in advance
   (`.claude/settings.json`), but a background agent is still refused every other tool not allowed there, such
   as a build in the copy; and the session has nothing to do while they read. Each prompt gives the
   sha, the range `origin/main...<sha>`, the copy's path and the pull request's text, never your own reading of
   the change, so the review is not anchored on it. It tells the reviewer to read the files in the copy, never
   in the checkout, which may hold the author's uncommitted edits, and to start its report with
   `Reviewed: <sha>`. Edit nothing in the tree until every reviewer has reported: the findings would mix
   several states of it (issue #740). A fix that cannot wait means stopping the reviewers, committing, and
   running them again on the new sha.
4. Check every finding before you act on it, against the sha it names: open the file at the line, and confirm
   the failure it claims (a test that fails, a path through the code, a rule it breaks). A finding that does
   not hold is dismissed, with the line that shows why.
5. Sort what holds:
   - **Fix now**: a bug, a security hole, a broken rule, a missing test or a stale doc in what this change
     touches. Fix it in this branch, with a test that failed before when it is a bug.
   - **Issue**: true, and outside this change or this stage. It goes to `/wrap-up`, which opens it.
   - **Dismissed**: wrong, or already handled; say where.
6. After the fixes, `/verify` again. A second round only for the reviewers whose findings changed code.
7. Report to the owner, in French, on a phone's screen: the reviewers that ran, then the three lists, one line
   a finding with its file. In the pull request's `## Notes for the reviewer`, the same three lists in English.

`code-reviewer` and `host-reviewer` keep a memory under `.claude/agent-memory/`; it works in cloud sessions (the
W5 trial in `docs/engineering/ai-workflow-plan.md`). What they write there, one `lessons/<branch>.md` file each,
is committed with the change, like any file; `/retro` folds those files into `lessons.md`.
