---
name: reviewers
description: Review the current branch, or a pull request, with the reviewers its paths call for (scripts/review/reviewers.mjs) - code, domain, host, pages, design, copy, infrastructure, security, statistics - run in parallel, then check each finding against the code and sort it - fix now, an issue, or dismissed with the reason. Use before /ship, after /verify, or when the owner says "relis", "review", "fais relire". Named reviewers because Claude Code has its own /review.
---

# Reviewers

The repository's reviewers read a change from one angle each. This picks them, runs them, and does not take
their word for it.

1. The range. The current branch: `git fetch origin main` and `origin/main...HEAD`. A pull request: its head
   fetched by name (`git fetch origin <branch>`), and `origin/main...FETCH_HEAD`.
2. The reviewers: `git diff --name-only <range> | node scripts/review/reviewers.mjs`. Add `archivist` only to
   answer a question a finding raises ("is this decided?"); it is not a reviewer.
3. Run them in parallel and in the foreground: one Agent call each in the same message, with
   `run_in_background: false`. A background agent is refused every tool not allowed in advance, and the memory
   writes of `code-reviewer` and `host-reviewer` are not. Each prompt gives only the
   range and the pull request's text, never your own reading of the change, so the review is not anchored on it.
4. Check every finding before you act on it: open the file at the line, and confirm the failure it claims (a
   test that fails, a path through the code, a rule it breaks). A finding that does not hold is dismissed, with
   the line that shows why.
5. Sort what holds:
   - **Fix now**: a bug, a security hole, a broken rule, a missing test or a stale doc in what this change
     touches. Fix it in this branch, with a test that failed before when it is a bug.
   - **Issue**: true, and outside this change or this stage. It goes to `/wrap-up`, which opens it.
   - **Dismissed**: wrong, or already handled; say where.
6. After the fixes, `/verify` again. A second round only for the reviewers whose findings changed code.
7. Report to the owner, in French, on a phone's screen: the reviewers that ran, then the three lists, one line
   a finding with its file. In the pull request's `## Notes for the reviewer`, the same three lists in English.

`code-reviewer` and `host-reviewer` keep a memory under `.claude/agent-memory/` (the W5 trial in
`docs/engineering/ai-workflow-plan.md`). What they write there is committed with the change, like any file.
