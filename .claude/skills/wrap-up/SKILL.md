---
name: wrap-up
description: End a stage's session - loose ends become GitHub issues, the stage is marked built in its plan, and the owner gets what was done, what was verified, what is left and the next stage's one-line prompt. Use once the stage's pull request is merged, past 300,000 tokens, or when the owner says "wrap up", "on s'arrête", "fais le bilan".
---

# Wrap up

A loose end written only in chat is lost. This turns the session's into issues, then stops.

1. Gather the loose ends: what the session's messages left open, the `Not verified` and `skipped` of the gate,
   flaky tests, the steps left to the owner, review findings put off. One issue each. When the stage's diff calls
   for `rules-keeper` (`git diff origin/main... | node scripts/review/reviewers.mjs`) and its pull request is still
   open, run `/rules` first; a section it could not rewrite is a loose end.
2. Look for an open issue that already holds it (`gh api -X GET search/issues -f q='repo:Downfallz/maintest is:issue is:open <words>'`)
   and add to it rather than opening a second.
3. Open the rest with `issue_write`: a title that says the problem, a body with the evidence (the path, the test
   name, the log line, the pull request), what was tried, and the plan and stage it came from. One label:
   `follow-up` (work left), `flaky` (a test that passed only on a retry), `ops` (a portal, a setting, the
   deployed app), `proposal` (an idea nobody asked for yet).
4. Mark the stage in its plan if `/stage` did not: the `*Built (<date>)*:` paragraph, with the issue numbers for
   what was left. If the pull request is still open, push it there; if it is merged, the mark goes in the next
   stage's pull request, and the wrap-up says so.
5. Answer the owner in French, on a phone's screen:
   - **Fait** : what was built, with the pull request.
   - **Vérifié** : what the gate ran, and what it skipped or could not reach.
   - **Reste** : the issues opened, by number; the steps that are the owner's, numbered, one block each.
   - **Suivant** : the next stage's one-line prompt, in a block to paste into a fresh session:
     `/stage <plan path> <next id>`.
6. Stop. A session whose stage is merged is not reused for the next one.
