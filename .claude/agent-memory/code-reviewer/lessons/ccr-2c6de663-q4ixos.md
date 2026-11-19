# code-reviewer lessons (branch ccr-2c6de663-q4ixos)

- The battle-tutorial mock answers every practice POST with the same table id (`practice-lesson`), while live.ts opens the result once per `table:seat` (`matchShown`, live.ts:681): a "Play again" spec cannot show the result reopening on a second playthrough unless the mock gives a new id per POST, as the host does (ScenariosApi.cs:313) (T1 tutorial result, staged on 78c1821).
- Running the browser specs on a `git archive` copy also needs `studio/browser/node_modules` linked from the checkout (`@playwright/test` lives there, not at the root) (T1, 78c1821).
