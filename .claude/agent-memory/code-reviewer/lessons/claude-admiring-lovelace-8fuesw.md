# code-reviewer lessons (branch claude/admiring-lovelace-8fuesw)

- An allowlist gate with a "token proves its own seat" escape must scope the escape to the seat's own paths: at 2bb00e9 GuestRefusalAsync let a guest's practice token through to every route (lobby, friends, push), and the hourly sweep deletes with `AccountRegistry.DeleteAsync`, not `AccountApi.ForgetAsync`; fixed by NamedBeforeTheTables in ab457ee (P5).
- A new chore put before others in TableTend.TendAsync, with its listing outside its catch, costs every later stage (turn timeouts) when it throws: ask for its place last or a catch round the whole chore (P5, dc5b784).
- The tip moved twice during one review (2bb00e9 -> ab457ee -> dc5b784): re-read `git rev-parse HEAD` after every long test run, not once at the end (P5).
