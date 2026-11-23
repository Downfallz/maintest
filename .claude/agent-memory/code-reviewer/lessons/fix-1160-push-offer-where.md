# code-reviewer lessons (branch fix/1160-push-offer-where)

- A field added to a source's state snapshot is lost on the source's early `{ ready: false }` return, and the consumer's default then says the old, wrong thing: read every return of the source, not only the ready one (#1160, battle-services offerState; like N4's skipped "ready").
- A unit test that asserts a module's output with the module's own exported constant (`where: SEAT_WHERE`) holds whatever the constant says: the words are pinned only by the browser spec (#1160).
- `TableWorkerNoticesTests.With_push_keys_the_worker_puts_its_pushes_on_the_notices_queue_and_sends_them_from_there` failed once in a full Cli.Tests run (Sent was empty) and passed alone, on a JS-only diff: rerun a lone red worker test before you blame the diff, and say it flaked (#1160, ede5911).
