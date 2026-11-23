# code-reviewer lessons (branch fix/1160-push-offer-where)

- A field added to a source's state snapshot is lost on the source's early `{ ready: false }` return, and the consumer's default then says the old, wrong thing: read every return of the source, not only the ready one (#1160, battle-services offerState; like N4's skipped "ready").
- A unit test that asserts a module's output with the module's own exported constant (`where: SEAT_WHERE`) holds whatever the constant says: the words are pinned only by the browser spec (#1160).
