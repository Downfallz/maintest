# code-reviewer lessons (branch ccr-b14c7153-osgm31)

- A focus guard with a "leave it where the player went" branch (giveReceiptFocus `!waiting`) was tested only on its happy path, under a title that names both halves: read each spec title's second clause for an assertion, and check that deleting the guard fails a test (#1076, battle-mutations.spec.js, e5aa5b0).
- A route stub keyed on `since=0` to hang "the whole feed" also matches the seat's first poll (battle-live.js starts `since: 0`): it is safe only when added after the first board arrived. Check where the stub sits against the mount (#1073, battle-live.spec.js, e5aa5b0).
