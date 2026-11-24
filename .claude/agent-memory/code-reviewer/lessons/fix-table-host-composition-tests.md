# code-reviewer lessons (branch fix/table-host-composition-tests)

- A chore added to the panel's pass (`JobsTabAsync`) is pinned only by what its test asserts: `IdleGuests`' preferences argument set to `null` kept Cli.Tests green, so a guest's settings outlive it. Probe each constructor argument, not only the part (second time after #1159; #1225, db3cd48).
- A panel pass part that opens its own store (`OpenActivityAsync` again in `JobsTabAsync`) adds a round trip and a throw the composition did not have: ask for the store already opened in `SignedInParts` (#1225, db3cd48).
