# code-reviewer lessons (branch ccr-b14c7153-osgm31)

- A focus guard with a "leave it where the player went" branch (giveReceiptFocus `!waiting`) was tested only on its happy path, under a title that names both halves: read each spec title's second clause for an assertion, and check that deleting the guard fails a test (#1076, battle-mutations.spec.js, e5aa5b0).
- A route stub keyed on `since=0` to hang "the whole feed" also matches the seat's first poll (battle-live.js starts `since: 0`): it is safe only when added after the first board arrived. Check where the stub sits against the mount (#1073, battle-live.spec.js, e5aa5b0).
- Answers to a review round can land as code only: the round guard on receiptAwaited and the inset minus the rise (85b09ee) came without a spec that fails without them. On a second round, map each first-round finding to a test that breaks if its fix is reverted (#1076, live.ts).
