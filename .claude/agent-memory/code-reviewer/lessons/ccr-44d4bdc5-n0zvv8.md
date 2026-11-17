# code-reviewer lessons (branch ccr-44d4bdc5-n0zvv8)

- Second time (R4 recap `PushNotices.Payload`, then #739 `Reaction.Gone`): a new refusal tested only at the class (`TableReactionsTests`) while its route mapping (`TableApi.ReactAsync` case, 409) has no host test; deleting the case falls to the default 503 with every test green. Ask for one test through the route (#739 PR, 59846a7).
- Twelfth time: the branch moved mid-review (757ba4d -> 59846a7, `Reaction.Gone` added); a full `dotnet test --no-build` run against binaries older than the tree failed 2 tests that passed after a rebuild: rebuild after any HEAD move before reporting a failure (#739 PR).
